// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Contains the HEVC picture fields required to decode the independently coded picture in one still-image item.
/// </summary>
internal sealed class HevcPictureParameterSet
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HevcPictureParameterSet"/> class.
    /// </summary>
    /// <param name="nalUnit">The decoded picture-parameter-set NAL unit.</param>
    /// <param name="sequenceParameterSets">The sequence parameter sets available to the coded image item.</param>
    /// <exception cref="InvalidImageContentException">
    /// The picture parameter set is malformed, references an unavailable sequence parameter set, or declares
    /// picture geometry outside that sequence parameter set.
    /// </exception>
    public HevcPictureParameterSet(
        HevcNalUnit nalUnit,
        IReadOnlyList<HevcSequenceParameterSet> sequenceParameterSets)
    {
        const byte pictureParameterSetNalUnitType = 34;
        if (nalUnit.Header.NalUnitType != pictureParameterSetNalUnitType
            || nalUnit.Header.LayerId != 0
            || nalUnit.Header.TemporalId != 0)
        {
            throw new InvalidImageContentException("The HEVC picture parameter set has an invalid NAL-unit header.");
        }

        HevcBitReader reader = new(nalUnit.Rbsp.Span);
        uint pictureParameterSetId = reader.ReadUnsignedExpGolomb();
        uint sequenceParameterSetId = reader.ReadUnsignedExpGolomb();
        if (pictureParameterSetId > 63 || sequenceParameterSetId > 15)
        {
            throw new InvalidImageContentException("The HEVC picture parameter set has an invalid identifier.");
        }

        this.Id = (byte)pictureParameterSetId;
        this.SequenceParameterSetId = (byte)sequenceParameterSetId;

        HevcSequenceParameterSet? sequenceParameterSet = null;
        foreach (HevcSequenceParameterSet candidate in sequenceParameterSets)
        {
            if (candidate.Id == this.SequenceParameterSetId)
            {
                sequenceParameterSet = candidate;
                break;
            }
        }

        if (sequenceParameterSet is null)
        {
            throw new InvalidImageContentException("The HEVC picture parameter set references an unavailable sequence parameter set.");
        }

        this.SequenceParameterSet = sequenceParameterSet;
        this.DependentSliceSegmentsEnabled = reader.ReadFlag();
        this.OutputFlagPresent = reader.ReadFlag();
        this.ExtraSliceHeaderBitCount = (int)reader.ReadBits(3);
        this.SignDataHidingEnabled = reader.ReadFlag();
        this.CabacInitializationPresent = reader.ReadFlag();

        uint defaultReferenceIndexCountList0MinusOne = reader.ReadUnsignedExpGolomb();
        uint defaultReferenceIndexCountList1MinusOne = reader.ReadUnsignedExpGolomb();
        if (defaultReferenceIndexCountList0MinusOne > 14 || defaultReferenceIndexCountList1MinusOne > 14)
        {
            throw new InvalidImageContentException("The HEVC picture parameter set declares too many default reference indices.");
        }

        this.DefaultReferenceIndexCountList0 = (int)defaultReferenceIndexCountList0MinusOne + 1;
        this.DefaultReferenceIndexCountList1 = (int)defaultReferenceIndexCountList1MinusOne + 1;

        this.InitialQuantizationParameterMinus26 = reader.ReadSignedExpGolomb();
        int minimumInitialQuantizationParameter = -26 - (6 * (sequenceParameterSet.BitDepthLuma - 8));
        if (this.InitialQuantizationParameterMinus26 < minimumInitialQuantizationParameter
            || this.InitialQuantizationParameterMinus26 > 25)
        {
            throw new InvalidImageContentException("The HEVC picture parameter set has an invalid initial quantization parameter.");
        }

        this.ConstrainedIntraPredictionEnabled = reader.ReadFlag();
        this.TransformSkipEnabled = reader.ReadFlag();
        this.CodingUnitQuantizationParameterDeltaEnabled = reader.ReadFlag();
        if (this.CodingUnitQuantizationParameterDeltaEnabled)
        {
            uint quantizationParameterDeltaDepth = reader.ReadUnsignedExpGolomb();
            int maximumDepth = sequenceParameterSet.CodingTreeBlockLog2 - sequenceParameterSet.MinCodingBlockLog2;
            if (quantizationParameterDeltaDepth > maximumDepth)
            {
                throw new InvalidImageContentException("The HEVC picture parameter set has an invalid quantization-parameter delta depth.");
            }

            this.QuantizationParameterDeltaDepth = (int)quantizationParameterDeltaDepth;
        }

        this.ChromaCbQuantizationParameterOffset = HevcParameterSetSyntax.ReadQuantizationParameterOffset(ref reader);
        this.ChromaCrQuantizationParameterOffset = HevcParameterSetSyntax.ReadQuantizationParameterOffset(ref reader);
        this.SliceChromaQuantizationParameterOffsetsPresent = reader.ReadFlag();
        this.WeightedPredictionEnabled = reader.ReadFlag();
        this.WeightedBiPredictionEnabled = reader.ReadFlag();
        this.TransquantizationBypassEnabled = reader.ReadFlag();
        this.TilesEnabled = reader.ReadFlag();
        this.EntropyCodingSynchronizationEnabled = reader.ReadFlag();

        int codingTreeBlockColumns = HevcParameterSetSyntax.GetCodingTreeBlockCount(
            sequenceParameterSet.Width,
            sequenceParameterSet.CodingTreeBlockLog2);

        int codingTreeBlockRows = HevcParameterSetSyntax.GetCodingTreeBlockCount(
            sequenceParameterSet.Height,
            sequenceParameterSet.CodingTreeBlockLog2);

        if (this.TilesEnabled)
        {
            uint tileColumnCountMinusOne = reader.ReadUnsignedExpGolomb();
            uint tileRowCountMinusOne = reader.ReadUnsignedExpGolomb();
            if (tileColumnCountMinusOne >= codingTreeBlockColumns
                || tileRowCountMinusOne >= codingTreeBlockRows
                || (tileColumnCountMinusOne == 0 && tileRowCountMinusOne == 0))
            {
                throw new InvalidImageContentException("The HEVC picture parameter set has an invalid tile grid.");
            }

            int tileColumnCount = (int)tileColumnCountMinusOne + 1;
            int tileRowCount = (int)tileRowCountMinusOne + 1;
            this.UniformTileSpacing = reader.ReadFlag();
            this.TileColumnWidths = ReadTileDimensions(
                ref reader,
                codingTreeBlockColumns,
                tileColumnCount,
                this.UniformTileSpacing);

            this.TileRowHeights = ReadTileDimensions(
                ref reader,
                codingTreeBlockRows,
                tileRowCount,
                this.UniformTileSpacing);

            this.LoopFilterAcrossTilesEnabled = reader.ReadFlag();
        }
        else
        {
            // A picture without tile syntax is one tile spanning the coded CTB grid. Materializing that inferred
            // layout lets slice addressing use the same bounded arrays for tiled and untiled image items.
            this.UniformTileSpacing = true;
            this.TileColumnWidths = [codingTreeBlockColumns];
            this.TileRowHeights = [codingTreeBlockRows];
            this.LoopFilterAcrossTilesEnabled = true;
        }

        this.LoopFilterAcrossSlicesEnabled = reader.ReadFlag();
        this.DeblockingFilterControlPresent = reader.ReadFlag();
        if (this.DeblockingFilterControlPresent)
        {
            this.DeblockingFilterOverrideEnabled = reader.ReadFlag();
            this.DeblockingFilterDisabled = reader.ReadFlag();
            if (!this.DeblockingFilterDisabled)
            {
                this.DeblockingFilterBetaOffsetDiv2 = HevcParameterSetSyntax.ReadDeblockingFilterOffset(ref reader);
                this.DeblockingFilterTcOffsetDiv2 = HevcParameterSetSyntax.ReadDeblockingFilterOffset(ref reader);
            }
        }

        this.ScalingListDataPresent = reader.ReadFlag();
        if (this.ScalingListDataPresent && !sequenceParameterSet.ScalingListEnabled)
        {
            throw new InvalidImageContentException("The HEVC picture parameter set declares scaling data disabled by its sequence parameter set.");
        }

        this.ScalingList = this.ScalingListDataPresent
            ? HevcScalingList.Parse(ref reader)
            : sequenceParameterSet.ScalingList;

        this.ReferenceListModificationPresent = reader.ReadFlag();
        uint parallelMergeLevelMinusTwo = reader.ReadUnsignedExpGolomb();
        if (parallelMergeLevelMinusTwo > sequenceParameterSet.CodingTreeBlockLog2 - 2)
        {
            throw new InvalidImageContentException("The HEVC picture parameter set has an invalid parallel merge level.");
        }

        this.ParallelMergeLevelLog2 = (int)parallelMergeLevelMinusTwo + 2;
        this.SliceSegmentHeaderExtensionPresent = reader.ReadFlag();

        this.MaxTransformSkipBlockLog2 = 2;
        if (reader.ReadFlag())
        {
            Span<bool> extensionFlags = stackalloc bool[8];
            for (int extensionFlag = 0; extensionFlag < extensionFlags.Length; extensionFlag++)
            {
                extensionFlags[extensionFlag] = reader.ReadFlag();
            }

            if (extensionFlags[1])
            {
                throw new InvalidImageContentException("Layered HEVC picture extensions are not supported for still-image items.");
            }

            if (extensionFlags[0])
            {
                this.ReadRangeExtension(ref reader);
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

    /// <summary>Gets the picture-parameter-set identifier.</summary>
    public byte Id { get; }

    /// <summary>Gets the referenced sequence-parameter-set identifier.</summary>
    public byte SequenceParameterSetId { get; }

    /// <summary>Gets the sequence parameters governing this picture parameter set.</summary>
    public HevcSequenceParameterSet SequenceParameterSet { get; }

    /// <summary>Gets a value indicating whether dependent slice segments can occur.</summary>
    public bool DependentSliceSegmentsEnabled { get; }

    /// <summary>Gets a value indicating whether slice headers contain the picture-output flag.</summary>
    public bool OutputFlagPresent { get; }

    /// <summary>Gets the number of reserved extra bits at the start of each independent slice header.</summary>
    public int ExtraSliceHeaderBitCount { get; }

    /// <summary>Gets a value indicating whether transform-coefficient sign hiding is enabled.</summary>
    public bool SignDataHidingEnabled { get; }

    /// <summary>Gets a value indicating whether slices can select an alternate CABAC initialization table.</summary>
    public bool CabacInitializationPresent { get; }

    /// <summary>Gets the default active reference-index count for reference list zero.</summary>
    public int DefaultReferenceIndexCountList0 { get; }

    /// <summary>Gets the default active reference-index count for reference list one.</summary>
    public int DefaultReferenceIndexCountList1 { get; }

    /// <summary>Gets the picture quantization-parameter initializer relative to 26.</summary>
    public int InitialQuantizationParameterMinus26 { get; }

    /// <summary>Gets a value indicating whether inter-coded neighbors are excluded from intra prediction.</summary>
    public bool ConstrainedIntraPredictionEnabled { get; }

    /// <summary>Gets a value indicating whether residual transform skipping can be selected.</summary>
    public bool TransformSkipEnabled { get; }

    /// <summary>Gets a value indicating whether coding units can change the quantization parameter.</summary>
    public bool CodingUnitQuantizationParameterDeltaEnabled { get; }

    /// <summary>Gets the coding-tree depth at which quantization-parameter deltas are signaled.</summary>
    public int QuantizationParameterDeltaDepth { get; }

    /// <summary>Gets the picture-level Cb quantization-parameter offset.</summary>
    public int ChromaCbQuantizationParameterOffset { get; }

    /// <summary>Gets the picture-level Cr quantization-parameter offset.</summary>
    public int ChromaCrQuantizationParameterOffset { get; }

    /// <summary>Gets a value indicating whether slices can add Cb and Cr quantization-parameter offsets.</summary>
    public bool SliceChromaQuantizationParameterOffsetsPresent { get; }

    /// <summary>Gets a value indicating whether weighted prediction can be used by predictive slices.</summary>
    public bool WeightedPredictionEnabled { get; }

    /// <summary>Gets a value indicating whether weighted prediction can be used by bidirectional slices.</summary>
    public bool WeightedBiPredictionEnabled { get; }

    /// <summary>Gets a value indicating whether coding units can bypass transform and quantization.</summary>
    public bool TransquantizationBypassEnabled { get; }

    /// <summary>Gets a value indicating whether the coded picture is partitioned into tiles.</summary>
    public bool TilesEnabled { get; }

    /// <summary>Gets a value indicating whether wavefront entropy-coding synchronization is enabled.</summary>
    public bool EntropyCodingSynchronizationEnabled { get; }

    /// <summary>Gets a value indicating whether the tile grid uses uniform proportional spacing.</summary>
    public bool UniformTileSpacing { get; }

    /// <summary>Gets the tile-column widths in coding-tree blocks.</summary>
    public IReadOnlyList<int> TileColumnWidths { get; }

    /// <summary>Gets the tile-row heights in coding-tree blocks.</summary>
    public IReadOnlyList<int> TileRowHeights { get; }

    /// <summary>Gets a value indicating whether in-loop filtering crosses tile boundaries.</summary>
    public bool LoopFilterAcrossTilesEnabled { get; }

    /// <summary>Gets a value indicating whether in-loop filtering crosses slice boundaries.</summary>
    public bool LoopFilterAcrossSlicesEnabled { get; }

    /// <summary>Gets a value indicating whether picture or slice syntax controls deblocking.</summary>
    public bool DeblockingFilterControlPresent { get; }

    /// <summary>Gets a value indicating whether slice headers can override picture-level deblocking.</summary>
    public bool DeblockingFilterOverrideEnabled { get; }

    /// <summary>Gets a value indicating whether deblocking is disabled by default for the picture.</summary>
    public bool DeblockingFilterDisabled { get; }

    /// <summary>Gets half the picture-level deblocking beta-threshold offset.</summary>
    public int DeblockingFilterBetaOffsetDiv2 { get; }

    /// <summary>Gets half the picture-level deblocking clipping-threshold offset.</summary>
    public int DeblockingFilterTcOffsetDiv2 { get; }

    /// <summary>Gets a value indicating whether this picture parameter set supplies scaling-list data.</summary>
    public bool ScalingListDataPresent { get; }

    /// <summary>Gets the effective quantization scaling matrices for slices using this picture parameter set.</summary>
    public HevcScalingList ScalingList { get; }

    /// <summary>Gets a value indicating whether slice headers can modify the initial reference-picture lists.</summary>
    public bool ReferenceListModificationPresent { get; }

    /// <summary>Gets the base-two logarithm of the parallel merge-estimation region width and height.</summary>
    public int ParallelMergeLevelLog2 { get; }

    /// <summary>Gets a value indicating whether slice-segment headers carry extension bytes.</summary>
    public bool SliceSegmentHeaderExtensionPresent { get; }

    /// <summary>Gets the base-two logarithm of the maximum transform-skip block width and height.</summary>
    public int MaxTransformSkipBlockLog2 { get; private set; }

    /// <summary>Gets a value indicating whether cross-component residual prediction is enabled.</summary>
    public bool CrossComponentPredictionEnabled { get; private set; }

    /// <summary>Gets the coding-tree depth at which chroma quantization-offset indices are signaled.</summary>
    public int ChromaQuantizationParameterOffsetDepth { get; private set; }

    /// <summary>Gets the Cb offsets in the selectable chroma quantization-parameter offset list.</summary>
    public IReadOnlyList<int> ChromaQuantizationParameterOffsetsCb { get; private set; } = Array.Empty<int>();

    /// <summary>Gets the Cr offsets in the selectable chroma quantization-parameter offset list.</summary>
    public IReadOnlyList<int> ChromaQuantizationParameterOffsetsCr { get; private set; } = Array.Empty<int>();

    /// <summary>Gets the base-two logarithm of the luma sample-adaptive-offset value scale.</summary>
    public int SampleAdaptiveOffsetScaleLumaLog2 { get; private set; }

    /// <summary>Gets the base-two logarithm of the chroma sample-adaptive-offset value scale.</summary>
    public int SampleAdaptiveOffsetScaleChromaLog2 { get; private set; }

    /// <summary>
    /// Reads the Range Extensions fields that change transform, chroma quantization, and SAO reconstruction.
    /// </summary>
    /// <param name="reader">The picture-parameter-set raw byte sequence payload reader.</param>
    /// <exception cref="InvalidImageContentException">
    /// A transform, coding-tree-depth, chroma offset, or sample-adaptive-offset scale is outside the governing
    /// sequence-parameter-set bounds.
    /// </exception>
    private void ReadRangeExtension(ref HevcBitReader reader)
    {
        if (this.TransformSkipEnabled)
        {
            uint maxTransformSkipBlockLog2MinusTwo = reader.ReadUnsignedExpGolomb();
            if (maxTransformSkipBlockLog2MinusTwo > this.SequenceParameterSet.MaxTransformBlockLog2 - 2)
            {
                throw new InvalidImageContentException("The HEVC picture parameter set has an invalid transform-skip block size.");
            }

            this.MaxTransformSkipBlockLog2 = (int)maxTransformSkipBlockLog2MinusTwo + 2;
        }

        this.CrossComponentPredictionEnabled = reader.ReadFlag();
        if (reader.ReadFlag())
        {
            uint chromaOffsetDepth = reader.ReadUnsignedExpGolomb();
            int maximumDepth = this.SequenceParameterSet.CodingTreeBlockLog2 - this.SequenceParameterSet.MinCodingBlockLog2;
            if (chromaOffsetDepth > maximumDepth)
            {
                throw new InvalidImageContentException("The HEVC picture parameter set has an invalid chroma quantization-offset depth.");
            }

            this.ChromaQuantizationParameterOffsetDepth = (int)chromaOffsetDepth;

            uint chromaOffsetCountMinusOne = reader.ReadUnsignedExpGolomb();
            if (chromaOffsetCountMinusOne > 5)
            {
                throw new InvalidImageContentException("The HEVC picture parameter set declares too many chroma quantization offsets.");
            }

            int chromaOffsetCount = (int)chromaOffsetCountMinusOne + 1;
            int[] cbOffsets = new int[chromaOffsetCount];
            int[] crOffsets = new int[chromaOffsetCount];
            for (int offset = 0; offset < chromaOffsetCount; offset++)
            {
                cbOffsets[offset] = HevcParameterSetSyntax.ReadQuantizationParameterOffset(ref reader);
                crOffsets[offset] = HevcParameterSetSyntax.ReadQuantizationParameterOffset(ref reader);
            }

            this.ChromaQuantizationParameterOffsetsCb = cbOffsets;
            this.ChromaQuantizationParameterOffsetsCr = crOffsets;
        }

        uint lumaScale = reader.ReadUnsignedExpGolomb();
        uint chromaScale = reader.ReadUnsignedExpGolomb();
        int maximumLumaScale = Math.Max(this.SequenceParameterSet.BitDepthLuma, 10) - 10;
        int maximumChromaScale = Math.Max(this.SequenceParameterSet.BitDepthChroma, 10) - 10;
        if (lumaScale > maximumLumaScale || chromaScale > maximumChromaScale)
        {
            throw new InvalidImageContentException("The HEVC picture parameter set has an invalid sample-adaptive-offset scale.");
        }

        this.SampleAdaptiveOffsetScaleLumaLog2 = (int)lumaScale;
        this.SampleAdaptiveOffsetScaleChromaLog2 = (int)chromaScale;
    }

    /// <summary>
    /// Reads or derives one axis of the tile grid.
    /// </summary>
    /// <param name="reader">The picture-parameter-set raw byte sequence payload reader.</param>
    /// <param name="codingTreeBlockCount">The complete picture dimension in coding-tree blocks.</param>
    /// <param name="tileCount">The tile count on the same axis.</param>
    /// <param name="uniformSpacing">Whether the widths or heights use proportional uniform spacing.</param>
    /// <returns>Every tile width or height in coding-tree blocks, including the inferred final tile.</returns>
    /// <exception cref="InvalidImageContentException">An explicit tile consumes the final block required by a later tile.</exception>
    private static int[] ReadTileDimensions(
        ref HevcBitReader reader,
        int codingTreeBlockCount,
        int tileCount,
        bool uniformSpacing)
    {
        int[] dimensions = new int[tileCount];
        if (uniformSpacing)
        {
            for (int tile = 0; tile < tileCount; tile++)
            {
                // The normative floor-difference formula assigns every CTB exactly once even when the picture
                // dimension is not divisible by the number of tiles.
                dimensions[tile] = (((tile + 1) * codingTreeBlockCount) / tileCount)
                    - ((tile * codingTreeBlockCount) / tileCount);
            }

            return dimensions;
        }

        int consumed = 0;
        for (int tile = 0; tile < tileCount - 1; tile++)
        {
            uint dimensionMinusOne = reader.ReadUnsignedExpGolomb();
            if (dimensionMinusOne >= codingTreeBlockCount - consumed - 1)
            {
                throw new InvalidImageContentException("The HEVC picture parameter set has an invalid explicit tile dimension.");
            }

            dimensions[tile] = (int)dimensionMinusOne + 1;
            consumed += dimensions[tile];
        }

        dimensions[^1] = codingTreeBlockCount - consumed;
        return dimensions;
    }
}
