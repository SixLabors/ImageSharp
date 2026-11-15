// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;
using SixLabors.ImageSharp.Compression;
using SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Components.Entropy;
using SixLabors.ImageSharp.Formats.Jxl.Components.Modular;
using SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Coordinates modular and VarDCT frame encoding and writes the image and frame headers.
/// </summary>
internal sealed class JxlEncoderCore : IDisposable
{
    /// <summary>
    /// The width and height of a coding group for the default modular group shift.
    /// </summary>
    public const int GroupDimension = 256;

    /// <summary>
    /// The signed sample carriers for colors, derived channels and one shared supplied-channel plane.
    /// </summary>
    private readonly Buffer2D<int>? samples;

    /// <summary>
    /// The prepared modular scratch region. VarDCT frames without extra channels need no modular sample storage.
    /// </summary>
    private readonly Buffer2DRegion<int> sampleRegion;

    /// <summary>
    /// The number of supplied-channel scratch planes removed by sharing their sequential carrier storage.
    /// </summary>
    private readonly int suppliedScratchOffset;

    /// <summary>
    /// The final transformed planes retained between progressive passes. Plain frames have no retained planes.
    /// </summary>
    private readonly JxlModularChannel[] progressiveChannels;

    /// <summary>
    /// Five horizontally reduced rows per color component. Vertical squeeze needs the preceding odd row and two pairs.
    /// </summary>
    private readonly Buffer2D<int>? squeezeRows;

    /// <summary>
    /// The number of color channels with two spare signed bits for reversible squeeze arithmetic.
    /// </summary>
    private readonly int squeezedComponents;

    /// <summary>
    /// The leading transformed channels encoded in the global stream instead of coding groups.
    /// </summary>
    private readonly int globalPrefixCount;

    /// <summary>
    /// One converted pixel row, reused before the next source row is read.
    /// </summary>
    private readonly IMemoryOwner<Vector4> pixelRow;

    /// <summary>
    /// Component rows and optional ICC scratch, reused before the next source row.
    /// </summary>
    private readonly IMemoryOwner<float> floatRows;

    /// <summary>
    /// The ordered byte lengths of the completed frame sections.
    /// </summary>
    private readonly IMemoryOwner<uint> sectionSizes;

    /// <summary>
    /// The encoded image dimensions.
    /// </summary>
    private readonly Size size;

    /// <summary>
    /// The retained EXIF orientation, or normal orientation when metadata is omitted.
    /// </summary>
    private readonly ushort orientation;

    /// <summary>
    /// The selected color model.
    /// </summary>
    private readonly JxlColorType colorType;

    /// <summary>
    /// The declared color sample precision.
    /// </summary>
    private readonly JxlBitDepth bitDepth;

    /// <summary>
    /// The declared alpha sample precision.
    /// </summary>
    private readonly JxlBitDepth alphaBitDepth;

    /// <summary>
    /// Whether the frame stores alpha samples.
    /// </summary>
    private readonly bool hasAlpha;

    /// <summary>
    /// Whether original-color output contains the ICC profile's black device component.
    /// </summary>
    private readonly bool hasBlack;

    /// <summary>
    /// The declared precision of the black device component.
    /// </summary>
    private readonly JxlBitDepth blackBitDepth;

    /// <summary>
    /// The retained optional alpha label.
    /// </summary>
    private readonly string alphaName;

    /// <summary>
    /// The retained optional black-component label.
    /// </summary>
    private readonly string blackName;

    /// <summary>
    /// Whether color samples are associated with alpha.
    /// </summary>
    private readonly bool alphaAssociated;

    /// <summary>
    /// The number of coding groups across the image.
    /// </summary>
    private readonly int groupColumns;

    /// <summary>
    /// The number of coding groups down the image.
    /// </summary>
    private readonly int groupRows;

    /// <summary>
    /// The total number of coding groups.
    /// </summary>
    private readonly int groupCount;

    /// <summary>
    /// The total number of DC groups.
    /// </summary>
    private readonly int dcGroupCount;

    /// <summary>
    /// The number of entries in the frame section table.
    /// </summary>
    private readonly int sectionCount;

    /// <summary>
    /// The width of the reusable group storage.
    /// </summary>
    private readonly int groupWidth;

    /// <summary>
    /// The height of each component plane in the reusable group storage.
    /// </summary>
    private readonly int groupHeight;

    /// <summary>
    /// The number of color components before alpha.
    /// </summary>
    private readonly int colorComponents;

    /// <summary>
    /// The total number of stored color and alpha components.
    /// </summary>
    private readonly int componentCount;

    /// <summary>
    /// The encoded descriptors: explicit input channels first, followed by missing pixel-derived channels.
    /// </summary>
    private readonly JxlExtraChannelMetadata[] extraChannels;

    /// <summary>
    /// The first supplied alpha channel, or minus one when pixels supply alpha.
    /// </summary>
    private readonly int suppliedAlphaIndex = -1;

    /// <summary>
    /// The encoded index of pixel-derived alpha, or minus one when no derived alpha is written.
    /// </summary>
    private readonly int derivedAlphaIndex = -1;

    /// <summary>
    /// The encoded index of profile-derived black, or minus one when no derived black is written.
    /// </summary>
    private readonly int derivedBlackIndex = -1;

    /// <summary>
    /// The quantized and expanded primary alpha used by working colors when its input is reduced.
    /// </summary>
    private readonly Buffer2D<float>? expandedAlpha;

    /// <summary>
    /// The retained format filter workspace for reduced primary alpha.
    /// </summary>
    private readonly JxlUpsampler? alphaUpsampler;

    /// <summary>
    /// The source frame currently supplying borrowed extra-channel buffers.
    /// </summary>
    private int sourceFrameIndex;

    /// <summary>
    /// The allocator-backed payload retained until its section lengths and bytes have been written.
    /// </summary>
    private readonly ChunkedMemoryStream payload;

    /// <summary>
    /// The settings retained for this operation.
    /// </summary>
    private readonly JxlEncoder encoder;

    /// <summary>
    /// The payload stream during section construction, then the caller's stream during final emission.
    /// </summary>
    private Stream outputStream;

    /// <summary>
    /// The original compatible output profile, retained without a clone or a changed rendering intent.
    /// </summary>
    private readonly IccProfile? profile;

    /// <summary>
    /// The encoded profile bytes borrowed or serialized once for the image header and level selection.
    /// </summary>
    private readonly ReadOnlyMemory<byte> profileData;

    /// <summary>
    /// Whether the declared samples fit the format's signed 16-bit modular storage promise.
    /// </summary>
    private readonly bool narrowSamples;

    /// <summary>
    /// Whether image features or displayed frame rates require a Level 10 container declaration.
    /// </summary>
    private bool requiresLevelTen;

    /// <summary>
    /// The prepared transform from working sRGB pixels into the embedded profile's device values.
    /// </summary>
    private readonly ColorProfileConverter? profileConverter;

    /// <summary>
    /// The paired converter selected once for the encoded color model.
    /// </summary>
    private readonly JxlColorConverterBase colorConverter;

    /// <summary>
    /// The compact encoding's serialized reference white.
    /// </summary>
    private readonly CieXyChromaticityCoordinates whitePoint;

    /// <summary>
    /// The compact RGB encoding's serialized primaries.
    /// </summary>
    private readonly RgbPrimariesChromaticityCoordinates primaries;

    /// <summary>
    /// The compact transfer function used when no explicit gamma is present.
    /// </summary>
    private readonly CicpTransferCharacteristics transferCharacteristics;

    /// <summary>
    /// The serialized compression exponent, in ten-millionths, or null for a named transfer function.
    /// </summary>
    private readonly uint? gamma;

    /// <summary>
    /// The rendering intent for a compact original or suggested output encoding.
    /// </summary>
    private readonly IccRenderingIntent renderingIntent;

    /// <summary>
    /// The serialized reference intensity in nits, shared by the color transform and tone-mapping header.
    /// </summary>
    private readonly float intensityTarget;

    /// <summary>
    /// Whether lossy coding transforms working RGB into XYB.
    /// </summary>
    private readonly bool usesXyb;

    /// <summary>
    /// The forward transform workspace and retained coefficients for lossy VarDCT frames.
    /// </summary>
    private readonly JxlVarDctEncoder? varDct;

    /// <summary>
    /// Whether the image contains a displayed frame sequence.
    /// </summary>
    private bool animated;

    /// <summary>
    /// The numerator of the sequence clock in ticks per second.
    /// </summary>
    private uint ticksPerSecondNumerator;

    /// <summary>
    /// The denominator of the sequence clock in ticks per second.
    /// </summary>
    private uint ticksPerSecondDenominator;

    /// <summary>
    /// The number of sequence plays; zero repeats indefinitely.
    /// </summary>
    private readonly uint repeatCount;

    /// <summary>
    /// The low-order bits waiting for byte emission.
    /// </summary>
    private ulong bits;

    /// <summary>
    /// The number of valid low-order bits in the reservoir, less than 32 between writes.
    /// </summary>
    private int pendingBits;

