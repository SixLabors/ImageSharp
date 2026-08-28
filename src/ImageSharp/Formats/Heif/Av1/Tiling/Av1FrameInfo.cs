// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Owns the mode, motion, segmentation, transform, coefficient, quantizer, and filter state decoded for one AV1 frame.
/// </summary>
internal partial class Av1FrameInfo : IDisposable
{
    /// <summary>
    /// The coefficient slots reserved for one 4x4 mode-information unit: one end index followed by 16 coefficients.
    /// </summary>
    public const int CoefficientCountPerModeInfo = 1 + 16;

    /// <summary>
    /// Stores raster-ordered luma coefficients for every frame superblock.
    /// </summary>
    private readonly int[] coefficientsY = [];

    /// <summary>
    /// Stores raster-ordered blue-difference chroma coefficients for every frame superblock.
    /// </summary>
    private readonly int[] coefficientsU = [];

    /// <summary>
    /// Stores raster-ordered red-difference chroma coefficients for every frame superblock.
    /// </summary>
    private readonly int[] coefficientsV = [];

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
    /// Stores one addressing view for each frame superblock.
    /// </summary>
    private readonly Av1SuperblockInfo[] superblockInfos;

    /// <summary>
    /// Stores decoded block mode information in bitstream traversal order.
    /// </summary>
    private readonly Av1BlockModeInfo[] modeInfos;

    /// <summary>
    /// Maps every frame-relative 4x4 position to its covering entry in <see cref="modeInfos"/>.
    /// </summary>
    private readonly Av1FrameModeInfoMap modeInfoMap;

    /// <summary>
    /// Stores the decoded segment identifier for each active 4x4 mode-information position in row-major order.
    /// </summary>
    private byte[] segmentIds = [];

    /// <summary>
    /// The number of active 4x4 columns in one row of <see cref="segmentIds"/>.
    /// </summary>
    private int segmentIdColumnCount;

    /// <summary>
    /// The number of active 4x4 rows represented by <see cref="segmentIds"/>.
    /// </summary>
    private int segmentIdRowCount;

    /// <summary>
    /// Stores luma transform information grouped by superblock.
    /// </summary>
    private readonly Av1TransformInfo[] transformInfosY;

    /// <summary>
    /// Stores both chroma planes' transform information grouped by superblock.
    /// </summary>
    private readonly Av1TransformInfo[] transformInfosUv;

    /// <summary>
    /// Stores the active base quantizer index for each frame superblock.
    /// </summary>
    private readonly int[] quantizerIndices;

    /// <summary>
    /// The base-2 number of constrained directional enhancement filter entries allocated per superblock.
    /// </summary>
    private readonly int cdefStrengthFactorLog2;

    /// <summary>
    /// Stores constrained directional enhancement filter strengths grouped by superblock.
    /// </summary>
    private readonly int[] cdefStrength;

    /// <summary>
    /// The base-2 number of loop-filter delta values stored per superblock.
    /// </summary>
    private readonly int deltaLoopFactorLog2 = 2;

    /// <summary>
    /// Stores the four loop-filter delta values for each superblock.
    /// </summary>
    private readonly int[] deltaLoopFilter;

    /// <summary>
    /// Stores raster-ordered loop-restoration units for each color plane.
    /// </summary>
    private readonly Av1LoopRestorationUnit[][] loopRestorationUnits = [[], [], []];

    /// <summary>
    /// Stores the number of loop-restoration unit columns for each color plane.
    /// </summary>
    private readonly int[] loopRestorationUnitColumns = new int[Av1Constants.MaxPlanes];

