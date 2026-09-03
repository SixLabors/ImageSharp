// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Cdef;

/// <summary>
/// Applies AV1 constrained directional enhancement filtering to a reconstructed still-image frame.
/// </summary>
internal sealed class Av1CdefDecoder
{
    /// <summary>
    /// The width and height of a CDEF unit in 4x4 luma mode-information units.
    /// </summary>
    private const int CdefUnitModeInfoSize = 16;

    /// <summary>
    /// The number of unavailable samples reserved on each source-plane edge.
    /// </summary>
    private const int SourceBorder = 2;

    /// <summary>
    /// The maximum number of non-skipped 8x8 luma blocks in one 64x64 CDEF unit.
    /// </summary>
    private const int MaximumBlocksPerUnit = 8 * 8;

    /// <summary>
    /// The maximum width or height of one CDEF unit in plane samples.
    /// </summary>
    private const int MaximumUnitPlaneSize = CdefUnitModeInfoSize << Av1Constants.ModeInfoSizeLog2;

    /// <summary>
    /// The stride of the reusable bordered CDEF source unit.
    /// </summary>
    private const int SourceStride = MaximumUnitPlaneSize + (SourceBorder * 2);

    /// <summary>
    /// The sample count of the reusable bordered CDEF source unit.
    /// </summary>
    private const int SourceBufferLength = SourceStride * (MaximumUnitPlaneSize + (SourceBorder * 2));

    /// <summary>
    /// The sequence-level superblock, bit-depth, and color configuration.
    /// </summary>
    private readonly ObuSequenceHeader sequenceHeader;

    /// <summary>
    /// The frame dimensions and CDEF strength table.
    /// </summary>
    private readonly ObuFrameHeader frameHeader;

    /// <summary>
    /// The decoded block skip state and CDEF-unit strength selections.
    /// </summary>
    private readonly Av1FrameInfo frameInfo;

    /// <summary>
    /// The reconstructed plane samples modified by CDEF.
    /// </summary>
    private readonly Av1FrameBuffer<byte> frameBuffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1CdefDecoder"/> class.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header defining CDEF availability and the color layout.</param>
    /// <param name="frameHeader">The frame header defining dimensions and CDEF strengths.</param>
    /// <param name="frameInfo">The decoded block skip state and per-unit strength selections.</param>
    /// <param name="frameBuffer">The deblocked frame samples to filter.</param>
    public Av1CdefDecoder(
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1FrameInfo frameInfo,
        Av1FrameBuffer<byte> frameBuffer)
    {
        this.sequenceHeader = sequenceHeader;
        this.frameHeader = frameHeader;
        this.frameInfo = frameInfo;
        this.frameBuffer = frameBuffer;
    }