    /// <summary>
    /// The initialized prefix of the borrowed output byte buffer.
    /// </summary>
    private int bufferedBytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlEncoderCore"/> class for one image operation.
    /// </summary>
    /// <param name="encoder">The caller's encoder settings.</param>
    /// <param name="image">The image that supplies dimensions, metadata and the allocation configuration.</param>
    /// <param name="stream">The caller's output stream, borrowed until encoding ends.</param>
    /// <exception cref="ImageFormatException">The image dimensions or ICC profile exceed the JPEG XL Level 10 limits.</exception>
    /// <exception cref="ArgumentException">Explicit XYB coding has a lossless reconstruction distance.</exception>
    public JxlEncoderCore(JxlEncoder encoder, Image image, Stream stream)
    {
        this.encoder = encoder;
        this.outputStream = stream;
        Configuration configuration = image.Configuration;
        Size size = image.Size;

        // Level 10 permits at most 2^30 pixels per axis and 2^40 pixels in total. Reject larger images
        // before creating format metadata or renting workspace; their sizes cannot be written in the header.
        if (size.Width > (1 << 30) || size.Height > (1 << 30) || (long)size.Width * size.Height > (1L << 40))
        {
            throw new ImageFormatException($"Image is too large to encode at {size.Width}x{size.Height} for JPEG XL format.");
        }

        JxlMetadata metadata = image.Metadata.GetJxlMetadata();
        JxlColorType colorType = encoder.ColorType ?? metadata.ColorType;

        // Native lossless encoding retains original color components. Explicit XYB selects its lossy
        // coding transform, so a zero distance cannot request both representations.
        Guard.IsTrue(
            encoder.ColorType != JxlColorType.Xyb || encoder.Distance > 0F,
            nameof(encoder),
            "Explicit XYB coding requires a positive reconstruction distance.");

        JxlBitDepth bitDepth = encoder.BitDepth ?? metadata.BitDepth;
        this.repeatCount = encoder.RepeatCount ?? metadata.RepeatCount;

        this.orientation = ExifOrientationMode.TopLeft;
        ExifProfile? exifProfile = image.Metadata.ExifProfile;

        if (!encoder.SkipMetadata && exifProfile?.Validate() == true
            && exifProfile.TryGetValue(ExifTag.Orientation, out IExifValue<ushort>? orientation)
            && orientation.Value is >= ExifOrientationMode.TopLeft and <= ExifOrientationMode.LeftBottom)
        {
            // The codestream's three-bit orientation takes precedence over its EXIF copy. Keep valid EXIF
            // values in that header without rotating stored pixels or changing the source profile.
            this.orientation = orientation.Value;
        }

        JxlExtraChannelMetadata? alpha = null;
        JxlExtraChannelMetadata? black = null;

        foreach (JxlExtraChannelMetadata channel in metadata.ExtraChannels)
        {
            if (channel.Type == JxlExtraChannelType.Alpha && alpha is null)
            {
                alpha = channel;
            }
            else if (channel.Type == JxlExtraChannelType.Black && black is null)
            {
                black = channel;
            }
        }

        bool suppliedBlack = false;

        for (int c = 0; c < encoder.ExtraChannels.Count; c++)
        {
            JxlExtraChannel channel = encoder.ExtraChannels[c];
            Guard.IsTrue(channel.Frames.Count == image.Frames.Count, nameof(encoder.ExtraChannels), "Each extra channel must supply every image frame.");
            int shift = channel.Metadata.DimensionShift;
            Size channelSize = new(((size.Width - 1) >> shift) + 1, ((size.Height - 1) >> shift) + 1);

            foreach (Buffer2D<float> frame in channel.Frames)
            {
                Guard.IsTrue(
                    frame.Width == channelSize.Width && frame.Height == channelSize.Height,
                    nameof(encoder.ExtraChannels),
                    "Extra-channel buffers must match their reduced image dimensions.");
            }

            if (channel.Metadata.Type == JxlExtraChannelType.Alpha && this.suppliedAlphaIndex < 0)
            {
                this.suppliedAlphaIndex = c;
                alpha = channel.Metadata;
            }

            suppliedBlack |= channel.Metadata.Type == JxlExtraChannelType.Black;
        }

        bool hasAlpha = alpha is not null || image.PixelType.AlphaRepresentation != PixelAlphaRepresentation.None;
        this.size = size;

        // XYB selects the coding transform, not an original device profile. A lossless replacement
        // uses decoded working RGB. Coding XYB keeps a gray ICC declaration when that profile requires it.
        IccProfile? sourceProfile = IccProfile.SelectValidProfile(image.Frames.RootFrame.Metadata.IccProfile, image.Metadata.IccProfile);

        // Native JPEG XL requires a complete ICC profile. Its decoder checks the profile in strict mode.
        // XYB uses the profile only to describe suggested output colors.
        if (encoder.Distance == 0F && sourceProfile is { WasRecovered: true })
        {
            sourceProfile = null;
        }

        if (colorType == JxlColorType.Other && sourceProfile == null)
        {
            if (encoder.ColorType == JxlColorType.Other)
            {
                throw new ImageFormatException("JPEG XL Other color requires a usable ICC profile.");
            }

            colorType = JxlColorType.Rgb;
        }

        // An ICC-defined request takes its color model from the profile, as in libjxl. Reuse the
        // shared ICC mapping and the metadata mapping so grayscale has one component and CMYK retains its black channel.
        // Other ICC data spaces keep Other; decoded YCbCr pixels map to RGB, but a YCbCr device profile does not.
        if (colorType == JxlColorType.Other && sourceProfile is not null
            && PixelColorTypeIccMapping.TryGetColorType(sourceProfile.Header.DataColorSpace, out PixelColorType profileColorType)
            && profileColorType is PixelColorType.Luminance or PixelColorType.RGB or PixelColorType.CMYK)
        {
            colorType = JxlMetadata.GetColorType(profileColorType);
        }

        bool useWorkingRgb = colorType == JxlColorType.Xyb && !encoder.ColorType.HasValue && encoder.Distance == 0F;
        this.colorType = colorType switch
        {
            JxlColorType.Xyb when encoder.Distance > 0F && sourceProfile?.Header.DataColorSpace == IccColorSpaceType.Gray => JxlColorType.Luminance,
            JxlColorType.Xyb => JxlColorType.Rgb,
            _ => colorType
        };

        this.bitDepth = bitDepth;
        this.usesXyb = encoder.Distance > 0;
        this.alphaBitDepth = alpha?.BitDepth ?? bitDepth;
        this.blackBitDepth = black?.BitDepth ?? bitDepth;
        this.alphaName = alpha?.Name ?? string.Empty;
        this.blackName = black?.Name ?? string.Empty;

        this.hasBlack = !this.usesXyb && this.colorType != JxlColorType.Luminance
            && sourceProfile?.Header.DataColorSpace == IccColorSpaceType.Cmyk;

        this.hasAlpha = hasAlpha;
        this.alphaAssociated = alpha?.AlphaRepresentation == PixelAlphaRepresentation.Associated;
        this.colorComponents = !this.usesXyb && !this.hasBlack && this.colorType == JxlColorType.Luminance ? 1 : 3;
        int extraCount = encoder.ExtraChannels.Count + (this.hasBlack && !suppliedBlack ? 1 : 0)
            + (hasAlpha && this.suppliedAlphaIndex < 0 ? 1 : 0);
        Guard.IsTrue(extraCount <= 256, nameof(encoder.ExtraChannels), "JPEG XL Level 10 permits at most 256 extra channels.");
        this.extraChannels = new JxlExtraChannelMetadata[extraCount];

        for (int c = 0; c < encoder.ExtraChannels.Count; c++)
        {
            this.extraChannels[c] = encoder.ExtraChannels[c].Metadata;
        }

        int derivedIndex = encoder.ExtraChannels.Count;

        if (this.hasBlack && !suppliedBlack)
        {
            this.derivedBlackIndex = derivedIndex;
            this.extraChannels[derivedIndex++] = new(JxlExtraChannelType.Black, this.blackBitDepth, PixelAlphaRepresentation.None)
            {
                Name = this.blackName
            };
        }

        if (hasAlpha && this.suppliedAlphaIndex < 0)
        {
            this.derivedAlphaIndex = derivedIndex;
            this.extraChannels[derivedIndex] = new(
                JxlExtraChannelType.Alpha,
                this.alphaBitDepth,
                this.alphaAssociated ? PixelAlphaRepresentation.Associated : PixelAlphaRepresentation.Unassociated)
            {
                Name = this.alphaName
            };
        }

        this.componentCount = this.colorComponents + extraCount;
        this.suppliedScratchOffset = Math.Max(encoder.ExtraChannels.Count - 1, 0);

        // Unsqueezed integers need one sign bit. Squeeze reserves two further arithmetic bits;
        // the native max_bitdepth + 2 < 16 rule includes that original sign bit.
        this.narrowSamples = !this.usesXyb && bitDepth.ExponentBits == 0 && bitDepth.BitsPerSample <= (encoder.Progressive ? 12 : 14);
        bool hasBlackChannel = false;

        foreach (JxlExtraChannelMetadata channel in this.extraChannels)
        {
            this.narrowSamples &= channel.BitDepth.ExponentBits == 0 && channel.BitDepth.BitsPerSample <= 14;
            hasBlackChannel |= channel.Type == JxlExtraChannelType.Black;
        }

        this.requiresLevelTen = !this.narrowSamples || hasBlackChannel || extraCount > 4
            || size.Width > (1 << 18) || size.Height > (1 << 18) || (long)size.Width * size.Height > (1L << 28);

        ColorProfileConverter? profileConverter = null;

        if (this.usesXyb)
        {
            // Coding XYB consumes working sRGB. Its retained ICC profile describes suggested output only.
            this.profile = this.colorType == JxlColorType.Luminance
                ? IccProfile.GetCompatibleProfileForEncoding(sourceProfile, PixelColorType.Luminance) : sourceProfile;
        }
        else if (this.colorType == JxlColorType.Other)
        {
            // Other has no compact encoding. Its ICC profile defines the encoded device components.
            this.profile = sourceProfile;
            profileConverter = IccProfile.CreateEncodingConverter(this.profile!, configuration.MemoryAllocator);
        }
        else
        {
            PixelColorType sampleColorType = this.hasBlack
                ? PixelColorType.CMYK : this.colorType == JxlColorType.Luminance ? PixelColorType.Luminance : PixelColorType.RGB;
            this.profile = IccProfile.GetCompatibleProfileForEncoding(
                sourceProfile, sampleColorType, configuration.MemoryAllocator, out profileConverter);
        }

        this.profileConverter = profileConverter;
        this.profileData = this.profile?.GetData() ?? ReadOnlyMemory<byte>.Empty;

        // The Level 10 bound applies to the original ICC bytes, independently of their entropy-coded size.
        // Check it before preparing a profile transform or renting encoder workspace.
        if (this.profileData.Length > (1 << 28))
        {
            throw new ImageFormatException("The ICC profile exceeds the JPEG XL limit of 268435456 bytes.");
        }

        _ = this.profileConverter?.GetIccTransform();
        this.requiresLevelTen |= this.profileData.Length > (1 << 22);

        // ICC data owns its color description. Otherwise retain explicit JPEG XL fields before shared CICP.
        // CICP matrix and range describe YUV storage, not the original RGB samples written by this encoder.
        // An implicit lossless replacement of coding XYB writes the decoded working-sRGB pixels.
        // Its suggested original encoding can have a different transfer curve; restoring that curve
        // would requantize the already packed source pixels instead of preserving their exact codes.
        CicpProfile? cicp = useWorkingRgb ? null : image.Metadata.CicpProfile;
        if (cicp?.Validate() != true)
        {
            cicp = null;
        }

        CicpTransferCharacteristics transfer = cicp?.TransferCharacteristics ?? CicpTransferCharacteristics.Iec61966_2_1;
        bool compatibleCicp = metadata.Gamma.HasValue || metadata.TransferCharacteristics.HasValue
            || (byte)transfer is 1 or 4 or 5 or 6 or 8 or 13 or 14 or 15 or 16 or 17 or 18;

        RgbPrimariesChromaticityCoordinates srgbPrimaries = KnownRgbWorkingSpaces.SRgb.ChromaticityCoordinates;
        ColorNumerics.GetCicpChromaticityCoordinates(
            compatibleCicp ? cicp?.ColorPrimaries ?? CicpColorPrimaries.Unspecified : CicpColorPrimaries.Unspecified,
            out CieXyChromaticityCoordinates cicpWhitePoint,
            out RgbPrimariesChromaticityCoordinates cicpPrimaries);

        CieXyChromaticityCoordinates whitePoint = useWorkingRgb ? KnownIlluminants.D65Chromaticity : metadata.WhitePoint ?? cicpWhitePoint;
        RgbPrimariesChromaticityCoordinates primaries = useWorkingRgb || this.colorType == JxlColorType.Luminance
            ? srgbPrimaries : metadata.Primaries ?? cicpPrimaries;

        // Named coordinates have exact format constants. Custom coordinates use signed millionths.
        this.whitePoint = whitePoint == KnownIlluminants.D65Chromaticity || whitePoint == KnownIlluminants.EChromaticity
            || whitePoint == KnownIlluminants.DciP3Chromaticity ? whitePoint : QuantizeChromaticity(whitePoint);
        this.primaries = primaries.Equals(srgbPrimaries) || primaries.Equals(KnownRgbWorkingSpaces.Rec2020.ChromaticityCoordinates)
            || primaries.Equals(KnownRgbWorkingSpaces.DisplayP3.ChromaticityCoordinates) ? primaries
            : new(QuantizeChromaticity(primaries.R), QuantizeChromaticity(primaries.G), QuantizeChromaticity(primaries.B));
        this.transferCharacteristics = useWorkingRgb ? CicpTransferCharacteristics.Iec61966_2_1 : metadata.TransferCharacteristics ?? (byte)transfer switch
        {
            1 or 6 or 14 or 15 => CicpTransferCharacteristics.ItuRBt709_6,
            8 or 13 or 16 or 17 or 18 => transfer,
            _ => CicpTransferCharacteristics.Iec61966_2_1
        };
        this.gamma = useWorkingRgb ? null : metadata.Gamma is Rational gamma ? (uint)Math.Round(gamma.ToDouble() * 10_000_000D)
            : metadata.TransferCharacteristics is null && (byte)transfer is 4 or 5
                ? (uint)Math.Round(10_000_000D * 5D / ((byte)transfer == 4 ? 11D : 14D)) : null;

        this.renderingIntent = useWorkingRgb ? IccRenderingIntent.MediaRelativeColorimetric
            : metadata.RenderingIntent ?? IccRenderingIntent.MediaRelativeColorimetric;

        float defaultIntensity = this.gamma.HasValue ? 255F
            : this.transferCharacteristics == CicpTransferCharacteristics.SmpteSt2084 ? 10000F
            : this.transferCharacteristics == CicpTransferCharacteristics.AribStdB67 ? 1000F : 255F;

        this.intensityTarget = HalfTypeHelper.Unpack(HalfTypeHelper.Pack(useWorkingRgb ? defaultIntensity : metadata.IntensityTarget ?? defaultIntensity));
        XybTransform iso = KnownXybTransforms.Iso18181;
        XybTransform xybTransform = new(iso.Forward, iso.Inverse, iso.Biases, this.intensityTarget);
        RgbWorkingSpace targetWorkingSpace = this.usesXyb || this.profile is not null ? KnownRgbWorkingSpaces.SRgb
            : JxlColorConverterBase.GetWorkingSpace(
                this.whitePoint,
                this.primaries,
                this.transferCharacteristics,
                this.gamma is uint g ? new Rational(g, 10_000_000) : null,
                this.intensityTarget,
                this.usesXyb);

        this.colorConverter = JxlColorConverterBase.CreateForEncoding(
            this.colorType, this.usesXyb, xybTransform, this.hasBlack, targetWorkingSpace);

        this.groupWidth = encoder.Progressive && !this.usesXyb ? size.Width : Math.Min(size.Width, GroupDimension);
        this.groupHeight = encoder.Progressive && !this.usesXyb ? 1 : Math.Min(size.Height, GroupDimension);
        this.groupColumns = ((size.Width - 1) / GroupDimension) + 1;
        this.groupRows = ((size.Height - 1) / GroupDimension) + 1;
        this.groupCount = checked(this.groupColumns * this.groupRows);

        // Each DC group spans eight coding groups on each axis. A single coding group combines every section.
        this.dcGroupCount = checked((((this.groupColumns - 1) / 8) + 1) * (((this.groupRows - 1) / 8) + 1));
        this.sectionCount = this.groupCount == 1 && !encoder.Progressive ? 1
            : checked(2 + this.dcGroupCount + (this.groupCount * (encoder.Progressive ? this.usesXyb ? 3 : 2 : 1)));

        MemoryAllocator allocator = configuration.MemoryAllocator;

        // The frame must retain its encoded bytes until section lengths are known. Use its declared sample
        // storage as a chunk-size hint, capped before multiplication and by the allocator's segment capacity.
        // This reduces small-chunk rentals without preallocating the complete uncompressed frame.
        int capacity = allocator.GetBufferCapacityInBytes();
        long pixels = Math.Min((long)size.Width * size.Height, capacity);
        int chunkSize = (int)Math.Min(pixels * this.componentCount * ((bitDepth.BitsPerSample + 7) / 8), capacity);
        this.payload = new ChunkedMemoryStream(allocator, chunkSize);
        Buffer2D<int>? samples = null;
        IMemoryOwner<Vector4>? pixelRow = null;
        IMemoryOwner<float>? floatRows = null;
        JxlModularChannel[] progressiveChannels = [];
        Buffer2D<int>? squeezeRows = null;
        Buffer2D<float>? expandedAlpha = null;
        JxlUpsampler? alphaUpsampler = null;
        JxlVarDctEncoder? varDct = null;

        try
        {
            // Modular colors and derived channels stay live together until group coding ends. Supplied channels
            // share one sequential scratch plane. VarDCT owns its colors and derived alpha, so it needs this
            // scratch only when supplied channels exist. Do not reserve another derived-alpha plane here.
            int samplePlanes = this.usesXyb ? Math.Min(encoder.ExtraChannels.Count, 1) : this.componentCount - this.suppliedScratchOffset;

            if (samplePlanes != 0)
            {
                samples = allocator.Allocate2D<int>(this.groupWidth, this.groupHeight * samplePlanes);
                this.sampleRegion = samples.GetRegion();
            }

            pixelRow = allocator.Allocate<Vector4>(this.groupWidth);

            // RGB and alpha rows, optional black, and non-overlapping ICC scratch share one reusable owner.
            floatRows = allocator.Allocate<float>(
                this.groupWidth * (this.hasBlack ? 12 : this.profileConverter is null ? 4 : 7));

            if (this.suppliedAlphaIndex >= 0 && this.extraChannels[this.suppliedAlphaIndex].DimensionShift != 0
                && (this.alphaAssociated || encoder.TransparentColorMode == TransparentColorMode.Clear))
            {
                // Associated colors and transparent-color removal use the alpha seen by the decoder.
                // Retain one expanded plane across frames; the supplied reduced samples remain caller-owned.
                expandedAlpha = allocator.Allocate2D<float>(size.Width, size.Height);
                int factor = 1 << this.extraChannels[this.suppliedAlphaIndex].DimensionShift;
                Size sourceSize = new(((size.Width - 1) / factor) + 1, ((size.Height - 1) / factor) + 1);
                alphaUpsampler = new(
                    ReadOnlyMemory<float>.Empty,
                    ReadOnlyMemory<float>.Empty,
                    ReadOnlyMemory<float>.Empty,
                    size,
                    sourceSize,
                    factor,
                    allocator);
            }

            if (this.usesXyb)
            {
                // DC sections precede AC sections. Retain quantized coefficients and pixel-derived alpha,
                // but reuse one float group for color conversion and the forward DCT across the whole frame.
                varDct = new(allocator, size, encoder.Distance, this.derivedAlphaIndex >= 0);

                for (int c = 0; c < extraCount; c++)
                {
                    JxlModularChannelLayout layout = this.GetExtraChannelLayout(c);

                    if (c != this.globalPrefixCount || layout.Width > GroupDimension || layout.Height > GroupDimension)
                    {
                        break;
                    }

                    this.globalPrefixCount++;
                }
            }
            else if (encoder.Progressive)
            {
                // Squeeze adds two signed arithmetic bits. Raw float carriers and wide integers retain their
                // original bits in the final pass. Quantized XYB already has the required signed headroom.
                this.squeezedComponents = this.usesXyb || (bitDepth.ExponentBits == 0 && bitDepth.BitsPerSample < 30)
                    ? this.colorComponents : 0;
                int colors = this.squeezedComponents;
                progressiveChannels = new JxlModularChannel[this.componentCount + (2 * colors)];
                int halfWidth = (size.Width + 1) / 2;
                int halfHeight = (size.Height + 1) / 2;

                for (int c = 0; c < progressiveChannels.Length; c++)
                {
                    // In-place horizontal then vertical squeeze orders averages, vertical details, horizontal
                    // details, and unchanged channels. Their combined sample count equals the original image.
                    JxlModularChannelLayout layout = c < colors
                        ? new(new Size(halfWidth, halfHeight), 1, 1)
                        : c < 2 * colors ? new(new Size(halfWidth, size.Height / 2), 1, 1)
                        : c < 3 * colors ? new(new Size(size.Width / 2, size.Height), 1, 0)
                        : c < (2 * colors) + this.colorComponents ? new(size, 0, 0)
                        : this.GetExtraChannelLayout(c - (2 * colors) - this.colorComponents);
                    progressiveChannels[c] = JxlModularChannel.Allocate(allocator, layout, false, AllocationOptions.None);

                    if (c == this.globalPrefixCount && layout.Width <= GroupDimension && layout.Height <= GroupDimension)
                    {
                        this.globalPrefixCount++;
                    }
                }

                if (colors != 0)
                {
                    squeezeRows = allocator.Allocate2D<int>(halfWidth, 5 * colors);
                }
            }

            // Sections with no selected samples emit no header. Clean lengths keep those TOC entries at zero.
            this.sectionSizes = allocator.Allocate<uint>(this.sectionCount, AllocationOptions.Clean);
        }
        catch
        {
            varDct?.Dispose();
            alphaUpsampler?.Dispose();
            expandedAlpha?.Dispose();
            squeezeRows?.Dispose();

            foreach (JxlModularChannel channel in progressiveChannels)
            {
                channel.Dispose();
            }

            // The core owns its buffers only after construction succeeds. A failed rent returns the local owners.
            floatRows?.Dispose();
            pixelRow?.Dispose();
            samples?.Dispose();
            this.payload.Dispose();
            throw;
        }

        this.samples = samples;
        this.pixelRow = pixelRow;
        this.floatRows = floatRows;
        this.progressiveChannels = progressiveChannels;
        this.squeezeRows = squeezeRows;
        this.expandedAlpha = expandedAlpha;
        this.alphaUpsampler = alphaUpsampler;
        this.varDct = varDct;
    }

    /// <summary>
    /// Resolves the image settings and encodes its samples without changing source pixels or format settings.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <param name="image">The source image.</param>
    /// <param name="cancellationToken">The token checked before groups and rows.</param>
    public void Encode<TPixel>(Image<TPixel> image, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.PrepareAnimation(image);
        this.WriteContainerMetadata(image.Metadata);

        if (this.usesXyb)
        {
            this.EncodeAlpha(image, default(JxlSampleConverter.Binary32Operator), cancellationToken);
            return;
        }

        JxlBitDepth bitDepth = this.bitDepth;

        if (bitDepth.ExponentBits == 0 && bitDepth.BitsPerSample < 23)
        {
            this.EncodeAlpha(image, new JxlSampleConverter.IntegerEncodingOperator(bitDepth.BitsPerSample), cancellationToken);
        }
        else if (bitDepth.ExponentBits == 0)
        {
            this.EncodeAlpha(image, new JxlSampleConverter.AccurateIntegerEncodingOperator(bitDepth.BitsPerSample), cancellationToken);
        }
        else if (bitDepth.BitsPerSample == 32)
        {
            this.EncodeAlpha(image, default(JxlSampleConverter.Binary32Operator), cancellationToken);
        }
        else if (bitDepth.BitsPerSample == 16 && bitDepth.ExponentBits == 5)
        {
            this.EncodeAlpha(image, default(JxlSampleConverter.HalfEncodingOperator), cancellationToken);
        }
        else if (bitDepth.ExponentBits == 8)
        {
            this.EncodeAlpha(image, new JxlSampleConverter.Exponent8EncodingOperator(bitDepth.BitsPerSample), cancellationToken);
        }
        else
        {
            this.EncodeAlpha(image, new JxlSampleConverter.CustomFloatEncodingOperator(bitDepth), cancellationToken);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.varDct?.Dispose();
        this.alphaUpsampler?.Dispose();
        this.expandedAlpha?.Dispose();
        this.squeezeRows?.Dispose();

        foreach (JxlModularChannel channel in this.progressiveChannels)
        {
            channel.Dispose();
        }

        this.sectionSizes.Dispose();
        this.floatRows.Dispose();
        this.pixelRow.Dispose();
        this.samples?.Dispose();
        this.payload.Dispose();
    }

    /// <summary>
    /// Converts the frame metadata fractions to the clock shared by all JPEG XL frames.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <param name="image">The complete coalesced source sequence.</param>
    private void PrepareAnimation<TPixel>(Image<TPixel> image)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        this.animated = image.Frames.Count > 1;

        if (!this.animated)
        {
            return;
        }

        // The header permits a rate numerator through 2^30 and a denominator through 1024.
        // Metadata owns the delay values. This setup changes only their representation in that shared file clock.
        const ulong MaximumRateNumerator = 1UL << 30;
        const uint MaximumRateDenominator = 1024;
        ulong numerator = 1;
        ulong longestNumerator = 0;
        ulong longestDenominator = 1;
        bool zeroDelay = false;

        foreach (ImageFrame<TPixel> frame in image.Frames)
        {
            LongRational delay = frame.Metadata.GetJxlMetadata().GetDisplayDuration().Simplify();

            // Equivalent positive fractions can have two negative components. Widen before taking
            // their magnitudes so long.MinValue remains representable, without changing source metadata.
            ulong durationNumerator = (ulong)Int128.Abs(delay.Numerator);
            ulong denominator = (ulong)Int128.Abs(delay.Denominator);
            UInt128 multiple = (UInt128)numerator * (denominator / Numerics.GreatestCommonDivisor(numerator, denominator));
            numerator = (ulong)UInt128.Min(multiple, MaximumRateNumerator);
            zeroDelay |= durationNumerator == 0;

            if ((UInt128)durationNumerator * longestDenominator > (UInt128)longestNumerator * denominator)
            {
                longestNumerator = durationNumerator;
                longestDenominator = denominator;
            }
        }

        if (longestNumerator != 0)
        {
            // A frame duration occupies uint ticks. Coarsen the clock only when the longest source duration needs it.
            UInt128 limit = (UInt128)uint.MaxValue * MaximumRateDenominator * longestDenominator / longestNumerator;
            numerator = Math.Max(1UL, (ulong)UInt128.Min(numerator, limit));
        }

        ulong common = 0;

        foreach (ImageFrame<TPixel> frame in image.Frames)
        {
            LongRational delay = frame.Metadata.GetJxlMetadata().GetDisplayDuration();
            ulong durationNumerator = (ulong)Int128.Abs(delay.Numerator);
            ulong durationDenominator = (ulong)Int128.Abs(delay.Denominator);
            ulong duration = (ulong)((UInt128)durationNumerator * numerator / durationDenominator);
            common = Numerics.GreatestCommonDivisor(common, duration);
        }

        ulong maximum = (ulong)((UInt128)longestNumerator * numerator / longestDenominator);
        ulong required = maximum == 0 ? 1 : ((maximum - 1) / uint.MaxValue) + 1;
        uint selected = (uint)Math.Min(required, MaximumRateDenominator);

        // A common divisor changes the clock without changing any duration. Choose the finest exact
        // clock whose longest duration fits uint, within the header's denominator bound.
        for (uint candidate = selected; common != 0 && candidate <= MaximumRateDenominator; candidate++)
        {
            if (common % candidate == 0)
            {
                selected = candidate;
                break;
            }
        }

        if (zeroDelay)
        {
            // A non-final zero-duration frame is hidden. Refine the exact clock before assigning one displayed tick.
            // Multiplying the rate and all durations equally preserves the positive metadata fractions.
            ulong maximumTicks = maximum / selected;
            ulong factor = MaximumRateNumerator / numerator;

            if (maximumTicks != 0)
            {
                // Durations beyond the file's range are already saturated. They cannot refine the
                // clock, but must retain its identity multiplier rather than reduce the rate to zero.
                factor = Math.Min(factor, Math.Max(1UL, uint.MaxValue / maximumTicks));
            }

            numerator *= factor;
        }

        this.ticksPerSecondNumerator = (uint)numerator;
        this.ticksPerSecondDenominator = selected;

        for (int i = 0; i < image.Frames.Count; i++)
        {
            uint duration = this.GetFrameDuration(image.Frames[i].Metadata.GetJxlMetadata().GetDisplayDuration(), i == image.Frames.Count - 1);

            // Level 5 permits displayed frames at no more than 120 fps. The encoded clock and duration,
            // including the one tick used for a displayed zero-delay source frame, determine that rate.
            this.requiresLevelTen |= duration > 0 && (UInt128)duration * selected * 120 < numerator;
        }
    }

