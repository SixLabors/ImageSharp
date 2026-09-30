// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

internal static partial class Av1IntraSuperblockEncoder
{
    internal partial struct ModeDecision<TSample, TOperator>
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
        /// <summary>
        /// Sets every block of the superblock to the fixed partition size, or to the largest square size that fits
        /// the tile at its bottom and right edges, and records the partition each node reads from those sizes.
        /// Reference: av1_set_fixed_partitioning() with set_partial_sb_partition(), then the get_partition() calls of
        /// av1_rd_use_partition().
        /// </summary>
        /// <param name="tile">The tile that holds the superblock.</param>
        /// <param name="superblockOrigin">The luma origin of the superblock.</param>
        private readonly void PrepareFixedPartitions(Av1TileInfo tile, Point superblockOrigin)
        {
            Av1BlockSize superblockSize = this.picture.Sequence.SequenceHeader.SuperblockSize;
            int superblockModeInfoSize = superblockSize.Get4x4WideCount();
            int modeInfoRow = superblockOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
            int modeInfoColumn = superblockOrigin.X >> Av1Constants.ModeInfoSizeLog2;
            int rowsRemaining = tile.ModeInfoRowEnd - modeInfoRow;
            int columnsRemaining = tile.ModeInfoColumnEnd - modeInfoColumn;
            Av1BlockSize fixedSize = this.picture.Parent.FixedPartitionSize;
            int step = fixedSize.Get4x4WideCount();

            // Positions that no block starts at keep no size, as the mode information grid keeps an entry of an
            // earlier frame there, which no node of these square partitions reads.
            Span<byte> sizes = stackalloc byte[superblockModeInfoSize * superblockModeInfoSize];
            sizes.Fill((byte)Av1BlockSize.Invalid);
            if (columnsRemaining >= superblockModeInfoSize && rowsRemaining >= superblockModeInfoSize)
            {
                for (int row = 0; row < superblockModeInfoSize; row += step)
                {
                    for (int column = 0; column < superblockModeInfoSize; column += step)
                    {
                        sizes[(row * superblockModeInfoSize) + column] = (byte)fixedSize;
                    }
                }
            }
            else
            {
                // The block height carries from the last block of a row to the next row, as in the reference.
                int height = step;
                for (int row = 0; row < superblockModeInfoSize; row += height)
                {
                    int width = step;
                    for (int column = 0; column < superblockModeInfoSize; column += width)
                    {
                        sizes[(row * superblockModeInfoSize) + column] = (byte)FindPartitionSize(
                            fixedSize, rowsRemaining - row, columnsRemaining - column, ref height, ref width);
                    }
                }
            }

            this.SetFixedPartitionTypes(sizes, superblockModeInfoSize, modeInfoRow, modeInfoColumn, 0, 0, superblockSize, 0);
        }

        /// <summary>
        /// Returns the largest square size, from the requested size down, whose block fits the remaining rows and
        /// columns, and leaves the dimensions of the last size tried. Reference: find_partition_size().
        /// </summary>
        private static Av1BlockSize FindPartitionSize(Av1BlockSize blockSize, int rowsLeft, int columnsLeft, ref int height, ref int width)
        {
            if (rowsLeft <= 0 || columnsLeft <= 0)
            {
                return blockSize < Av1BlockSize.Block8x8 ? blockSize : Av1BlockSize.Block8x8;
            }

            int size = (int)blockSize;
            for (; size > 0; size -= 3)
            {
                height = ((Av1BlockSize)size).Get4x4HighCount();
                width = ((Av1BlockSize)size).Get4x4WideCount();
                if (height <= rowsLeft && width <= columnsLeft)
                {
                    break;
                }
            }

            return (Av1BlockSize)size;
        }

        /// <summary>
        /// Records the partition of a node inside the frame from the block sizes of the fixed partitioning, and the
        /// partitions of its split children. Reference: get_partition(), and the PARTITION_NONE of blocks smaller
        /// than 8x8 in av1_rd_use_partition().
        /// </summary>
        private readonly void SetFixedPartitionTypes(
            ReadOnlySpan<byte> sizes,
            int stride,
            int superblockRow,
            int superblockColumn,
            int row,
            int column,
            Av1BlockSize blockSize,
            int nodeIndex)
        {
            int modeInfoRowCount = this.picture.Parent.Common.ModeInfoRowCount;
            int modeInfoColumnCount = this.picture.Parent.Common.ModeInfoColumnCount;
            int modeInfoRow = superblockRow + row;
            int modeInfoColumn = superblockColumn + column;
            if (modeInfoRow >= modeInfoRowCount || modeInfoColumn >= modeInfoColumnCount)
            {
                return;
            }

            Span<byte> partitions = this.superblock.Workspace.PartitionSearchTypes;
            if (blockSize < Av1BlockSize.Block8x8)
            {
                partitions[nodeIndex] = (byte)Av1PartitionType.None;
                return;
            }

            Av1BlockSize subSize = (Av1BlockSize)sizes[(row * stride) + column];
            Av1PartitionType partition;
            int high = blockSize.Get4x4HighCount();
            int wide = blockSize.Get4x4WideCount();
            if (subSize == blockSize)
            {
                partition = Av1PartitionType.None;
            }
            else
            {
                int subHigh = subSize.Get4x4HighCount();
                int subWide = subSize.Get4x4WideCount();
                if (blockSize > Av1BlockSize.Block8x8 &&
                    modeInfoRow + (wide / 2) < modeInfoRowCount &&
                    modeInfoColumn + (high / 2) < modeInfoColumnCount)
                {
                    Av1BlockSize right = (Av1BlockSize)sizes[(row * stride) + column + (wide / 2)];
                    Av1BlockSize below = (Av1BlockSize)sizes[((row + (high / 2)) * stride) + column];
                    if (subWide == wide)
                    {
                        partition = subHigh * 4 == high ? Av1PartitionType.Horizontal4
                            : below == subSize ? Av1PartitionType.Horizontal : Av1PartitionType.HorizontalB;
                    }
                    else if (subHigh == high)
                    {
                        partition = subWide * 4 == wide ? Av1PartitionType.Vertical4
                            : right == subSize ? Av1PartitionType.Vertical : Av1PartitionType.VerticalB;
                    }
                    else if (subWide * 2 != wide || subHigh * 2 != high)
                    {
                        partition = Av1PartitionType.Split;
                    }
                    else
                    {
                        partition = below != Av1BlockSize.Invalid && below.Get4x4WideCount() == wide ? Av1PartitionType.HorizontalA
                            : right != Av1BlockSize.Invalid && right.Get4x4HighCount() == high ? Av1PartitionType.VerticalA
                            : Av1PartitionType.Split;
                    }
                }
                else
                {
                    bool verticalSplit = subWide < wide;
                    bool horizontalSplit = subHigh < high;
                    partition = verticalSplit && horizontalSplit ? Av1PartitionType.Split
                        : verticalSplit ? Av1PartitionType.Vertical : Av1PartitionType.Horizontal;
                }
            }

            partitions[nodeIndex] = (byte)partition;
            if (partition == Av1PartitionType.Split)
            {
                Av1BlockSize childSize = Av1PartitionType.Split.GetBlockSubSize(blockSize);
                int half = childSize.Get4x4WideCount();
                for (int child = 0; child < 4; child++)
                {
                    this.SetFixedPartitionTypes(
                        sizes,
                        stride,
                        superblockRow,
                        superblockColumn,
                        row + ((child >> 1) * half),
                        column + ((child & 1) * half),
                        childSize,
                        (nodeIndex * 4) + child + 1);
                }
            }
        }
    }
}