    /// <summary>
    /// The number of loop-restoration unit rows allocated for each color plane.
    /// </summary>
    private readonly int[] loopRestorationUnitRows = new int[Av1Constants.MaxPlanes];

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1FrameInfo"/> class.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header defining maximum dimensions, superblock size, and color sampling.</param>
    public Av1FrameInfo(ObuSequenceHeader sequenceHeader)
    {
        // Size frame-owned storage from the sequence maximums because later frame headers may select
        // any coded dimensions up to these bounds without rebuilding the decoder's indexing model.
        int superblockSizeLog2 = sequenceHeader.SuperblockSizeLog2;
        int superblockAlignedWidth = Av1Math.AlignPowerOf2(sequenceHeader.MaxFrameWidth, superblockSizeLog2);
        int superblockAlignedHeight = Av1Math.AlignPowerOf2(sequenceHeader.MaxFrameHeight, superblockSizeLog2);
        this.superblockColumnCount = superblockAlignedWidth >> superblockSizeLog2;
        this.superblockRowCount = superblockAlignedHeight >> superblockSizeLog2;
        int superblockCount = this.superblockColumnCount * this.superblockRowCount;
        this.modeInfoSizePerSuperblock = 1 << (superblockSizeLog2 - Av1Constants.ModeInfoSizeLog2);
        this.modeInfoCountPerSuperblock = this.modeInfoSizePerSuperblock * this.modeInfoSizePerSuperblock;
        int numPlanes = sequenceHeader.ColorConfig.IsMonochrome ? 1 : Av1Constants.MaxPlanes;

        // A decoded block can cover multiple 4x4 positions, so modeInfos stores each block once while
        // modeInfoMap makes every covered position resolve to that single traversal-order entry.
        this.superblockInfos = new Av1SuperblockInfo[superblockCount];
        this.modeInfos = new Av1BlockModeInfo[superblockCount * this.modeInfoCountPerSuperblock];
        this.modeInfoMap = new Av1FrameModeInfoMap(new Size(this.modeInfoSizePerSuperblock * this.superblockColumnCount, this.modeInfoSizePerSuperblock * this.superblockRowCount));
        this.transformInfosY = new Av1TransformInfo[superblockCount * this.modeInfoCountPerSuperblock];
        this.transformInfosUv = new Av1TransformInfo[2 * superblockCount * this.modeInfoCountPerSuperblock];

        // Superblock views retain only their grid position and address all storage through this owner.
        int i = 0;
        for (int y = 0; y < this.superblockRowCount; y++)
        {
            for (int x = 0; x < this.superblockColumnCount; x++)
            {
                Point point = new(x, y);
                this.superblockInfos[i] = new(this, point);
                i++;
            }
        }

        bool subX = sequenceHeader.ColorConfig.SubSamplingX;
        bool subY = sequenceHeader.ColorConfig.SubSamplingY;

        // Chroma capacity scales by two for each sampled axis: 4:4:4 => 0, 4:2:2 => 1, 4:2:0 => 2.
        this.subsamplingFactor = (subX && subY) ? 2 : (subX && !subY) ? 1 : (!subX && !subY) ? 0 : -1;
        Guard.IsFalse(this.subsamplingFactor == -1, nameof(this.subsamplingFactor), "Invalid combination of subsampling.");
        int lumaCoefficientCountPerSuperblock = this.modeInfoCountPerSuperblock * CoefficientCountPerModeInfo;
        int chromaCoefficientCountPerSuperblock = lumaCoefficientCountPerSuperblock >> this.subsamplingFactor;
        this.coefficientsY = new int[superblockCount * lumaCoefficientCountPerSuperblock];
        this.coefficientsU = new int[superblockCount * chromaCoefficientCountPerSuperblock];
        this.coefficientsV = new int[superblockCount * chromaCoefficientCountPerSuperblock];
        this.quantizerIndices = new int[superblockCount];

        // A 128x128 superblock contains four 64x64 CDEF filter blocks; a 64x64 superblock contains one.
        this.cdefStrengthFactorLog2 = (superblockSizeLog2 - 6) << 1;
        this.cdefStrength = new int[superblockCount << this.cdefStrengthFactorLog2];
        Array.Fill(this.cdefStrength, -1);
        this.deltaLoopFilter = new int[superblockCount << this.deltaLoopFactorLog2];
    }

