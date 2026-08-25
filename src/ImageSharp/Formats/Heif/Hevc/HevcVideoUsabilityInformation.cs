// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Contains the still-image presentation fields declared by HEVC video-usability information.
/// </summary>
internal sealed class HevcVideoUsabilityInformation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HevcVideoUsabilityInformation"/> class.
    /// </summary>
    /// <param name="reader">The sequence-parameter-set raw byte sequence payload reader.</param>
    /// <param name="chromaFormat">The sequence chroma-format identifier.</param>
    /// <param name="separateColorPlane">Whether 4:4:4 components are coded as separate planes.</param>
    /// <param name="maxSubLayersMinusOne">The highest declared temporal sublayer index.</param>
    /// <exception cref="InvalidImageContentException">The VUI syntax is invalid for a still-image item.</exception>
    public HevcVideoUsabilityInformation(
        ref HevcBitReader reader,
        byte chromaFormat,
        bool separateColorPlane,
        int maxSubLayersMinusOne)
    {
        this.AspectRatioInfoPresent = reader.ReadFlag();
        if (this.AspectRatioInfoPresent)
        {
            this.AspectRatioIdc = (byte)reader.ReadBits(8);
            if (this.AspectRatioIdc == byte.MaxValue)
            {
                this.SarWidth = (ushort)reader.ReadBits(16);
                this.SarHeight = (ushort)reader.ReadBits(16);
                if (this.SarWidth == 0 || this.SarHeight == 0)
                {
                    throw new InvalidImageContentException("The HEVC VUI declares an invalid extended sample aspect ratio.");
                }
            }
            else if (this.AspectRatioIdc > 16)
            {
                throw new InvalidImageContentException("The HEVC VUI declares a reserved sample aspect ratio.");
            }
        }

        if (reader.ReadFlag())
        {
            reader.ReadFlag();
        }

        this.VideoSignalTypePresent = reader.ReadFlag();
        if (this.VideoSignalTypePresent)
        {
            reader.ReadBits(3);
            this.FullRange = reader.ReadFlag();
            this.ColorDescriptionPresent = reader.ReadFlag();
            if (this.ColorDescriptionPresent)
            {
                this.ColorPrimaries = (byte)reader.ReadBits(8);
                this.TransferCharacteristics = (byte)reader.ReadBits(8);
                this.MatrixCoefficients = (byte)reader.ReadBits(8);
            }
        }

        this.ChromaLocationInfoPresent = reader.ReadFlag();
        if (this.ChromaLocationInfoPresent)
        {
            uint topFieldLocation = reader.ReadUnsignedExpGolomb();
            uint bottomFieldLocation = reader.ReadUnsignedExpGolomb();
            if (topFieldLocation > 5 || bottomFieldLocation > 5)
            {
                throw new InvalidImageContentException("The HEVC VUI declares an invalid chroma sample location.");
            }

            this.ChromaSampleLocationTopField = (HevcChromaSampleLocation)topFieldLocation;
            this.ChromaSampleLocationBottomField = (HevcChromaSampleLocation)bottomFieldLocation;
        }

        reader.ReadFlag();
        if (reader.ReadFlag())
        {
            // A field sequence requires paired-field presentation state and is not a single HEIF image item.
            throw new InvalidImageContentException("Interlaced HEVC field sequences are not supported as still-image items.");
        }

        reader.ReadFlag();

        this.DefaultDisplayWindowPresent = reader.ReadFlag();
        if (this.DefaultDisplayWindowPresent)
        {
            int cropUnitWidth = HevcParameterSetSyntax.GetCropUnitWidth(chromaFormat, separateColorPlane);
            int cropUnitHeight = HevcParameterSetSyntax.GetCropUnitHeight(chromaFormat, separateColorPlane);
            this.DefaultDisplayWindowLeftOffset = ReadScaledOffset(ref reader, cropUnitWidth);
            this.DefaultDisplayWindowRightOffset = ReadScaledOffset(ref reader, cropUnitWidth);
            this.DefaultDisplayWindowTopOffset = ReadScaledOffset(ref reader, cropUnitHeight);
            this.DefaultDisplayWindowBottomOffset = ReadScaledOffset(ref reader, cropUnitHeight);
        }

        if (reader.ReadFlag())
        {
            // VUI timing and HRD fields affect scheduling, not the reconstructed still-image samples.
            reader.ReadBits(32);
            reader.ReadBits(32);
            if (reader.ReadFlag())
            {
                reader.ReadUnsignedExpGolomb();
            }

            if (reader.ReadFlag())
            {
                bool nalHrdParametersPresent = false;
                bool vclHrdParametersPresent = false;
                bool subPictureHrdParametersPresent = false;
                HevcParameterSetSyntax.SkipHrdParameters(
                    ref reader,
                    true,
                    maxSubLayersMinusOne,
                    ref nalHrdParametersPresent,
                    ref vclHrdParametersPresent,
                    ref subPictureHrdParametersPresent);
            }
        }

        if (reader.ReadFlag())
        {
            reader.ReadFlag();
            reader.ReadFlag();
            reader.ReadFlag();
            uint minimumSpatialSegmentation = reader.ReadUnsignedExpGolomb();
            if (minimumSpatialSegmentation >= 4096)
            {
                throw new InvalidImageContentException("The HEVC VUI spatial-segmentation value is invalid.");
            }

            reader.ReadUnsignedExpGolomb();
            reader.ReadUnsignedExpGolomb();
            reader.ReadUnsignedExpGolomb();
            reader.ReadUnsignedExpGolomb();
        }
    }

    /// <summary>
    /// Gets a value indicating whether sample-aspect-ratio information is present.
    /// </summary>
    public bool AspectRatioInfoPresent { get; }

    /// <summary>
    /// Gets the registered sample-aspect-ratio identifier.
    /// </summary>
    public byte AspectRatioIdc { get; }

    /// <summary>
    /// Gets the explicit horizontal sample spacing when <see cref="AspectRatioIdc"/> is 255.
    /// </summary>
    public ushort SarWidth { get; }

    /// <summary>
    /// Gets the explicit vertical sample spacing when <see cref="AspectRatioIdc"/> is 255.
    /// </summary>
    public ushort SarHeight { get; }

    /// <summary>
    /// Gets a value indicating whether video-signal-type information is present.
    /// </summary>
    public bool VideoSignalTypePresent { get; }

    /// <summary>
    /// Gets a value indicating whether component samples use the full numeric range.
    /// </summary>
    public bool FullRange { get; }

    /// <summary>
    /// Gets a value indicating whether color-description fields are present.
    /// </summary>
    public bool ColorDescriptionPresent { get; }

    /// <summary>
    /// Gets the coded color-primary identifier.
    /// </summary>
    public byte ColorPrimaries { get; }

    /// <summary>
    /// Gets the coded transfer-characteristic identifier.
    /// </summary>
    public byte TransferCharacteristics { get; }

    /// <summary>
    /// Gets the coded matrix-coefficient identifier.
    /// </summary>
    public byte MatrixCoefficients { get; }

    /// <summary>
    /// Gets a value indicating whether chroma sample-location information is present.
    /// </summary>
    public bool ChromaLocationInfoPresent { get; }

    /// <summary>
    /// Gets the top-field chroma sample-location identifier.
    /// </summary>
    public HevcChromaSampleLocation ChromaSampleLocationTopField { get; }

    /// <summary>
    /// Gets the bottom-field chroma sample-location identifier.
    /// </summary>
    public HevcChromaSampleLocation ChromaSampleLocationBottomField { get; }

    /// <summary>
    /// Gets a value indicating whether a default display window is present.
    /// </summary>
    public bool DefaultDisplayWindowPresent { get; }

    /// <summary>
    /// Gets the default display-window left offset in luma samples.
    /// </summary>
    public int DefaultDisplayWindowLeftOffset { get; }

    /// <summary>
    /// Gets the default display-window right offset in luma samples.
    /// </summary>
    public int DefaultDisplayWindowRightOffset { get; }

    /// <summary>
    /// Gets the default display-window top offset in luma samples.
    /// </summary>
    public int DefaultDisplayWindowTopOffset { get; }

    /// <summary>
    /// Gets the default display-window bottom offset in luma samples.
    /// </summary>
    public int DefaultDisplayWindowBottomOffset { get; }

    /// <summary>
    /// Reads a conformance-window offset and converts it to luma-sample units.
    /// </summary>
    /// <param name="reader">The sequence-parameter-set raw byte sequence payload reader.</param>
    /// <param name="unit">The chroma-dependent luma-sample unit.</param>
    /// <returns>The scaled offset.</returns>
    /// <exception cref="InvalidImageContentException">The scaled offset exceeds the supported image dimension range.</exception>
    private static int ReadScaledOffset(ref HevcBitReader reader, int unit)
    {
        uint offset = reader.ReadUnsignedExpGolomb();
        if (offset > int.MaxValue / unit)
        {
            throw new InvalidImageContentException("The HEVC VUI display-window offset is too large.");
        }

        return (int)offset * unit;
    }
}
