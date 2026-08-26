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
internal class Av1CdefDecoder
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

        int lumaBlockColumnCount = this.frameHeader.ModeInfoColumnCount >> 1;
        int lumaBlockRowCount = this.frameHeader.ModeInfoRowCount >> 1;
        int mapLength = lumaBlockColumnCount * lumaBlockRowCount;
        MemoryAllocator allocator = this.frameBuffer.MemoryAllocator;
        using IMemoryOwner<int> directionOwner = allocator.Allocate<int>(mapLength, AllocationOptions.Clean);
        using IMemoryOwner<int> varianceOwner = allocator.Allocate<int>(mapLength, AllocationOptions.Clean);
        Span<int> directions = directionOwner.Memory.Span[..mapLength];
        Span<int> variances = varianceOwner.Memory.Span[..mapLength];
        ObuColorConfig colorConfig = this.sequenceHeader.ColorConfig;

        // Luma must be processed first even when its strengths are zero because chroma CDEF
        // consumes directions derived from the immutable, deblocked luma source.
        for (int planeIndex = 0; planeIndex < colorConfig.PlaneCount; planeIndex++)
        {
            Av1Plane plane = (Av1Plane)planeIndex;
            int subsamplingX = plane != Av1Plane.Y && colorConfig.SubSamplingX ? 1 : 0;
            int subsamplingY = plane != Av1Plane.Y && colorConfig.SubSamplingY ? 1 : 0;
            this.FilterPlane(plane, subsamplingX, subsamplingY, directions, variances, lumaBlockColumnCount);
        }
    }

    /// <summary>
    /// Filters one color plane from an immutable snapshot of its deblocked samples.
    /// </summary>
    /// <param name="plane">The color plane to filter.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <param name="subsamplingY">The vertical chroma subsampling shift.</param>
    /// <param name="directions">The frame-wide luma direction map in 8x8 block order.</param>
    /// <param name="variances">The frame-wide luma directional-variance map in 8x8 block order.</param>
    /// <param name="lumaBlockColumnCount">The number of 8x8 blocks in an aligned luma row.</param>
    private void FilterPlane(
        Av1Plane plane,
        int subsamplingX,
        int subsamplingY,
        Span<int> directions,
        Span<int> variances,
        int lumaBlockColumnCount)
    {
        int planeWidth = this.frameHeader.ModeInfoColumnCount << (Av1Constants.ModeInfoSizeLog2 - subsamplingX);
        int planeHeight = this.frameHeader.ModeInfoRowCount << (Av1Constants.ModeInfoSizeLog2 - subsamplingY);
        int sourceStride = planeWidth + (SourceBorder * 2);
        int sourceLength = (planeHeight + (SourceBorder * 2)) * sourceStride;
        using IMemoryOwner<ushort> sourceOwner = this.frameBuffer.MemoryAllocator.Allocate<ushort>(sourceLength);
        Span<ushort> source = sourceOwner.Memory.Span[..sourceLength];

        // CDEF output must never become input to a later block. The sentinel border also makes
        // frame-edge taps follow AV1 without exposing the frame buffer's prediction padding. Every
        // sample in the requested working allocation is initialized before any filter can read it.
        source.Fill(Av1CdefFilter.VeryLarge);

        Span<byte> lowBitDepthDestination = default;
        Span<ushort> highBitDepthDestination = default;
        int destinationStride;
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

        int sourceOffset = (SourceBorder * sourceStride) + SourceBorder;
        int destinationOffset = destinationStride;
        if (this.frameBuffer.BytesPerSample == 2)
        {
            Av1CdefFilter.CopyPlane(
                highBitDepthDestination,
                destinationOffset,
                destinationStride,
                source,
                sourceOffset,
                sourceStride,
                planeWidth,
                planeHeight);
        }
        else
        {
            Av1CdefFilter.CopyPlane(
                lowBitDepthDestination,
                destinationOffset,
                destinationStride,
                source,
                sourceOffset,
                sourceStride,
                planeWidth,
                planeHeight);
        }

        ObuConstraintDirectionalEnhancementFilterParameters parameters = this.frameHeader.CdefParameters;
        int coefficientShift = Math.Max(this.frameBuffer.BitDepth.GetBitCount() - 8, 0);
        int blockWidth = 8 >> subsamplingX;
        int blockHeight = 8 >> subsamplingY;
        int unitColumnCount = (this.frameHeader.ModeInfoColumnCount + CdefUnitModeInfoSize - 1) / CdefUnitModeInfoSize;
        int unitRowCount = (this.frameHeader.ModeInfoRowCount + CdefUnitModeInfoSize - 1) / CdefUnitModeInfoSize;

        for (int unitRow = 0; unitRow < unitRowCount; unitRow++)
        {
            for (int unitColumn = 0; unitColumn < unitColumnCount; unitColumn++)
            {
                int unitModeInfoRow = unitRow * CdefUnitModeInfoSize;
                int unitModeInfoColumn = unitColumn * CdefUnitModeInfoSize;
                int strengthIndex = this.GetStrengthIndex(unitModeInfoColumn, unitModeInfoRow);
                if (strengthIndex < 0)
                {
                    continue;
                }

                int yStrength = parameters.YStrength[strengthIndex];
                int uvStrength = parameters.UvStrength[strengthIndex];
                bool unitNeedsDirections = yStrength != 0 ||
                    (this.sequenceHeader.ColorConfig.PlaneCount > 1 && uvStrength != 0);

                if ((plane == Av1Plane.Y && !unitNeedsDirections) || (plane != Av1Plane.Y && uvStrength == 0))
                {
                    continue;
                }

                int codedStrength = plane == Av1Plane.Y ? yStrength : uvStrength;
                int primaryStrength = (codedStrength / 4) << coefficientShift;
                int secondaryStrength = codedStrength % 4;

                // The two-bit secondary field leaves value three unused and represents strength four instead.
                secondaryStrength += secondaryStrength == 3 ? 1 : 0;
                secondaryStrength <<= coefficientShift;
                int damping = parameters.Damping + coefficientShift - (plane == Av1Plane.Y ? 0 : 1);
                int unitModeInfoRowEnd = Math.Min(unitModeInfoRow + CdefUnitModeInfoSize, this.frameHeader.ModeInfoRowCount);
                int unitModeInfoColumnEnd = Math.Min(unitModeInfoColumn + CdefUnitModeInfoSize, this.frameHeader.ModeInfoColumnCount);
                CdefBlockList blocks = default;
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

                if (plane == Av1Plane.Y)
                {
                    int blockIndex = 0;

                    // libaom analyzes two listed 8x8 blocks together. The per-unit fixed list preserves that traversal
                    // without allocating a managed block list or repeating four skip-map lookups during filtering.
                    for (; blockIndex < blockCount - 1; blockIndex += 2)
                    {
                        CdefBlock firstBlock = blocks[blockIndex];
                        CdefBlock secondBlock = blocks[blockIndex + 1];
                        int firstDirectionIndex = firstBlock.GetDirectionIndex(lumaBlockColumnCount);
                        int secondDirectionIndex = secondBlock.GetDirectionIndex(lumaBlockColumnCount);

                        Av1CdefFilter.FindDirections(
                            source,
                            firstBlock.GetSourceOffset(sourceStride, SourceBorder),
                            secondBlock.GetSourceOffset(sourceStride, SourceBorder),
                            sourceStride,
                            coefficientShift,
                            out directions[firstDirectionIndex],
                            out variances[firstDirectionIndex],
                            out directions[secondDirectionIndex],
                            out variances[secondDirectionIndex]);
                    }

                    if (blockIndex < blockCount)
                    {
                        CdefBlock block = blocks[blockIndex];
                        int directionIndex = block.GetDirectionIndex(lumaBlockColumnCount);

                        directions[directionIndex] = Av1CdefFilter.FindDirection(
                            source,
                            block.GetSourceOffset(sourceStride, SourceBorder),
                            sourceStride,
                            coefficientShift,
                            out variances[directionIndex]);
                    }
                }

                for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
                {
                    CdefBlock block = blocks[blockIndex];
                    int directionIndex = block.GetDirectionIndex(lumaBlockColumnCount);

                    if (codedStrength == 0)
                    {
                        continue;
                    }

                    int filteredPrimaryStrength = plane == Av1Plane.Y
                        ? Av1CdefFilter.AdjustStrength(primaryStrength, variances[directionIndex])
                        : primaryStrength;

                    if (filteredPrimaryStrength == 0 && secondaryStrength == 0)
                    {
                        continue;
                    }

                    // Secondary-only filtering uses direction zero; otherwise chroma remaps the
                    // luma direction into its asymmetrically subsampled sample grid when required.
                    int direction = primaryStrength != 0
                        ? Av1CdefFilter.ConvertDirection(directions[directionIndex], subsamplingX, subsamplingY)
                        : 0;
                    int planeColumn = (block.ModeInfoColumn << Av1Constants.ModeInfoSizeLog2) >> subsamplingX;
                    int planeRow = (block.ModeInfoRow << Av1Constants.ModeInfoSizeLog2) >> subsamplingY;
                    int blockSourceOffset = ((planeRow + SourceBorder) * sourceStride) + planeColumn + SourceBorder;
                    int blockDestinationOffset = destinationStride + (planeRow * destinationStride) + planeColumn;

                    if (this.frameBuffer.BytesPerSample == 2)
                    {
                        Av1CdefFilter.FilterBlock(
                            source,
                            blockSourceOffset,
                            sourceStride,
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
                            sourceStride,
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
    /// Stores the non-skipped blocks in one CDEF unit without a managed allocation.
    /// </summary>
    [InlineArray(MaximumBlocksPerUnit)]
    private struct CdefBlockList
    {
        /// <summary>
        /// The first block in the inline storage.
        /// </summary>
        private CdefBlock element0;
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
        /// Gets the frame-wide direction-map index for this block.
        /// </summary>
        /// <param name="lumaBlockColumnCount">The number of 8x8 blocks in an aligned luma row.</param>
        /// <returns>The direction-map index.</returns>
        public int GetDirectionIndex(int lumaBlockColumnCount)
            => ((this.ModeInfoRow >> 1) * lumaBlockColumnCount) + (this.ModeInfoColumn >> 1);

        /// <summary>
        /// Gets the offset of this luma block in the bordered CDEF source plane.
        /// </summary>
        /// <param name="sourceStride">The number of samples between adjacent source rows.</param>
        /// <param name="sourceBorder">The number of unavailable samples surrounding the source.</param>
        /// <returns>The source-plane offset.</returns>
        public int GetSourceOffset(int sourceStride, int sourceBorder)
        {
            int planeColumn = this.ModeInfoColumn << Av1Constants.ModeInfoSizeLog2;
            int planeRow = this.ModeInfoRow << Av1Constants.ModeInfoSizeLog2;
            return ((planeRow + sourceBorder) * sourceStride) + planeColumn + sourceBorder;
        }
    }
}
