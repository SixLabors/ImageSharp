// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

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
        int[] directions = new int[lumaBlockColumnCount * lumaBlockRowCount];
        int[] variances = new int[directions.Length];
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
        int[] directions,
        int[] variances,
        int lumaBlockColumnCount)
    {
        int planeWidth = this.frameHeader.ModeInfoColumnCount << (Av1Constants.ModeInfoSizeLog2 - subsamplingX);
        int planeHeight = this.frameHeader.ModeInfoRowCount << (Av1Constants.ModeInfoSizeLog2 - subsamplingY);
        int sourceStride = planeWidth + (SourceBorder * 2);

        // CDEF output must never become input to a later block. The sentinel border also makes
        // frame-edge taps follow AV1 without exposing the frame buffer's prediction padding.
        ushort[] source = new ushort[(planeHeight + (SourceBorder * 2)) * sourceStride];
        Array.Fill(source, Av1CdefFilter.VeryLarge);

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

        for (int row = 0; row < planeHeight; row++)
        {
            Span<ushort> sourceRow = source.AsSpan(
                ((row + SourceBorder) * sourceStride) + SourceBorder,
                planeWidth);

            int destinationOffset = destinationStride + (row * destinationStride);
            if (this.frameBuffer.BytesPerSample == 2)
            {
                highBitDepthDestination.Slice(destinationOffset, planeWidth).CopyTo(sourceRow);
            }
            else
            {
                Span<byte> destinationRow = lowBitDepthDestination.Slice(destinationOffset, planeWidth);
                for (int column = 0; column < planeWidth; column++)
                {
                    sourceRow[column] = destinationRow[column];
                }
            }
        }

        ObuConstraintDirectionalEnhancementFilterParameters parameters = this.frameHeader.CdefParameters;
        int coefficientShift = Math.Max(this.frameBuffer.BitDepth.GetBitCount() - 8, 0);
        int blockWidth = 8 >> subsamplingX;
        int blockHeight = 8 >> subsamplingY;
        int unitColumnCount = (this.frameHeader.ModeInfoColumnCount + CdefUnitModeInfoSize - 1) / CdefUnitModeInfoSize;
        int unitRowCount = (this.frameHeader.ModeInfoRowCount + CdefUnitModeInfoSize - 1) / CdefUnitModeInfoSize;
        Span<ushort> filteredBlock = stackalloc ushort[8 * 8];

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

                for (int blockModeInfoRow = unitModeInfoRow; blockModeInfoRow < unitModeInfoRowEnd; blockModeInfoRow += 2)
                {
                    for (int blockModeInfoColumn = unitModeInfoColumn; blockModeInfoColumn < unitModeInfoColumnEnd; blockModeInfoColumn += 2)
                    {
                        if (this.IsBlockSkipped(blockModeInfoColumn, blockModeInfoRow))
                        {
                            continue;
                        }

                        int lumaBlockRow = blockModeInfoRow >> 1;
                        int lumaBlockColumn = blockModeInfoColumn >> 1;
                        int directionIndex = (lumaBlockRow * lumaBlockColumnCount) + lumaBlockColumn;
                        int planeColumn = (blockModeInfoColumn << Av1Constants.ModeInfoSizeLog2) >> subsamplingX;
                        int planeRow = (blockModeInfoRow << Av1Constants.ModeInfoSizeLog2) >> subsamplingY;
                        int sourceOffset = ((planeRow + SourceBorder) * sourceStride) + planeColumn + SourceBorder;

                        if (plane == Av1Plane.Y)
                        {
                            directions[directionIndex] = Av1CdefFilter.FindDirection(
                                source,
                                sourceOffset,
                                sourceStride,
                                coefficientShift,
                                out variances[directionIndex]);
                        }

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

                        Av1CdefFilter.FilterBlock(
                            source,
                            sourceOffset,
                            sourceStride,
                            filteredBlock,
                            0,
                            blockWidth,
                            filteredPrimaryStrength,
                            secondaryStrength,
                            direction,
                            damping,
                            damping,
                            coefficientShift,
                            blockWidth,
                            blockHeight);

                        int destinationOffset = destinationStride + (planeRow * destinationStride) + planeColumn;
                        for (int row = 0; row < blockHeight; row++)
                        {
                            ReadOnlySpan<ushort> filteredRow = filteredBlock.Slice(row * blockWidth, blockWidth);
                            if (this.frameBuffer.BytesPerSample == 2)
                            {
                                filteredRow.CopyTo(highBitDepthDestination.Slice(destinationOffset + (row * destinationStride), blockWidth));
                            }
                            else
                            {
                                Span<byte> destinationRow = lowBitDepthDestination.Slice(
                                    destinationOffset + (row * destinationStride),
                                    blockWidth);

                                for (int column = 0; column < blockWidth; column++)
                                {
                                    destinationRow[column] = (byte)filteredRow[column];
                                }
                            }
                        }
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
}
