// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Identifies the inter-prediction features selected by decoded AV1 block syntax.
/// </summary>
[Flags]
internal enum Av1InterPredictionFeatures
{
    /// <summary>
    /// No tracked inter-prediction feature was selected.
    /// </summary>
    None = 0,

    /// <summary>
    /// Distance-weighted compound prediction was selected.
    /// </summary>
    DistanceWeightedCompound = 1 << 0,

    /// <summary>
    /// A non-inverted wedge compound mask was selected.
    /// </summary>
    WedgeCompound = 1 << 1,

    /// <summary>
    /// An inverted wedge compound mask was selected.
    /// </summary>
    InvertedWedgeCompound = 1 << 2,

    /// <summary>
    /// The first difference-weighted compound mask orientation was selected.
    /// </summary>
    DifferenceWeightedCompound = 1 << 3,

    /// <summary>
    /// The inverted difference-weighted compound mask orientation was selected.
    /// </summary>
    InvertedDifferenceWeightedCompound = 1 << 4,

    /// <summary>
    /// Smooth inter-intra prediction was selected.
    /// </summary>
    SmoothInterIntra = 1 << 5,

    /// <summary>
    /// Wedge inter-intra prediction was selected.
    /// </summary>
    WedgeInterIntra = 1 << 6,

    /// <summary>
    /// Overlapping motion compensation was selected.
    /// </summary>
    Obmc = 1 << 7,

    /// <summary>
    /// Local warped-motion prediction was selected.
    /// </summary>
    LocalWarp = 1 << 8,

    /// <summary>
    /// Non-translational global warped-motion prediction was selected.
    /// </summary>
    GlobalWarp = 1 << 9
}

/// <summary>
/// Owns the mode, motion, segmentation, transform, coefficient, quantizer, and filter state decoded for one AV1 frame.
/// </summary>
internal sealed partial class Av1FrameInfo : IDisposable
{
    /// <summary>
    /// The allocator that owns frame-sized syntax and retained-reference state.
    /// </summary>
    private readonly MemoryAllocator memoryAllocator;

    /// <summary>
    /// The raster coefficient slots reserved for one 4x4 mode-information unit.
    /// </summary>
    public const int CoefficientCountPerModeInfo = 16;

    /// <summary>
    /// Owns the luma and chroma coefficient scratch for the superblock currently being decoded.
    /// </summary>
    private readonly IMemoryOwner<int> coefficientScratch;

    /// <summary>
    /// The number of luma coefficient entries at the start of <see cref="coefficientScratch"/>.
    /// </summary>
    private readonly int lumaCoefficientCount;

    /// <summary>
    /// The number of coefficient entries reserved for each chroma plane.
    /// </summary>
    private readonly int chromaCoefficientCount;

    /// <summary>
    /// The width and height of a superblock in 4x4 mode-information units.
    /// </summary>
    private readonly int modeInfoSizePerSuperblock;

    /// <summary>
    /// The number of 4x4 mode-information positions in one square superblock.
    /// </summary>
    private readonly int modeInfoCountPerSuperblock;

    /// <summary>
    /// The number of columns in the frame superblock grid.
    /// </summary>
    private readonly int superblockColumnCount;

    /// <summary>
    /// The number of rows in the frame superblock grid.
    /// </summary>
    private readonly int superblockRowCount;

    /// <summary>
    /// The base-2 reduction from luma coefficient capacity to per-chroma-plane capacity.
    /// </summary>
    private readonly int subsamplingFactor;

    /// <summary>
    /// Stores decoded block mode information in bitstream traversal order.
    /// </summary>
    private readonly MemoryGroup<Av1BlockModeInfo> modeInfos;

    /// <summary>
    /// Stores the number of mode-information records written to each superblock row in <see cref="modeInfos"/>.
    /// </summary>
    private readonly Buffer2D<int> modeInfoCounts;

    /// <summary>
    /// Maps every frame-relative 4x4 position to its covering entry in <see cref="modeInfos"/>.
    /// </summary>
    private readonly Av1FrameModeInfoMap modeInfoMap;

    /// <summary>
    /// Stores the decoded segment identifier for each active 4x4 mode-information position in row-major order.
    /// </summary>
    private Buffer2D<byte>? segmentIds;

    /// <summary>
    /// The number of active 4x4 columns in one row of <see cref="segmentIds"/>.
    /// </summary>
    private int segmentIdColumnCount;

    /// <summary>
    /// The number of active 4x4 rows represented by <see cref="segmentIds"/>.
    /// </summary>
    private int segmentIdRowCount;

    /// <summary>
    /// Owns the luma and shared-chroma transform-information scratch for the superblock currently being decoded.
    /// </summary>
    private readonly IMemoryOwner<Av1TransformInfo> transformInfoScratch;

    /// <summary>
    /// Stores the active base quantizer index for each frame superblock.
    /// </summary>
    private readonly Buffer2D<int> quantizerIndices;