    /// <summary>
    /// Filters every enabled color plane using directions derived from the deblocked luma plane.
    /// </summary>
    public void DecodeFrame()
    {
        if (!this.sequenceHeader.EnableCdef || this.frameHeader.CodedLossless || this.frameHeader.AllowIntraBlockCopy)
        {
            return;
        }

        ObuConstraintDirectionalEnhancementFilterParameters parameters = this.frameHeader.CdefParameters;
        int strengthCount = 1 << parameters.BitCount;
        bool hasNonZeroStrength = false;
        for (int i = 0; i < strengthCount; i++)
        {
            if (parameters.YStrength[i] != 0 ||
                (this.sequenceHeader.ColorConfig.PlaneCount > 1 && parameters.UvStrength[i] != 0))
            {
                hasNonZeroStrength = true;
                break;
            }
        }

        if (!hasNonZeroStrength)
        {
            return;
        }

        ObuColorConfig colorConfig = this.sequenceHeader.ColorConfig;
        int planeCount = colorConfig.PlaneCount;
        Span<int> subsamplingX = stackalloc int[3];
        Span<int> subsamplingY = stackalloc int[3];
        Span<int> planeWidths = stackalloc int[3];
        Span<int> lineBufferOffsets = stackalloc int[3];
        Span<int> columnBufferOffsets = stackalloc int[3];
        Span<int> columnBufferLengths = stackalloc int[3];
        int lineBufferLength = 0;
        int columnBufferLength = 0;

        for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
        {
            Av1Plane plane = (Av1Plane)planeIndex;
            int planeSubsamplingX = plane != Av1Plane.Y && colorConfig.SubSamplingX ? 1 : 0;
            int planeSubsamplingY = plane != Av1Plane.Y && colorConfig.SubSamplingY ? 1 : 0;
            int planeWidth = this.frameHeader.ModeInfoColumnCount << (Av1Constants.ModeInfoSizeLog2 - planeSubsamplingX);
            int maximumUnitHeight = MaximumUnitPlaneSize >> planeSubsamplingY;

            subsamplingX[planeIndex] = planeSubsamplingX;
            subsamplingY[planeIndex] = planeSubsamplingY;
            planeWidths[planeIndex] = planeWidth;
            lineBufferOffsets[planeIndex] = lineBufferLength;
            columnBufferOffsets[planeIndex] = columnBufferLength;
            columnBufferLengths[planeIndex] = (maximumUnitHeight + (SourceBorder * 2)) * SourceBorder;
            lineBufferLength += planeWidth * SourceBorder * 2;
            columnBufferLength += columnBufferLengths[planeIndex];
        }

        int directionStorageLength = MaximumBlocksPerUnit * sizeof(int) / sizeof(ushort);
        int blockStorageLength = MaximumBlocksPerUnit * Unsafe.SizeOf<CdefBlock>() / sizeof(ushort);
        int unitStorageOffset = SourceBufferLength + lineBufferLength + columnBufferLength;
        int scratchLength = unitStorageOffset + (directionStorageLength * 2) + blockStorageLength;
        MemoryAllocator allocator = this.frameBuffer.MemoryAllocator;
        using IMemoryOwner<ushort> scratchOwner = allocator.Allocate<ushort>(scratchLength);
        Span<ushort> scratch = scratchOwner.Memory.Span[..scratchLength];
        Span<ushort> source = scratch[..SourceBufferLength];
        Span<ushort> lineBuffer = scratch.Slice(SourceBufferLength, lineBufferLength);
        Span<ushort> columnBuffer = scratch.Slice(SourceBufferLength + lineBufferLength, columnBufferLength);

        // Every preceding plane region has an even ushort length, so the appended unit state remains 32-bit aligned.
        // Directions, variances, and block coordinates share the owner because they are reused one unit at a time.
        Span<int> directions = MemoryMarshal.Cast<ushort, int>(
            scratch.Slice(unitStorageOffset, directionStorageLength));

        Span<int> variances = MemoryMarshal.Cast<ushort, int>(
            scratch.Slice(unitStorageOffset + directionStorageLength, directionStorageLength));

        Span<CdefBlock> blocks = MemoryMarshal.Cast<ushort, CdefBlock>(
            scratch.Slice(unitStorageOffset + (directionStorageLength * 2), blockStorageLength));

        Span<bool> cdefLeft = stackalloc bool[3];
        int unitColumnCount = (this.frameHeader.ModeInfoColumnCount + CdefUnitModeInfoSize - 1) / CdefUnitModeInfoSize;
        int unitRowCount = (this.frameHeader.ModeInfoRowCount + CdefUnitModeInfoSize - 1) / CdefUnitModeInfoSize;

        // libaom traverses one 64x64 unit at a time so chroma consumes the luma directions before
        // the fixed direction arrays are reused. This also bounds direction storage to 64 entries.
        for (int unitRow = 0; unitRow < unitRowCount; unitRow++)
        {
            cdefLeft.Clear();
            int unitModeInfoRow = unitRow * CdefUnitModeInfoSize;

            // Preserve the final two unfiltered rows before this unit row is modified. The alternating
            // slots keep the previous row available while the next row's top border is captured.
            if (unitRow < unitRowCount - 1)
            {
                for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
                {
                    Av1Plane plane = (Av1Plane)planeIndex;
                    int planeSubsamplingX = subsamplingX[planeIndex];
                    int planeSubsamplingY = subsamplingY[planeIndex];
                    int planeWidth = planeWidths[planeIndex];
                    int nextPlaneRow = ((unitModeInfoRow + CdefUnitModeInfoSize) << Av1Constants.ModeInfoSizeLog2) >> planeSubsamplingY;
                    int lineSlotOffset = lineBufferOffsets[planeIndex] +
                        ((unitRow & 1) * SourceBorder * planeWidth);

                    this.GetPlaneDestination(
                        plane,
                        planeSubsamplingX,
                        planeSubsamplingY,
                        out Span<byte> lowBitDepthDestination,
                        out Span<ushort> highBitDepthDestination,
                        out int destinationStride);

                    this.CopyFrameRectangle(
                        lowBitDepthDestination,
                        highBitDepthDestination,
                        destinationStride + ((nextPlaneRow - SourceBorder) * destinationStride),
                        destinationStride,
                        lineBuffer,
                        lineSlotOffset,
                        planeWidth,
                        planeWidth,
                        SourceBorder);
                }
            }

            for (int unitColumn = 0; unitColumn < unitColumnCount; unitColumn++)
            {
                int unitModeInfoColumn = unitColumn * CdefUnitModeInfoSize;
                int strengthIndex = this.GetStrengthIndex(unitModeInfoColumn, unitModeInfoRow);
                if (strengthIndex < 0)
                {
                    cdefLeft.Clear();
                    continue;
                }

                int yStrength = parameters.YStrength[strengthIndex];
                int uvStrength = parameters.UvStrength[strengthIndex];
                bool unitNeedsDirections = yStrength != 0 || (planeCount > 1 && uvStrength != 0);
                if (!unitNeedsDirections)
                {
                    cdefLeft.Clear();
                    continue;
                }

                int unitModeInfoRowEnd = Math.Min(unitModeInfoRow + CdefUnitModeInfoSize, this.frameHeader.ModeInfoRowCount);
                int unitModeInfoColumnEnd = Math.Min(unitModeInfoColumn + CdefUnitModeInfoSize, this.frameHeader.ModeInfoColumnCount);
                int blockCount = 0;

                for (int blockModeInfoRow = unitModeInfoRow; blockModeInfoRow < unitModeInfoRowEnd; blockModeInfoRow += 2)
                {
                    for (int blockModeInfoColumn = unitModeInfoColumn; blockModeInfoColumn < unitModeInfoColumnEnd; blockModeInfoColumn += 2)
                    {
                        if (this.IsBlockSkipped(blockModeInfoColumn, blockModeInfoRow))
                        {
                            continue;
                        }

                        blocks[blockCount++] = new CdefBlock(blockModeInfoColumn, blockModeInfoRow);
                    }
                }

                if (blockCount == 0)
                {
                    cdefLeft.Clear();
                    continue;
                }

                // Luma is always prepared first when either plane type needs CDEF because it owns
                // the direction search. Chroma then reuses those per-unit results without a frame map.
                for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
                {
                    if (planeIndex != (int)Av1Plane.Y && uvStrength == 0)
                    {
                        cdefLeft[planeIndex] = false;
                        continue;
                    }

                    int planeWidth = planeWidths[planeIndex];
                    int currentLineSlotOffset = lineBufferOffsets[planeIndex] +
                        (((unitRow - 1) & 1) * SourceBorder * planeWidth);
                    ReadOnlySpan<ushort> topLineBuffer = unitRow == 0
                        ? default
                        : lineBuffer.Slice(currentLineSlotOffset, SourceBorder * planeWidth);
                    Span<ushort> planeColumnBuffer = columnBuffer.Slice(
                        columnBufferOffsets[planeIndex],
                        columnBufferLengths[planeIndex]);

                    this.FilterPlane(
                        (Av1Plane)planeIndex,
                        subsamplingX[planeIndex],
                        subsamplingY[planeIndex],
                        unitModeInfoColumn,
                        unitModeInfoRow,
                        blocks[..blockCount],
                        directions,
                        variances,
                        yStrength,
                        uvStrength,
                        source,
                        topLineBuffer,
                        planeColumnBuffer,
                        cdefLeft[planeIndex]);

                    cdefLeft[planeIndex] = true;
                }
            }
        }
    }