    /// <summary>
    /// Expresses one source delay in the prepared file clock, retaining non-final frames as displayed frames.
    /// </summary>
    /// <param name="delay">The finite, nonnegative delay supplied by frame metadata, in seconds.</param>
    /// <param name="last">Whether this is the final displayed frame.</param>
    /// <returns>The serialized unsigned tick count.</returns>
    private uint GetFrameDuration(LongRational delay, bool last)
    {
        // Metadata supplies a nonnegative duration. Component magnitudes retain signed equivalents
        // and long.MinValue components without changing the source fraction.
        ulong numerator = (ulong)Int128.Abs(delay.Numerator);
        ulong denominator = (ulong)Int128.Abs(delay.Denominator);
        UInt128 ticks = (UInt128)numerator * this.ticksPerSecondNumerator
            / ((UInt128)denominator * this.ticksPerSecondDenominator);
        uint duration = (uint)UInt128.Min(ticks, uint.MaxValue);

        // A non-final zero duration denotes a hidden reference frame. Source image frames are displayed.
        return !last && duration == 0 ? 1U : duration;
    }

    /// <summary>
    /// Writes the required level declaration and retained ancillary metadata before the final codestream box.
    /// </summary>
    /// <param name="metadata">The source image metadata, borrowed without changes.</param>
    private void WriteContainerMetadata(SixLabors.ImageSharp.Metadata.ImageMetadata metadata)
    {
        byte[]? exif = this.encoder.SkipMetadata ? null : metadata.ExifProfile?.GetDataForEncoding();
        byte[]? xmp = this.encoder.SkipMetadata ? null : metadata.XmpProfile?.GetDataForEncoding();

        if (exif is null && xmp is null && !this.requiresLevelTen)
        {
            return;
        }

        this.outputStream.Write(JxlConstants.ContainerSignature);
        Span<byte> header = stackalloc byte[20];
        BinaryPrimitives.WriteUInt32BigEndian(header, 20);
        BinaryPrimitives.WriteUInt32BigEndian(header[4..], JxlConstants.FileTypeBox);
        BinaryPrimitives.WriteUInt32BigEndian(header[8..], JxlConstants.MajorBrand);
        BinaryPrimitives.WriteUInt32BigEndian(header[12..], 0);
        BinaryPrimitives.WriteUInt32BigEndian(header[16..], JxlConstants.MajorBrand);
        this.outputStream.Write(header);

        if (this.requiresLevelTen)
        {
            // The level box follows ftyp and contains exactly one level byte. A raw codestream has no
            // level declaration, so Level 10 output needs the container even when metadata is omitted.
            BinaryPrimitives.WriteUInt32BigEndian(header, 9);
            BinaryPrimitives.WriteUInt32BigEndian(header[4..], JxlConstants.LevelBox);
            header[8] = 10;
            this.outputStream.Write(header[..9]);
        }

        if (exif is not null)
        {
            BinaryPrimitives.WriteUInt32BigEndian(header, checked((uint)exif.Length + 12U));
            BinaryPrimitives.WriteUInt32BigEndian(header[4..], JxlConstants.ExifBox);
            BinaryPrimitives.WriteUInt32BigEndian(header[8..], 0);
            this.outputStream.Write(header[..12]);
            this.outputStream.Write(exif);
        }

        if (xmp is not null)
        {
            BinaryPrimitives.WriteUInt32BigEndian(header, checked((uint)xmp.Length + 8U));
            BinaryPrimitives.WriteUInt32BigEndian(header[4..], JxlConstants.XmlBox);
            this.outputStream.Write(header[..8]);
            this.outputStream.Write(xmp);
        }

        // The final box extends to EOF. The codec can write its codestream directly without another retained copy.
        BinaryPrimitives.WriteUInt32BigEndian(header, 0);
        BinaryPrimitives.WriteUInt32BigEndian(header[4..], JxlConstants.CompleteCodestreamBox);
        this.outputStream.Write(header[..8]);
    }

    /// <summary>
    /// Rounds custom xy coordinates to the signed millionths stored in the compact color header.
    /// </summary>
    /// <param name="value">The source chromaticity, validated at its metadata boundary.</param>
    /// <returns>The exact float coordinates that the header reader will reconstruct.</returns>
    private static CieXyChromaticityCoordinates QuantizeChromaticity(CieXyChromaticityCoordinates value)
        => new(MathF.Round(value.X * 1_000_000F) * 1E-6F, MathF.Round(value.Y * 1_000_000F) * 1E-6F);

