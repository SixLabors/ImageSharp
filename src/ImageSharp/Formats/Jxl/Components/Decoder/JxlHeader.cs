// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;

/// <summary>
/// Reads and retains the image-wide fields needed by JPEG XL frame decoding.
/// </summary>
/// <remarks>
/// Field order and default groups follow libjxl's image_metadata.cc, headers.cc and color_encoding_internal.cc.
/// The decoder owns this header. Color coordinates, transfer identifiers and sample layouts use the shared types.
/// </remarks>
internal sealed class JxlHeader
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JxlHeader"/> class with the format's default field values.
    /// </summary>
    private JxlHeader()
    {
    }

    /// <summary>
    /// Gets the decoded embedded ICC profile, or null when it is absent or its recoverable prediction error was ignored.
    /// </summary>
    public IccProfile? IccProfile { get; private set; }

    /// <summary>
    /// Gets the absorbance transform used for XYB reconstruction.
    /// </summary>
    public XybTransform XybTransform { get; private set; } = KnownXybTransforms.Iso18181;

    /// <summary>
    /// Gets the three quantization biases and the bias numerator used for small coefficients.
    /// </summary>
    public Vector4 QuantizationBias { get; private set; } = new(1F - 0.05465007330715401F, 1F - 0.07005449891748593F, 1F - 0.049935103337343655F, 0.145F);

    /// <summary>
    /// Gets the custom twofold upsampling kernel weights. Empty storage selects the format defaults.
    /// </summary>
    public ReadOnlyMemory<float> Upsampling2Weights { get; private set; }

    /// <summary>
    /// Gets the custom fourfold upsampling kernel weights. Empty storage selects the format defaults.
    /// </summary>
    public ReadOnlyMemory<float> Upsampling4Weights { get; private set; }

    /// <summary>
    /// Gets the custom eightfold upsampling kernel weights. Empty storage selects the format defaults.
    /// </summary>
    public ReadOnlyMemory<float> Upsampling8Weights { get; private set; }

    /// <summary>
    /// Gets the encoded image dimensions, before orientation.
    /// </summary>
    public Size Size { get; private set; }

    /// <summary>
    /// Gets the optional intended display dimensions.
    /// </summary>
    public Size? IntrinsicSize { get; private set; }

    /// <summary>
    /// Gets the optional preview dimensions. The preview is a separate frame in the codestream.
    /// </summary>
    public Size? PreviewSize { get; private set; }

    /// <summary>
    /// Gets the format metadata populated from the image header.
    /// </summary>
    public JxlMetadata Metadata { get; } = new();

    /// <summary>
    /// Gets a value indicating whether reconstruction uses the XYB absorbance transform.
    /// This syntax flag is independent of the color space declared by the original profile.
    /// </summary>
    public bool UsesXybTransform { get; private set; } = true;

    /// <summary>
    /// Gets the EXIF-compatible orientation of the encoded image.
    /// </summary>
    public ushort Orientation { get; private set; } = ExifOrientationMode.TopLeft;

    /// <summary>
    /// Gets the animation tick rate, or null for a still image. Frame durations retain their integer tick counts.
    /// </summary>
    public Rational? TicksPerSecond { get; private set; }

    /// <summary>
    /// Gets a value indicating whether animation frame headers contain time codes.
    /// </summary>
    public bool HasTimeCodes { get; private set; }

    /// <summary>
    /// Gets a value indicating whether signed 16-bit buffers can hold modular intermediate samples.
    /// </summary>
    public bool Modular16BitBufferSufficient { get; private set; } = true;

    /// <summary>
    /// Gets a value indicating whether the declared color profile follows the transform header as compressed ICC data.
    /// </summary>
    public bool HasIccProfile { get; private set; }

    /// <summary>
    /// Gets the declared profile's component model. XYB coding does not change this declaration.
    /// </summary>
    public JxlColorType ProfileColorType { get; private set; }

    /// <summary>
    /// Gets the compact profile's white point. An embedded ICC profile supplies its own color description.
    /// </summary>
    public CieXyChromaticityCoordinates WhitePoint { get; private set; } = KnownIlluminants.D65Chromaticity;

    /// <summary>
    /// Gets the compact RGB profile's primary chromaticities.
    /// </summary>
    public RgbPrimariesChromaticityCoordinates Primaries { get; private set; } = KnownRgbWorkingSpaces.SRgb.ChromaticityCoordinates;

    /// <summary>
    /// Gets the compact profile's transfer identifier when <see cref="Gamma"/> is null.
    /// </summary>
    public CicpTransferCharacteristics TransferCharacteristics { get; private set; } = CicpTransferCharacteristics.Iec61966_2_1;

    /// <summary>
    /// Gets an explicit compression exponent, or null when the profile specifies a transfer identifier.
    /// </summary>
    public Rational? Gamma { get; private set; }

    /// <summary>
    /// Gets the compact profile's rendering intent.
    /// </summary>
    public IccRenderingIntent RenderingIntent { get; private set; } = IccRenderingIntent.MediaRelativeColorimetric;

    /// <summary>
    /// Gets the reference intensity in nits used by image reconstruction.
    /// </summary>
    public float IntensityTarget { get; private set; } = KnownXybTransforms.Iso18181.IntensityTarget;

    /// <summary>
    /// Gets the image's declared minimum luminance in nits.
    /// </summary>
    public float MinimumLuminance { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the linear tone-mapping threshold is relative to the display maximum.
    /// </summary>
    public bool RelativeToMaximumDisplay { get; private set; }

    /// <summary>
    /// Gets the tone-mapping threshold below which luminance is scaled linearly.
    /// </summary>
    public float LinearBelow { get; private set; }

    /// <summary>
    /// Reads the complete image header, including custom transforms and embedded ICC data.
    /// </summary>
    /// <param name="reader">The codestream bit reader, passed by reference to retain prefetched bits.</param>
    /// <param name="allocator">The decoder's allocator for entropy tables and ICC scratch storage.</param>
    /// <param name="handling">The policy for recoverable label and suggested output-profile errors.</param>
    /// <param name="cancellationToken">The token checked during ICC reconstruction.</param>
    /// <returns>The parsed header.</returns>
    public static JxlHeader Read(ref JxlBitReader reader, MemoryAllocator allocator, SegmentIntegrityHandling handling, CancellationToken cancellationToken)
    {
        JxlHeader header = ReadMetadata(ref reader, handling);
        header.ReadTransforms(ref reader);
        if (header.HasIccProfile)
        {
            // XYB reconstruction has its own matrix and intensity fields. Its ICC profile describes
            // suggested output colors. Native-component images need the ICC profile to interpret their samples.
            header.IccProfile = JxlIccReader.Read(
                ref reader, allocator, header.UsesXybTransform ? handling : SegmentIntegrityHandling.Strict, cancellationToken);
        }

        // Frame syntax starts on the next byte. The padding belongs to this header and must be zero,
        // including when a compact color description omits the compressed ICC section.
        reader.AlignToByte();
        return header;
    }

    /// <summary>
    /// Reads image-wide metadata before the custom transforms and entropy-coded profile.
    /// </summary>
    /// <param name="reader">The codestream bit reader.</param>
    /// <param name="handling">The policy for invalid optional extra-channel labels.</param>
    /// <returns>The header with its image metadata populated.</returns>
    private static JxlHeader ReadMetadata(ref JxlBitReader reader, SegmentIntegrityHandling handling)
    {
        if (reader.ReadBits(16) != 0x0AFF)
        {
            throw new InvalidImageContentException("The JPEG XL codestream signature is invalid.");
        }

        JxlHeader header = new() { Size = reader.ReadSize() };
        if (reader.ReadBoolean())
        {
            // The all-default group includes XYB coding, although the declared output profile is sRGB.
            header.Metadata.ColorType = JxlColorType.Xyb;
            return header;
        }

        bool extraFields = reader.ReadBoolean();
        if (extraFields)
        {
            // This group precedes the sample layout. Its presence controls orientation, display sizing,
            // preview, animation and the later tone-mapping group; absent fields retain the format defaults.
            header.Orientation = (ushort)(reader.ReadBits(3) + 1);
            header.IntrinsicSize = reader.ReadBoolean() ? reader.ReadSize() : null;
            header.PreviewSize = reader.ReadBoolean() ? ReadPreviewSize(ref reader) : null;
            if (reader.ReadBoolean())
            {
                uint numerator = reader.ReadU32((0, 100), (0, 1000), (10, 1), (30, 1));
                uint denominator = reader.ReadU32((0, 1), (0, 1001), (8, 1), (10, 1));
                header.TicksPerSecond = new Rational(numerator, denominator);
                header.Metadata.RepeatCount = reader.ReadU32((0, 0), (3, 0), (16, 0), (32, 0));
                header.HasTimeCodes = reader.ReadBoolean();
            }
        }

        header.Metadata.BitDepth = ReadBitDepth(ref reader);
        header.Modular16BitBufferSufficient = reader.ReadBoolean();

        // Extra channels have independent precision and resolution. Read their descriptions before
        // the color encoding so the same immutable metadata is available to every later frame.
        uint extraChannelCount = reader.ReadU32((0, 0), (0, 1), (4, 2), (12, 1));
        for (uint i = 0; i < extraChannelCount; i++)
        {
            header.Metadata.ExtraChannels.Add(ReadExtraChannel(ref reader, handling));
        }

        // The coding transform and declared profile are separate fields. In an XYB image, the profile
        // describes the suggested output space; it does not describe the native XYB component values.
        header.UsesXybTransform = reader.ReadBoolean();
        header.ReadColorEncoding(ref reader);
        header.Metadata.ColorType = header.UsesXybTransform ? JxlColorType.Xyb : header.ProfileColorType;
        if (extraFields && !reader.ReadBoolean())
        {
            // Luminance fields use nits. LinearBelow is relative to the maximum display only when its flag is set,
            // so the upper bound of one applies only in that mode. ReadHalf already rejects nonfinite syntax.
            header.IntensityTarget = reader.ReadHalf();
            header.MinimumLuminance = reader.ReadHalf();
            header.RelativeToMaximumDisplay = reader.ReadBoolean();
            header.LinearBelow = reader.ReadHalf();
            if (header.IntensityTarget <= 0 || header.MinimumLuminance < 0 || header.MinimumLuminance > header.IntensityTarget
                || header.LinearBelow < 0 || (header.RelativeToMaximumDisplay && header.LinearBelow > 1))
            {
                throw new InvalidImageContentException("The JPEG XL tone-mapping parameters are invalid.");
            }
        }

        reader.SkipExtensions();
        return header;
    }

    /// <summary>
    /// Reads a sample layout and validates it before constructing the shared metadata value.
    /// </summary>
    /// <param name="reader">The codestream bit reader.</param>
    /// <returns>The validated sample layout.</returns>
    private static JxlBitDepth ReadBitDepth(ref JxlBitReader reader)
    {
        // Floating layouts include one sign bit. The remaining bits divide into the explicit exponent
        // and mantissa fields; integer layouts have no exponent. Validate before narrowing to metadata bytes.
        bool floating = reader.ReadBoolean();
        uint bits = floating ? reader.ReadU32((0, 32), (0, 16), (0, 24), (6, 1)) : reader.ReadU32((0, 8), (0, 10), (0, 12), (6, 1));
        uint exponent = floating ? reader.ReadBits(4) + 1 : 0;
        int mantissa = (int)bits - (int)exponent - 1;
        if (floating ? exponent is < 2 or > 8 || mantissa is < 2 or > 23 : bits > 31)
        {
            throw new InvalidImageContentException("The JPEG XL sample precision is invalid.");
        }

        return new JxlBitDepth((byte)bits, (byte)exponent);
    }

    /// <summary>
    /// Reads one extra channel in encoded order, including its type-specific fields.
    /// </summary>
    /// <param name="reader">The codestream bit reader.</param>
    /// <param name="handling">The policy for an invalid optional channel label.</param>
    /// <returns>The immutable channel description.</returns>
    private static JxlExtraChannelMetadata ReadExtraChannel(ref JxlBitReader reader, SegmentIntegrityHandling handling)
    {
        if (reader.ReadBoolean())
        {
            return new JxlExtraChannelMetadata(JxlExtraChannelType.Alpha, new JxlBitDepth(8, 0), PixelAlphaRepresentation.Unassociated);
        }

        // Values 7 through 14 are reserved. Unknown required channels (15) cannot be decoded; optional channels (16) remain valid.
        JxlExtraChannelType type = (JxlExtraChannelType)reader.ReadEnum(0x1007F);
        JxlBitDepth depth = ReadBitDepth(ref reader);
        uint shift = reader.ReadU32((0, 0), (0, 3), (0, 4), (3, 1));
        if (shift > 3)
        {
            throw new InvalidImageContentException("The JPEG XL extra-channel dimension shift exceeds three.");
        }

        string name = reader.ReadName(handling);

        // Only alpha channels encode association. Spot colors and CFA channels each have their own
        // following fields, so reading an absent field would shift the remainder of the image header.
        PixelAlphaRepresentation alpha = type == JxlExtraChannelType.Alpha
            ? reader.ReadBoolean() ? PixelAlphaRepresentation.Associated : PixelAlphaRepresentation.Unassociated
            : PixelAlphaRepresentation.None;

        // Spot mixtures affect reconstructed colors. Their F16 fields must be finite even when optional
        // labels can be ignored: no replacement mixture is defined, and removing a channel would change channel indices.
        Vector4 spotColor = type == JxlExtraChannelType.SpotColor
            ? new Vector4(reader.ReadHalf(), reader.ReadHalf(), reader.ReadHalf(), reader.ReadHalf())
            : default;

        ushort cfaChannel = type == JxlExtraChannelType.ColorFilterArray ? (ushort)reader.ReadU32((0, 1), (2, 0), (4, 3), (8, 19)) : (ushort)0;
        return new JxlExtraChannelMetadata(type, depth, alpha)
        {
            Name = name,
            DimensionShift = (byte)shift,
            SpotColor = spotColor,
            ColorFilterArrayChannel = cfaChannel
        };
    }

    /// <summary>
    /// Reads the preview's distinct dimension coding. Unlike the image size, its reduced dimensions do not subtract one.
    /// </summary>
    /// <param name="reader">The codestream bit reader.</param>
    /// <returns>The preview dimensions.</returns>
    private static Size ReadPreviewSize(ref JxlBitReader reader)
    {
        bool divided = reader.ReadBoolean();
        uint height = divided ? reader.ReadU32((0, 16), (0, 32), (5, 1), (9, 33)) * 8 : reader.ReadU32((6, 1), (8, 65), (10, 321), (12, 1345));
        uint ratio = reader.ReadBits(3);
        uint width = ratio switch
        {
            0 => divided ? reader.ReadU32((0, 16), (0, 32), (5, 1), (9, 33)) * 8 : reader.ReadU32((6, 1), (8, 65), (10, 321), (12, 1345)),
            1 => height,
            2 => height * 12 / 10,
            3 => height * 4 / 3,
            4 => height * 3 / 2,
            5 => height * 16 / 9,
            6 => height * 5 / 4,
            _ => height * 2
        };

        return new Size((int)width, (int)height);
    }

    /// <summary>
    /// Reads a compact profile description or the component declaration that precedes embedded ICC data.
    /// </summary>
    /// <param name="reader">The codestream bit reader.</param>
    private void ReadColorEncoding(ref JxlBitReader reader)
    {
        if (reader.ReadBoolean())
        {
            return;
        }

        this.HasIccProfile = reader.ReadBoolean();
        this.ProfileColorType = (JxlColorType)reader.ReadEnum(0xF);
        if (this.HasIccProfile)
        {
            // The component declaration remains present, but the ICC bytes replace all compact profile fields.
            // Their entropy-coded payload follows the custom transform group, rather than this declaration.
            return;
        }

        if (this.ProfileColorType != JxlColorType.Xyb)
        {
            this.WhitePoint = reader.ReadEnum((1UL << 1) | (1UL << 2) | (1UL << 10) | (1UL << 11)) switch
            {
                1 => KnownIlluminants.D65Chromaticity,
                2 => ReadChromaticity(ref reader),
                10 => KnownIlluminants.EChromaticity,
                _ => KnownIlluminants.DciP3Chromaticity
            };
        }

        if (this.ProfileColorType is not (JxlColorType.Luminance or JxlColorType.Xyb))
        {
            this.Primaries = reader.ReadEnum((1UL << 1) | (1UL << 2) | (1UL << 9) | (1UL << 11)) switch
            {
                1 => KnownRgbWorkingSpaces.SRgb.ChromaticityCoordinates,
                2 => new RgbPrimariesChromaticityCoordinates(ReadChromaticity(ref reader), ReadChromaticity(ref reader), ReadChromaticity(ref reader)),
                9 => KnownRgbWorkingSpaces.Rec2020.ChromaticityCoordinates,
                _ => KnownRgbWorkingSpaces.DisplayP3.ChromaticityCoordinates
            };
        }

        if (this.ProfileColorType == JxlColorType.Xyb)
        {
            // A declared XYB profile has an implicit cube-root transfer. No transfer field is present.
            this.Gamma = new Rational(1, 3);
        }
        else if (reader.ReadBoolean())
        {
            // Gamma is an integer number of ten-millionths. Keep that exact ratio here instead of
            // rounding it through float before a transfer-function implementation consumes it.
            uint gamma = reader.ReadBits(24);
            const uint gammaScale = 10_000_000;
            const uint maximumExpansionExponent = 8192;
            if (gamma > gammaScale || (ulong)gamma * maximumExpansionExponent < gammaScale)
            {
                throw new InvalidImageContentException("The JPEG XL gamma exponent is invalid.");
            }

            this.Gamma = new Rational(gamma, gammaScale);
        }
        else
        {
            const ulong transferMask = (1UL << 1) | (1UL << 8) | (1UL << 13) | (1UL << 16) | (1UL << 17) | (1UL << 18);
            this.TransferCharacteristics = (CicpTransferCharacteristics)reader.ReadEnum(transferMask);
        }

        this.RenderingIntent = (IccRenderingIntent)reader.ReadEnum(0xF);
        if (this.ProfileColorType == JxlColorType.Other)
        {
            throw new InvalidImageContentException("An unknown JPEG XL color space requires an ICC profile.");
        }
    }

    /// <summary>
    /// Reads signed millionths without rounding through an integer or pixel representation.
    /// </summary>
    /// <param name="reader">The codestream bit reader.</param>
    /// <returns>The encoded chromaticity coordinates.</returns>
    private static CieXyChromaticityCoordinates ReadChromaticity(ref JxlBitReader reader)
    {
        uint x = reader.ReadU32((19, 0), (19, 524288), (20, 1048576), (21, 2097152));
        uint y = reader.ReadU32((19, 0), (19, 524288), (20, 1048576), (21, 2097152));

        // Zigzag coding alternates nonnegative and negative integers: 0, -1, 1, -2, ... .
        // The encoded coordinate has at most 22 bits, so the signed values and subtraction fit in int.
        int signedX = (int)(x >> 1) ^ -(int)(x & 1);
        int signedY = (int)(y >> 1) ^ -(int)(y & 1);
        return new CieXyChromaticityCoordinates(signedX * 1E-6F, signedY * 1E-6F);
    }

    /// <summary>
    /// Reads custom reconstruction parameters without allocating storage for default upsampling kernels.
    /// </summary>
    /// <param name="reader">The codestream bit reader.</param>
    private void ReadTransforms(ref JxlBitReader reader)
    {
        Matrix4x4 inverse = this.XybTransform.Inverse;
        Vector3 biases = this.XybTransform.Biases;
        bool customOpsin = false;
        if (!reader.ReadBoolean())
        {
            if (this.UsesXybTransform && !reader.ReadBoolean())
            {
                // Retain the file's row-major coefficients. ColorConversionOptions prepares the
                // row-vector matrix layout when the codec selects this transform for public conversion.
                float m00 = reader.ReadHalf();
                float m01 = reader.ReadHalf();
                float m02 = reader.ReadHalf();
                float m10 = reader.ReadHalf();
                float m11 = reader.ReadHalf();
                float m12 = reader.ReadHalf();
                float m20 = reader.ReadHalf();
                float m21 = reader.ReadHalf();
                float m22 = reader.ReadHalf();
                inverse = new Matrix4x4(m00, m01, m02, 0, m10, m11, m12, 0, m20, m21, m22, 0, 0, 0, 0, 1);
                biases = new Vector3(reader.ReadHalf(), reader.ReadHalf(), reader.ReadHalf());
                this.QuantizationBias = new Vector4(reader.ReadHalf(), reader.ReadHalf(), reader.ReadHalf(), reader.ReadHalf());
                customOpsin = true;
            }

            uint mask = reader.ReadBits(3);

            // Each bit independently replaces one upsampling kernel. Missing kernels keep empty storage,
            // which selects the standard coefficients without allocating arrays for the common defaults.
            this.Upsampling2Weights = (mask & 1) != 0 ? ReadWeights(ref reader, 15) : default;
            this.Upsampling4Weights = (mask & 2) != 0 ? ReadWeights(ref reader, 55) : default;
            this.Upsampling8Weights = (mask & 4) != 0 ? ReadWeights(ref reader, 210) : default;
        }

        if (customOpsin || this.IntensityTarget != KnownXybTransforms.Iso18181.IntensityTarget)
        {
            this.XybTransform = new XybTransform(inverse, biases, this.IntensityTarget);
        }
    }

    /// <summary>
    /// Reads a custom kernel's fixed number of finite half-precision weights.
    /// </summary>
    /// <param name="reader">The codestream bit reader.</param>
    /// <param name="count">The weight count fixed by the selected kernel size.</param>
    /// <returns>The retained custom weights.</returns>
    private static float[] ReadWeights(ref JxlBitReader reader, int count)
    {
        float[] result = new float[count];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = reader.ReadHalf();
        }

        return result;
    }
}