    /// <summary>
    /// Filters one color plane in a CDEF unit from a bounded immutable source snapshot.
    /// </summary>
    /// <param name="plane">The color plane to filter.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <param name="subsamplingY">The vertical chroma subsampling shift.</param>
    /// <param name="unitModeInfoColumn">The unit's frame-relative column in 4x4 luma units.</param>
    /// <param name="unitModeInfoRow">The unit's frame-relative row in 4x4 luma units.</param>
    /// <param name="blocks">The unit's non-skipped 8x8 luma blocks.</param>
    /// <param name="directions">The unit-local luma directions in block-list order.</param>
    /// <param name="variances">The unit-local luma directional variances in block-list order.</param>
    /// <param name="yStrength">The coded luma strength.</param>
    /// <param name="uvStrength">The coded chroma strength.</param>
    /// <param name="source">The reusable bordered source-unit buffer.</param>
    /// <param name="topLineBuffer">The two preserved unfiltered rows immediately above this unit row.</param>
    /// <param name="columnBuffer">The preserved unfiltered columns immediately left of this unit.</param>
    /// <param name="leftPrepared">Whether the preceding unit overwrote samples needed by this unit.</param>
    private void FilterPlane(
        Av1Plane plane,
        int subsamplingX,
        int subsamplingY,
        int unitModeInfoColumn,
        int unitModeInfoRow,
        ReadOnlySpan<CdefBlock> blocks,
        Span<int> directions,
        Span<int> variances,
        int yStrength,
        int uvStrength,
        Span<ushort> source,
        ReadOnlySpan<ushort> topLineBuffer,
        Span<ushort> columnBuffer,
        bool leftPrepared)
    {
        int planeWidth = this.frameHeader.ModeInfoColumnCount << (Av1Constants.ModeInfoSizeLog2 - subsamplingX);
        int planeHeight = this.frameHeader.ModeInfoRowCount << (Av1Constants.ModeInfoSizeLog2 - subsamplingY);
        int planeColumn = (unitModeInfoColumn << Av1Constants.ModeInfoSizeLog2) >> subsamplingX;
        int planeRow = (unitModeInfoRow << Av1Constants.ModeInfoSizeLog2) >> subsamplingY;
        int unitWidth = Math.Min(MaximumUnitPlaneSize >> subsamplingX, planeWidth - planeColumn);
        int unitHeight = Math.Min(MaximumUnitPlaneSize >> subsamplingY, planeHeight - planeRow);
        bool hasLeft = planeColumn > 0;
        bool hasRight = planeColumn + unitWidth < planeWidth;
        bool hasTop = planeRow > 0;
        bool hasBottom = planeRow + unitHeight < planeHeight;
        int leftSampleCount = hasLeft ? SourceBorder : 0;
        int rightSampleCount = hasRight ? SourceBorder : 0;
        int copyColumn = planeColumn - leftSampleCount;
        int copyWidth = leftSampleCount + unitWidth + rightSampleCount;
        int sourceColumn = SourceBorder - leftSampleCount;

        // CDEF output must never become input to a later unit. libaom therefore reconstructs a
        // bordered unit from saved top/left samples and still-unmodified frame samples. Filling first
        // also gives every unavailable frame-edge tap the normative CDEF_VERY_LARGE sentinel.
        source.Fill(Av1CdefFilter.VeryLarge);

        this.GetPlaneDestination(
            plane,
            subsamplingX,
            subsamplingY,
            out Span<byte> lowBitDepthDestination,
            out Span<ushort> highBitDepthDestination,
            out int destinationStride);

        if (hasTop)
        {
            Av1CdefFilter.CopyPlane(
                topLineBuffer,
                copyColumn,
                planeWidth,
                source,
                sourceColumn,
                SourceStride,
                copyWidth,
                SourceBorder);
        }

        this.CopyFrameRectangle(
            lowBitDepthDestination,
            highBitDepthDestination,
            destinationStride + (planeRow * destinationStride) + copyColumn,
            destinationStride,
            source,
            (SourceBorder * SourceStride) + sourceColumn,
            SourceStride,
            copyWidth,
            unitHeight);

        if (hasBottom)
        {
            this.CopyFrameRectangle(
                lowBitDepthDestination,
                highBitDepthDestination,
                destinationStride + ((planeRow + unitHeight) * destinationStride) + copyColumn,
                destinationStride,
                source,
                ((SourceBorder + unitHeight) * SourceStride) + sourceColumn,
                SourceStride,
                copyWidth,
                SourceBorder);
        }

        int preservedHeight = SourceBorder + unitHeight + (hasBottom ? SourceBorder : 0);
        if (leftPrepared)
        {
            Av1CdefFilter.CopyPlane(
                columnBuffer,
                0,
                SourceBorder,
                source,
                0,
                SourceStride,
                SourceBorder,
                preservedHeight);
        }

        // Save the final unfiltered columns before this unit writes its destination. The next unit
        // restores them over the frame samples that this unit has already replaced.
        Av1CdefFilter.CopyPlane(
            source,
            unitWidth,
            SourceStride,
            columnBuffer,
            0,
            SourceBorder,
            SourceBorder,
            preservedHeight);

        ObuConstraintDirectionalEnhancementFilterParameters parameters = this.frameHeader.CdefParameters;
        int coefficientShift = Math.Max(this.frameBuffer.BitDepth.GetBitCount() - 8, 0);
        int blockWidth = 8 >> subsamplingX;
        int blockHeight = 8 >> subsamplingY;
        int codedStrength = plane == Av1Plane.Y ? yStrength : uvStrength;
        int primaryStrength = (codedStrength / 4) << coefficientShift;
        int secondaryStrength = codedStrength % 4;

        // The two-bit secondary field leaves value three unused and represents strength four instead.
        secondaryStrength += secondaryStrength == 3 ? 1 : 0;
        secondaryStrength <<= coefficientShift;
        int damping = parameters.Damping + coefficientShift - (plane == Av1Plane.Y ? 0 : 1);

        if (plane == Av1Plane.Y)
        {
            int blockIndex = 0;

            // The reference decoder analyzes two listed 8x8 blocks together. The per-unit fixed list preserves that traversal
            // without allocating a managed block list or repeating four skip-map lookups during filtering.
            for (; blockIndex < blocks.Length - 1; blockIndex += 2)
            {
                CdefBlock firstBlock = blocks[blockIndex];
                CdefBlock secondBlock = blocks[blockIndex + 1];

                Av1CdefFilter.FindDirections(
                    source,
                    firstBlock.GetSourceOffset(SourceStride, SourceBorder, unitModeInfoColumn, unitModeInfoRow, 0, 0),
                    secondBlock.GetSourceOffset(SourceStride, SourceBorder, unitModeInfoColumn, unitModeInfoRow, 0, 0),
                    SourceStride,
                    coefficientShift,
                    out directions[blockIndex],
                    out variances[blockIndex],
                    out directions[blockIndex + 1],
                    out variances[blockIndex + 1]);
            }

            if (blockIndex < blocks.Length)
            {
                CdefBlock block = blocks[blockIndex];

                directions[blockIndex] = Av1CdefFilter.FindDirection(
                    source,
                    block.GetSourceOffset(SourceStride, SourceBorder, unitModeInfoColumn, unitModeInfoRow, 0, 0),
                    SourceStride,
                    coefficientShift,
                    out variances[blockIndex]);
            }
        }

        if (codedStrength == 0)
        {
            return;
        }

        for (int blockIndex = 0; blockIndex < blocks.Length; blockIndex++)
        {
            CdefBlock block = blocks[blockIndex];
            int filteredPrimaryStrength = plane == Av1Plane.Y
                ? Av1CdefFilter.AdjustStrength(primaryStrength, variances[blockIndex])
                : primaryStrength;

            if (filteredPrimaryStrength == 0 && secondaryStrength == 0)
            {
                continue;
            }

            // Secondary-only filtering uses direction zero; otherwise chroma remaps the
            // luma direction into its asymmetrically subsampled sample grid when required.
            int direction = primaryStrength != 0
                ? Av1CdefFilter.ConvertDirection(directions[blockIndex], subsamplingX, subsamplingY)
                : 0;
            int blockPlaneColumn = (block.ModeInfoColumn << Av1Constants.ModeInfoSizeLog2) >> subsamplingX;
            int blockPlaneRow = (block.ModeInfoRow << Av1Constants.ModeInfoSizeLog2) >> subsamplingY;
            int blockSourceOffset = block.GetSourceOffset(
                SourceStride,
                SourceBorder,
                unitModeInfoColumn,
                unitModeInfoRow,
                subsamplingX,
                subsamplingY);

            int blockDestinationOffset = destinationStride + (blockPlaneRow * destinationStride) + blockPlaneColumn;

            if (this.frameBuffer.BytesPerSample == 2)
            {
                Av1CdefFilter.FilterBlock(
                    source,
                    blockSourceOffset,
                    SourceStride,
                    highBitDepthDestination,
                    blockDestinationOffset,
                    destinationStride,
                    filteredPrimaryStrength,
                    secondaryStrength,
                    direction,
                    damping,
                    damping,
                    coefficientShift,
                    blockWidth,
                    blockHeight);
            }
            else
            {
                Av1CdefFilter.FilterBlock(
                    source,
                    blockSourceOffset,
                    SourceStride,
                    lowBitDepthDestination,
                    blockDestinationOffset,
                    destinationStride,
                    filteredPrimaryStrength,
                    secondaryStrength,
                    direction,
                    damping,
                    damping,
                    coefficientShift,
                    blockWidth,
                    blockHeight);
            }
        }
    }