    /// <summary>
    /// Selects alpha packing once before the frame's group and row traversal.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <typeparam name="TColor">The prepared color packing operation.</typeparam>
    /// <param name="image">The source frame.</param>
    /// <param name="color">The prepared color operation.</param>
    /// <param name="cancellationToken">The token checked during encoding.</param>
    private void EncodeAlpha<TPixel, TColor>(Image<TPixel> image, TColor color, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
        where TColor : struct, JxlSampleConverter.IConversionOperator<float, int>
    {
        if (!this.hasAlpha || (!this.usesXyb && this.alphaBitDepth == this.bitDepth))
        {
            this.EncodeBlack(image, this.outputStream, color, color, cancellationToken);
            return;
        }

        JxlBitDepth bitDepth = this.alphaBitDepth;

        if (bitDepth.ExponentBits == 0 && bitDepth.BitsPerSample < 23)
        {
            this.EncodeBlack(image, this.outputStream, color, new JxlSampleConverter.IntegerEncodingOperator(bitDepth.BitsPerSample), cancellationToken);
        }
        else if (bitDepth.ExponentBits == 0)
        {
            this.EncodeBlack(
                image,
                this.outputStream,
                color,
                new JxlSampleConverter.AccurateIntegerEncodingOperator(bitDepth.BitsPerSample),
                cancellationToken);
        }
        else if (bitDepth.BitsPerSample == 32)
        {
            this.EncodeBlack(image, this.outputStream, color, default(JxlSampleConverter.Binary32Operator), cancellationToken);
        }
        else if (bitDepth.BitsPerSample == 16 && bitDepth.ExponentBits == 5)
        {
            this.EncodeBlack(image, this.outputStream, color, default(JxlSampleConverter.HalfEncodingOperator), cancellationToken);
        }
        else if (bitDepth.ExponentBits == 8)
        {
            this.EncodeBlack(image, this.outputStream, color, new JxlSampleConverter.Exponent8EncodingOperator(bitDepth.BitsPerSample), cancellationToken);
        }
        else
        {
            this.EncodeBlack(image, this.outputStream, color, new JxlSampleConverter.CustomFloatEncodingOperator(bitDepth), cancellationToken);
        }
    }

    /// <summary>
    /// Selects the black component's representation before frame traversal.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <typeparam name="TColor">The color conversion type.</typeparam>
    /// <typeparam name="TAlpha">The alpha conversion type.</typeparam>
    /// <param name="image">The complete source sequence.</param>
    /// <param name="stream">The borrowed destination stream.</param>
    /// <param name="color">The prepared color conversion.</param>
    /// <param name="alpha">The prepared alpha conversion.</param>
    /// <param name="cancellationToken">The token checked during encoding.</param>
    private void EncodeBlack<TPixel, TColor, TAlpha>(
        Image<TPixel> image, Stream stream, TColor color, TAlpha alpha, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
        where TColor : struct, JxlSampleConverter.IConversionOperator<float, int>
        where TAlpha : struct, JxlSampleConverter.IConversionOperator<float, int>
    {
        if (!this.hasBlack || this.blackBitDepth == this.bitDepth)
        {
            this.EncodeFrame(image, stream, color, alpha, color, cancellationToken);
            return;
        }

        if (this.blackBitDepth == this.alphaBitDepth)
        {
            this.EncodeFrame(image, stream, color, alpha, alpha, cancellationToken);
            return;
        }

        JxlBitDepth bitDepth = this.blackBitDepth;

        if (bitDepth.ExponentBits == 0 && bitDepth.BitsPerSample < 23)
        {
            this.EncodeFrame(image, stream, color, alpha, new JxlSampleConverter.IntegerEncodingOperator(bitDepth.BitsPerSample), cancellationToken);
        }
        else if (bitDepth.ExponentBits == 0)
        {
            this.EncodeFrame(image, stream, color, alpha, new JxlSampleConverter.AccurateIntegerEncodingOperator(bitDepth.BitsPerSample), cancellationToken);
        }
        else if (bitDepth.BitsPerSample == 32)
        {
            this.EncodeFrame(image, stream, color, alpha, default(JxlSampleConverter.Binary32Operator), cancellationToken);
        }
        else if (bitDepth.BitsPerSample == 16 && bitDepth.ExponentBits == 5)
        {
            this.EncodeFrame(image, stream, color, alpha, default(JxlSampleConverter.HalfEncodingOperator), cancellationToken);
        }
        else if (bitDepth.ExponentBits == 8)
        {
            this.EncodeFrame(image, stream, color, alpha, new JxlSampleConverter.Exponent8EncodingOperator(bitDepth.BitsPerSample), cancellationToken);
        }
        else
        {
            this.EncodeFrame(image, stream, color, alpha, new JxlSampleConverter.CustomFloatEncodingOperator(bitDepth), cancellationToken);
        }
    }

    /// <summary>
    /// Selects primary-alpha reconstruction before traversing frames, groups or rows.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <typeparam name="TColor">The prepared color packing operation.</typeparam>
    /// <typeparam name="TAlpha">The prepared alpha packing operation.</typeparam>
    /// <typeparam name="TBlack">The prepared black-component conversion.</typeparam>
    /// <param name="source">The source image and complete frame sequence.</param>
    /// <param name="stream">The destination stream.</param>
    /// <param name="colorOperation">The color packing operation selected before traversal.</param>
    /// <param name="alphaOperation">The alpha packing operation selected before traversal.</param>
    /// <param name="blackOperation">The black-component conversion selected before traversal.</param>
    /// <param name="cancellationToken">The token checked during conversion and emission.</param>
    private void EncodeFrame<TPixel, TColor, TAlpha, TBlack>(
        Image<TPixel> source,
        Stream stream,
        TColor colorOperation,
        TAlpha alphaOperation,
        TBlack blackOperation,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
        where TColor : struct, JxlSampleConverter.IConversionOperator<float, int>
        where TAlpha : struct, JxlSampleConverter.IConversionOperator<float, int>
        where TBlack : struct, JxlSampleConverter.IConversionOperator<float, int>
    {
        JxlBitDepth depth = this.alphaBitDepth;

        if (this.suppliedAlphaIndex < 0 || (!this.alphaAssociated && this.encoder.TransparentColorMode != TransparentColorMode.Clear))
        {
            this.EncodeFrame(source, stream, colorOperation, alphaOperation, blackOperation, default(JxlSampleConverter.Binary32Operator), cancellationToken);
        }
        else if (depth.ExponentBits == 0 && this.bitDepth.BitsPerSample < 23)
        {
            this.EncodeFrame(
                source,
                stream,
                colorOperation,
                alphaOperation,
                blackOperation,
                new JxlSampleConverter.IntegerOperator(depth.BitsPerSample),
                cancellationToken);
        }
        else if (depth.ExponentBits == 0)
        {
            this.EncodeFrame(
                source,
                stream,
                colorOperation,
                alphaOperation,
                blackOperation,
                new JxlSampleConverter.AccurateIntegerOperator(depth.BitsPerSample),
                cancellationToken);
        }
        else if (depth.BitsPerSample == 32)
        {
            this.EncodeFrame(source, stream, colorOperation, alphaOperation, blackOperation, default(JxlSampleConverter.Binary32Operator), cancellationToken);
        }
        else if (depth.ExponentBits == 8)
        {
            this.EncodeFrame(
                source,
                stream,
                colorOperation,
                alphaOperation,
                blackOperation,
                new JxlSampleConverter.Exponent8Operator(depth.BitsPerSample),
                cancellationToken);
        }
        else
        {
            this.EncodeFrame(
                source,
                stream,
                colorOperation,
                alphaOperation,
                blackOperation,
                new JxlSampleConverter.CustomFloatOperator(depth),
                cancellationToken);
        }
    }

    /// <summary>
    /// Builds frame sections in retained workspace before writing their lengths and payload.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <typeparam name="TColor">The prepared color packing operation.</typeparam>
    /// <typeparam name="TAlpha">The prepared alpha packing operation.</typeparam>
    /// <typeparam name="TBlack">The prepared black-component operation.</typeparam>
    /// <typeparam name="TAlphaDecode">The prepared reconstruction of primary alpha from stored carriers.</typeparam>
    /// <param name="source">The source image and complete frame sequence.</param>
    /// <param name="stream">The destination stream.</param>
    /// <param name="colorOperation">The prepared color operation.</param>
    /// <param name="alphaOperation">The prepared alpha operation.</param>
    /// <param name="blackOperation">The packing instance for the profile-derived black component.</param>
    /// <param name="alphaDecodeOperation">The matching primary-alpha reconstruction.</param>
    /// <param name="cancellationToken">The token checked between groups, rows and output transfers.</param>
    private void EncodeFrame<TPixel, TColor, TAlpha, TBlack, TAlphaDecode>(
        Image<TPixel> source,
        Stream stream,
        TColor colorOperation,
        TAlpha alphaOperation,
        TBlack blackOperation,
        TAlphaDecode alphaDecodeOperation,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
        where TColor : struct, JxlSampleConverter.IConversionOperator<float, int>
        where TAlpha : struct, JxlSampleConverter.IConversionOperator<float, int>
        where TBlack : struct, JxlSampleConverter.IConversionOperator<float, int>
        where TAlphaDecode : struct, JxlSampleConverter.IConversionOperator<int, float>
    {
        Configuration configuration = source.Configuration;
        MemoryAllocator allocator = configuration.MemoryAllocator;
        int maximumBits = this.usesXyb || this.encoder.Progressive ? 32 : this.bitDepth.BitsPerSample;

        foreach (JxlExtraChannelMetadata channel in this.extraChannels)
        {
            maximumBits = Math.Max(maximumBits, channel.BitDepth.BitsPerSample);
        }

        // Zigzag values can use every uint bit. Hybrid configuration zero therefore needs tokens through 32.
        // The alphabet is a complete power of two selected from declared precision, independently of int carrier storage.
        int codeLength = Math.Max(4, BitOperations.Log2((uint)maximumBits + 1) + 1);
        int alphabetSize = 1 << codeLength;

        // Each residual has one prefix and at most 31 literal bits. Reserve space for every complete
        // word emitted by a 256-value batch, including up to 31 bits retained from the preceding batch.
        int batchBytes = (((GroupDimension * (codeLength + 31)) + 31) / 32) * sizeof(uint);
        using IMemoryOwner<byte> outputOwner = allocator.Allocate<byte>(Math.Max(batchBytes, configuration.StreamProcessingBufferSize));
        using IMemoryOwner<int> lengthOwner = allocator.Allocate<int>(alphabetSize);
        using IMemoryOwner<int> sortedOwner = allocator.Allocate<int>(alphabetSize);
        using IMemoryOwner<uint> codeOwner = allocator.Allocate<uint>(alphabetSize);
        Span<byte> output = outputOwner.Memory.Span;
        Span<int> lengths = lengthOwner.Memory.Span[..alphabetSize];
        Span<int> sorted = sortedOwner.Memory.Span[..alphabetSize];
        Span<uint> prefixCodes = codeOwner.Memory.Span[..alphabetSize];
        lengths.Fill(codeLength);
        _ = HuffmanTableBuilder.Build([], codeLength, lengths, sorted, prefixCodes);

        bool combined = this.groupCount == 1 && !this.encoder.Progressive;
        bool useGradient = this.usesXyb || this.bitDepth.ExponentBits == 0;

        foreach (JxlExtraChannelMetadata channel in this.extraChannels)
        {
            useGradient &= channel.BitDepth.ExponentBits == 0;
        }

        bool clearTransparent = this.suppliedAlphaIndex >= 0
            ? this.encoder.TransparentColorMode == TransparentColorMode.Clear
            : EncodingUtilities.ShouldReplaceTransparentPixels<TPixel>(this.encoder.TransparentColorMode);
        Span<Vector4> pixelRow = this.pixelRow.Memory.Span[..this.groupWidth];
        Span<float> floatRows = this.floatRows.Memory.Span;
        Span<uint> sectionSizes = this.sectionSizes.Memory.Span[..this.sectionCount];
        ChunkedMemoryStream payload = this.payload;
        this.WriteImageHeader(codeLength, prefixCodes, output);

        if (this.varDct is JxlVarDctEncoder varDct)
        {
            this.EncodeVarDctFrame(
                source,
                stream,
                varDct,
                useGradient,
                clearTransparent,
                codeLength,
                prefixCodes,
                pixelRow,
                floatRows,
                sectionSizes,
                output,
                colorOperation,
                alphaOperation,
                blackOperation,
                alphaDecodeOperation,
                cancellationToken);

            return;
        }

        int frameIndex = 0;

        foreach (ImageFrame<TPixel> image in source.Frames)
        {
            this.sourceFrameIndex = frameIndex;
            this.PrepareExpandedAlpha(alphaOperation, alphaDecodeOperation, cancellationToken);

            // Rewind the same pooled chunks for every frame. The recorded position bounds each payload,
            // so a smaller frame never emits bytes left over from its predecessor.
            payload.Position = 0;
            this.outputStream = payload;

            // LF-global starts with DC factors. The global tree has one leaf and no LZ77 or ANS state.
            // Floating carriers use the zero predictor: their raw signed bit patterns need no overflowing subtraction.
            this.WriteBits(1, 1, output);
            this.WriteGlobalTree(useGradient, codeLength, prefixCodes, output);
            this.WriteBits(1, 1, output); // Use the global tree.
            this.WriteBits(1, 1, output); // Default weighted-predictor parameters.

            if (this.squeezedComponents != 0)
            {
                this.WriteBits(2, 1, output); // One channel transform.
                this.WriteBits(2, 2, output); // Squeeze transform.
                this.WriteU32(2, (0, 0), (4, 1), (6, 9), (8, 41), output);

                for (int direction = 0; direction < 2; direction++)
                {
                    this.WriteBits(1, direction == 0 ? 1U : 0U, output); // Horizontal, then vertical.
                    this.WriteBits(1, 1, output); // Insert residuals after the selected colors.
                    this.WriteU32(0, (3, 0), (6, 8), (10, 72), (13, 1096), output);
                    this.WriteU32((uint)this.squeezedComponents, (0, 1), (0, 2), (0, 3), (4, 4), output);
                }
            }
            else
            {
                this.WriteBits(2, 0, output); // No channel transforms.
            }

            if (this.encoder.Progressive)
            {
                this.PrepareProgressiveSamples(
                    image, clearTransparent, pixelRow, floatRows, colorOperation, alphaOperation, blackOperation, alphaDecodeOperation, cancellationToken);
                this.WriteProgressiveSections(useGradient, codeLength, prefixCodes, floatRows, sectionSizes, output, cancellationToken);
            }
            else
            {
                if (!combined)
                {
                    // A leading full-resolution channel larger than one coding group ends the global prefix.
                    // Reduced channels then use DC or AC groups; HF-global has no data in this modular frame.
                    this.FlushBits(output);
                    sectionSizes[0] = checked((uint)payload.Position);
                    this.WritePlainDcSections(useGradient, codeLength, prefixCodes, floatRows, sectionSizes, output, cancellationToken);
                }

                for (int index = 0; index < this.groupCount; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int x = (index % this.groupColumns) * GroupDimension;
                    int y = (index / this.groupColumns) * GroupDimension;
                    Rectangle bounds = new(x, y, Math.Min(GroupDimension, image.Width - x), Math.Min(GroupDimension, image.Height - y));
                    long sectionStart = combined ? 0 : payload.Position;

                    if (!combined)
                    {
                        // Each AC group reuses the global tree and has no local channel transforms.
                        this.WriteBits(1, 1, output);
                        this.WriteBits(1, 1, output);
                        this.WriteBits(2, 0, output);
                    }

                    this.WriteGroup(
                        image,
                        bounds,
                        useGradient,
                        clearTransparent,
                        codeLength,
                        prefixCodes,
                        pixelRow,
                        floatRows,
                        output,
                        colorOperation,
                        alphaOperation,
                        blackOperation,
                        alphaDecodeOperation,
                        cancellationToken);

                    this.FlushBits(output);
                    sectionSizes[combined ? 0 : 2 + this.dcGroupCount + index] = checked((uint)(payload.Position - sectionStart));
                }
            }

            long remaining = payload.Position;
            this.outputStream = stream;
            bool last = ++frameIndex == source.Frames.Count;
            uint duration = 0;

            if (this.animated)
            {
                duration = this.GetFrameDuration(image.Metadata.GetJxlMetadata().GetDisplayDuration(), last);
            }

            this.WriteFrameHeader(sectionSizes, duration, last, output);

            // Section lengths precede the entropy payload. Once the table is written, transfer this frame's
            // completed bytes directly from retained chunks without copying them through the output workspace.
            payload.WriteTo(stream, remaining, cancellationToken);
        }
    }

    /// <summary>
    /// Transforms lossy frames, then writes their DC, metadata and progressive AC sections in format order.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <typeparam name="TColor">The closed color packing operator.</typeparam>
    /// <typeparam name="TAlpha">The closed alpha packing operator.</typeparam>
    /// <typeparam name="TBlack">The closed black packing operator.</typeparam>
    /// <typeparam name="TAlphaDecode">The closed alpha reconstruction operator.</typeparam>
    /// <param name="source">The unchanged source sequence.</param>
    /// <param name="stream">The caller-owned output stream.</param>
    /// <param name="varDct">The retained forward-transform and coefficient owner.</param>
    /// <param name="gradient">Whether the shared modular tree uses the gradient predictor.</param>
    /// <param name="clearTransparent">Whether conversion clears hidden transparent colors.</param>
    /// <param name="codeLength">The prepared prefix width.</param>
    /// <param name="prefixCodes">The prepared LSB-first codes.</param>
    /// <param name="pixelRow">The reusable working pixel row.</param>
    /// <param name="floatRows">The reusable conversion rows and residual scratch.</param>
    /// <param name="sectionSizes">The retained section lengths.</param>
    /// <param name="output">The retained byte buffer.</param>
    /// <param name="colorOperation">The selected color sample operation.</param>
    /// <param name="alphaOperation">The selected alpha sample operation.</param>
    /// <param name="blackOperation">The selected black sample operation.</param>
    /// <param name="alphaDecodeOperation">The selected alpha reconstruction.</param>
    /// <param name="cancellationToken">The token checked between groups, rows and output transfers.</param>
    private void EncodeVarDctFrame<TPixel, TColor, TAlpha, TBlack, TAlphaDecode>(
        Image<TPixel> source,
        Stream stream,
        JxlVarDctEncoder varDct,
        bool gradient,
        bool clearTransparent,
        int codeLength,
        ReadOnlySpan<uint> prefixCodes,
        Span<Vector4> pixelRow,
        Span<float> floatRows,
        Span<uint> sectionSizes,
        Span<byte> output,
        TColor colorOperation,
        TAlpha alphaOperation,
        TBlack blackOperation,
        TAlphaDecode alphaDecodeOperation,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
        where TColor : struct, JxlSampleConverter.IConversionOperator<float, int>
        where TAlpha : struct, JxlSampleConverter.IConversionOperator<float, int>
        where TBlack : struct, JxlSampleConverter.IConversionOperator<float, int>
        where TAlphaDecode : struct, JxlSampleConverter.IConversionOperator<int, float>
    {
        bool combined = this.sectionCount == 1;
        int passes = this.encoder.Progressive ? 3 : 1;
        int dcColumns = ((this.groupColumns - 1) / 8) + 1;
        Size blocks = new(((this.size.Width - 1) / 8) + 1, ((this.size.Height - 1) / 8) + 1);
        Span<uint> residuals = MemoryMarshal.Cast<float, uint>(floatRows[..this.groupWidth]);
        InlineArray64<uint> orderStorage = default;
        Span<uint> order = orderStorage;
        JxlCoefficientOrder.WriteNaturalCoefficientOrder(0, order, cancellationToken);
        int frameIndex = 0;

        foreach (ImageFrame<TPixel> image in source.Frames)
        {
            this.sourceFrameIndex = frameIndex;
            this.PrepareExpandedAlpha(alphaOperation, alphaDecodeOperation, cancellationToken);

            // Convert each source pixel once and transform each eight-row strip before reusing its
            // float planes. Group origins remain aligned to 256 pixels, so alpha tiles and retained
            // coefficients keep their existing coordinates. Only the final image strip needs bottom padding.
            for (int group = 0; group < this.groupCount; group++)
            {
                int x = (group % this.groupColumns) * GroupDimension;
                int y = (group / this.groupColumns) * GroupDimension;
                int width = Math.Min(GroupDimension, image.Width - x);
                int bottom = Math.Min(y + GroupDimension, image.Height);

                for (; y < bottom; y += varDct.GroupHeight)
                {
                    Rectangle bounds = new(x, y, width, Math.Min(varDct.GroupHeight, bottom - y));
                    this.ConvertRows(
                        image,
                        bounds,
                        clearTransparent,
                        pixelRow,
                        floatRows,
                        colorOperation,
                        alphaOperation,
                        blackOperation,
                        alphaDecodeOperation,
                        cancellationToken);

                    varDct.ProcessGroup(bounds, cancellationToken);
                }
            }

            this.payload.Position = 0;
            this.outputStream = this.payload;
            this.WriteBits(1, 1, output); // Default DC matrices in X, Y, B order.
            this.WriteU32((uint)varDct.GlobalScale, (11, 1), (11, 2049), (12, 4097), (16, 8193), output);
            this.WriteU32((uint)varDct.DcQuantizer, (0, 16), (5, 1), (8, 1), (16, 1), output);
            this.WriteBits(1, 1, output); // Default block contexts: no DC or quantizer thresholds.
            this.WriteBits(1, 1, output); // Default color correlation: X=0, B=1.
            this.WriteGlobalTree(gradient, codeLength, prefixCodes, output);

            if (this.extraChannels.Length != 0)
            {
                this.WriteModularHeader(output);

                for (int c = 0; c < this.globalPrefixCount; c++)
                {
                    JxlModularChannelLayout layout = this.GetExtraChannelLayout(c);
                    this.WriteVarDctExtraChannel(
                        varDct,
                        c,
                        new Rectangle(0, 0, layout.Width, layout.Height),
                        gradient,
                        residuals,
                        codeLength,
                        prefixCodes,
                        output,
                        cancellationToken);
                }
            }

            if (!combined)
            {
                this.FlushBits(output);
                sectionSizes[0] = checked((uint)this.payload.Position);
            }

            for (int group = 0; group < this.dcGroupCount; group++)
            {
                long start = this.payload.Position;
                int x = (group % dcColumns) * GroupDimension;
                int y = (group / dcColumns) * GroupDimension;
                Rectangle rectangle = new(x, y, Math.Min(GroupDimension, blocks.Width - x), Math.Min(GroupDimension, blocks.Height - y));
                this.WriteBits(2, 0, output); // Linear DC quantization needs no extra precision bits.
                this.WriteModularHeader(output);

                // DC modular streams visit Y, X, B. Each substream resets its predictor at its own edges.
                for (int encodedComponent = 0; encodedComponent < 3; encodedComponent++)
                {
                    int component = encodedComponent < 2 ? encodedComponent ^ 1 : encodedComponent;

                    // Pixel conversion has finished. Reuse two of its component rows for DC prediction;
                    // the disjoint first row still holds packed residuals for entropy emission.
                    Span<int> values = MemoryMarshal.Cast<float, int>(floatRows.Slice(this.groupWidth, rectangle.Width));
                    Span<int> previous = MemoryMarshal.Cast<float, int>(floatRows.Slice(2 * this.groupWidth, rectangle.Width));

                    for (int row = 0; row < rectangle.Height; row++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        varDct.CopyDcRow(component, rectangle.X, rectangle.Y + row, values);
                        Span<uint> packed = residuals[..rectangle.Width];

                        if (gradient)
                        {
                            JxlModularPredictor.PackGradientResiduals(values, row == 0 ? [] : previous, packed);
                        }
                        else
                        {
                            JxlModularPredictor.PackZeroResiduals(values, packed);
                        }

                        this.WriteValues(packed, codeLength, prefixCodes, output);

                        // Retain the previous row by swapping scratch spans. Each component and DC
                        // group starts with an empty predictor row, so old scratch values are never read.
                        Span<int> next = previous;
                        previous = values;
                        values = next;
                    }
                }

                Rectangle pixels = new(rectangle.X * 8, rectangle.Y * 8, 8 * GroupDimension, 8 * GroupDimension);
                this.WriteVarDctExtras(varDct, pixels, 3, 1000, gradient, residuals, codeLength, prefixCodes, output, cancellationToken);

                // DCT8 has one transform record per block. The two correlation planes retain the native
                // fast-path defaults. Sharpness four selects the native fast-path restoration strength.
                int area = rectangle.Width * rectangle.Height;
                this.WriteBits(32 - BitOperations.LeadingZeroCount((uint)(area - 1)), (uint)area - 1, output);
                this.WriteModularHeader(output);
                Size correlation = new(((rectangle.Width - 1) / 8) + 1, ((rectangle.Height - 1) / 8) + 1);
                this.WriteConstantChannel(correlation, 0, gradient, residuals, codeLength, prefixCodes, output, cancellationToken);
                this.WriteConstantChannel(correlation, 0, gradient, residuals, codeLength, prefixCodes, output, cancellationToken);
                this.WriteConstantChannel(new Size(area, 1), 0, gradient, residuals, codeLength, prefixCodes, output, cancellationToken);
                this.WriteConstantChannel(
                    new Size(area, 1), varDct.AcQuantizer - 1, gradient, residuals, codeLength, prefixCodes, output, cancellationToken);

                this.WriteConstantChannel(rectangle.Size, 4, gradient, residuals, codeLength, prefixCodes, output, cancellationToken);

                if (!combined)
                {
                    this.FlushBits(output);
                    sectionSizes[1 + group] = checked((uint)(this.payload.Position - start));
                }
            }

            long acGlobalStart = this.payload.Position;
            this.WriteBits(1, 1, output); // Default AC quantization matrices.
            this.WriteBits(32 - BitOperations.LeadingZeroCount((uint)this.groupCount - 1), 0, output); // One AC histogram set.

            for (int pass = 0; pass < passes; pass++)
            {
                this.WriteU32(0, (0, 0x5F), (0, 0x13), (0, 0), (13, 0), output); // Natural coefficient orders.
                this.WritePrefixHeader(
                    JxlConstants.DefaultBlockContextCount * (JxlConstants.NonzeroContextCount + JxlConstants.ZeroDensityContextCount),
                    codeLength,
                    output);
            }

            if (!combined)
            {
                this.FlushBits(output);
                sectionSizes[1 + this.dcGroupCount] = checked((uint)(this.payload.Position - acGlobalStart));
            }

            for (int pass = 0; pass < passes; pass++)
            {
                // Spectral stages expose quarter and half resolution. Extra channels with those
                // dimension shifts follow the same stages; full-resolution alpha belongs to the last pass.
                int minimumShift = passes == 1 ? 0 : 2 - pass;
                int maximumShift = passes == 1 ? 2 : minimumShift;

                for (int group = 0; group < this.groupCount; group++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    long start = combined ? 0 : this.payload.Position;
                    int x = (group % this.groupColumns) * (GroupDimension / 8);
                    int y = (group / this.groupColumns) * (GroupDimension / 8);
                    Rectangle rectangle = new(x, y, Math.Min(GroupDimension / 8, blocks.Width - x), Math.Min(GroupDimension / 8, blocks.Height - y));
                    this.WriteAcGroup(varDct, rectangle, pass, passes, order, codeLength, prefixCodes, output, cancellationToken);
                    Rectangle pixels = new(x * 8, y * 8, GroupDimension, GroupDimension);
                    this.WriteVarDctExtras(
                        varDct, pixels, minimumShift, maximumShift, gradient, residuals, codeLength, prefixCodes, output, cancellationToken);

                    this.FlushBits(output);
                    sectionSizes[combined ? 0 : 2 + this.dcGroupCount + (pass * this.groupCount) + group]
                        = checked((uint)(this.payload.Position - start));
                }
            }

            long length = this.payload.Position;
            this.outputStream = stream;
            bool last = ++frameIndex == source.Frames.Count;
            uint duration = this.animated ? this.GetFrameDuration(image.Metadata.GetJxlMetadata().GetDisplayDuration(), last) : 0;
            this.WriteFrameHeader(sectionSizes, duration, last, output);
            this.payload.WriteTo(stream, length, cancellationToken);
        }
    }

    /// <summary>
    /// Writes one spectral contribution in block-row and Y, X, B order through the shared prefix writer.
    /// </summary>
    /// <param name="varDct">The retained quantized coefficients.</param>
    /// <param name="rectangle">The group in block coordinates.</param>
    /// <param name="pass">The spectral pass index.</param>
    /// <param name="passes">One full pass or three spectral passes.</param>
    /// <param name="order">The natural DCT8 coefficient order.</param>
    /// <param name="codeLength">The prepared prefix width.</param>
    /// <param name="prefixCodes">The prepared LSB-first codes.</param>
    /// <param name="output">The retained byte buffer.</param>
    /// <param name="cancellationToken">The token checked between block rows.</param>
    private void WriteAcGroup(
        JxlVarDctEncoder varDct,
        Rectangle rectangle,
        int pass,
        int passes,
        ReadOnlySpan<uint> order,
        int codeLength,
        ReadOnlySpan<uint> prefixCodes,
        Span<byte> output,
        CancellationToken cancellationToken)
    {
        InlineArray64<int> scanStorage = default;
        Span<int> scan = scanStorage;
        InlineArray64<uint> packedStorage = default;
        Span<uint> packedValues = packedStorage;
        InlineArray64<int> positionStorage = default;
        Span<int> positions = positionStorage;
        InlineArray64<int> indexStorage = default;
        Span<int> indices = indexStorage;
        int selectedCount = 0;

        // Select each pass's frequencies once. The three spectral regions contain 3, 5 and 55 AC
        // coefficients. Their scan positions retain the natural order, including gaps for earlier passes.
        // Unselected scan entries start at zero and are never changed. Every selected entry is overwritten
        // for every block, so scans need neither another coefficient pass nor a per-block clear.
        for (int k = 1; k < 64; k++)
        {
            int index = (int)order[k];
            int fx = index / 8;
            int fy = index & 7;
            int stage = fx < 2 && fy < 2 ? 0 : fx < 3 && fy < 3 ? 1 : 2;

            if (passes == 1 || pass == stage)
            {
                positions[selectedCount] = k;
                indices[selectedCount++] = index;
            }
        }

        for (int y = rectangle.Y; y < rectangle.Bottom; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = varDct.TryGetCoefficientRow(0, rectangle.X, y, rectangle.Width, out Span<int> rowX);
            _ = varDct.TryGetCoefficientRow(1, rectangle.X, y, rectangle.Width, out Span<int> rowY);
            _ = varDct.TryGetCoefficientRow(2, rectangle.X, y, rectangle.Width, out Span<int> rowB);

            for (int x = rectangle.X; x < rectangle.Right; x++)
            {
                for (int encodedComponent = 0; encodedComponent < 3; encodedComponent++)
                {
                    int component = encodedComponent < 2 ? encodedComponent ^ 1 : encodedComponent;
                    ReadOnlySpan<int> row = component switch
                    {
                        0 => rowX,
                        1 => rowY,
                        _ => rowB
                    };

                    // A normal allocator exposes this group row as one contiguous footprint. Small
                    // segments borrow individual blocks instead; both paths visit the same 64-value layout.
                    ReadOnlySpan<int> coefficients = row.IsEmpty
                        ? varDct.GetCoefficientBlock(component, x, y)
                        : row.Slice((x - rectangle.X) * 64, 64);

                    int count = 0;
                    int last = 0;

                    // DC never appears in this scan. Selected positions increase in wire order, so
                    // the final nonzero position also bounds the coefficient symbols that must be emitted.
                    for (int i = 0; i < selectedCount; i++)
                    {
                        int k = positions[i];
                        int value = coefficients[indices[i]];
                        scan[k] = value;

                        if (value != 0)
                        {
                            count++;
                            last = k;
                        }
                    }

                    this.WriteValue((uint)count, codeLength, prefixCodes, output);

                    if (last != 0)
                    {
                        Span<uint> packed = packedValues[..last];
                        JxlModularPredictor.PackZeroResiduals(scan.Slice(1, last), packed);
                        this.WriteValues(packed, codeLength, prefixCodes, output);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Writes the extra channels assigned to one DC or progressive AC section.
    /// </summary>
    /// <param name="varDct">The retained derived-alpha owner.</param>
    /// <param name="pixels">The uncropped section rectangle in full-resolution pixels.</param>
    /// <param name="minimumShift">The inclusive minimum channel reduction.</param>
    /// <param name="maximumShift">The inclusive maximum channel reduction.</param>
    /// <param name="gradient">Whether the global tree uses gradient prediction.</param>
    /// <param name="residuals">The reusable residual row.</param>
    /// <param name="codeLength">The prepared prefix width.</param>
    /// <param name="prefixCodes">The prepared LSB-first codes.</param>
    /// <param name="output">The retained byte buffer.</param>
    /// <param name="cancellationToken">The token checked between channels and rows.</param>
    private void WriteVarDctExtras(
        JxlVarDctEncoder varDct,
        Rectangle pixels,
        int minimumShift,
        int maximumShift,
        bool gradient,
        Span<uint> residuals,
        int codeLength,
        ReadOnlySpan<uint> prefixCodes,
        Span<byte> output,
        CancellationToken cancellationToken)
    {
        bool hasSamples = false;

        for (int c = this.globalPrefixCount; c < this.extraChannels.Length; c++)
        {
            int shift = this.extraChannels[c].DimensionShift;

            if (shift < minimumShift || shift > maximumShift)
            {
                continue;
            }

            Rectangle region = this.GetExtraChannelRegion(c, pixels.X, pixels.Y, pixels.Width);

            if (region.Width == 0 || region.Height == 0)
            {
                continue;
            }

            if (!hasSamples)
            {
                this.WriteModularHeader(output);
                hasSamples = true;
            }

            this.WriteVarDctExtraChannel(varDct, c, region, gradient, residuals, codeLength, prefixCodes, output, cancellationToken);
        }
    }

    /// <summary>
    /// Writes supplied extra samples or retained pixel-derived alpha without another color conversion.
    /// </summary>
    /// <param name="varDct">The retained derived-alpha owner.</param>
    /// <param name="index">The encoded extra-channel index.</param>
    /// <param name="region">The clipped channel region in its own reduced coordinates.</param>
    /// <param name="gradient">Whether the global tree uses gradient prediction.</param>
    /// <param name="residuals">The reusable residual row.</param>
    /// <param name="codeLength">The prepared prefix width.</param>
    /// <param name="prefixCodes">The prepared LSB-first codes.</param>
    /// <param name="output">The retained byte buffer.</param>
    /// <param name="cancellationToken">The token checked between rows.</param>
    private void WriteVarDctExtraChannel(
        JxlVarDctEncoder varDct,
        int index,
        Rectangle region,
        bool gradient,
        Span<uint> residuals,
        int codeLength,
        ReadOnlySpan<uint> prefixCodes,
        Span<byte> output,
        CancellationToken cancellationToken)
    {
        if (index < this.encoder.ExtraChannels.Count)
        {
            this.CopySuppliedSamples(index, region, cancellationToken);

            if (gradient)
            {
                this.WriteGradientComponent(this.colorComponents + index, region.Size, residuals, codeLength, prefixCodes, output, cancellationToken);
            }
            else
            {
                this.WriteZeroComponent(this.colorComponents + index, region.Size, residuals, codeLength, prefixCodes, output, cancellationToken);
            }

            return;
        }

        Span<uint> packed = residuals[..region.Width];

        for (int y = 0; y < region.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadOnlySpan<int> values = varDct.GetAlphaRow(region.X, region.Y + y)[..region.Width];
            ReadOnlySpan<int> previous = y == 0 ? [] : varDct.GetAlphaRow(region.X, region.Y + y - 1)[..region.Width];

            if (gradient)
            {
                JxlModularPredictor.PackGradientResiduals(values, previous, packed);
            }
            else
            {
                JxlModularPredictor.PackZeroResiduals(values, packed);
            }

            this.WriteValues(packed, codeLength, prefixCodes, output);
        }
    }

    /// <summary>
    /// Emits a constant metadata plane in bounded batches without allocating its full sample grid.
    /// </summary>
    /// <param name="size">The metadata plane dimensions.</param>
    /// <param name="value">The actual transform, quantizer, correlation or sharpness value.</param>
    /// <param name="gradient">Whether the global tree uses gradient prediction.</param>
    /// <param name="residuals">The reusable packed-value scratch.</param>
    /// <param name="codeLength">The prepared prefix width.</param>
    /// <param name="prefixCodes">The prepared LSB-first codes.</param>
    /// <param name="output">The retained byte buffer.</param>
    /// <param name="cancellationToken">The token checked between rows and batches.</param>
    private void WriteConstantChannel(
        Size size,
        int value,
        bool gradient,
        Span<uint> residuals,
        int codeLength,
        ReadOnlySpan<uint> prefixCodes,
        Span<byte> output,
        CancellationToken cancellationToken)
    {
        Span<int> carriers = MemoryMarshal.Cast<uint, int>(residuals);

        // A constant gradient-predicted plane has only one nonzero residual, at its first sample.
        // The zero predictor stores the same value at every position. Pack through the existing SIMD worker.
        carriers.Fill(gradient ? 0 : value);
        JxlModularPredictor.PackZeroResiduals(carriers, residuals);

        for (int y = 0; y < size.Height; y++)
        {
            for (int x = 0; x < size.Width; x += residuals.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int count = Math.Min(residuals.Length, size.Width - x);

                if (gradient && y == 0 && x == 0)
                {
                    carriers[0] = value;
                    JxlModularPredictor.PackZeroResiduals(carriers[..1], residuals[..1]);
                }

                this.WriteValues(residuals[..count], codeLength, prefixCodes, output);

                if (gradient)
                {
                    residuals[0] = 0;
                }
            }
        }
    }

    /// <summary>
    /// Writes the single-leaf shared modular tree and its residual entropy description.
    /// </summary>
    /// <param name="gradient">Whether integer channels use the clamped gradient instead of the zero predictor.</param>
    /// <param name="codeLength">The prepared prefix width.</param>
    /// <param name="prefixCodes">The prepared LSB-first codes.</param>
    /// <param name="output">The retained byte buffer.</param>
    private void WriteGlobalTree(bool gradient, int codeLength, ReadOnlySpan<uint> prefixCodes, Span<byte> output)
    {
        this.WriteBits(1, 1, output);
        this.WritePrefixHeader(6, codeLength, output);
        this.WriteValue(0, codeLength, prefixCodes, output); // Property zero marks a leaf.
        this.WriteValue(gradient ? 5U : 0U, codeLength, prefixCodes, output);
        this.WriteValue(0, codeLength, prefixCodes, output); // No signed prediction offset.
        this.WriteValue(0, codeLength, prefixCodes, output); // Multiplier exponent zero.
        this.WriteValue(0, codeLength, prefixCodes, output); // Unit residual multiplier, stored minus one.
        this.WritePrefixHeader(1, codeLength, output);
    }

    /// <summary>
    /// Writes a modular substream header that reuses the global tree and has no local transforms.
    /// </summary>
    /// <param name="output">The retained byte buffer.</param>
    private void WriteModularHeader(Span<byte> output)
    {
        this.WriteBits(1, 1, output); // Reuse the global tree.
        this.WriteBits(1, 1, output); // Default weighted-predictor parameters.
        this.WriteBits(2, 0, output); // No local channel transforms.
    }

    /// <summary>
    /// Converts one group into declared sample carriers, then emits its component-major entropy streams.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <typeparam name="TColor">The prepared color packing operation.</typeparam>
    /// <typeparam name="TAlpha">The prepared alpha packing operation.</typeparam>
    /// <typeparam name="TBlack">The prepared black-component conversion.</typeparam>
    /// <typeparam name="TAlphaDecode">The prepared primary-alpha reconstruction.</typeparam>
    /// <param name="image">The unchanged source image.</param>
    /// <param name="bounds">The clipped coding group in source coordinates.</param>
    /// <param name="useGradient">Whether integer samples use the bounded gradient predictor.</param>
    /// <param name="clearTransparent">Whether the output conversion clears colors at zero alpha.</param>
    /// <param name="codeLength">The common prefix-code width.</param>
    /// <param name="prefixCodes">The prepared least-significant-bit-first codes.</param>
    /// <param name="pixelRow">The retained converted vector row.</param>
    /// <param name="floatRows">Four retained component rows with this.groupWidth elements each.</param>
    /// <param name="output">The retained output byte buffer.</param>
    /// <param name="colorOperation">The color packing operation selected before traversal.</param>
    /// <param name="alphaOperation">The alpha packing operation selected before traversal.</param>
    /// <param name="blackOperation">The black-component conversion selected before traversal.</param>
    /// <param name="alphaDecodeOperation">The primary-alpha reconstruction selected before traversal.</param>
    /// <param name="cancellationToken">The token checked before rows.</param>
    private void WriteGroup<TPixel, TColor, TAlpha, TBlack, TAlphaDecode>(
        ImageFrame<TPixel> image,
        Rectangle bounds,
        bool useGradient,
        bool clearTransparent,
        int codeLength,
        ReadOnlySpan<uint> prefixCodes,
        Span<Vector4> pixelRow,
        Span<float> floatRows,
        Span<byte> output,
        TColor colorOperation,
        TAlpha alphaOperation,
        TBlack blackOperation,
        TAlphaDecode alphaDecodeOperation,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
        where TColor : struct, JxlSampleConverter.IConversionOperator<float, int>
        where TAlpha : struct, JxlSampleConverter.IConversionOperator<float, int>
        where TBlack : struct, JxlSampleConverter.IConversionOperator<float, int>
        where TAlphaDecode : struct, JxlSampleConverter.IConversionOperator<int, float>
    {
        this.ConvertRows(
            image,
            bounds,
            clearTransparent,
            pixelRow,
            floatRows,
            colorOperation,
            alphaOperation,
            blackOperation,
            alphaDecodeOperation,
            cancellationToken);

        // Conversion is complete for the group. Reuse its first float scratch row as packed uint residuals;
        // the next group overwrites that row before any float sample reads it. No additional row owner is needed.
        Span<uint> residuals = MemoryMarshal.Cast<float, uint>(floatRows[..bounds.Width]);

        // Select the predictor outside sample traversal. Both workers borrow the same completed component planes.
        for (int component = 0; component < this.componentCount; component++)
        {
            Size componentSize = bounds.Size;
            int extraIndex = component - this.colorComponents;

            if (extraIndex >= 0 && extraIndex < this.encoder.ExtraChannels.Count)
            {
                int shift = this.extraChannels[extraIndex].DimensionShift;

                // Eightfold-reduced samples belong to DC groups unless the whole frame has one combined stream.
                if (shift >= 3 && this.groupCount != 1)
                {
                    continue;
                }

                Rectangle region = this.GetExtraChannelRegion(extraIndex, bounds.X, bounds.Y, GroupDimension);
                componentSize = region.Size;
                this.CopySuppliedSamples(extraIndex, region, cancellationToken);
            }

            if (useGradient)
            {
                this.WriteGradientComponent(component, componentSize, residuals, codeLength, prefixCodes, output, cancellationToken);
            }
            else
            {
                this.WriteZeroComponent(component, componentSize, residuals, codeLength, prefixCodes, output, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Gets a supplied or derived extra channel's coded dimensions and grid reduction.
    /// </summary>
    /// <param name="index">The extra-channel index in encoded order.</param>
    /// <returns>The channel layout before modular transforms.</returns>
    private JxlModularChannelLayout GetExtraChannelLayout(int index)
    {
        int shift = this.extraChannels[index].DimensionShift;
        return new(new Size(((this.size.Width - 1) >> shift) + 1, ((this.size.Height - 1) >> shift) + 1), shift, shift);
    }

    /// <summary>
    /// Maps a full coding-grid rectangle to one extra channel and clips its final reduced samples.
    /// </summary>
    /// <param name="index">The extra-channel index.</param>
    /// <param name="x">The full-resolution group origin on the horizontal axis.</param>
    /// <param name="y">The full-resolution group origin on the vertical axis.</param>
    /// <param name="dimension">The full coding-grid extent before edge clipping.</param>
    /// <returns>The selected region in reduced channel coordinates.</returns>
    private Rectangle GetExtraChannelRegion(int index, int x, int y, int dimension)
    {
        JxlModularChannelLayout layout = this.GetExtraChannelLayout(index);
        x >>= layout.HorizontalShift;
        y >>= layout.VerticalShift;
        int width = Math.Min(dimension >> layout.HorizontalShift, Math.Max(layout.Width - x, 0));
        int height = Math.Min(dimension >> layout.VerticalShift, Math.Max(layout.Height - y, 0));
        return new(x, y, width, height);
    }

    /// <summary>
    /// Packs a borrowed supplied region into the existing group sample rows without changing its source.
    /// </summary>
    /// <param name="index">The supplied extra-channel index.</param>
    /// <param name="bounds">The region in reduced channel coordinates.</param>
    /// <param name="cancellationToken">The token checked between rows.</param>
    private void CopySuppliedSamples(int index, Rectangle bounds, CancellationToken cancellationToken)
    {
        // The preceding supplied channel has finished entropy coding before this plane is filled again.
        Rectangle targetBounds = new(0, this.usesXyb ? 0 : this.colorComponents * this.groupHeight, bounds.Width, bounds.Height);
        this.EncodeSuppliedSamples(index, bounds, this.sampleRegion.GetSubRegion(targetBounds), cancellationToken);
    }

    /// <summary>
    /// Selects one supplied channel's packing operation before traversing its plain or progressive target rows.
    /// </summary>
    /// <param name="index">The supplied extra-channel index.</param>
    /// <param name="bounds">The borrowed source region in channel coordinates.</param>
    /// <param name="destination">The ready group scratch or progressive target region.</param>
    /// <param name="cancellationToken">The token checked between rows.</param>
    private void EncodeSuppliedSamples(int index, Rectangle bounds, Buffer2DRegion<int> destination, CancellationToken cancellationToken)
    {
        Buffer2DRegion<float> source = this.encoder.ExtraChannels[index].Frames[this.sourceFrameIndex].GetRegion(bounds);
        JxlBitDepth depth = this.extraChannels[index].BitDepth;

        // Match the main color and alpha dispatch. Each closed worker keeps precision selection outside row traversal
        // and reuses the sample converter's SIMD arithmetic and scalar remainder without a second packing implementation.
        if (depth.ExponentBits == 0 && depth.BitsPerSample < 23)
        {
            EncodeSuppliedSamples(source, destination, new JxlSampleConverter.IntegerEncodingOperator(depth.BitsPerSample), cancellationToken);
        }
        else if (depth.ExponentBits == 0)
        {
            EncodeSuppliedSamples(source, destination, new JxlSampleConverter.AccurateIntegerEncodingOperator(depth.BitsPerSample), cancellationToken);
        }
        else if (depth.BitsPerSample == 32)
        {
            EncodeSuppliedSamples(source, destination, default(JxlSampleConverter.Binary32Operator), cancellationToken);
        }
        else if (depth.BitsPerSample == 16 && depth.ExponentBits == 5)
        {
            EncodeSuppliedSamples(source, destination, default(JxlSampleConverter.HalfEncodingOperator), cancellationToken);
        }
        else if (depth.ExponentBits == 8)
        {
            EncodeSuppliedSamples(source, destination, new JxlSampleConverter.Exponent8EncodingOperator(depth.BitsPerSample), cancellationToken);
        }
        else
        {
            EncodeSuppliedSamples(source, destination, new JxlSampleConverter.CustomFloatEncodingOperator(depth), cancellationToken);
        }
    }

    /// <summary>
    /// Packs supplied sample rows with the representation selected for this channel.
    /// </summary>
    /// <typeparam name="TOperation">The prepared sample packing operation.</typeparam>
    /// <param name="source">The caller-owned reduced source region.</param>
    /// <param name="destination">The encoder-owned carrier target of the same dimensions.</param>
    /// <param name="operation">The prepared integer or floating-point operation.</param>
    /// <param name="cancellationToken">The token checked between rows.</param>
    private static void EncodeSuppliedSamples<TOperation>(
        Buffer2DRegion<float> source,
        Buffer2DRegion<int> destination,
        TOperation operation,
        CancellationToken cancellationToken)
        where TOperation : struct, JxlSampleConverter.IConversionOperator<float, int>
    {
        for (int y = 0; y < source.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JxlSampleConverter.Encode(source.DangerousGetRowSpan(y), destination.DangerousGetRowSpan(y), operation);
        }
    }

    /// <summary>
    /// Reconstructs reduced primary alpha with the same sample precision and phase filters as decoding.
    /// </summary>
    /// <typeparam name="TAlpha">The alpha packing operation selected before frame traversal.</typeparam>
    /// <typeparam name="TAlphaDecode">The matching primary-alpha reconstruction.</typeparam>
    /// <param name="operation">The prepared alpha packing operation.</param>
    /// <param name="decodeOperation">The reconstruction instance for the primary alpha's declared precision.</param>
    /// <param name="cancellationToken">The token checked between rows and filter passes.</param>
    private void PrepareExpandedAlpha<TAlpha, TAlphaDecode>(TAlpha operation, TAlphaDecode decodeOperation, CancellationToken cancellationToken)
        where TAlpha : struct, JxlSampleConverter.IConversionOperator<float, int>
        where TAlphaDecode : struct, JxlSampleConverter.IConversionOperator<int, float>
    {
        if (this.expandedAlpha is not Buffer2D<float> destination || this.alphaUpsampler is not JxlUpsampler upsampler)
        {
            return;
        }

        Buffer2D<float> source = this.encoder.ExtraChannels[this.suppliedAlphaIndex].Frames[this.sourceFrameIndex];
        Span<int> carriers = this.GetComponentRow(this.colorComponents + this.suppliedAlphaIndex, 0);

        for (int y = 0; y < source.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadOnlySpan<float> input = source.DangerousGetRowSpan(y);
            Span<float> output = destination.DangerousGetRowSpan(y);

            // The plain encoder retains group-width carrier scratch, even when a reduced source row spans groups.
            // Convert it in contiguous chunks before the in-place upsampler consumes the complete reduced rectangle.
            for (int x = 0; x < source.Width; x += carriers.Length)
            {
                int count = Math.Min(carriers.Length, source.Width - x);
                JxlSampleConverter.Encode(input.Slice(x, count), carriers[..count], operation);
                JxlSampleConverter.Convert<int, TAlphaDecode>(carriers[..count], output.Slice(x, count), decodeOperation);
            }
        }

        upsampler.Apply(destination, new Size(source.Width, source.Height), 1 << this.extraChannels[this.suppliedAlphaIndex].DimensionShift, cancellationToken);
    }

    /// <summary>
    /// Writes eightfold-reduced supplied channels into their DC-group streams in a plain modular frame.
    /// </summary>
    /// <param name="gradient">Whether the shared tree uses gradient prediction.</param>
    /// <param name="codeLength">The prepared prefix-code width.</param>
    /// <param name="prefixCodes">The prepared prefix codes.</param>
    /// <param name="floatRows">Conversion scratch reused for unsigned residuals.</param>
    /// <param name="sectionSizes">The frame section table.</param>
    /// <param name="output">The retained output byte workspace.</param>
    /// <param name="cancellationToken">The token checked between groups and rows.</param>
    private void WritePlainDcSections(
        bool gradient,
        int codeLength,
        ReadOnlySpan<uint> prefixCodes,
        Span<float> floatRows,
        Span<uint> sectionSizes,
        Span<byte> output,
        CancellationToken cancellationToken)
    {
        int columns = ((this.groupColumns - 1) / 8) + 1;
        Span<uint> residuals = MemoryMarshal.Cast<float, uint>(floatRows[..this.groupWidth]);

        for (int group = 0; group < this.dcGroupCount; group++)
        {
            long start = this.payload.Position;
            bool hasSamples = false;

            for (int c = 0; c < this.encoder.ExtraChannels.Count; c++)
            {
                if (this.extraChannels[c].DimensionShift < 3)
                {
                    continue;
                }

                Rectangle region = this.GetExtraChannelRegion(
                    c,
                    (group % columns) * (8 * GroupDimension),
                    (group / columns) * (8 * GroupDimension),
                    8 * GroupDimension);

                if (region.Width == 0 || region.Height == 0)
                {
                    continue;
                }

                if (!hasSamples)
                {
                    this.WriteBits(1, 1, output); // Reuse the global tree.
                    this.WriteBits(1, 1, output); // Default weighted predictor.
                    this.WriteBits(2, 0, output); // No local transforms.
                    hasSamples = true;
                }

                this.CopySuppliedSamples(c, region, cancellationToken);

                if (gradient)
                {
                    this.WriteGradientComponent(this.colorComponents + c, region.Size, residuals, codeLength, prefixCodes, output, cancellationToken);
                }
                else
                {
                    this.WriteZeroComponent(this.colorComponents + c, region.Size, residuals, codeLength, prefixCodes, output, cancellationToken);
                }
            }

            this.FlushBits(output);
            sectionSizes[1 + group] = checked((uint)(this.payload.Position - start));
        }
    }

    /// <summary>
    /// Converts source rows into the reusable sample planes for group coding or forward squeeze.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <typeparam name="TColor">The prepared color sample operation.</typeparam>
    /// <typeparam name="TAlpha">The prepared alpha sample operation.</typeparam>
    /// <typeparam name="TBlack">The prepared black sample operation.</typeparam>
    /// <typeparam name="TAlphaDecode">The prepared primary-alpha reconstruction.</typeparam>
    /// <param name="image">The unchanged source frame.</param>
    /// <param name="bounds">The source rows and columns to convert.</param>
    /// <param name="clearTransparent">Whether output conversion clears hidden transparent colors.</param>
    /// <param name="pixelRow">The borrowed working pixel row.</param>
    /// <param name="floatRows">The borrowed component and conversion workspace.</param>
    /// <param name="colorOperation">The color operation selected before traversal.</param>
    /// <param name="alphaOperation">The alpha operation selected before traversal.</param>
    /// <param name="blackOperation">The black operation selected before traversal.</param>
    /// <param name="alphaDecodeOperation">The primary-alpha reconstruction selected before traversal.</param>
    /// <param name="cancellationToken">The token checked before each source row.</param>
    private void ConvertRows<TPixel, TColor, TAlpha, TBlack, TAlphaDecode>(
        ImageFrame<TPixel> image,
        Rectangle bounds,
        bool clearTransparent,
        Span<Vector4> pixelRow,
        Span<float> floatRows,
        TColor colorOperation,
        TAlpha alphaOperation,
        TBlack blackOperation,
        TAlphaDecode alphaDecodeOperation,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
        where TColor : struct, JxlSampleConverter.IConversionOperator<float, int>
        where TAlpha : struct, JxlSampleConverter.IConversionOperator<float, int>
        where TBlack : struct, JxlSampleConverter.IConversionOperator<float, int>
        where TAlphaDecode : struct, JxlSampleConverter.IConversionOperator<int, float>
    {
        Configuration configuration = image.Configuration;
        PixelOperations<TPixel> pixelOperations = PixelOperations<TPixel>.Instance;
        Span<Vector4> vectors = pixelRow[..bounds.Width];
        Span<float> first = floatRows[..bounds.Width];
        Span<float> second = floatRows.Slice(this.groupWidth, bounds.Width);
        Span<float> third = floatRows.Slice(2 * this.groupWidth, bounds.Width);
        Span<float> black = this.hasBlack ? floatRows.Slice(3 * this.groupWidth, bounds.Width) : [];
        Span<float> alpha = floatRows.Slice((this.hasBlack ? 4 : 3) * this.groupWidth, bounds.Width);
        Span<float> scratch = this.profileConverter is null
            ? [] : floatRows.Slice((this.hasBlack ? 5 : 4) * this.groupWidth, (this.hasBlack ? 7 : 3) * bounds.Width);

        for (int y = 0; y < bounds.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadOnlySpan<TPixel> source = image.PixelBuffer.DangerousGetRowSpan(bounds.Y + y).Slice(bounds.X, bounds.Width);
            pixelOperations.ToUnassociatedScaledVector4(configuration, source, vectors);

            if (this.suppliedAlphaIndex >= 0 && (this.alphaAssociated || clearTransparent))
            {
                // Alpha is replaced after unpacking source pixels. Source association still belongs to TPixel.
                // Use the quantized, reconstructed alpha because that is what native output unpremultiplies.
                if (this.expandedAlpha is Buffer2D<float> expanded)
                {
                    expanded.DangerousGetRowSpan(bounds.Y + y).Slice(bounds.X, bounds.Width).CopyTo(alpha);
                }
                else
                {
                    Buffer2D<float> input = this.encoder.ExtraChannels[this.suppliedAlphaIndex].Frames[this.sourceFrameIndex];
                    Span<int> carriers = this.GetComponentRow(this.colorComponents + this.suppliedAlphaIndex, y)[..bounds.Width];
                    JxlSampleConverter.Encode(input.DangerousGetRowSpan(bounds.Y + y).Slice(bounds.X, bounds.Width), carriers, alphaOperation);
                    JxlSampleConverter.Convert<int, TAlphaDecode>(carriers, alpha, alphaDecodeOperation);
                }

                // Shared deinterleave/interleave workers preserve RGB bits and replace W across all SIMD widths and tails.
                SimdUtils.DeinterleaveFloatPlanes(vectors, first, second, third, []);
                SimdUtils.InterleaveFloatPlanes(first, second, third, alpha, vectors);
            }

            if (clearTransparent)
            {
                EncodingUtilities.ReplaceTransparentPixels(vectors);
            }

            // The paired converter owns device conversion, association and the coding color transform.
            // Its scratch does not overlap its outputs. All storage belongs to this operation.
            this.colorConverter.ConvertFromRgb(
                vectors,
                first,
                this.colorComponents == 1 ? [] : second,
                this.colorComponents == 1 ? [] : third,
                black,
                this.hasAlpha ? alpha : [],
                this.alphaAssociated,
                this.profileConverter,
                scratch);

            if (this.varDct is JxlVarDctEncoder varDct)
            {
                // VarDCT quantizes frequency coefficients. Keep the converted XYB samples at float
                // precision until the forward transform; modular sample quantization does not apply here.
                first.CopyTo(varDct.GetColorRow(0, y));
                second.CopyTo(varDct.GetColorRow(1, y));
                third.CopyTo(varDct.GetColorRow(2, y));
            }
            else
            {
                JxlSampleConverter.Encode(first, this.GetComponentRow(0, y)[..bounds.Width], colorOperation);

                if (this.colorComponents == 3)
                {
                    JxlSampleConverter.Encode(second, this.GetComponentRow(1, y)[..bounds.Width], colorOperation);
                    JxlSampleConverter.Encode(third, this.GetComponentRow(2, y)[..bounds.Width], colorOperation);
                }
            }

            if (this.derivedBlackIndex >= 0)
            {
                JxlSampleConverter.Encode(black, this.GetComponentRow(this.colorComponents + this.derivedBlackIndex, y)[..bounds.Width], blackOperation);
            }

            if (this.derivedAlphaIndex >= 0)
            {
                int alphaComponent = this.colorComponents + this.derivedAlphaIndex;
                Span<int> destination = this.varDct is JxlVarDctEncoder varDctAlpha
                    ? varDctAlpha.GetAlphaRow(bounds.X, bounds.Y + y)[..bounds.Width]
                    : this.GetComponentRow(alphaComponent, y)[..bounds.Width];

                JxlSampleConverter.Encode(alpha, destination, alphaOperation);
            }
        }
    }

    /// <summary>
    /// Converts rows into the final progressive channel order without retaining an untransformed image.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <typeparam name="TColor">The prepared color sample operation.</typeparam>
    /// <typeparam name="TAlpha">The prepared alpha sample operation.</typeparam>
    /// <typeparam name="TBlack">The prepared black sample operation.</typeparam>
    /// <typeparam name="TAlphaDecode">The prepared primary-alpha reconstruction.</typeparam>
    /// <param name="image">The unchanged source frame.</param>
    /// <param name="clearTransparent">Whether to clear hidden transparent colors.</param>
    /// <param name="pixelRow">The working pixel row.</param>
    /// <param name="floatRows">The component and conversion workspace.</param>
    /// <param name="color">The prepared color operation.</param>
    /// <param name="alpha">The prepared alpha operation.</param>
    /// <param name="black">The prepared black operation.</param>
    /// <param name="alphaDecode">The primary-alpha reconstruction selected before traversal.</param>
    /// <param name="cancellationToken">The token checked before each converted row.</param>
    private void PrepareProgressiveSamples<TPixel, TColor, TAlpha, TBlack, TAlphaDecode>(
        ImageFrame<TPixel> image,
        bool clearTransparent,
        Span<Vector4> pixelRow,
        Span<float> floatRows,
        TColor color,
        TAlpha alpha,
        TBlack black,
        TAlphaDecode alphaDecode,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
        where TColor : struct, JxlSampleConverter.IConversionOperator<float, int>
        where TAlpha : struct, JxlSampleConverter.IConversionOperator<float, int>
        where TBlack : struct, JxlSampleConverter.IConversionOperator<float, int>
        where TAlphaDecode : struct, JxlSampleConverter.IConversionOperator<int, float>
    {
        int colors = this.squeezedComponents;
        Buffer2D<int>? squeezeRows = this.squeezeRows;

        for (int y = 0; y < image.Height; y++)
        {
            this.ConvertRows(
                image, new Rectangle(0, y, image.Width, 1), clearTransparent, pixelRow, floatRows, color, alpha, black, alphaDecode, cancellationToken);

            if (squeezeRows is not null)
            {
                for (int c = 0; c < colors; c++)
                {
                    JxlModularChannel horizontal = this.progressiveChannels[(2 * colors) + c];
                    Span<int> details = horizontal.IsEmpty ? [] : horizontal.GetRow<int>(y);
                    JxlModularSqueeze.ForwardHorizontal(
                        this.GetComponentRow(c, 0), squeezeRows.DangerousGetRowSpan((5 * c) + (y % 5)), details);
                }

                // Five rows retain the preceding original odd row, the current pair and its lookahead pair.
                // Each completed pair writes directly to the retained average and vertical-detail planes.
                if (y >= 3 && (y & 1) != 0)
                {
                    this.SqueezeVerticalPair(squeezeRows, y - 3);
                }
            }

            for (int c = colors; c < this.componentCount; c++)
            {
                if (c >= this.colorComponents && c < this.colorComponents + this.encoder.ExtraChannels.Count)
                {
                    continue;
                }

                this.GetComponentRow(c, 0).CopyTo(this.progressiveChannels[(2 * colors) + c].GetRow<int>(y));
            }
        }

        // Supplied planes already have their declared spatial reduction. They bypass color conversion and squeeze.
        // The retained progressive planes carry their quantized samples until their global, DC or AC stream is emitted.
        for (int c = 0; c < this.encoder.ExtraChannels.Count; c++)
        {
            JxlModularChannel destination = this.progressiveChannels[(2 * colors) + this.colorComponents + c];
            this.EncodeSuppliedSamples(c, new Rectangle(Point.Empty, destination.Size), destination.Region, cancellationToken);
        }

        if (squeezeRows is not null)
        {
            if (image.Height >= 2)
            {
                this.SqueezeVerticalPair(squeezeRows, ((image.Height / 2) - 1) * 2);
            }

            // An unmatched final row is already the final vertical average. No detail sample exists for it.
            if ((image.Height & 1) != 0)
            {
                for (int c = 0; c < colors; c++)
                {
                    squeezeRows.DangerousGetRowSpan((5 * c) + ((image.Height - 1) % 5))
                        .CopyTo(this.progressiveChannels[c].GetRow<int>(image.Height / 2));
                }
            }
        }
    }

    /// <summary>
    /// Splits one vertical pair using the retained original rows and format-defined edge samples.
    /// </summary>
    /// <param name="rows">Five horizontal-average rows per squeezed component.</param>
    /// <param name="y">The even coordinate of the original first row.</param>
    private void SqueezeVerticalPair(Buffer2D<int> rows, int y)
    {
        for (int c = 0; c < this.squeezedComponents; c++)
        {
            int start = 5 * c;
            JxlModularSqueeze.ForwardVerticalPair(
                rows.DangerousGetRowSpan(start + (y % 5)),
                rows.DangerousGetRowSpan(start + ((y + 1) % 5)),
                y == 0 ? [] : rows.DangerousGetRowSpan(start + ((y - 1) % 5)),
                y + 2 < this.size.Height ? rows.DangerousGetRowSpan(start + ((y + 2) % 5)) : [],
                y + 3 < this.size.Height ? rows.DangerousGetRowSpan(start + ((y + 3) % 5)) : [],
                this.progressiveChannels[c].GetRow<int>(y / 2),
                this.progressiveChannels[this.squeezedComponents + c].GetRow<int>(y / 2));
        }
    }

    /// <summary>
    /// Writes global progressive samples and two ordered passes over the remaining channel regions.
    /// </summary>
    /// <param name="gradient">Whether the global tree selects clamped gradient prediction.</param>
    /// <param name="codeLength">The common symbol width.</param>
    /// <param name="prefixCodes">The prepared prefix codes.</param>
    /// <param name="floatRows">Conversion scratch reused as unsigned residuals after conversion ends.</param>
    /// <param name="sectionSizes">The retained frame section table.</param>
    /// <param name="output">The output byte workspace.</param>
    /// <param name="cancellationToken">The token checked between groups and rows.</param>
    private void WriteProgressiveSections(
        bool gradient,
        int codeLength,
        ReadOnlySpan<uint> prefixCodes,
        Span<float> floatRows,
        Span<uint> sectionSizes,
        Span<byte> output,
        CancellationToken cancellationToken)
    {
        Span<uint> residuals = MemoryMarshal.Cast<float, uint>(floatRows[..this.groupWidth]);

        for (int c = 0; c < this.globalPrefixCount; c++)
        {
            JxlModularChannel channel = this.progressiveChannels[c];

            // A one-column or one-row image has an empty squeeze-detail plane and emits no samples for it.
            if (channel.IsEmpty)
            {
                continue;
            }

            this.WriteProgressiveChannel(
                channel, new Rectangle(Point.Empty, channel.Size), gradient, residuals, codeLength, prefixCodes, output, cancellationToken);
        }

        this.FlushBits(output);
        sectionSizes[0] = checked((uint)this.payload.Position);

        int dcColumns = ((this.groupColumns - 1) / 8) + 1;

        for (int group = 0; group < this.dcGroupCount; group++)
        {
            long start = this.payload.Position;
            bool hasSamples = false;
            int groupX = (group % dcColumns) * (8 * GroupDimension);
            int groupY = (group / dcColumns) * (8 * GroupDimension);

            for (int c = this.globalPrefixCount; c < this.progressiveChannels.Length; c++)
            {
                JxlModularChannel channel = this.progressiveChannels[c];
                int shift = Math.Min(channel.HorizontalShift, channel.VerticalShift);

                if (shift < 3)
                {
                    continue;
                }

                int x = groupX >> channel.HorizontalShift;
                int y = groupY >> channel.VerticalShift;
                int width = Math.Min((8 * GroupDimension) >> channel.HorizontalShift, Math.Max(channel.Width - x, 0));
                int height = Math.Min((8 * GroupDimension) >> channel.VerticalShift, Math.Max(channel.Height - y, 0));

                if (width == 0 || height == 0)
                {
                    continue;
                }

                if (!hasSamples)
                {
                    this.WriteBits(1, 1, output); // Reuse the global tree.
                    this.WriteBits(1, 1, output); // Default weighted predictor.
                    this.WriteBits(2, 0, output); // No local transforms.
                    hasSamples = true;
                }

                this.WriteProgressiveChannel(
                    channel, new Rectangle(x, y, width, height), gradient, residuals, codeLength, prefixCodes, output, cancellationToken);
            }

            this.FlushBits(output);
            sectionSizes[1 + group] = checked((uint)(this.payload.Position - start));
        }

        for (int pass = 0; pass < 2; pass++)
        {
            for (int group = 0; group < this.groupCount; group++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                long start = this.payload.Position;
                bool hasSamples = false;
                int groupX = (group % this.groupColumns) * GroupDimension;
                int groupY = (group / this.groupColumns) * GroupDimension;

                for (int c = this.globalPrefixCount; c < this.progressiveChannels.Length; c++)
                {
                    JxlModularChannel channel = this.progressiveChannels[c];
                    int shift = Math.Min(channel.HorizontalShift, channel.VerticalShift);

                    if (shift < (1 - pass) || shift > (pass == 0 ? 2 : 0))
                    {
                        continue;
                    }

                    // Shift the full coding-group rectangle before clipping its odd image edge. This is the
                    // same grid used by the decoder, so a reduced edge sample belongs to exactly one group.
                    int x = groupX >> channel.HorizontalShift;
                    int y = groupY >> channel.VerticalShift;
                    int width = Math.Min(GroupDimension >> channel.HorizontalShift, Math.Max(channel.Width - x, 0));
                    int height = Math.Min(GroupDimension >> channel.VerticalShift, Math.Max(channel.Height - y, 0));

                    if (width == 0 || height == 0)
                    {
                        continue;
                    }

                    if (!hasSamples)
                    {
                        this.WriteBits(1, 1, output); // Reuse the global tree.
                        this.WriteBits(1, 1, output); // Default weighted predictor.
                        this.WriteBits(2, 0, output); // No local transforms.
                        hasSamples = true;
                    }

                    this.WriteProgressiveChannel(
                        channel, new Rectangle(x, y, width, height), gradient, residuals, codeLength, prefixCodes, output, cancellationToken);
                }

                // A stream without selected channel samples has no header and occupies zero bytes.
                this.FlushBits(output);
                sectionSizes[2 + this.dcGroupCount + (pass * this.groupCount) + group] = checked((uint)(this.payload.Position - start));
            }
        }
    }

    /// <summary>
    /// Emits a channel region with prediction restarted at its modular stream boundary.
    /// </summary>
    /// <param name="channel">The retained transformed plane.</param>
    /// <param name="bounds">The selected region in channel coordinates.</param>
    /// <param name="gradient">Whether to use the clamped-gradient tree leaf.</param>
    /// <param name="workspace">The reusable unsigned residual row.</param>
    /// <param name="codeLength">The prefix-code width.</param>
    /// <param name="prefixCodes">The prepared prefix codes.</param>
    /// <param name="output">The output byte workspace.</param>
    /// <param name="cancellationToken">The token checked before each row.</param>
    private void WriteProgressiveChannel(
        JxlModularChannel channel,
        Rectangle bounds,
        bool gradient,
        Span<uint> workspace,
        int codeLength,
        ReadOnlySpan<uint> prefixCodes,
        Span<byte> output,
        CancellationToken cancellationToken)
    {
        Span<uint> residuals = workspace[..bounds.Width];

        for (int y = 0; y < bounds.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadOnlySpan<int> row = channel.GetRow<int>(bounds.Y + y).Slice(bounds.X, bounds.Width);

            if (gradient)
            {
                ReadOnlySpan<int> previous = y == 0 ? [] : channel.GetRow<int>(bounds.Y + y - 1).Slice(bounds.X, bounds.Width);
                JxlModularPredictor.PackGradientResiduals(row, previous, residuals);
            }
            else
            {
                JxlModularPredictor.PackZeroResiduals(row, residuals);
            }

            this.WriteValues(residuals, codeLength, prefixCodes, output);
        }
    }

    /// <summary>
    /// Encodes one integer component with the shared clamped-gradient predictor.
    /// </summary>
    /// <param name="component">The selected component index.</param>
    /// <param name="size">The clipped group size.</param>
    /// <param name="residuals">The reused row scratch for packed unsigned residuals.</param>
    /// <param name="codeLength">The common symbol width.</param>
    /// <param name="prefixCodes">The prepared symbol codes.</param>
    /// <param name="output">The retained output buffer.</param>
    /// <param name="cancellationToken">The token checked before rows.</param>
    private void WriteGradientComponent(
        int component,
        Size size,
        Span<uint> residuals,
        int codeLength,
        ReadOnlySpan<uint> prefixCodes,
        Span<byte> output,
        CancellationToken cancellationToken)
    {
        residuals = residuals[..size.Width];

        for (int y = 0; y < size.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadOnlySpan<int> row = this.GetComponentRow(component, y)[..size.Width];
            ReadOnlySpan<int> previous = y == 0 ? [] : this.GetComponentRow(component, y - 1)[..size.Width];
            JxlModularPredictor.PackGradientResiduals(row, previous, residuals);

            // Prediction and signed packing use independent SIMD lanes. Entropy emission retains one ordered bit reservoir.
            this.WriteValues(residuals, codeLength, prefixCodes, output);
        }
    }

    /// <summary>
    /// Encodes raw floating-point carrier bits without prediction subtraction.
    /// </summary>
    /// <param name="component">The selected component index.</param>
    /// <param name="size">The clipped group size.</param>
    /// <param name="residuals">The reused row scratch for packed unsigned residuals.</param>
    /// <param name="codeLength">The common symbol width.</param>
    /// <param name="prefixCodes">The prepared symbol codes.</param>
    /// <param name="output">The retained output buffer.</param>
    /// <param name="cancellationToken">The token checked before rows.</param>
    private void WriteZeroComponent(
        int component,
        Size size,
        Span<uint> residuals,
        int codeLength,
        ReadOnlySpan<uint> prefixCodes,
        Span<byte> output,
        CancellationToken cancellationToken)
    {
        residuals = residuals[..size.Width];

        for (int y = 0; y < size.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadOnlySpan<int> row = this.GetComponentRow(component, y)[..size.Width];

            JxlModularPredictor.PackZeroResiduals(row, residuals);

            this.WriteValues(residuals, codeLength, prefixCodes, output);
        }
    }

    /// <summary>
    /// Writes the image dimensions, color model and declared component precision.
    /// </summary>
    /// <param name="codeLength">The prepared entropy symbol width, also sufficient for profile bytes.</param>
    /// <param name="prefixCodes">The prepared canonical symbol codes.</param>
    /// <param name="output">The retained output buffer.</param>
    private void WriteImageHeader(int codeLength, ReadOnlySpan<uint> prefixCodes, Span<byte> output)
    {
        this.WriteBits(16, 0x0AFF, output);
        this.WriteBits(1, 0, output); // General dimensions, including sizes that are not multiples of eight.
        this.WriteU32((uint)this.size.Height, (9, 1), (13, 1), (18, 1), (30, 1), output);
        this.WriteBits(3, 0, output); // Explicit width instead of an aspect-ratio code.
        this.WriteU32((uint)this.size.Width, (9, 1), (13, 1), (18, 1), (30, 1), output);
        this.WriteBits(1, 0, output); // Explicit image metadata.
        bool extraFields = this.animated || this.intensityTarget != 255F || this.orientation != ExifOrientationMode.TopLeft;
        this.WriteBits(1, extraFields ? 1U : 0U, output);

        if (extraFields)
        {
            this.WriteBits(3, (uint)this.orientation - 1U, output); // Orientation changes display coordinates, not stored dimensions.
            this.WriteBits(1, 0, output); // No intrinsic display size.
            this.WriteBits(1, 0, output); // No preview.
            this.WriteBits(1, this.animated ? 1U : 0U, output);
        }

        if (this.animated)
        {
            this.WriteU32(this.ticksPerSecondNumerator, (0, 100), (0, 1000), (10, 1), (30, 1), output);
            this.WriteU32(this.ticksPerSecondDenominator, (0, 1), (0, 1001), (8, 1), (10, 1), output);
            this.WriteU32(this.repeatCount, (0, 0), (3, 0), (16, 0), (32, 0), output);
            this.WriteBits(1, 0, output); // No timecodes.
        }

        this.WriteBitDepth(this.bitDepth, output);

        // Without channel transforms, samples below 15 declared bits and their gradient residuals fit signed 16-bit storage.
        // This promise describes the encoded values; the encoder's int workspace does not determine it.
        this.WriteBits(1, this.narrowSamples ? 1U : 0U, output);
        this.WriteU32((uint)this.extraChannels.Length, (0, 0), (0, 1), (4, 2), (12, 1), output);

        foreach (JxlExtraChannelMetadata channel in this.extraChannels)
        {
            this.WriteExtraChannel(channel, output);
        }

        this.WriteBits(1, this.usesXyb ? 1U : 0U, output);

        if (this.profile is not null)
        {
            this.WriteBits(1, 0, output); // Explicit color encoding.
            this.WriteBits(1, 1, output); // The embedded profile declares the device encoding.
            this.WriteU32((uint)this.colorType, (0, 0), (0, 1), (4, 2), (6, 18), output);
        }
        else if (this.colorType == JxlColorType.Rgb && this.whitePoint == KnownIlluminants.D65Chromaticity
            && this.primaries.Equals(KnownRgbWorkingSpaces.SRgb.ChromaticityCoordinates) && this.gamma is null
            && this.transferCharacteristics == CicpTransferCharacteristics.Iec61966_2_1
            && this.renderingIntent == IccRenderingIntent.MediaRelativeColorimetric)
        {
            this.WriteBits(1, 1, output); // Default compact sRGB encoding.
        }
        else
        {
            this.WriteBits(1, 0, output); // Explicit compact color encoding.
            this.WriteBits(1, 0, output); // No embedded ICC profile.
            this.WriteBits(2, this.colorType == JxlColorType.Luminance ? 1U : 0U, output);
            uint white = this.whitePoint == KnownIlluminants.D65Chromaticity ? 1U
                : this.whitePoint == KnownIlluminants.EChromaticity ? 10U
                : this.whitePoint == KnownIlluminants.DciP3Chromaticity ? 11U : 2U;
            this.WriteU32(white, (0, 0), (0, 1), (4, 2), (6, 18), output);

            if (white == 2)
            {
                this.WriteChromaticity(this.whitePoint, output);
            }

            if (this.colorType != JxlColorType.Luminance)
            {
                uint primaries = this.primaries.Equals(KnownRgbWorkingSpaces.SRgb.ChromaticityCoordinates) ? 1U
                    : this.primaries.Equals(KnownRgbWorkingSpaces.Rec2020.ChromaticityCoordinates) ? 9U
                    : this.primaries.Equals(KnownRgbWorkingSpaces.DisplayP3.ChromaticityCoordinates) ? 11U : 2U;
                this.WriteU32(primaries, (0, 0), (0, 1), (4, 2), (6, 18), output);

                if (primaries == 2)
                {
                    this.WriteChromaticity(this.primaries.R, output);
                    this.WriteChromaticity(this.primaries.G, output);
                    this.WriteChromaticity(this.primaries.B, output);
                }
            }

            this.WriteBits(1, this.gamma.HasValue ? 1U : 0U, output);

            if (this.gamma is uint gamma)
            {
                this.WriteBits(24, gamma, output);
            }
            else
            {
                this.WriteU32((uint)this.transferCharacteristics, (0, 0), (0, 1), (4, 2), (6, 18), output);
            }

            this.WriteU32((uint)this.renderingIntent, (0, 0), (0, 1), (4, 2), (6, 18), output);
        }

        if (extraFields)
        {
            bool defaultToneMapping = this.intensityTarget == 255F;
            this.WriteBits(1, defaultToneMapping ? 1U : 0U, output);

            if (!defaultToneMapping)
            {
                this.WriteBits(16, HalfTypeHelper.Pack(this.intensityTarget), output);
                this.WriteBits(16, 0, output); // Minimum luminance is zero nits.
                this.WriteBits(1, 0, output); // The linear threshold is in nits, not a fraction of the display peak.
                this.WriteBits(16, 0, output); // No linear segment below the transfer function.
            }
        }

        this.WriteBits(2, 0, output); // No image extensions.
        this.WriteBits(1, 1, output); // Default custom transform data.

        if (this.profile is not null)
        {
            this.WriteIccProfile(this.profileData.Span, codeLength, prefixCodes, output);
        }

        this.FlushBits(output);
    }

    /// <summary>
    /// Writes a custom chromaticity with the format's signed-millionths coordinates.
    /// </summary>
    /// <param name="value">The coordinate already rounded to the serialized grid during setup.</param>
    /// <param name="output">The output byte workspace.</param>
    private void WriteChromaticity(CieXyChromaticityCoordinates value, Span<byte> output)
    {
        int x = (int)MathF.Round(value.X * 1_000_000F);
        int y = (int)MathF.Round(value.Y * 1_000_000F);

        // Zigzag maps signed integers to 0, -1, 1, -2, 2. Setup and metadata validation ensure 22-bit storage.
        this.WriteU32((uint)((x << 1) ^ (x >> 31)), (19, 0), (19, 524288), (20, 1048576), (21, 2097152), output);
        this.WriteU32((uint)((y << 1) ^ (y >> 31)), (19, 0), (19, 524288), (20, 1048576), (21, 2097152), output);
    }

    /// <summary>
    /// Writes one extra-channel descriptor, including its type-specific rendering fields.
    /// </summary>
    /// <param name="channel">The declared type, sample precision, spatial reduction and rendering fields.</param>
    /// <param name="output">The borrowed output byte buffer.</param>
    private void WriteExtraChannel(JxlExtraChannelMetadata channel, Span<byte> output)
    {
        JxlExtraChannelType type = channel.Type;
        bool associated = channel.AlphaRepresentation == PixelAlphaRepresentation.Associated;
        string name = channel.Name;
        bool defaults = type == JxlExtraChannelType.Alpha && channel.BitDepth == new JxlBitDepth(8, 0)
            && !associated && name.Length == 0 && channel.DimensionShift == 0;
        this.WriteBits(1, defaults ? 1U : 0U, output);

        if (defaults)
        {
            return;
        }

        this.WriteU32((uint)type, (0, 0), (0, 1), (4, 2), (6, 18), output);
        this.WriteBitDepth(channel.BitDepth, output);
        this.WriteU32(channel.DimensionShift, (0, 0), (0, 3), (0, 4), (3, 1), output);
        int count = Encoding.UTF8.GetByteCount(name);
        this.WriteU32((uint)count, (0, 0), (4, 0), (5, 16), (10, 48), output);

        // Channel metadata bounds this label to 1071 UTF-8 bytes. This setup scratch is not retained by any row.
        Span<byte> bytes = stackalloc byte[count];
        Encoding.UTF8.GetBytes(name, bytes);

        foreach (byte value in bytes[..count])
        {
            this.WriteBits(8, value, output);
        }

        if (type == JxlExtraChannelType.Alpha)
        {
            this.WriteBits(1, associated ? 1U : 0U, output);
        }
        else if (type == JxlExtraChannelType.SpotColor)
        {
            this.WriteBits(16, HalfTypeHelper.Pack(channel.SpotColor.X), output);
            this.WriteBits(16, HalfTypeHelper.Pack(channel.SpotColor.Y), output);
            this.WriteBits(16, HalfTypeHelper.Pack(channel.SpotColor.Z), output);
            this.WriteBits(16, HalfTypeHelper.Pack(channel.SpotColor.W), output);
        }
        else if (type == JxlExtraChannelType.ColorFilterArray)
        {
            this.WriteU32(channel.ColorFilterArrayChannel, (0, 1), (2, 0), (4, 3), (8, 19), output);
        }
    }

    /// <summary>
    /// Writes ICC prediction bytes directly into their entropy stream without a second profile-sized buffer.
    /// </summary>
    /// <param name="data">The unchanged original profile bytes.</param>
    /// <param name="codeLength">The prepared entropy symbol width.</param>
    /// <param name="prefixCodes">The canonical symbol codes.</param>
    /// <param name="output">The borrowed output byte buffer.</param>
    private void WriteIccProfile(ReadOnlySpan<byte> data, int codeLength, ReadOnlySpan<uint> prefixCodes, Span<byte> output)
    {
        // A zero tag-list command followed by one literal insertion represents every valid profile tail.
        // Header residuals still use the shared previous-byte predictions. All byte contexts share one prefix table.
        Span<byte> commands = stackalloc byte[12];
        int commandLength = 0;

        if (data.Length > JxlIccPrediction.HeaderSize)
        {
            commands[commandLength++] = 0;
            commands[commandLength++] = 1;
            commandLength += WriteVarUInt((uint)(data.Length - JxlIccPrediction.HeaderSize), commands[commandLength..]);
        }

        Span<byte> preamble = stackalloc byte[12];
        int preambleLength = WriteVarUInt((uint)data.Length, preamble);
        preambleLength += WriteVarUInt((uint)commandLength, preamble[preambleLength..]);
        this.WriteU64((ulong)(data.Length + commandLength + preambleLength), output);
        this.WritePrefixHeader(41, codeLength, output);

        foreach (byte value in preamble[..preambleLength])
        {
            this.WriteValue(value, codeLength, prefixCodes, output);
        }

        foreach (byte value in commands[..commandLength])
        {
            this.WriteValue(value, codeLength, prefixCodes, output);
        }

        for (int i = 0; i < JxlIccPrediction.HeaderSize; i++)
        {
            byte prediction = JxlIccPrediction.GetHeaderPrediction(data, data.Length, i);
            this.WriteValue((byte)(data[i] - prediction), codeLength, prefixCodes, output);
        }

        foreach (byte value in data[JxlIccPrediction.HeaderSize..])
        {
            this.WriteValue(value, codeLength, prefixCodes, output);
        }
    }

    /// <summary>
    /// Writes an unsigned base-128 profile size or command operand into caller-owned scratch.
    /// </summary>
    /// <param name="value">The profile length or command operand.</param>
    /// <param name="destination">Storage for the complete operand, at most five bytes.</param>
    /// <returns>The number of initialized bytes.</returns>
    private static int WriteVarUInt(uint value, Span<byte> destination)
    {
        int count = 0;
        do
        {
            byte next = (byte)(value & 127);
            value >>= 7;
            destination[count++] = (byte)(next | (value != 0 ? 128 : 0));
        }
        while (value != 0);

        return count;
    }

    /// <summary>
    /// Writes a JPEG XL unsigned 64-bit field with the format's bounded continuation groups.
    /// </summary>
    /// <param name="value">The field value.</param>
    /// <param name="output">The borrowed output byte buffer.</param>
    private void WriteU64(ulong value, Span<byte> output)
    {
        if (value == 0)
        {
            this.WriteBits(2, 0, output);
        }
        else if (value <= 16)
        {
            this.WriteBits(2, 1, output);
            this.WriteBits(4, (uint)value - 1, output);
        }
        else if (value <= 272)
        {
            this.WriteBits(2, 2, output);
            this.WriteBits(8, (uint)value - 17, output);
        }
        else
        {
            this.WriteBits(2, 3, output);
            this.WriteBits(12, (uint)value & 4095U, output);
            value >>= 12;

            // Twelve initial bits leave six full bytes and a final four-bit group. The last group has no stop bit.
            for (int shift = 12; shift <= 60; shift += 8)
            {
                this.WriteBits(1, value != 0 ? 1U : 0U, output);

                if (value == 0)
                {
                    break;
                }

                int width = shift == 60 ? 4 : 8;
                this.WriteBits(width, (uint)value & ((1U << width) - 1), output);
                value >>= width;
            }
        }
    }

    /// <summary>
    /// Writes the declared integer or floating-point sample layout.
    /// </summary>
    /// <param name="bitDepth">The validated sample precision, independent of carrier storage.</param>
    /// <param name="output">The retained output buffer.</param>
    private void WriteBitDepth(JxlBitDepth bitDepth, Span<byte> output)
    {
        bool floating = bitDepth.ExponentBits != 0;
        this.WriteBits(1, floating ? 1U : 0U, output);

        if (floating)
        {
            this.WriteU32(bitDepth.BitsPerSample, (0, 32), (0, 16), (0, 24), (6, 1), output);
            this.WriteBits(4, (uint)(bitDepth.ExponentBits - 1), output);
        }
        else
        {
            this.WriteU32(bitDepth.BitsPerSample, (0, 8), (0, 10), (0, 12), (6, 1), output);
        }
    }

    /// <summary>
    /// Writes a full-canvas modular frame with replacement blending and its ordered section lengths.
    /// </summary>
    /// <param name="sectionSizes">The complete ordered byte lengths, including empty DC/HF sections.</param>
    /// <param name="duration">The display duration in sequence-clock ticks.</param>
    /// <param name="last">Whether this is the final displayed frame.</param>
    /// <param name="output">The retained output buffer.</param>
    private void WriteFrameHeader(ReadOnlySpan<uint> sectionSizes, uint duration, bool last, Span<byte> output)
    {
        // Lossless frames use modular samples. Lossy frames use XYB VarDCT with DCT8 blocks.
        // Both paths keep full-resolution color and the default 256-pixel coding groups.
        this.WriteBits(1, 0, output); // Explicit frame fields.
        this.WriteBits(2, 0, output); // Regular displayed frame.
        this.WriteBits(1, this.usesXyb ? 0U : 1U, output);
        this.WriteBits(2, 0, output); // U64 zero: no frame feature flags.

        if (!this.usesXyb)
        {
            this.WriteBits(1, 0, output); // Original-color samples do not use JPEG YCbCr.
        }

        this.WriteBits(2, 0, output); // Full-resolution color.

        for (int i = this.colorComponents; i < this.componentCount; i++)
        {
            this.WriteBits(2, 0, output); // No additional reduction beyond the channel's declared dimension shift.
        }

        if (this.varDct is JxlVarDctEncoder varDct)
        {
            this.WriteBits(3, (uint)varDct.XQuantizationScale, output);
            this.WriteBits(3, (uint)varDct.BQuantizationScale, output);
        }
        else
        {
            this.WriteBits(2, 1, output); // Modular group size shift one selects 256 pixels.
        }

        this.WriteBits(2, this.encoder.Progressive ? this.usesXyb ? 2U : 1U : 0U, output);

        if (this.encoder.Progressive)
        {
            this.WriteBits(2, this.usesXyb ? 2U : 1U, output);
            this.WriteBits(2, 0, output); // First-pass coefficients retain their full precision.

            if (this.usesXyb)
            {
                this.WriteBits(2, 0, output); // Second-pass coefficients also retain their full precision.
                this.WriteBits(2, 2, output); // Quarter-resolution display after the 2x2 frequency region.
            }

            this.WriteBits(2, 1, output); // Half-resolution display stage.
            this.WriteBits(2, 0, output); // The first display stage ends after pass zero.

            if (this.usesXyb)
            {
                this.WriteBits(2, 1, output); // The half-resolution stage ends after the 3x3 frequency region.
            }
        }

        this.WriteBits(1, 0, output); // No crop or custom origin.
        this.WriteBits(2, 0, output); // Replace the canvas color components.

        for (int i = this.colorComponents; i < this.componentCount; i++)
        {
            this.WriteBits(2, 0, output); // Replace each coalesced extra channel.
        }

        if (this.animated)
        {
            this.WriteU32(duration, (0, 0), (0, 1), (8, 0), (32, 0), output);
        }

        this.WriteBits(1, last ? 1U : 0U, output);

        if (!last)
        {
            this.WriteBits(2, 0, output); // Full-canvas replacements do not retain native reference slots.
        }

        this.WriteBits(2, 0, output); // Empty frame name.
        this.WriteBits(1, 0, output); // Explicit restoration parameters.
        this.WriteBits(1, 0, output); // Disable Gaborish.
        int iterations = this.varDct?.EpfIterations ?? 0;
        this.WriteBits(2, (uint)iterations, output);

        if (iterations != 0)
        {
            this.WriteBits(1, 0, output); // Default VarDCT sharpness factors.
            this.WriteBits(1, 0, output); // Default component distance scales and flush thresholds.
            this.WriteBits(1, 0, output); // Default quantization, pass and border multipliers.
        }

        this.WriteBits(2, 0, output); // No restoration extensions.
        this.WriteBits(2, 0, output); // No frame extensions.
        this.WriteBits(1, 0, output); // Natural section order; no TOC permutation.
        this.WriteBits((-this.pendingBits) & 7, 0, output);

        foreach (uint size in sectionSizes)
        {
            this.WriteU32(size, (10, 0), (14, 1024), (22, 17408), (30, 4211712), output);
        }

        this.FlushBits(output);
    }

    /// <summary>
    /// Writes one no-LZ77 prefix description with a complete fixed-length alphabet and hybrid integer configuration zero.
    /// </summary>
    /// <param name="contexts">The syntax-defined context count, all assigned to one cluster.</param>
    /// <param name="codeLength">The fixed code width selected from the declared sample precision.</param>
    /// <param name="output">The retained output buffer.</param>
    private void WritePrefixHeader(int contexts, int codeLength, Span<byte> output)
    {
        this.WriteBits(1, 0, output); // No LZ77 copy symbols or history.

        if (contexts > 1)
        {
            this.WriteBits(1, 1, output); // Simple context map.
            this.WriteBits(2, 0, output); // Zero bits per entry assigns every context to cluster zero.
        }

        this.WriteBits(1, 1, output); // Prefix entropy coding.
        this.WriteBits(4, 0, output); // Hybrid split zero; leading and trailing configuration widths are also zero.
        this.WriteBits(1, 1, output); // Alphabet size is greater than one.
        this.WriteBits(4, (uint)(codeLength - 1), output);
        this.WriteBits(codeLength - 1, (1U << (codeLength - 1)) - 1, output);

        // A complete fixed-length alphabet needs only one code-length symbol.
        // Skip the first three zero entries. Give only the selected length a one-bit meta-code; its sole symbol
        // then consumes zero bits until the complete fixed-length alphabet fills the prefix space.
        this.WriteBits(2, 3, output);
        ReadOnlySpan<byte> order = [4, 0, 5, 17, 6, 16, 7, 8, 9, 10, 11, 12, 13, 14, 15];

        foreach (byte length in order)
        {
            this.WriteBits(length == codeLength ? 4 : 2, length == codeLength ? 7U : 0U, output);
        }
    }

    /// <summary>
    /// Emits a residual row in bounded batches with reservoir state retained in local variables.
    /// </summary>
    /// <param name="values">The unsigned residuals in sample order.</param>
    /// <param name="codeLength">The common entropy-symbol width.</param>
    /// <param name="prefixCodes">The prepared code for each symbol.</param>
    /// <param name="output">The retained buffer, large enough for one complete batch.</param>
    private void WriteValues(ReadOnlySpan<uint> values, int codeLength, ReadOnlySpan<uint> prefixCodes, Span<byte> output)
    {
        // Like JPEG's coding-unit reserve, the batch bound moves stream-capacity checks outside sample emission.
        // Only complete uint words leave the reservoir. Partial words remain unaligned between rows and batches.
        ulong bits = this.bits;
        int pendingBits = this.pendingBits;
        int bufferedBytes = this.bufferedBytes;

        while (!values.IsEmpty)
        {
            int count = Math.Min(values.Length, GroupDimension);
            int maximumBytes = (((count * (codeLength + 31)) + 31) / 32) * sizeof(uint);

            if (bufferedBytes > output.Length - maximumBytes)
            {
                this.outputStream.Write(output[..bufferedBytes]);
                bufferedBytes = 0;
            }

            ReadOnlySpan<uint> batch = values[..count];

            // The complete equal-length canonical table assigns prefix zero to symbol zero, which has no literals.
            // The shared span search checks whole SIMD registers and handles scalar tails. An all-zero batch can
            // therefore append its prefix bits together instead of splitting and emitting every residual separately.
            if (!batch.ContainsAnyExcept(0U))
            {
                int totalBits = pendingBits + (count * codeLength);
                int completeBytes = (totalBits / 32) * sizeof(uint);

                if (completeBytes != 0)
                {
                    // Existing pending bits occupy only the first word. Subsequent complete words and the retained
                    // partial word contain zeros. A short batch that emits no word must retain the existing bits.
                    BinaryPrimitives.WriteUInt32LittleEndian(output[bufferedBytes..], (uint)bits);
                    output.Slice(bufferedBytes + sizeof(uint), completeBytes - sizeof(uint)).Clear();
                    bufferedBytes += completeBytes;
                    bits = 0;
                }

                pendingBits = totalBits & 31;
                values = values[count..];
                continue;
            }

            foreach (uint value in batch)
            {
                JxlHybridUint.Zero.Encode(value, out uint token, out int extraBitCount, out uint extraBits);
                uint field = prefixCodes[(int)token];
                int fieldBits = codeLength + extraBitCount;

                if (fieldBits > 32)
                {
                    // A wide residual can have 31 literal bits after its prefix. Drain the prefix first
                    // when needed so the next field plus fewer than 32 pending bits fits one ulong.
                    bits |= (ulong)field << pendingBits;
                    pendingBits += codeLength;

                    if (pendingBits >= 32)
                    {
                        BinaryPrimitives.WriteUInt32LittleEndian(output[bufferedBytes..], (uint)bits);
                        bufferedBytes += sizeof(uint);
                        bits >>= 32;
                        pendingBits -= 32;
                    }

                    field = extraBits;
                    fieldBits = extraBitCount;
                }
                else
                {
                    field |= extraBits << codeLength;
                }

                bits |= (ulong)field << pendingBits;
                pendingBits += fieldBits;

                if (pendingBits >= 32)
                {
                    // Byte-based writes support unaligned output on every target. No partial word is exposed.
                    BinaryPrimitives.WriteUInt32LittleEndian(output[bufferedBytes..], (uint)bits);
                    bufferedBytes += sizeof(uint);
                    bits >>= 32;
                    pendingBits -= 32;
                }
            }

            values = values[count..];
        }

        // Header fields and section padding resume through WriteBits after the completed row.
        this.bits = bits;
        this.pendingBits = pendingBits;
        this.bufferedBytes = bufferedBytes;
    }

    /// <summary>
    /// Emits one unsigned value through hybrid integer configuration zero and prepared canonical prefix codes.
    /// </summary>
    /// <param name="value">The tree value or packed sample residual.</param>
    /// <param name="codeLength">The common entropy-symbol width.</param>
    /// <param name="prefixCodes">The prepared code for each symbol.</param>
    /// <param name="output">The retained output buffer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteValue(uint value, int codeLength, ReadOnlySpan<uint> prefixCodes, Span<byte> output)
    {
        // The shared hybrid-integer owner supplies the same token layout used by decoding.
        JxlHybridUint.Zero.Encode(value, out uint token, out int extraBitCount, out uint extraBits);
        uint prefix = prefixCodes[(int)token];
        int count = codeLength + extraBitCount;

        // The prefix comes first in the least-significant-bit-first stream. Join fields that fit one uint
        // to update the reservoir once. Wider carriers retain two writes so fewer than 32 pending bits plus
        // the next field always fit the 64-bit reservoir, including a full-width signed residual.
        if (count <= 32)
        {
            this.WriteBits(count, prefix | (extraBits << codeLength), output);
        }
        else
        {
            this.WriteBits(codeLength, prefix, output);
            this.WriteBits(extraBitCount, extraBits, output);
        }
    }

    /// <summary>
    /// Selects the shortest applicable U32 alternative and writes its offset-relative payload.
    /// </summary>
    /// <param name="value">The validated value to encode.</param>
    /// <param name="first">The first alternative's payload width and value offset.</param>
    /// <param name="second">The second alternative's payload width and value offset.</param>
    /// <param name="third">The third alternative's payload width and value offset.</param>
    /// <param name="fourth">The final alternative's payload width and value offset.</param>
    /// <param name="output">The retained output buffer.</param>
    private void WriteU32(
        uint value,
        (int Bits, uint Offset) first,
        (int Bits, uint Offset) second,
        (int Bits, uint Offset) third,
        (int Bits, uint Offset) fourth,
        Span<byte> output)
    {
        // Offsets can overlap. Test each interval in wire order; the final format-defined interval contains the remaining value.
        uint selector;
        (int Bits, uint Offset) selected;

        if (value >= first.Offset && (ulong)(value - first.Offset) < (1UL << first.Bits))
        {
            selector = 0;
            selected = first;
        }
        else if (value >= second.Offset && (ulong)(value - second.Offset) < (1UL << second.Bits))
        {
            selector = 1;
            selected = second;
        }
        else if (value >= third.Offset && (ulong)(value - third.Offset) < (1UL << third.Bits))
        {
            selector = 2;
            selected = third;
        }
        else
        {
            selector = 3;
            selected = fourth;
        }

        this.WriteBits(2, selector, output);
        this.WriteBits(selected.Bits, value - selected.Offset, output);
    }

    /// <summary>
    /// Appends a field of at most 32 bits to the least-significant-bit-first reservoir.
    /// </summary>
    /// <param name="count">The validated field width, including zero for an implicit value.</param>
    /// <param name="value">The field value with all bits above its width clear.</param>
    /// <param name="output">The retained output buffer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteBits(int count, uint value, Span<byte> output)
    {
        // Fewer than 32 bits remain between calls. One more 32-bit field therefore fits in the 64-bit reservoir.
        this.bits |= (ulong)value << this.pendingBits;
        this.pendingBits += count;

        if (this.pendingBits >= 32)
        {
            if (this.bufferedBytes > output.Length - sizeof(uint))
            {
                this.outputStream.Write(output[..this.bufferedBytes]);
                this.bufferedBytes = 0;
            }

            BinaryPrimitives.WriteUInt32LittleEndian(output[this.bufferedBytes..], (uint)this.bits);
            this.bufferedBytes += sizeof(uint);
            this.bits >>= 32;
            this.pendingBits -= 32;
        }
    }

    /// <summary>
    /// Pads the current section through a byte boundary and writes its complete buffered bytes without closing the stream.
    /// </summary>
    /// <param name="output">The retained output buffer.</param>
    private void FlushBits(Span<byte> output)
    {
        this.WriteBits((-this.pendingBits) & 7, 0, output);

        while (this.pendingBits != 0)
        {
            if (this.bufferedBytes == output.Length)
            {
                this.outputStream.Write(output);
                this.bufferedBytes = 0;
            }

            output[this.bufferedBytes++] = (byte)this.bits;
            this.bits >>= 8;
            this.pendingBits -= 8;
        }

        if (this.bufferedBytes != 0)
        {
            this.outputStream.Write(output[..this.bufferedBytes]);
            this.bufferedBytes = 0;
        }
    }

    /// <summary>
    /// Borrows one row from the reusable scratch plane assigned to a logical component.
    /// </summary>
    /// <param name="component">The validated component index.</param>
    /// <param name="y">The validated row coordinate within the group.</param>
    /// <returns>The signed sample carriers for that component row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Span<int> GetComponentRow(int component, int y)
    {
        // Supplied logical components share the first extra plane. Derived black and alpha follow that plane
        // without holes. Zero or one supplied channel keeps the original direct component-to-plane mapping.
        if (this.suppliedScratchOffset != 0 && component >= this.colorComponents)
        {
            component = Math.Max(this.colorComponents, component - this.suppliedScratchOffset);
        }

        // VarDCT keeps colors in its float group and coefficient storage. This modular workspace
        // therefore starts at the first extra channel instead of reserving unused color planes.
        if (this.usesXyb)
        {
            component -= this.colorComponents;
        }

        return this.sampleRegion.DangerousGetRowSpan((component * this.groupHeight) + y);
    }
}