    /// <summary>
    /// Gets the total mode-information capacity allocated for the frame.
    /// </summary>
    public int ModeInfoCount => this.modeInfos.Length;

    /// <summary>
    /// Gets the width or height of one square superblock in 4x4 mode-information units.
    /// </summary>
    public int SuperblockModeInfoSize => this.modeInfoSizePerSuperblock;

    /// <summary>
    /// Initializes the active frame's contiguous segment map and applies whole-map inheritance when requested.
    /// </summary>
    /// <param name="frameHeader">The frame header defining active geometry and segmentation update behavior.</param>
    /// <param name="primaryReferenceFrameInfo">
    /// The retained state selected by the primary reference, or <see langword="null"/> when no primary reference exists.
    /// </param>
    public void InitializeSegmentIds(ObuFrameHeader frameHeader, Av1FrameInfo? primaryReferenceFrameInfo)
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
        this.segmentIds = new byte[this.segmentIdColumnCount * this.segmentIdRowCount];

        if (segmentationParameters.SegmentationUpdateMap == 0 &&
            primaryReferenceFrameInfo is not null &&
            primaryReferenceFrameInfo.segmentIdColumnCount == this.segmentIdColumnCount &&
            primaryReferenceFrameInfo.segmentIdRowCount == this.segmentIdRowCount)
        {
            // AV1 decodemv.c copies the selected primary frame's block coverage when update_map is zero. Copying the
            // same contiguous map once establishes the identical final state without repeating a row copy per block.
            primaryReferenceFrameInfo.segmentIds.CopyTo(this.segmentIds, 0);
        }
    }

    /// <summary>
    /// Gets the segment identifier stored at one active 4x4 mode-information position.
    /// </summary>
    /// <param name="row">The zero-based mode-information row.</param>
    /// <param name="column">The zero-based mode-information column.</param>
    /// <returns>The segment identifier stored at the requested position.</returns>
    public byte GetSegmentId(int row, int column) => this.segmentIds[(row * this.segmentIdColumnCount) + column];

    /// <summary>
    /// Gets the minimum retained segment identifier across a block's clipped mode-information coverage.
    /// </summary>
    /// <param name="primaryReferenceFrameInfo">
    /// The retained primary-frame state, or <see langword="null"/> when no compatible map is available.
    /// </param>
    /// <param name="blockSize">The block size whose 4x4 coverage is inspected.</param>
    /// <param name="modeInfoPosition">The block origin in frame-relative 4x4 units.</param>
    /// <returns>
    /// The minimum retained segment identifier, or zero when no same-sized retained segmentation map is available.
    /// </returns>
    public int GetPredictedSegmentId(Av1FrameInfo? primaryReferenceFrameInfo, Av1BlockSize blockSize, Point modeInfoPosition)
    {
        if (primaryReferenceFrameInfo is null ||
            primaryReferenceFrameInfo.segmentIds.Length == 0 ||
            primaryReferenceFrameInfo.segmentIdColumnCount != this.segmentIdColumnCount ||
            primaryReferenceFrameInfo.segmentIdRowCount != this.segmentIdRowCount)
        {
            // libaom exposes the prior map only when both mode-info dimensions match the active frame. Treating a
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
            int offset = ((modeInfoPosition.Y + row) * primaryReferenceFrameInfo.segmentIdColumnCount) + modeInfoPosition.X;
            ReadOnlySpan<byte> segmentRow = primaryReferenceFrameInfo.segmentIds.AsSpan(offset, columnCount);

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

        // Each block contributes one ID to all covered 4x4 cells. Filling contiguous row slices retains the native
        // row-major layout without the per-row object indirection of the previous jagged map.
        for (int row = 0; row < rowCount; row++)
        {
            int offset = ((modeInfoPosition.Y + row) * this.segmentIdColumnCount) + modeInfoPosition.X;
            this.segmentIds.AsSpan(offset, columnCount).Fill((byte)segmentId);
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
            Av1LoopRestorationUnit[] units = new Av1LoopRestorationUnit[columnCount * rowCount];
            for (int i = 0; i < units.Length; i++)
            {
                units[i] = new();
            }

            this.loopRestorationUnitColumns[planeIndex] = columnCount;
            this.loopRestorationUnitRows[planeIndex] = rowCount;
            this.loopRestorationUnits[planeIndex] = units;
        }
    }

    /// <summary>
    /// Gets the superblock view at the specified frame-grid position.
    /// </summary>
    /// <param name="index">The position in the frame superblock grid.</param>
    /// <returns>The superblock view.</returns>
    public Av1SuperblockInfo GetSuperblock(Point index)
    {
        Span<Av1SuperblockInfo> span = this.superblockInfos;
        int i = (index.Y * this.superblockColumnCount) + index.X;
        return span[i];
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
        int index = this.modeInfoMap[location];
        return this.modeInfos[index];
    }

    /// <summary>
    /// Gets the mode information record covering the specified frame-relative mode information position.
    /// </summary>
    /// <param name="modeInfoPosition">The frame-relative position in 4x4 mode-information units.</param>
    /// <returns>The mode information covering the position.</returns>
    public Av1BlockModeInfo GetModeInfoAt(Point modeInfoPosition) => this.modeInfos[this.modeInfoMap[modeInfoPosition]];

    /// <summary>
    /// Gets the mode information records parsed for the specified superblock in bitstream order.
    /// </summary>
    /// <param name="superblockIndex">The position in the frame superblock grid.</param>
    /// <param name="count">The number of parsed records to return.</param>
    /// <returns>The parsed mode-information records.</returns>
    public Span<Av1BlockModeInfo> GetModeInfos(Point superblockIndex, int count)
    {
        Point location = this.GetModeInfoPosition(superblockIndex, Point.Empty);
        int index = this.modeInfoMap[location];
        return this.modeInfos.AsSpan(index, count);
    }

    /// <summary>
    /// Gets the transform-information storage for one plane of a specified superblock.
    /// </summary>
    /// <param name="plane">The zero-based plane index.</param>
    /// <param name="index">The position in the frame superblock grid.</param>
    /// <returns>The luma storage for plane zero; otherwise, the shared chroma storage.</returns>
    public Span<Av1TransformInfo> GetSuperblockTransform(int plane, Point index)
    {
        if (plane == 0)
        {
            return this.GetSuperblockTransformY(index);
        }

        return this.GetSuperblockTransformUv(index);
    }

    /// <summary>
    /// Gets the luma transform-information storage for a specified superblock.
    /// </summary>
    /// <param name="index">The position in the frame superblock grid.</param>
    /// <returns>The superblock luma transform-information span.</returns>
    public Span<Av1TransformInfo> GetSuperblockTransformY(Point index)
    {
        Span<Av1TransformInfo> span = this.transformInfosY;
        int offset = ((index.Y * this.superblockColumnCount) + index.X) * this.modeInfoCountPerSuperblock;
        return span.Slice(offset, this.modeInfoCountPerSuperblock);
    }

    /// <summary>
    /// Gets the shared chroma transform-information storage for a specified superblock.
    /// </summary>
    /// <param name="index">The position in the frame superblock grid.</param>
    /// <returns>The superblock chroma transform-information span.</returns>
    public Span<Av1TransformInfo> GetSuperblockTransformUv(Point index)
    {
        Span<Av1TransformInfo> span = this.transformInfosUv;
        int offset = (((index.Y * this.superblockColumnCount) + index.X) * this.modeInfoCountPerSuperblock) << 1;
        return span.Slice(offset, this.modeInfoCountPerSuperblock << 1);
    }

    /// <summary>
    /// Gets the luma coefficient storage for a specified superblock.
    /// </summary>
    /// <param name="index">The position in the frame superblock grid.</param>
    /// <returns>The superblock luma coefficient span.</returns>
    public Span<int> GetCoefficientsY(Point index)
    {
        Span<int> span = this.coefficientsY;
        int count = this.modeInfoCountPerSuperblock * CoefficientCountPerModeInfo;
        int superblock = (index.Y * this.superblockColumnCount) + index.X;
        return span.Slice(superblock * count, count);
    }

    /// <summary>
    /// Gets the blue-difference chroma coefficient storage for a specified superblock.
    /// </summary>
    /// <param name="index">The position in the frame superblock grid.</param>
    /// <returns>The superblock blue-difference chroma coefficient span.</returns>
    public Span<int> GetCoefficientsU(Point index)
    {
        Span<int> span = this.coefficientsU;
        int count = (this.modeInfoCountPerSuperblock * CoefficientCountPerModeInfo) >> this.subsamplingFactor;
        int superblock = (index.Y * this.superblockColumnCount) + index.X;
        return span.Slice(superblock * count, count);
    }

    /// <summary>
    /// Gets the red-difference chroma coefficient storage for a specified superblock.
    /// </summary>
    /// <param name="index">The position in the frame superblock grid.</param>
    /// <returns>The superblock red-difference chroma coefficient span.</returns>
    public Span<int> GetCoefficientsV(Point index)
    {
        Span<int> span = this.coefficientsV;
        int count = (this.modeInfoCountPerSuperblock * CoefficientCountPerModeInfo) >> this.subsamplingFactor;
        int superblock = (index.Y * this.superblockColumnCount) + index.X;
        return span.Slice(superblock * count, count);
    }

    /// <summary>
    /// Gets a reference to the active base quantizer index for a specified superblock.
    /// </summary>
    /// <param name="index">The position in the frame superblock grid.</param>
    /// <returns>A reference to the superblock base quantizer index.</returns>
    public ref int GetQuantizerIndex(Point index)
    {
        Span<int> span = this.quantizerIndices;
        int i = (index.Y * this.superblockColumnCount) + index.X;
        return ref span[i];
    }

    /// <summary>
    /// Gets the constrained directional enhancement filter strengths for a specified superblock.
    /// </summary>
    /// <param name="index">The position in the frame superblock grid.</param>
    /// <returns>The superblock filter-strength span.</returns>
    public Span<int> GetCdefStrength(Point index)
    {
        Span<int> span = this.cdefStrength;
        int i = ((index.Y * this.superblockColumnCount) + index.X) << this.cdefStrengthFactorLog2;
        return span.Slice(i, 1 << this.cdefStrengthFactorLog2);
    }

    /// <summary>
    /// Resets every constrained directional enhancement filter strength for a superblock to its unassigned value.
    /// </summary>
    /// <param name="index">The position in the frame superblock grid.</param>
    internal void ClearCdef(Point index)
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
        Span<int> span = this.deltaLoopFilter;
        int i = ((index.Y * this.superblockColumnCount) + index.X) << this.deltaLoopFactorLog2;
        return span.Slice(i, 1 << this.deltaLoopFactorLog2);
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
    public Av1LoopRestorationUnit GetLoopRestorationUnit(int plane, int row, int column)
    {
        int index = (row * this.loopRestorationUnitColumns[plane]) + column;
        return this.loopRestorationUnits[plane][index];
    }

    /// <summary>
    /// Stores decoded mode information and maps every 4x4 position covered by its block.
    /// </summary>
    /// <param name="modeInfo">The decoded block mode information.</param>
    /// <param name="superblockInfo">The containing superblock.</param>
    public void UpdateModeInfo(Av1BlockModeInfo modeInfo, Av1SuperblockInfo superblockInfo)
    {
        Point modeInfoPosition = this.GetModeInfoPosition(superblockInfo.Position, modeInfo.PositionInSuperblock);
        this.modeInfos[this.modeInfoMap.NextIndex] = modeInfo;
        this.UpdateRetainedMotionField(modeInfo, modeInfoPosition);
        this.modeInfoMap.Update(modeInfoPosition, modeInfo.BlockSize);
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
}