    /// <summary>
    /// Gets the byte or native 16-bit destination span for one frame plane.
    /// </summary>
    /// <param name="plane">The color plane.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <param name="subsamplingY">The vertical chroma subsampling shift.</param>
    /// <param name="lowBitDepthDestination">Receives the byte destination for an eight-bit frame.</param>
    /// <param name="highBitDepthDestination">Receives the native destination for a high-bit-depth frame.</param>
    /// <param name="destinationStride">Receives the number of samples between adjacent rows.</param>
    private void GetPlaneDestination(
        Av1Plane plane,
        int subsamplingX,
        int subsamplingY,
        out Span<byte> lowBitDepthDestination,
        out Span<ushort> highBitDepthDestination,
        out int destinationStride)
    {
        lowBitDepthDestination = default;
        highBitDepthDestination = default;

        if (this.frameBuffer.BytesPerSample == 2)
        {
            Span<short> signedDestination = this.frameBuffer.DeriveBlockPointer16(
                plane,
                Point.Empty,
                subsamplingX,
                subsamplingY,
                out destinationStride);

            highBitDepthDestination = MemoryMarshal.Cast<short, ushort>(signedDestination);
        }
        else
        {
            lowBitDepthDestination = this.frameBuffer.DeriveBlockPointer(
                plane,
                Point.Empty,
                subsamplingX,
                subsamplingY,
                out destinationStride);
        }
    }