    /// <summary>
    /// The base-2 number of constrained directional enhancement filter entries allocated per superblock.
    /// </summary>
    private readonly int cdefStrengthFactorLog2;

    /// <summary>
    /// Stores constrained directional enhancement filter strengths grouped by superblock.
    /// </summary>
    private readonly Buffer2D<int> cdefStrength;

    /// <summary>
    /// The base-2 number of loop-filter delta values stored per superblock.
    /// </summary>
    private readonly int deltaLoopFactorLog2 = 2;

    /// <summary>
    /// Stores the four loop-filter delta values for each superblock.
    /// </summary>
    private readonly Buffer2D<int> deltaLoopFilter;

    /// <summary>
    /// Stores raster-ordered loop-restoration units for each color plane.
    /// </summary>
    private InlineArray4<Buffer2D<Av1LoopRestorationUnit>?> loopRestorationUnits;

    /// <summary>
    /// Stores the number of loop-restoration unit columns for each color plane.
    /// </summary>
    private InlineArray4<int> loopRestorationUnitColumns;

    /// <summary>
    /// The number of loop-restoration unit rows allocated for each color plane.
    /// </summary>
    private InlineArray4<int> loopRestorationUnitRows;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1FrameInfo"/> class using sequence-maximum dimensions.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header defining maximum dimensions, superblock size, and color sampling.</param>
    public Av1FrameInfo(ObuSequenceHeader sequenceHeader)
        : this(Configuration.Default, sequenceHeader, sequenceHeader.MaxFrameWidth, sequenceHeader.MaxFrameHeight)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1FrameInfo"/> class for one active coded frame.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header defining superblock size and color sampling.</param>
    /// <param name="frameHeader">The frame header defining the active coded dimensions.</param>
    public Av1FrameInfo(ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
        : this(
            Configuration.Default,
            sequenceHeader,
            frameHeader.FrameSize.FrameWidth,
            frameHeader.FrameSize.FrameHeight)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1FrameInfo"/> class for one active coded frame.
    /// </summary>
    /// <param name="configuration">The decoder configuration providing frame-sized storage.</param>
    /// <param name="sequenceHeader">The sequence header defining superblock size and color sampling.</param>
    /// <param name="frameHeader">The frame header defining the active coded dimensions.</param>
    public Av1FrameInfo(Configuration configuration, ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
        : this(
            configuration,
            sequenceHeader,
            frameHeader.FrameSize.FrameWidth,
            frameHeader.FrameSize.FrameHeight)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1FrameInfo"/> class for explicit coded dimensions.
    /// </summary>
    /// <param name="configuration">The decoder configuration providing frame-sized storage.</param>
    /// <param name="sequenceHeader">The sequence header defining superblock size and color sampling.</param>
    /// <param name="frameWidth">The active coded frame width.</param>
    /// <param name="frameHeight">The active coded frame height.</param>
    private Av1FrameInfo(Configuration configuration, ObuSequenceHeader sequenceHeader, int frameWidth, int frameHeight)
    {
        // A FrameInfo instance belongs to one coded frame, so transient syntax storage follows that frame rather
        // than the potentially much larger sequence maximum declared by an untrusted stream.
        this.memoryAllocator = configuration.MemoryAllocator;
        int superblockSizeLog2 = sequenceHeader.SuperblockSizeLog2;
        int superblockAlignedWidth = Av1Math.AlignPowerOf2(frameWidth, superblockSizeLog2);
        int superblockAlignedHeight = Av1Math.AlignPowerOf2(frameHeight, superblockSizeLog2);
        this.superblockColumnCount = superblockAlignedWidth >> superblockSizeLog2;
        this.superblockRowCount = superblockAlignedHeight >> superblockSizeLog2;
        int superblockCount = this.superblockColumnCount * this.superblockRowCount;
        this.modeInfoSizePerSuperblock = 1 << (superblockSizeLog2 - Av1Constants.ModeInfoSizeLog2);
        this.modeInfoCountPerSuperblock = this.modeInfoSizePerSuperblock * this.modeInfoSizePerSuperblock;
        bool subX = sequenceHeader.ColorConfig.SubSamplingX;
        bool subY = sequenceHeader.ColorConfig.SubSamplingY;

        // Chroma capacity scales by two for each sampled axis: 4:4:4 => 0, 4:2:2 => 1, 4:2:0 => 2.
        this.subsamplingFactor = (subX && subY) ? 2 : (subX && !subY) ? 1 : (!subX && !subY) ? 0 : -1;
        Guard.IsFalse(this.subsamplingFactor == -1, nameof(this.subsamplingFactor), "Invalid combination of subsampling.");
        this.lumaCoefficientCount = this.modeInfoCountPerSuperblock * CoefficientCountPerModeInfo;
        this.chromaCoefficientCount = sequenceHeader.ColorConfig.IsMonochrome
            ? 0
            : this.lumaCoefficientCount >> this.subsamplingFactor;

        IMemoryOwner<int>? allocatedCoefficientScratch = null;
        MemoryGroup<Av1BlockModeInfo>? allocatedModeInfos = null;
        Buffer2D<int>? allocatedModeInfoCounts = null;
        Av1FrameModeInfoMap? allocatedModeInfoMap = null;
        IMemoryOwner<Av1TransformInfo>? allocatedTransformInfoScratch = null;
        Buffer2D<int>? allocatedQuantizerIndices = null;
        Buffer2D<int>? allocatedCdefStrength = null;
        Buffer2D<int>? allocatedDeltaLoopFilter = null;

        try
        {
            // Reconstruction consumes one superblock before parsing the next. One allocator-owned scratch surface
            // therefore covers all three planes without per-block arrays or unmanaged ownership outside ImageSharp.
            int coefficientScratchLength = checked(this.lumaCoefficientCount + (2 * this.chromaCoefficientCount));
            allocatedCoefficientScratch = this.memoryAllocator.Allocate<int>(coefficientScratchLength, AllocationOptions.Clean);

            // A decoded block can cover multiple 4x4 positions. Each allocator-backed row stores one
            // superblock's traversal records, while the map resolves every covered 4x4 position to them.
            AllocationOptions clean = AllocationOptions.Clean;

            // Mode information survives until frame completion, but one superblock row is much larger than a
            // constrained allocator segment. Store it as a discontiguous group and address records by packed index.
            long modeInfoLength = (long)this.modeInfoCountPerSuperblock * superblockCount;
            allocatedModeInfos = this.memoryAllocator.AllocateGroup<Av1BlockModeInfo>(modeInfoLength, 1, clean);
            allocatedModeInfoCounts = this.memoryAllocator.Allocate2D<int>(1, superblockCount, clean);
            allocatedModeInfoMap = new Av1FrameModeInfoMap(
                this.memoryAllocator,
                new Size(
                    this.modeInfoSizePerSuperblock * this.superblockColumnCount,
                    this.modeInfoSizePerSuperblock * this.superblockRowCount));

            // Tile parsing reconstructs each superblock before advancing to the next one. A single three-plane
            // scratch owner therefore preserves every active transform while avoiding frame-wide retained copies.
            int transformInfoScratchLength = checked(this.modeInfoCountPerSuperblock * 3);
            allocatedTransformInfoScratch = this.memoryAllocator.Allocate<Av1TransformInfo>(transformInfoScratchLength, clean);
            allocatedQuantizerIndices = this.memoryAllocator.Allocate2D<int>(1, superblockCount, clean);

            // A 128x128 superblock contains four 64x64 CDEF filter blocks; a 64x64 superblock contains one.
            this.cdefStrengthFactorLog2 = (superblockSizeLog2 - 6) << 1;
            allocatedCdefStrength = this.memoryAllocator.Allocate2D<int>(1 << this.cdefStrengthFactorLog2, superblockCount, clean);
            allocatedCdefStrength.MemoryGroup.Fill(-1);
            allocatedDeltaLoopFilter = this.memoryAllocator.Allocate2D<int>(1 << this.deltaLoopFactorLog2, superblockCount, clean);
        }
        catch
        {
            allocatedDeltaLoopFilter?.Dispose();
            allocatedCdefStrength?.Dispose();
            allocatedQuantizerIndices?.Dispose();
            allocatedTransformInfoScratch?.Dispose();
            allocatedModeInfoMap?.Dispose();
            allocatedModeInfoCounts?.Dispose();
            allocatedModeInfos?.Dispose();
            allocatedCoefficientScratch?.Dispose();
            throw;
        }

        this.coefficientScratch = allocatedCoefficientScratch;
        this.modeInfos = allocatedModeInfos;
        this.modeInfoCounts = allocatedModeInfoCounts;
        this.modeInfoMap = allocatedModeInfoMap;
        this.transformInfoScratch = allocatedTransformInfoScratch;
        this.quantizerIndices = allocatedQuantizerIndices;
        this.cdefStrength = allocatedCdefStrength;
        this.deltaLoopFilter = allocatedDeltaLoopFilter;
    }

    /// <summary>
    /// Gets the total mode-information capacity allocated for the frame.
    /// </summary>
    public int ModeInfoCount => checked((int)this.modeInfos.TotalLength);

    /// <summary>
    /// Gets the width or height of one square superblock in 4x4 mode-information units.
    /// </summary>
    public int SuperblockModeInfoSize => this.modeInfoSizePerSuperblock;

    /// <summary>
    /// Gets a bit mask containing every luma transform type decoded in this frame.
    /// </summary>
    public int LumaTransformTypeCoverage { get; private set; }

    /// <summary>
    /// Gets the inter-prediction features selected by coding blocks in this frame.
    /// </summary>
    public Av1InterPredictionFeatures InterPredictionFeatures { get; private set; }

    /// <summary>
    /// Records one decoded luma transform type before the current-superblock scratch is reused.
    /// </summary>
    /// <param name="transformType">The decoded luma transform type.</param>
    public void RecordLumaTransformType(Av1TransformType transformType) =>
        this.LumaTransformTypeCoverage |= 1 << (int)transformType;

    /// <summary>
    /// Records the inter-prediction features selected by one completed coding block.
    /// </summary>
    /// <param name="modeInfo">The completed block mode information.</param>
    /// <param name="frameHeader">The frame header containing global-motion parameters.</param>
    public void RecordInterPredictionFeatures(Av1BlockModeInfo modeInfo, ObuFrameHeader frameHeader)
    {
        if (modeInfo.ReferenceFrames[0] <= Av1ReferenceFrameType.Intra)
        {
            return;
        }

        Av1InterPredictionFeatures features = Av1InterPredictionFeatures.None;
        if (modeInfo.MotionMode == Av1MotionMode.Obmc)
        {
            features |= Av1InterPredictionFeatures.Obmc;
        }

        if (modeInfo.MotionMode == Av1MotionMode.Warped)
        {
            features |= Av1InterPredictionFeatures.LocalWarp;
        }

        if (modeInfo.YMode is Av1PredictionMode.GlobalMotionVector or Av1PredictionMode.GlobalGlobalMotionVector &&
            Math.Min(modeInfo.BlockSize.GetWidth(), modeInfo.BlockSize.GetHeight()) >= 8)
        {
            int referenceCount = modeInfo.ReferenceFrames[1] > Av1ReferenceFrameType.Intra ? 2 : 1;
            Span<Av1GlobalMotionParameters> globalMotionParameters = frameHeader.GetGlobalMotionParameters();

            for (int referenceIndex = 0; referenceIndex < referenceCount; referenceIndex++)
            {
                int canonicalReferenceIndex =
                    (int)modeInfo.ReferenceFrames[referenceIndex] - (int)Av1ReferenceFrameType.Last;

                Av1GlobalMotionParameters parameters = globalMotionParameters[canonicalReferenceIndex];
                if (parameters.Type > Av1GlobalMotionType.Translation && !parameters.IsInvalid)
                {
                    features |= Av1InterPredictionFeatures.GlobalWarp;
                }
            }
        }

        if (modeInfo.ReferenceFrames[1] == Av1ReferenceFrameType.Intra)
        {
            features |= modeInfo.UseInterIntraWedge
                ? Av1InterPredictionFeatures.WedgeInterIntra
                : Av1InterPredictionFeatures.SmoothInterIntra;
        }
        else if (modeInfo.ReferenceFrames[1] > Av1ReferenceFrameType.Intra)
        {
            features |= modeInfo.CompoundType switch
            {
                Av1CompoundType.DistanceWeighted => Av1InterPredictionFeatures.DistanceWeightedCompound,
                Av1CompoundType.Wedge => modeInfo.CompoundWedgeSign
                    ? Av1InterPredictionFeatures.InvertedWedgeCompound
                    : Av1InterPredictionFeatures.WedgeCompound,
                Av1CompoundType.DifferenceWeighted => modeInfo.DifferenceWeightedMaskType == Av1DifferenceWeightedMaskType.Type38Inverse
                    ? Av1InterPredictionFeatures.InvertedDifferenceWeightedCompound
                    : Av1InterPredictionFeatures.DifferenceWeightedCompound,
                _ => Av1InterPredictionFeatures.None,
            };
        }

        this.InterPredictionFeatures |= features;
    }

    /// <summary>
    /// Initializes the active frame's contiguous segment map and applies whole-map inheritance when requested.
    /// </summary>
    /// <param name="frameHeader">The frame header defining active geometry and segmentation update behavior.</param>
    /// <param name="primaryReferenceState">
    /// The retained state selected by the primary reference, or <see langword="null"/> when no primary reference exists.
    /// </param>
    public void InitializeSegmentIds(ObuFrameHeader frameHeader, ReferenceState? primaryReferenceState)
    {
        ObuSegmentationParameters segmentationParameters = frameHeader.SegmentationParameters;
        if (!segmentationParameters.Enabled)
        {
            // A disabled map is normatively all zero. Empty storage represents that state without retaining one byte
            // for every 4x4 position on frames that cannot use segmentation.
            return;
        }

        this.segmentIdColumnCount = frameHeader.ModeInfoColumnCount;
        this.segmentIdRowCount = frameHeader.ModeInfoRowCount;
        this.segmentIds = this.memoryAllocator.Allocate2D<byte>(
            this.segmentIdColumnCount,
            this.segmentIdRowCount,
            AllocationOptions.Clean);

        Buffer2D<byte>? primarySegmentIds = primaryReferenceState?.SegmentIds;
        if (segmentationParameters.SegmentationUpdateMap == 0 &&
            primaryReferenceState is not null &&
            primarySegmentIds is not null &&
            primaryReferenceState.SegmentIdColumnCount == this.segmentIdColumnCount &&
            primaryReferenceState.SegmentIdRowCount == this.segmentIdRowCount)
        {
            // AV1 decodemv.c copies the selected primary frame's block coverage when update_map is zero. Copying the
            // same contiguous map once establishes the identical final state without repeating a row copy per block.
            primarySegmentIds.CopyTo(this.segmentIds);
        }
    }

    /// <summary>
    /// Gets the segment identifier stored at one active 4x4 mode-information position.
    /// </summary>
    /// <param name="row">The zero-based mode-information row.</param>
    /// <param name="column">The zero-based mode-information column.</param>
    /// <returns>The segment identifier stored at the requested position.</returns>
    public byte GetSegmentId(int row, int column)
    {
        Buffer2D<byte> segmentIds = this.segmentIds
            ?? this.referenceState?.SegmentIds
            ?? throw new InvalidOperationException("The AV1 frame has no active segmentation map.");

        return segmentIds[column, row];
    }

    /// <summary>
    /// Gets the minimum retained segment identifier across a block's clipped mode-information coverage.
    /// </summary>
    /// <param name="primaryReferenceState">
    /// The retained primary-frame state, or <see langword="null"/> when no compatible map is available.
    /// </param>
    /// <param name="blockSize">The block size whose 4x4 coverage is inspected.</param>
    /// <param name="modeInfoPosition">The block origin in frame-relative 4x4 units.</param>
    /// <returns>
    /// The minimum retained segment identifier, or zero when no same-sized retained segmentation map is available.
    /// </returns>
    public int GetPredictedSegmentId(ReferenceState? primaryReferenceState, Av1BlockSize blockSize, Point modeInfoPosition)
    {
        Buffer2D<byte>? primarySegmentIds = primaryReferenceState?.SegmentIds;
        if (primaryReferenceState is null ||
            primarySegmentIds is null ||
            primaryReferenceState.SegmentIdColumnCount != this.segmentIdColumnCount ||
            primaryReferenceState.SegmentIdRowCount != this.segmentIdRowCount)
        {
            // the reference decoder exposes the prior map only when both mode-info dimensions match the active frame. Treating a
            // differently sized retained map as absent prevents coordinates from being reinterpreted with a new stride.
            return 0;
        }

        int columnCount = Math.Min(blockSize.Get4x4WideCount(), this.segmentIdColumnCount - modeInfoPosition.X);
        int rowCount = Math.Min(blockSize.Get4x4HighCount(), this.segmentIdRowCount - modeInfoPosition.Y);
        int segmentId = Av1Constants.MaxSegmentCount;

        // Temporal prediction uses the minimum over every clipped 4x4 cell, not merely the block origin. This is the
        // dec_get_segment_id rule used when segmentation_temporal_update selects the retained primary map.
        for (int row = 0; row < rowCount; row++)
        {
            ReadOnlySpan<byte> segmentRow = primarySegmentIds
                .DangerousGetRowSpan(modeInfoPosition.Y + row)
                .Slice(modeInfoPosition.X, columnCount);

            for (int column = 0; column < segmentRow.Length; column++)
            {
                segmentId = Math.Min(segmentId, segmentRow[column]);
            }
        }

        return segmentId;
    }

    /// <summary>
    /// Writes one segment identifier over a block's clipped mode-information coverage.
    /// </summary>
    /// <param name="blockSize">The block size whose 4x4 coverage is updated.</param>
    /// <param name="modeInfoPosition">The block origin in frame-relative 4x4 units.</param>
    /// <param name="segmentId">The decoded segment identifier.</param>
    public void SetSegmentId(Av1BlockSize blockSize, Point modeInfoPosition, int segmentId)
    {
        int columnCount = Math.Min(blockSize.Get4x4WideCount(), this.segmentIdColumnCount - modeInfoPosition.X);
        int rowCount = Math.Min(blockSize.Get4x4HighCount(), this.segmentIdRowCount - modeInfoPosition.Y);
        Buffer2D<byte> segmentIds = this.segmentIds
            ?? throw new InvalidOperationException("The AV1 frame has no writable segmentation map.");

        // Each block contributes one ID to all covered 4x4 cells. Filling contiguous row slices retains the native
        // row-major layout without the per-row object indirection of the previous jagged map.
        for (int row = 0; row < rowCount; row++)
        {
            segmentIds
                .DangerousGetRowSpan(modeInfoPosition.Y + row)
                .Slice(modeInfoPosition.X, columnCount)
                .Fill((byte)segmentId);
        }
    }

    /// <summary>
    /// Allocates the loop-restoration unit grid described by the active frame header.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header defining the plane count and chroma subsampling.</param>
    /// <param name="frameHeader">The frame header defining upscaled dimensions and restoration-unit sizes.</param>
    public void InitializeLoopRestoration(ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        ObuColorConfig colorConfig = sequenceHeader.ColorConfig;
        for (int planeIndex = 0; planeIndex < colorConfig.PlaneCount; planeIndex++)
        {
            ObuLoopRestorationItem item = frameHeader.LoopRestorationParameters.Items[planeIndex];
            if (item.Type == ObuRestorationType.None)
            {
                continue;
            }

            Av1Plane plane = (Av1Plane)planeIndex;
            int subsamplingX = plane != Av1Plane.Y && colorConfig.SubSamplingX ? 1 : 0;
            int subsamplingY = plane != Av1Plane.Y && colorConfig.SubSamplingY ? 1 : 0;
            int planeWidth = Av1Math.DivideLog2Ceiling(frameHeader.FrameSize.SuperResolutionUpscaledWidth, subsamplingX);
            int planeHeight = Av1Math.DivideLog2Ceiling(frameHeader.FrameSize.FrameHeight, subsamplingY);

            // A final unit may extend to 150 percent of the nominal size, so AV1 rounds the
            // unit count to nearest instead of unconditionally rounding a partial unit upward.
            int columnCount = Math.Max((planeWidth + (item.Size >> 1)) / item.Size, 1);
            int rowCount = Math.Max((planeHeight + (item.Size >> 1)) / item.Size, 1);
            this.loopRestorationUnitColumns[planeIndex] = columnCount;
            this.loopRestorationUnitRows[planeIndex] = rowCount;
            this.loopRestorationUnits[planeIndex] = this.memoryAllocator.Allocate2D<Av1LoopRestorationUnit>(
                columnCount,
                rowCount,
                AllocationOptions.Clean);
        }
    }

    /// <summary>
    /// Gets the superblock view at the specified frame-grid position.
    /// </summary>
    /// <param name="index">The position in the frame superblock grid.</param>
    /// <returns>The superblock view.</returns>
    public Av1SuperblockInfo GetSuperblock(Point index) => new(this, index);

    /// <summary>
    /// Gets the number of mode-information records parsed for a specified superblock.
    /// </summary>
    /// <param name="index">The position in the frame superblock grid.</param>
    /// <returns>The number of parsed records.</returns>
    public int GetModeInfoCount(Point index)
    {
        int storageRow = (index.Y * this.superblockColumnCount) + index.X;
        return this.modeInfoCounts[0, storageRow];
    }

    /// <summary>
    /// Gets the mode information covering the origin of a specified superblock.
    /// </summary>
    /// <param name="superblockIndex">The position in the frame superblock grid.</param>
    /// <returns>The mode information covering the superblock origin.</returns>
    public Av1BlockModeInfo GetModeInfo(Point superblockIndex) => this.GetModeInfo(superblockIndex, Point.Empty);

    /// <summary>
    /// Gets the mode information covering a position relative to a specified superblock.
    /// </summary>
    /// <param name="superblockIndex">The position in the frame superblock grid.</param>
    /// <param name="modeInfoIndex">The position within the superblock in 4x4 mode-information units.</param>
    /// <returns>The mode information covering the position.</returns>
    public Av1BlockModeInfo GetModeInfo(Point superblockIndex, Point modeInfoIndex)
    {
        Point location = this.GetModeInfoPosition(superblockIndex, modeInfoIndex);
        return this.GetModeInfoByStorageIndex(this.modeInfoMap[location]);
    }

    /// <summary>
    /// Gets the mode information record covering the specified frame-relative mode information position.
    /// </summary>
    /// <param name="modeInfoPosition">The frame-relative position in 4x4 mode-information units.</param>
    /// <returns>The mode information covering the position.</returns>
    public Av1BlockModeInfo GetModeInfoAt(Point modeInfoPosition)
        => this.GetModeInfoByStorageIndex(this.modeInfoMap[modeInfoPosition]);

    /// <summary>
    /// Gets the mode information records parsed for the specified superblock in bitstream order.
    /// </summary>
    /// <param name="superblockIndex">The position in the frame superblock grid.</param>
    /// <param name="count">The number of parsed records to return.</param>
    /// <returns>A discontiguous view of the parsed mode-information records.</returns>
    public ModeInfoCollection GetModeInfos(Point superblockIndex, int count)
    {
        int storageRow = (superblockIndex.Y * this.superblockColumnCount) + superblockIndex.X;
        return new ModeInfoCollection(this, storageRow * this.modeInfoCountPerSuperblock, count);
    }

    /// <summary>
    /// Gets the transform-information scratch for one plane of the current superblock.
    /// </summary>
    /// <param name="plane">The zero-based plane index.</param>
    /// <returns>The luma storage for plane zero; otherwise, the shared chroma storage.</returns>
    public Span<Av1TransformInfo> GetSuperblockTransform(int plane)
    {
        if (plane == 0)
        {
            return this.GetSuperblockTransformY();
        }

        return this.GetSuperblockTransformUv();
    }

    /// <summary>
    /// Gets the luma transform-information scratch for the current superblock.
    /// </summary>
    /// <returns>The current-superblock luma transform-information span.</returns>
    public Span<Av1TransformInfo> GetSuperblockTransformY()
        => this.transformInfoScratch.GetSpan()[..this.modeInfoCountPerSuperblock];

    /// <summary>
    /// Gets the shared chroma transform-information scratch for the current superblock.
    /// </summary>
    /// <returns>The current-superblock chroma transform-information span.</returns>
    public Span<Av1TransformInfo> GetSuperblockTransformUv()
        => this.transformInfoScratch.GetSpan().Slice(
            this.modeInfoCountPerSuperblock,
            this.modeInfoCountPerSuperblock << 1);

    /// <summary>
    /// Gets the luma coefficient scratch reused for the current superblock.
    /// </summary>
    /// <returns>The current superblock luma coefficient span.</returns>
    public Span<int> GetCoefficientsY()
        => this.coefficientScratch.GetSpan()[..this.lumaCoefficientCount];

    /// <summary>
    /// Gets the blue-difference chroma coefficient scratch reused for the current superblock.
    /// </summary>
    /// <returns>The current superblock blue-difference chroma coefficient span.</returns>
    public Span<int> GetCoefficientsU()
        => this.coefficientScratch.GetSpan().Slice(this.lumaCoefficientCount, this.chromaCoefficientCount);

    /// <summary>
    /// Gets the red-difference chroma coefficient scratch reused for the current superblock.
    /// </summary>
    /// <returns>The current superblock red-difference chroma coefficient span.</returns>
    public Span<int> GetCoefficientsV()
        => this.coefficientScratch.GetSpan().Slice(
            this.lumaCoefficientCount + this.chromaCoefficientCount,
            this.chromaCoefficientCount);

    /// <summary>
    /// Gets a reference to the active base quantizer index for a specified superblock.
    /// </summary>
    /// <param name="index">The position in the frame superblock grid.</param>
    /// <returns>A reference to the superblock base quantizer index.</returns>
    public ref int GetQuantizerIndex(Point index)
    {
        int storageRow = (index.Y * this.superblockColumnCount) + index.X;
        return ref this.quantizerIndices[0, storageRow];
    }

    /// <summary>
    /// Gets the constrained directional enhancement filter strengths for a specified superblock.
    /// </summary>
    /// <param name="index">The position in the frame superblock grid.</param>
    /// <returns>The superblock filter-strength span.</returns>
    public Span<int> GetCdefStrength(Point index)
    {
        int storageRow = (index.Y * this.superblockColumnCount) + index.X;
        return this.cdefStrength.DangerousGetRowSpan(storageRow);
    }

    /// <summary>
    /// Resets every constrained directional enhancement filter strength for a superblock to its unassigned value.
    /// </summary>
    /// <param name="index">The position in the frame superblock grid.</param>
    public void ClearCdef(Point index)
    {
        Span<int> cdefs = this.GetCdefStrength(index);
        for (int i = 0; i < cdefs.Length; i++)
        {
            cdefs[i] = -1;
        }
    }

    /// <summary>
    /// Gets the four loop-filter delta values for a specified superblock.
    /// </summary>
    /// <param name="index">The position in the frame superblock grid.</param>
    /// <returns>The superblock loop-filter delta span.</returns>
    public Span<int> GetDeltaLoopFilter(Point index)
    {
        int storageRow = (index.Y * this.superblockColumnCount) + index.X;
        return this.deltaLoopFilter.DangerousGetRowSpan(storageRow);
    }

    /// <summary>
    /// Gets the number of loop-restoration unit columns allocated for a color plane.
    /// </summary>
    /// <param name="plane">The zero-based color-plane index.</param>
    /// <returns>The number of restoration-unit columns.</returns>
    public int GetLoopRestorationUnitColumnCount(int plane) => this.loopRestorationUnitColumns[plane];

    /// <summary>
    /// Gets the number of loop-restoration unit rows allocated for a color plane.
    /// </summary>
    /// <param name="plane">The zero-based color-plane index.</param>
    /// <returns>The number of restoration-unit rows.</returns>
    public int GetLoopRestorationUnitRowCount(int plane) => this.loopRestorationUnitRows[plane];

    /// <summary>
    /// Gets the loop-restoration unit at a plane-relative grid position.
    /// </summary>
    /// <param name="plane">The zero-based color-plane index.</param>
    /// <param name="row">The restoration-unit row.</param>
    /// <param name="column">The restoration-unit column.</param>
    /// <returns>The decoded restoration-unit information.</returns>
    public ref Av1LoopRestorationUnit GetLoopRestorationUnit(int plane, int row, int column)
    {
        Buffer2D<Av1LoopRestorationUnit> units = this.loopRestorationUnits[plane]
            ?? throw new InvalidOperationException("The selected AV1 plane has no loop-restoration units.");

        return ref units[column, row];
    }

    /// <summary>
    /// Stores decoded mode information and maps every 4x4 position covered by its block.
    /// </summary>
    /// <param name="modeInfo">The decoded block mode information.</param>
    /// <param name="superblockInfo">The containing superblock.</param>
    /// <returns>A reference to the published mode information.</returns>
    public ref Av1BlockModeInfo UpdateModeInfo(Av1BlockModeInfo modeInfo, Av1SuperblockInfo superblockInfo)
    {
        Point modeInfoPosition = this.GetModeInfoPosition(superblockInfo.Position, modeInfo.PositionInSuperblock);
        int storageRow = (superblockInfo.Position.Y * this.superblockColumnCount) + superblockInfo.Position.X;
        ref int modeInfoCount = ref this.modeInfoCounts[0, storageRow];
        DebugGuard.MustBeLessThan(modeInfoCount, this.modeInfoCountPerSuperblock, nameof(modeInfoCount));
        int storageIndex = (storageRow * this.modeInfoCountPerSuperblock) + modeInfoCount;
        modeInfo.ModeInfoIndex = this.modeInfoMap.NextIndex;
        this.GetModeInfoByStorageIndex(storageIndex) = modeInfo;
        modeInfoCount++;
        this.UpdateRetainedMotionField(modeInfo, modeInfoPosition);
        this.modeInfoMap.Update(modeInfoPosition, modeInfo.BlockSize, storageIndex);
        return ref this.GetModeInfoByStorageIndex(storageIndex);
    }

    /// <summary>
    /// Gets a reference to one packed mode-information record across allocator segments.
    /// </summary>
    /// <param name="storageIndex">The packed frame-storage index.</param>
    /// <returns>A reference to the selected mode information.</returns>
    private ref Av1BlockModeInfo GetModeInfoByStorageIndex(int storageIndex)
    {
        int bufferIndex = storageIndex / this.modeInfos.BufferLength;
        int elementIndex = storageIndex - (bufferIndex * this.modeInfos.BufferLength);
        return ref this.modeInfos[bufferIndex].Span[elementIndex];
    }

    /// <summary>
    /// Converts a superblock-relative mode-information position to frame-relative coordinates.
    /// </summary>
    /// <param name="superblockPosition">The position in the frame superblock grid.</param>
    /// <param name="positionInSuperblock">The position within the superblock in 4x4 units.</param>
    /// <returns>The frame-relative position in 4x4 mode-information units.</returns>
    private Point GetModeInfoPosition(Point superblockPosition, Point positionInSuperblock)
    {
        int x = (superblockPosition.X * this.modeInfoSizePerSuperblock) + positionInSuperblock.X;
        int y = (superblockPosition.Y * this.modeInfoSizePerSuperblock) + positionInSuperblock.Y;
        return new Point(x, y);
    }

    /// <summary>
    /// Provides indexed and reference-preserving traversal over one superblock's discontiguous mode information.
    /// </summary>
    public readonly struct ModeInfoCollection
    {
        private readonly Av1FrameInfo owner;
        private readonly int startIndex;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModeInfoCollection"/> struct.
        /// </summary>
        /// <param name="owner">The frame that owns the mode-information group.</param>
        /// <param name="startIndex">The packed index of the first record.</param>
        /// <param name="length">The number of records in the view.</param>
        public ModeInfoCollection(Av1FrameInfo owner, int startIndex, int length)
        {
            this.owner = owner;
            this.startIndex = startIndex;
            this.Length = length;
        }

        /// <summary>
        /// Gets the number of records in the view.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Gets a reference to the record at the specified traversal index.
        /// </summary>
        /// <param name="index">The zero-based traversal index.</param>
        public ref Av1BlockModeInfo this[int index] =>
            ref this.owner.GetModeInfoByStorageIndex(this.startIndex + index);

        /// <summary>
        /// Creates a reference-preserving enumerator over the records.
        /// </summary>
        /// <returns>The initialized enumerator.</returns>
        public Enumerator GetEnumerator() => new(this.owner, this.startIndex, this.Length);

        /// <summary>
        /// Enumerates one superblock's mode-information records without flattening allocator segments.
        /// </summary>
        public struct Enumerator
        {
            private readonly Av1FrameInfo owner;
            private readonly int startIndex;
            private readonly int length;
            private int index;

            /// <summary>
            /// Initializes a new instance of the <see cref="Enumerator"/> struct.
            /// </summary>
            /// <param name="owner">The frame that owns the mode-information group.</param>
            /// <param name="startIndex">The packed index of the first record.</param>
            /// <param name="length">The number of records in the view.</param>
            public Enumerator(Av1FrameInfo owner, int startIndex, int length)
            {
                this.owner = owner;
                this.startIndex = startIndex;
                this.length = length;
                this.index = -1;
            }

            /// <summary>
            /// Gets a reference to the current record.
            /// </summary>
            public ref Av1BlockModeInfo Current =>
                ref this.owner.GetModeInfoByStorageIndex(this.startIndex + this.index);

            /// <summary>
            /// Advances to the next record.
            /// </summary>
            /// <returns><see langword="true"/> when another record is available.</returns>
            public bool MoveNext()
            {
                this.index++;
                return this.index < this.length;
            }
        }
    }
}
