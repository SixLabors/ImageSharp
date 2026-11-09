// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;
using SixLabors.ImageSharp.Compression;
using SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Components.Entropy;
using SixLabors.ImageSharp.Formats.Jxl.Components.Modular;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Coordinates lossless modular frame encoding and writes the image and frame headers.
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
    private readonly Buffer2D<int> samples;

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
    /// The serialized X, Y and B reconstruction steps, in native component units.
    /// </summary>
    private readonly Vector3 dcFactors;

    /// <summary>
    /// The prepared signed X quantizer.
    /// </summary>
    private readonly JxlSampleConverter.ScaledIntegerEncodingOperator xybXOperation;

    /// <summary>
    /// The prepared signed Y quantizer.
    /// </summary>
    private readonly JxlSampleConverter.ScaledIntegerEncodingOperator xybYOperation;

    /// <summary>
    /// The prepared B quantizer, before the encoded Y component is subtracted.
    /// </summary>
    private readonly JxlSampleConverter.AccurateScaledIntegerEncodingOperator xybBOperation;

    /// <summary>
    /// The X quantizer for image precision of at least 23 bits.
    /// </summary>
    private readonly JxlSampleConverter.AccurateScaledIntegerEncodingOperator xybAccurateXOperation;

    /// <summary>
    /// The Y quantizer for image precision of at least 23 bits.
    /// </summary>
    private readonly JxlSampleConverter.AccurateScaledIntegerEncodingOperator xybAccurateYOperation;

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

        if (!encoder.SkipMetadata && exifProfile is not null
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
        IccProfile? sourceProfile = image.Frames.RootFrame.Metadata.IccProfile ?? image.Metadata.IccProfile;
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

        if (this.colorType == JxlColorType.Other)
        {
            // Other has no compact color description, including when its profile is suggested output for coding XYB.
            Guard.IsTrue(sourceProfile is not null, nameof(image), "JPEG XL Other color requires an ICC profile.");
        }

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
                ? IccProfile.GetIccProfileForEncoding(sourceProfile, PixelColorType.Luminance) : sourceProfile;
        }
        else if (this.colorType == JxlColorType.Other)
        {
            // Other has no compact encoding. Its ICC profile defines the encoded device components.
            this.profile = sourceProfile;
            profileConverter = new ColorProfileConverter(new ColorConversionOptions
            {
                SourceIccProfile = CompactSrgbV4Profile.Profile,
                TargetIccProfile = this.profile,
                MemoryAllocator = configuration.MemoryAllocator
            });
        }
        else
        {
            PixelColorType sampleColorType = this.hasBlack
                ? PixelColorType.CMYK : this.colorType == JxlColorType.Luminance ? PixelColorType.Luminance : PixelColorType.RGB;
            this.profile = IccProfile.GetIccProfileForEncoding(
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

        if (this.usesXyb)
        {
            // Match the reference modular quantizer, including its minimum positive distance of 0.05.
            float distance = Math.Max(encoder.Distance, 0.05F);
            float xFactor = 65536F * (1F / (1F + (23F * distance)));
            float yFactor = 4096F * (1F / (1F + (14F * distance)));

            // DC steps use half storage after a factor of 128. Quantize with the serialized steps,
            // rather than the unrounded setup factors. The header retains X, Y and B order.
            float xStep = GetDcFactor(xFactor);
            float yStep = GetDcFactor(yFactor);
            this.dcFactors = new Vector3(xStep, yStep, yStep);

            if (bitDepth.BitsPerSample < 23)
            {
                this.xybXOperation = new(1F / xStep, true);
                this.xybYOperation = new(1F / yStep, true);
            }
            else
            {
                this.xybAccurateXOperation = new(1F / xStep, true);
                this.xybAccurateYOperation = new(1F / yStep, true);
            }

            // Native B quantization uses a double factor independently of the image precision.
            this.xybBOperation = new(1F / yStep, false);
        }

        this.groupWidth = encoder.Progressive ? size.Width : Math.Min(size.Width, GroupDimension);
        this.groupHeight = encoder.Progressive ? 1 : Math.Min(size.Height, GroupDimension);
        this.groupColumns = ((size.Width - 1) / GroupDimension) + 1;
        this.groupRows = ((size.Height - 1) / GroupDimension) + 1;
        this.groupCount = checked(this.groupColumns * this.groupRows);

        // Each DC group spans eight coding groups on each axis. A single coding group combines every section.
        this.dcGroupCount = checked((((this.groupColumns - 1) / 8) + 1) * (((this.groupRows - 1) / 8) + 1));
        this.sectionCount = this.groupCount == 1 && !encoder.Progressive ? 1
            : checked(2 + this.dcGroupCount + (this.groupCount * (encoder.Progressive ? 2 : 1)));

        MemoryAllocator allocator = configuration.MemoryAllocator;
        this.payload = new ChunkedMemoryStream(allocator);
        Buffer2D<int>? samples = null;
        IMemoryOwner<Vector4>? pixelRow = null;
        IMemoryOwner<float>? floatRows = null;
        JxlModularChannel[] progressiveChannels = [];
        Buffer2D<int>? squeezeRows = null;
        Buffer2D<float>? expandedAlpha = null;
        JxlUpsampler? alphaUpsampler = null;

        try
        {
            // Colors and derived channels stay live together until group coding ends. Supplied channels are
            // packed and emitted one at a time, so they share one plane. Row starts retain the same group-width stride.
            samples = allocator.Allocate2D<int>(this.groupWidth, this.groupHeight * (this.componentCount - this.suppliedScratchOffset));
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

            if (encoder.Progressive)
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
            if (this.bitDepth.BitsPerSample < 23)
            {
                this.EncodeAlpha(image, this.xybXOperation, cancellationToken);
            }
            else
            {
                this.EncodeAlpha(image, this.xybAccurateXOperation, cancellationToken);
            }

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
        this.samples.Dispose();
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
        LongRational longest = new(0, 1);
        bool zeroDelay = false;

        foreach (ImageFrame<TPixel> frame in image.Frames)
        {
            LongRational delay = frame.Metadata.GetJxlMetadata().FrameDelay.Simplify();
            ulong denominator = (ulong)delay.Denominator;
            UInt128 multiple = (UInt128)numerator * (denominator / Numerics.GreatestCommonDivisor(numerator, denominator));
            numerator = (ulong)UInt128.Min(multiple, MaximumRateNumerator);
            zeroDelay |= delay.Numerator == 0;

            if ((Int128)delay.Numerator * longest.Denominator > (Int128)longest.Numerator * delay.Denominator)
            {
                longest = delay;
            }
        }

        if (longest.Numerator != 0)
        {
            // A frame duration occupies uint ticks. Coarsen the clock only when the longest source duration needs it.
            UInt128 limit = (UInt128)uint.MaxValue * MaximumRateDenominator * (ulong)longest.Denominator / (ulong)longest.Numerator;
            numerator = Math.Max(1UL, (ulong)UInt128.Min(numerator, limit));
        }

        ulong common = 0;

        foreach (ImageFrame<TPixel> frame in image.Frames)
        {
            LongRational delay = frame.Metadata.GetJxlMetadata().FrameDelay;
            ulong duration = (ulong)((UInt128)(ulong)delay.Numerator * numerator / (ulong)delay.Denominator);
            common = Numerics.GreatestCommonDivisor(common, duration);
        }

        ulong maximum = (ulong)((UInt128)(ulong)longest.Numerator * numerator / (ulong)longest.Denominator);
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
                factor = Math.Min(factor, uint.MaxValue / maximumTicks);
            }

            numerator *= factor;
        }

        this.ticksPerSecondNumerator = (uint)numerator;
        this.ticksPerSecondDenominator = selected;

        for (int i = 0; i < image.Frames.Count; i++)
        {
            uint duration = this.GetFrameDuration(image.Frames[i].Metadata.GetJxlMetadata().FrameDelay, i == image.Frames.Count - 1);

            // Level 5 permits displayed frames at no more than 120 fps. The encoded clock and duration,
            // including the one tick used for a displayed zero-delay source frame, determine that rate.
            this.requiresLevelTen |= duration > 0 && (UInt128)duration * selected * 120 < numerator;
        }
    }

    /// <summary>
    /// Expresses one source delay in the prepared file clock, retaining non-final frames as displayed frames.
    /// </summary>
    /// <param name="delay">The source delay in seconds.</param>
    /// <param name="last">Whether this is the final displayed frame.</param>
    /// <returns>The serialized unsigned tick count.</returns>
    private uint GetFrameDuration(LongRational delay, bool last)
    {
        UInt128 ticks = (UInt128)(ulong)delay.Numerator * this.ticksPerSecondNumerator
            / ((UInt128)(ulong)delay.Denominator * this.ticksPerSecondDenominator);
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
        byte[]? exif = this.encoder.SkipMetadata ? null : metadata.ExifProfile?.ToByteArray();
        byte[]? xmp = this.encoder.SkipMetadata ? null : metadata.XmpProfile?.Data;

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
    /// Gets the reconstruction step after the format's half-precision field conversion.
    /// </summary>
    /// <param name="encodingFactor">The positive encoding scale selected from the validated distance.</param>
    /// <returns>The reconstruction step in native component units.</returns>
    private static float GetDcFactor(float encodingFactor)
    {
        // The reference field writer truncates the mantissa. These distance-derived values are normal half values,
        // so removing the thirteen excess binary32 mantissa bits makes the shared half packer exact here.
        float value = (1F / encodingFactor) * 128F;
        uint bits = BitConverter.SingleToUInt32Bits(value) & 0xFFFFE000U;
        ushort packed = HalfTypeHelper.Pack(BitConverter.UInt32BitsToSingle(bits));

        return HalfTypeHelper.Unpack(packed) * (1F / 128F);
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
        using IMemoryOwner<byte> outputOwner = allocator.Allocate<byte>(Math.Max(sizeof(uint), configuration.StreamProcessingBufferSize));
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
            this.WriteBits(1, this.usesXyb ? 0U : 1U, output);

            if (this.usesXyb)
            {
                this.WriteBits(16, HalfTypeHelper.Pack(this.dcFactors.X * 128F), output);
                this.WriteBits(16, HalfTypeHelper.Pack(this.dcFactors.Y * 128F), output);
                this.WriteBits(16, HalfTypeHelper.Pack(this.dcFactors.Z * 128F), output);
            }

            this.WriteBits(1, 1, output);
            this.WritePrefixHeader(6, codeLength, output);
            this.WriteValue(0, codeLength, prefixCodes, output); // Property zero marks a leaf.
            this.WriteValue(useGradient ? 5U : 0U, codeLength, prefixCodes, output); // Clamped gradient or zero predictor.
            this.WriteValue(0, codeLength, prefixCodes, output); // No signed prediction offset.
            this.WriteValue(0, codeLength, prefixCodes, output); // Multiplier exponent zero.
            this.WriteValue(0, codeLength, prefixCodes, output); // Stored multiplier minus one: use unit residuals.
            this.WritePrefixHeader(1, codeLength, output);
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
                duration = this.GetFrameDuration(image.Metadata.GetJxlMetadata().FrameDelay, last);
            }

            this.WriteFrameHeader(sectionSizes, duration, last, output);

            // Section lengths precede the variable-length entropy payload. Its allocator-backed stream retains
            // the encoded bytes until the table is written; the output buffer is then reused for this final transfer.
            payload.Position = 0;

            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int count = payload.Read(output[..(int)Math.Min(remaining, output.Length)]);
                stream.Write(output[..count]);
                remaining -= count;
            }
        }
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
        Rectangle targetBounds = new(0, this.colorComponents * this.groupHeight, bounds.Width, bounds.Height);
        this.EncodeSuppliedSamples(index, bounds, this.samples.GetRegion(targetBounds), cancellationToken);
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
        Span<int> carriers = this.GetComponentRow(0, 0);

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

            if (this.usesXyb)
            {
                // Modular XYB stores Y, X and B-minus-Y. Subtract quantized Y, not a floating difference:
                // reconstruction adds integer Y before applying B's reconstruction step.
                Span<int> yRow = this.GetComponentRow(0, y)[..bounds.Width];
                Span<int> xRow = this.GetComponentRow(1, y)[..bounds.Width];
                Span<int> bRow = this.GetComponentRow(2, y)[..bounds.Width];

                // The closed color operation selects float or double arithmetic before traversal.
                // The JIT removes this type test for each closed worker, as it does for short sample loading.
                if (typeof(TColor) == typeof(JxlSampleConverter.AccurateScaledIntegerEncodingOperator))
                {
                    JxlSampleConverter.Encode(second, yRow, this.xybAccurateYOperation);
                }
                else
                {
                    JxlSampleConverter.Encode(second, yRow, this.xybYOperation);
                }

                JxlSampleConverter.Encode(first, xRow, colorOperation);
                JxlSampleConverter.Encode(third, bRow, this.xybBOperation);
                TensorPrimitives.Subtract<int>(bRow, yRow, bRow);
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
                JxlSampleConverter.Encode(alpha, this.GetComponentRow(alphaComponent, y)[..bounds.Width], alphaOperation);
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

            foreach (uint residual in residuals)
            {
                this.WriteValue(residual, codeLength, prefixCodes, output);
            }
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
            foreach (uint residual in residuals)
            {
                this.WriteValue(residual, codeLength, prefixCodes, output);
            }
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

            foreach (uint residual in residuals)
            {
                this.WriteValue(residual, codeLength, prefixCodes, output);
            }
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
        // The frame uses original-color modular samples, no optional features, no upsampling and the default 256-pixel groups.
        this.WriteBits(1, 0, output); // Explicit frame fields.
        this.WriteBits(2, 0, output); // Regular displayed frame.
        this.WriteBits(1, 1, output); // Modular coding.
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

        this.WriteBits(2, 1, output); // Group size shift one selects 256 pixels.
        this.WriteBits(2, this.encoder.Progressive ? 1U : 0U, output); // Two passes or one pass.

        if (this.encoder.Progressive)
        {
            this.WriteBits(2, 1, output); // One progressive display stage.
            this.WriteBits(2, 0, output); // No coefficient precision shift in the first pass.
            this.WriteBits(2, 1, output); // Half-resolution display stage.
            this.WriteBits(2, 0, output); // The half-resolution stage ends after pass zero.
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
        this.WriteBits(2, 0, output); // Disable the edge-preserving filter.
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

        return this.samples.DangerousGetRowSpan((component * this.groupHeight) + y);
    }
}