    /// <summary>
    /// Copies one frame rectangle into 16-bit CDEF working storage.
    /// </summary>
    /// <param name="lowBitDepthSource">The byte source for an eight-bit frame.</param>
    /// <param name="highBitDepthSource">The native source for a high-bit-depth frame.</param>
    /// <param name="sourceOffset">The offset of the rectangle's top-left source sample.</param>
    /// <param name="sourceStride">The number of samples between adjacent source rows.</param>
    /// <param name="destination">The 16-bit working destination.</param>
    /// <param name="destinationOffset">The offset of the rectangle's top-left destination sample.</param>
    /// <param name="destinationStride">The number of samples between adjacent destination rows.</param>
    /// <param name="width">The rectangle width in samples.</param>
    /// <param name="height">The rectangle height in samples.</param>
    private void CopyFrameRectangle(
        ReadOnlySpan<byte> lowBitDepthSource,
        ReadOnlySpan<ushort> highBitDepthSource,
        int sourceOffset,
        int sourceStride,
        Span<ushort> destination,
        int destinationOffset,
        int destinationStride,
        int width,
        int height)
    {
        if (this.frameBuffer.BytesPerSample == 2)
        {
            Av1CdefFilter.CopyPlane(
                highBitDepthSource,
                sourceOffset,
                sourceStride,
                destination,
                destinationOffset,
                destinationStride,
                width,
                height);
        }
        else
        {
            Av1CdefFilter.CopyPlane(
                lowBitDepthSource,
                sourceOffset,
                sourceStride,
                destination,
                destinationOffset,
                destinationStride,
                width,
                height);
        }
    }

