// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Contains the HEVC sequence fields required to reconstruct one independently coded still image.
/// </summary>
internal sealed class HevcSequenceParameterSet
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HevcSequenceParameterSet"/> class.
    /// </summary>
    /// <param name="nalUnit">The decoded sequence-parameter-set NAL unit.</param>
    /// <exception cref="InvalidImageContentException">The sequence parameter set is malformed or outside the still-image profile.</exception>
    public HevcSequenceParameterSet(HevcNalUnit nalUnit)
    {
        const byte sequenceParameterSetNalUnitType = 33;
        if (nalUnit.Header.NalUnitType != sequenceParameterSetNalUnitType
            || nalUnit.Header.LayerId != 0
            || nalUnit.Header.TemporalId != 0)
        {
            throw new InvalidImageContentException("The HEVC sequence parameter set has an invalid NAL-unit header.");
        }

        HevcBitReader reader = new(nalUnit.Rbsp.Span);
        this.VideoParameterSetId = (byte)reader.ReadBits(4);
        int maxSubLayersMinusOne = (int)reader.ReadBits(3);
        if (maxSubLayersMinusOne > 6)
        {
            throw new InvalidImageContentException("The HEVC sequence parameter set declares too many temporal sublayers.");
        }

        this.MaxSubLayers = maxSubLayersMinusOne + 1;
        this.TemporalIdNestingFlag = reader.ReadFlag();
        if (maxSubLayersMinusOne == 0 && !this.TemporalIdNestingFlag)
        {
            throw new InvalidImageContentException("The HEVC sequence parameter set has invalid temporal nesting.");
        }

        this.ProfileTierLevel = new HevcProfileTierLevel(ref reader, maxSubLayersMinusOne);
        uint sequenceParameterSetId = reader.ReadUnsignedExpGolomb();
        if (sequenceParameterSetId > 15)
        {
            throw new InvalidImageContentException("The HEVC sequence parameter set identifier is invalid.");
        }

        this.Id = (byte)sequenceParameterSetId;
        uint chromaFormat = reader.ReadUnsignedExpGolomb();
        if (chromaFormat > 3)
        {
            throw new InvalidImageContentException("The HEVC sequence parameter set has an invalid chroma format.");
        }

        this.ChromaFormat = (byte)chromaFormat;
        this.SeparateColorPlaneFlag = this.ChromaFormat == 3 && reader.ReadFlag();

        uint width = reader.ReadUnsignedExpGolomb();
        uint height = reader.ReadUnsignedExpGolomb();
        if (width is 0 or > int.MaxValue || height is 0 or > int.MaxValue)
        {
            throw new InvalidImageContentException("The HEVC sequence parameter set has invalid coded dimensions.");
        }

        this.Width = (int)width;
        this.Height = (int)height;
        if (reader.ReadFlag())
        {
            int cropUnitWidth = HevcParameterSetSyntax.GetCropUnitWidth(this.ChromaFormat, this.SeparateColorPlaneFlag);
            int cropUnitHeight = HevcParameterSetSyntax.GetCropUnitHeight(this.ChromaFormat, this.SeparateColorPlaneFlag);
            this.ConformanceWindowLeftOffset = ReadScaledOffset(ref reader, cropUnitWidth);
            this.ConformanceWindowRightOffset = ReadScaledOffset(ref reader, cropUnitWidth);
            this.ConformanceWindowTopOffset = ReadScaledOffset(ref reader, cropUnitHeight);
            this.ConformanceWindowBottomOffset = ReadScaledOffset(ref reader, cropUnitHeight);
        }

        if ((long)this.ConformanceWindowLeftOffset + this.ConformanceWindowRightOffset >= this.Width
            || (long)this.ConformanceWindowTopOffset + this.ConformanceWindowBottomOffset >= this.Height)
        {
            throw new InvalidImageContentException("The HEVC sequence parameter set has an invalid conformance window.");
        }

        this.DisplayWidth = this.Width - this.ConformanceWindowLeftOffset - this.ConformanceWindowRightOffset;
        this.DisplayHeight = this.Height - this.ConformanceWindowTopOffset - this.ConformanceWindowBottomOffset;

        this.BitDepthLuma = ReadBitDepth(ref reader);
        this.BitDepthChroma = ReadBitDepth(ref reader);
        uint log2MaxPictureOrderCountLsbMinusFour = reader.ReadUnsignedExpGolomb();
        if (log2MaxPictureOrderCountLsbMinusFour > 12)
        {
            throw new InvalidImageContentException("The HEVC picture-order-count width is invalid.");
        }

        this.PictureOrderCountLsbBits = (int)log2MaxPictureOrderCountLsbMinusFour + 4;

        bool subLayerOrderingInfoPresent = reader.ReadFlag();
        int firstOrderingSubLayer = subLayerOrderingInfoPresent ? 0 : maxSubLayersMinusOne;
        for (int subLayer = firstOrderingSubLayer; subLayer <= maxSubLayersMinusOne; subLayer++)
        {
            uint maxDecodedPictureBufferingMinusOne = reader.ReadUnsignedExpGolomb();
            uint maxNumReorderPictures = reader.ReadUnsignedExpGolomb();
            reader.ReadUnsignedExpGolomb();
            if (maxNumReorderPictures > maxDecodedPictureBufferingMinusOne)
            {
                throw new InvalidImageContentException("The HEVC sequence parameter set has invalid sublayer ordering limits.");
            }
        }

        uint minCodingBlockLog2MinusThree = reader.ReadUnsignedExpGolomb();
        if (minCodingBlockLog2MinusThree > 3)
        {
            throw new InvalidImageContentException("The HEVC minimum coding-block size is invalid.");
        }

        this.MinCodingBlockLog2 = (int)minCodingBlockLog2MinusThree + 3;
        uint codingBlockSizeDifference = reader.ReadUnsignedExpGolomb();
        if (codingBlockSizeDifference > 6 - this.MinCodingBlockLog2)
        {
            throw new InvalidImageContentException("The HEVC coding-tree-block size is invalid.");
        }

        this.CodingTreeBlockLog2 = this.MinCodingBlockLog2 + (int)codingBlockSizeDifference;

        uint minTransformBlockLog2MinusTwo = reader.ReadUnsignedExpGolomb();
        if (minTransformBlockLog2MinusTwo > this.MinCodingBlockLog2 - 3)
        {
            throw new InvalidImageContentException("The HEVC minimum transform-block size is invalid.");
        }

        this.MinTransformBlockLog2 = (int)minTransformBlockLog2MinusTwo + 2;
        uint transformBlockSizeDifference = reader.ReadUnsignedExpGolomb();
        int maximumTransformBlockLog2 = Math.Min(5, this.CodingTreeBlockLog2);
        if (transformBlockSizeDifference > maximumTransformBlockLog2 - this.MinTransformBlockLog2)
        {
            throw new InvalidImageContentException("The HEVC maximum transform-block size is invalid.");
        }

        this.MaxTransformBlockLog2 = this.MinTransformBlockLog2 + (int)transformBlockSizeDifference;
        uint maxTransformHierarchyDepthInter = reader.ReadUnsignedExpGolomb();
        uint maxTransformHierarchyDepthIntra = reader.ReadUnsignedExpGolomb();
        uint maxHierarchyDepth = (uint)(this.CodingTreeBlockLog2 - this.MinTransformBlockLog2);
        if (maxTransformHierarchyDepthInter > maxHierarchyDepth || maxTransformHierarchyDepthIntra > maxHierarchyDepth)
        {
            throw new InvalidImageContentException("The HEVC transform hierarchy depth is invalid.");
        }

        this.MaxTransformHierarchyDepthInter = (int)maxTransformHierarchyDepthInter + 1;
        this.MaxTransformHierarchyDepthIntra = (int)maxTransformHierarchyDepthIntra + 1;

        this.ScalingListEnabled = reader.ReadFlag();
        this.ScalingList = new HevcScalingList();
        if (this.ScalingListEnabled && reader.ReadFlag())
        {
            this.ScalingList = HevcScalingList.Parse(ref reader);
        }

        this.AsymmetricMotionPartitionsEnabled = reader.ReadFlag();
        this.SampleAdaptiveOffsetEnabled = reader.ReadFlag();
        this.PcmEnabled = reader.ReadFlag();
        if (this.PcmEnabled)
        {
            this.PcmBitDepthLuma = (int)reader.ReadBits(4) + 1;
            this.PcmBitDepthChroma = (int)reader.ReadBits(4) + 1;
            if (this.PcmBitDepthLuma > this.BitDepthLuma || this.PcmBitDepthChroma > this.BitDepthChroma)
            {
                throw new InvalidImageContentException("The HEVC PCM bit depth exceeds the coded sample precision.");
            }

            uint minPcmCodingBlockLog2MinusThree = reader.ReadUnsignedExpGolomb();
            this.MinPcmCodingBlockLog2 = (int)minPcmCodingBlockLog2MinusThree + 3;
            int maximumPcmCodingBlockLog2 = Math.Min(this.CodingTreeBlockLog2, 5);
            if (this.MinPcmCodingBlockLog2 < Math.Min(this.MinCodingBlockLog2, 5)
                || this.MinPcmCodingBlockLog2 > maximumPcmCodingBlockLog2)
            {
                throw new InvalidImageContentException("The HEVC minimum PCM coding-block size is invalid.");
            }

            uint pcmCodingBlockSizeDifference = reader.ReadUnsignedExpGolomb();
            if (pcmCodingBlockSizeDifference > maximumPcmCodingBlockLog2 - this.MinPcmCodingBlockLog2)
            {
                throw new InvalidImageContentException("The HEVC maximum PCM coding-block size is invalid.");
            }

            this.MaxPcmCodingBlockLog2 = this.MinPcmCodingBlockLog2 + (int)pcmCodingBlockSizeDifference;
            this.PcmLoopFilterDisabled = reader.ReadFlag();
        }

        uint shortTermReferencePictureSetCount = reader.ReadUnsignedExpGolomb();
        if (shortTermReferencePictureSetCount > 64)
        {
            throw new InvalidImageContentException("The HEVC sequence parameter set declares too many short-term reference-picture sets.");
        }

        List<HevcShortTermReferencePictureSet> shortTermReferencePictureSets = new((int)shortTermReferencePictureSetCount);
        for (int referenceSet = 0; referenceSet < shortTermReferencePictureSetCount; referenceSet++)
        {
            shortTermReferencePictureSets.Add(
                HevcShortTermReferencePictureSet.Parse(ref reader, shortTermReferencePictureSets, referenceSet));
        }

        this.ShortTermReferencePictureSets = shortTermReferencePictureSets;

        if (reader.ReadFlag())
        {
            uint longTermReferencePictureCount = reader.ReadUnsignedExpGolomb();
            if (longTermReferencePictureCount > 32)
            {
                throw new InvalidImageContentException("The HEVC sequence parameter set declares too many long-term reference pictures.");
            }

            uint[] pictureOrderCounts = new uint[longTermReferencePictureCount];
            bool[] usedByCurrentPicture = new bool[longTermReferencePictureCount];
            for (int reference = 0; reference < pictureOrderCounts.Length; reference++)
            {
                pictureOrderCounts[reference] = reader.ReadBits(this.PictureOrderCountLsbBits);
                usedByCurrentPicture[reference] = reader.ReadFlag();
            }

            this.LongTermReferencePictureOrderCounts = pictureOrderCounts;
            this.LongTermReferencePicturesUsedByCurrent = usedByCurrentPicture;
        }
        else
        {
            this.LongTermReferencePictureOrderCounts = Array.Empty<uint>();
            this.LongTermReferencePicturesUsedByCurrent = Array.Empty<bool>();
        }

        this.TemporalMotionVectorPredictionEnabled = reader.ReadFlag();
        this.StrongIntraSmoothingEnabled = reader.ReadFlag();
        if (reader.ReadFlag())
        {
            this.VideoUsabilityInformation = new HevcVideoUsabilityInformation(
                ref reader,
                this.ChromaFormat,
                this.SeparateColorPlaneFlag,
                maxSubLayersMinusOne);
        }

        if (reader.ReadFlag())
        {
            Span<bool> extensionFlags = stackalloc bool[8];
            for (int extensionFlag = 0; extensionFlag < extensionFlags.Length; extensionFlag++)
            {
                extensionFlags[extensionFlag] = reader.ReadFlag();
            }

            if (extensionFlags[1])
            {
                throw new InvalidImageContentException("Layered HEVC sequence extensions are not supported for still-image items.");
            }

            if (extensionFlags[0])
            {
                this.TransformSkipRotationEnabled = reader.ReadFlag();
                this.TransformSkipContextEnabled = reader.ReadFlag();
                this.ImplicitResidualDpcmEnabled = reader.ReadFlag();
                this.ExplicitResidualDpcmEnabled = reader.ReadFlag();
                this.ExtendedPrecisionProcessingEnabled = reader.ReadFlag();
                this.IntraSmoothingDisabled = reader.ReadFlag();
                this.HighPrecisionOffsetsEnabled = reader.ReadFlag();
                this.PersistentRiceAdaptationEnabled = reader.ReadFlag();
                this.CabacBypassAlignmentEnabled = reader.ReadFlag();
            }

            bool unknownExtensionPresent = false;
            for (int extensionFlag = 2; extensionFlag < extensionFlags.Length; extensionFlag++)
            {
                unknownExtensionPresent |= extensionFlags[extensionFlag];
            }

            if (unknownExtensionPresent)
            {
                while (reader.HasMoreRbspData())
                {
                    reader.ReadFlag();
                }
            }
        }

        reader.ReadRbspTrailingBits();
    }

    /// <summary>Gets the referenced video-parameter-set identifier.</summary>
    public byte VideoParameterSetId { get; }

    /// <summary>Gets the sequence-parameter-set identifier.</summary>
    public byte Id { get; }

    /// <summary>Gets the declared number of temporal sublayers.</summary>
    public int MaxSubLayers { get; }

    /// <summary>Gets a value indicating whether temporal identifiers are nested.</summary>
    public bool TemporalIdNestingFlag { get; }

    /// <summary>Gets the general profile, tier, constraint, and level description.</summary>
    public HevcProfileTierLevel ProfileTierLevel { get; }

    /// <summary>Gets the coded chroma format, from monochrome through YUV 4:4:4.</summary>
    public byte ChromaFormat { get; }

    /// <summary>Gets a value indicating whether 4:4:4 components are coded as separate color planes.</summary>
    public bool SeparateColorPlaneFlag { get; }

    /// <summary>Gets the coded luma width before conformance cropping.</summary>
    public int Width { get; }

    /// <summary>Gets the coded luma height before conformance cropping.</summary>
    public int Height { get; }

    /// <summary>Gets the displayed width after conformance cropping.</summary>
    public int DisplayWidth { get; }

    /// <summary>Gets the displayed height after conformance cropping.</summary>
    public int DisplayHeight { get; }

    /// <summary>Gets the conformance-window left offset in luma samples.</summary>
    public int ConformanceWindowLeftOffset { get; }

    /// <summary>Gets the conformance-window right offset in luma samples.</summary>
    public int ConformanceWindowRightOffset { get; }

    /// <summary>Gets the conformance-window top offset in luma samples.</summary>
    public int ConformanceWindowTopOffset { get; }

    /// <summary>Gets the conformance-window bottom offset in luma samples.</summary>
    public int ConformanceWindowBottomOffset { get; }

    /// <summary>Gets the luma sample precision in bits.</summary>
    public int BitDepthLuma { get; }

    /// <summary>Gets the chroma sample precision in bits.</summary>
    public int BitDepthChroma { get; }

    /// <summary>Gets the coded picture-order-count least-significant-bit width.</summary>
    public int PictureOrderCountLsbBits { get; }

    /// <summary>Gets the base-two logarithm of the minimum luma coding-block size.</summary>
    public int MinCodingBlockLog2 { get; }

    /// <summary>Gets the base-two logarithm of the coding-tree-block size.</summary>
    public int CodingTreeBlockLog2 { get; }

    /// <summary>Gets the base-two logarithm of the minimum luma transform-block size.</summary>
    public int MinTransformBlockLog2 { get; }

    /// <summary>Gets the base-two logarithm of the maximum luma transform-block size.</summary>
    public int MaxTransformBlockLog2 { get; }

    /// <summary>Gets the maximum inter-predicted transform hierarchy depth.</summary>
    public int MaxTransformHierarchyDepthInter { get; }

    /// <summary>Gets the maximum intra-predicted transform hierarchy depth.</summary>
    public int MaxTransformHierarchyDepthIntra { get; }

    /// <summary>Gets a value indicating whether scaling lists affect inverse quantization.</summary>
    public bool ScalingListEnabled { get; }

    /// <summary>Gets the effective quantization scaling matrices.</summary>
    public HevcScalingList ScalingList { get; }

    /// <summary>Gets a value indicating whether asymmetric motion partitions are enabled.</summary>
    public bool AsymmetricMotionPartitionsEnabled { get; }

    /// <summary>Gets a value indicating whether sample-adaptive offset filtering is enabled.</summary>
    public bool SampleAdaptiveOffsetEnabled { get; }

    /// <summary>Gets a value indicating whether pulse-code-modulated coding blocks are enabled.</summary>
    public bool PcmEnabled { get; }

    /// <summary>Gets the PCM luma sample precision in bits.</summary>
    public int PcmBitDepthLuma { get; }

    /// <summary>Gets the PCM chroma sample precision in bits.</summary>
    public int PcmBitDepthChroma { get; }

    /// <summary>Gets the base-two logarithm of the minimum PCM coding-block size.</summary>
    public int MinPcmCodingBlockLog2 { get; }

    /// <summary>Gets the base-two logarithm of the maximum PCM coding-block size.</summary>
    public int MaxPcmCodingBlockLog2 { get; }

    /// <summary>Gets a value indicating whether in-loop filtering is disabled for PCM blocks.</summary>
    public bool PcmLoopFilterDisabled { get; }

    /// <summary>Gets the SPS short-term reference-picture sets.</summary>
    public IReadOnlyList<HevcShortTermReferencePictureSet> ShortTermReferencePictureSets { get; }

    /// <summary>Gets the long-term reference picture-order-count values.</summary>
    public IReadOnlyList<uint> LongTermReferencePictureOrderCounts { get; }

    /// <summary>Gets the long-term reference-picture current-usage flags.</summary>
    public IReadOnlyList<bool> LongTermReferencePicturesUsedByCurrent { get; }

    /// <summary>Gets a value indicating whether temporal motion-vector prediction is enabled.</summary>
    public bool TemporalMotionVectorPredictionEnabled { get; }

    /// <summary>Gets a value indicating whether strong intra smoothing is enabled.</summary>
    public bool StrongIntraSmoothingEnabled { get; }

    /// <summary>Gets the optional still-image VUI presentation description.</summary>
    public HevcVideoUsabilityInformation? VideoUsabilityInformation { get; }

    /// <summary>Gets a value indicating whether transform-skip coefficient rotation is enabled.</summary>
    public bool TransformSkipRotationEnabled { get; }

    /// <summary>Gets a value indicating whether transform-skip-specific entropy contexts are enabled.</summary>
    public bool TransformSkipContextEnabled { get; }

    /// <summary>Gets a value indicating whether implicit residual DPCM is enabled.</summary>
    public bool ImplicitResidualDpcmEnabled { get; }

    /// <summary>Gets a value indicating whether explicit residual DPCM is enabled.</summary>
    public bool ExplicitResidualDpcmEnabled { get; }

    /// <summary>Gets a value indicating whether extended-precision processing is enabled.</summary>
    public bool ExtendedPrecisionProcessingEnabled { get; }

    /// <summary>Gets a value indicating whether intra smoothing is disabled.</summary>
    public bool IntraSmoothingDisabled { get; }

    /// <summary>Gets a value indicating whether high-precision prediction offsets are enabled.</summary>
    public bool HighPrecisionOffsetsEnabled { get; }

    /// <summary>Gets a value indicating whether persistent Rice adaptation is enabled.</summary>
    public bool PersistentRiceAdaptationEnabled { get; }

    /// <summary>Gets a value indicating whether CABAC bypass alignment is enabled.</summary>
    public bool CabacBypassAlignmentEnabled { get; }

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
            throw new InvalidImageContentException("The HEVC conformance-window offset is too large.");
        }

        return (int)offset * unit;
    }

    /// <summary>
    /// Reads and validates a coded HEVC sample precision.
    /// </summary>
    /// <param name="reader">The sequence-parameter-set raw byte sequence payload reader.</param>
    /// <returns>The sample precision in bits.</returns>
    /// <exception cref="InvalidImageContentException">The declared precision exceeds 16 bits.</exception>
    private static int ReadBitDepth(ref HevcBitReader reader)
    {
        uint bitDepthMinusEight = reader.ReadUnsignedExpGolomb();
        if (bitDepthMinusEight > 8)
        {
            throw new InvalidImageContentException("The HEVC sample bit depth is invalid.");
        }

        return (int)bitDepthMinusEight + 8;
    }
}