    /// <summary>
    /// Gets the strength-table selection assigned to a 64x64 CDEF unit.
    /// </summary>
    /// <param name="modeInfoColumn">The unit's frame-relative column in 4x4 luma units.</param>
    /// <param name="modeInfoRow">The unit's frame-relative row in 4x4 luma units.</param>
    /// <returns>The strength-table index, or minus one when every block in the unit is skipped.</returns>
    private int GetStrengthIndex(int modeInfoColumn, int modeInfoRow)
    {
        int superblockModeInfoSize = this.frameInfo.SuperblockModeInfoSize;
        Point superblockPosition = new(
            modeInfoColumn / superblockModeInfoSize,
            modeInfoRow / superblockModeInfoSize);

        int unitColumn = (modeInfoColumn % superblockModeInfoSize) / CdefUnitModeInfoSize;
        int unitRow = (modeInfoRow % superblockModeInfoSize) / CdefUnitModeInfoSize;

        // A 128x128 superblock stores four raster-ordered 64x64 selections; the same
        // expression naturally resolves to index zero for a 64x64 superblock.
        int unitIndex = unitColumn + (unitRow << 1);
        return this.frameInfo.GetCdefStrength(superblockPosition)[unitIndex];
    }

    /// <summary>
    /// Determines whether every 4x4 mode-information block covered by an 8x8 CDEF block is skipped.
    /// </summary>
    /// <param name="modeInfoColumn">The block's frame-relative column in 4x4 luma units.</param>
    /// <param name="modeInfoRow">The block's frame-relative row in 4x4 luma units.</param>
    /// <returns><see langword="true"/> when the complete 8x8 block is skipped; otherwise, <see langword="false"/>.</returns>
    private bool IsBlockSkipped(int modeInfoColumn, int modeInfoRow)
    {
        for (int row = 0; row < 2; row++)
        {
            for (int column = 0; column < 2; column++)
            {
                if (!this.frameInfo.GetModeInfoAt(new Point(modeInfoColumn + column, modeInfoRow + row)).Skip)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Identifies one 8x8 luma block by its frame-relative mode-information coordinates.
    /// </summary>
    private readonly struct CdefBlock
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CdefBlock"/> struct.
        /// </summary>
        /// <param name="modeInfoColumn">The frame-relative column in 4x4 luma units.</param>
        /// <param name="modeInfoRow">The frame-relative row in 4x4 luma units.</param>
        public CdefBlock(int modeInfoColumn, int modeInfoRow)
        {
            this.ModeInfoColumn = modeInfoColumn;
            this.ModeInfoRow = modeInfoRow;
        }

        /// <summary>
        /// Gets the frame-relative column in 4x4 luma units.
        /// </summary>
        public int ModeInfoColumn { get; }

        /// <summary>
        /// Gets the frame-relative row in 4x4 luma units.
        /// </summary>
        public int ModeInfoRow { get; }

        /// <summary>
        /// Gets the block offset in a bordered CDEF source unit.
        /// </summary>
        /// <param name="sourceStride">The number of samples between adjacent source rows.</param>
        /// <param name="sourceBorder">The number of unavailable samples surrounding the source.</param>
        /// <param name="unitModeInfoColumn">The unit's frame-relative column in 4x4 luma units.</param>
        /// <param name="unitModeInfoRow">The unit's frame-relative row in 4x4 luma units.</param>
        /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
        /// <param name="subsamplingY">The vertical chroma subsampling shift.</param>
        /// <returns>The source-unit offset.</returns>
        public int GetSourceOffset(
            int sourceStride,
            int sourceBorder,
            int unitModeInfoColumn,
            int unitModeInfoRow,
            int subsamplingX,
            int subsamplingY)
        {
            int planeColumn = ((this.ModeInfoColumn - unitModeInfoColumn) << Av1Constants.ModeInfoSizeLog2) >> subsamplingX;
            int planeRow = ((this.ModeInfoRow - unitModeInfoRow) << Av1Constants.ModeInfoSizeLog2) >> subsamplingY;
            return ((planeRow + sourceBorder) * sourceStride) + planeColumn + sourceBorder;
        }
    }
}
