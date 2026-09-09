// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Selects intra partitions from the spatial variance of rounded four-by-four averages.
/// </content>
internal static partial class Av1IntraSuperblockEncoder
{
    private static int GetPartitionVariance(int sum, uint sumSquares, int log2Count)
    {
        uint centeredSumSquares = sumSquares - (uint)(((long)sum * sum) >> log2Count);
        return (int)((256U * centeredSumSquares) >> log2Count);
    }

    private struct VariancePartitionNode
    {
        public int Sum;
        public uint SumSquares;
        public int Variance;
        public bool ForceParentSplit;
    }

    internal partial struct ModeDecision<TSample, TOperator>
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
        private void PrepareVariancePartitions(Av1MacroBlockD macroBlock, Point superblockOrigin)
        {
            int speed = (int)this.picture.Parent.EncodingSpeed;
            int frameWidth = this.picture.Parent.FrameHeader.FrameSize.FrameWidth;
            int frameHeight = this.picture.Parent.FrameHeader.FrameSize.FrameHeight;
            long threshold = 120L * Av1QuantizationLookup.GetAcQuant(this.quantization.QIndex[0], 0, this.bitDepth);
            bool largePartitions = speed >= 8 && Math.Min(frameWidth, frameHeight) >= 720;
            if (largePartitions && speed == 8)
            {
                threshold <<= 1;
            }

            bool smallFrame = (long)frameWidth * frameHeight < 1280 * 720;
            long threshold32 = smallFrame ? threshold / 3 : threshold >> (largePartitions ? 1 : 2);
            long threshold16 = threshold >> (smallFrame || largePartitions ? 1 : 2);

            // Partition analysis precedes residual coding. Its compact moment tree borrows coefficient scratch
            // only for this pass; the selected partition bytes survive after transform trials reuse that storage.
            Span<VariancePartitionNode> nodes = MemoryMarshal.Cast<int, VariancePartitionNode>(this.blockWorkspace.TransformCoefficients);
            this.BuildVariancePartitions(
                this.source.GetPlane(Av1Plane.Y),
                macroBlock.Tile,
                superblockOrigin,
                this.picture.Sequence.SequenceHeader.SuperblockSize,
                0,
                nodes,
                threshold32,
                threshold16,
                speed >= 9);
        }

        private void BuildVariancePartitions(
            Buffer2DRegion<TSample> sourcePlane,
            Av1TileInfo tile,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int nodeIndex,
            Span<VariancePartitionNode> nodes,
            long threshold32,
            long threshold16,
            bool pruneSixteenSplit)
        {
            int width = blockSize.GetWidth();
            ref VariancePartitionNode node = ref nodes[nodeIndex];
            node = default;
            if (width == 8)
            {
                // Each four-by-four average is one variance sample. Positions outside the coded frame
                // contribute zero moments, while present blocks retain their complete padded samples.
                for (int y = 0; y < 8; y += 4)
                {
                    for (int x = 0; x < 8; x += 4)
                    {
                        Point origin = blockOrigin + new Size(x, y);
                        if (origin.X < sourcePlane.Width && origin.Y < sourcePlane.Height)
                        {
                            int difference = TOperator.GetAverage4x4(sourcePlane, origin) - 128;
                            node.Sum += difference;
                            node.SumSquares += (uint)(difference * difference);
                        }
                    }
                }

                node.Variance = GetPartitionVariance(node.Sum, node.SumSquares, 2);
                this.superblock.Workspace.PartitionSearchTypes[nodeIndex] = (byte)Av1PartitionType.None;
                return;
            }

            int firstChild = (nodeIndex * 4) + 1;
            int halfWidth = width >> 1;
            Av1BlockSize childSize = Av1PartitionType.Split.GetBlockSubSize(blockSize);
            for (int child = 0; child < 4; child++)
            {
                Point childOrigin = blockOrigin + new Size((child & 1) * halfWidth, (child >> 1) * halfWidth);
                this.BuildVariancePartitions(
                    sourcePlane,
                    tile,
                    childOrigin,
                    childSize,
                    firstChild + child,
                    nodes,
                    threshold32,
                    threshold16,
                    pruneSixteenSplit);
            }

            // Intra variance partitioning always splits blocks larger than thirty-two samples.
            // Their moments cannot affect any surviving decision, so retain only their child partitions.
            if (width > 32)
            {
                this.superblock.Workspace.PartitionSearchTypes[nodeIndex] = (byte)Av1PartitionType.Split;
                node.ForceParentSplit = true;
                return;
            }

            int minimumVariance = int.MaxValue;
            int maximumVariance = 0;
            for (int child = 0; child < 4; child++)
            {
                ref VariancePartitionNode childNode = ref nodes[firstChild + child];
                node.Sum += childNode.Sum;
                node.SumSquares += childNode.SumSquares;
                node.ForceParentSplit |= childNode.ForceParentSplit;
                minimumVariance = Math.Min(minimumVariance, childNode.Variance);
                maximumVariance = Math.Max(maximumVariance, childNode.Variance);
            }

            int log2Count = 2 * (BitOperations.Log2((uint)width) - 2);
            node.Variance = GetPartitionVariance(node.Sum, node.SumSquares, log2Count);
            long threshold = width == 32 ? threshold32 : threshold16;
            bool aboveThreshold = node.Variance > threshold;
            node.ForceParentSplit |= aboveThreshold;

            int blockColumns = width >> Av1Constants.ModeInfoSizeLog2;
            int column = blockOrigin.X >> Av1Constants.ModeInfoSizeLog2;
            int row = blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
            int columnsRequired = blockColumns;
            int rowsRequired = blockColumns;
            int verticalColumnsRequired = blockColumns >> 1;
            int horizontalRowsRequired = blockColumns >> 1;
            if (this.picture.Sequence.SequenceHeader.SuperblockSize == Av1BlockSize.Block64x64)
            {
                // At the outer frame edge a sixty-four-sized superblock permits leaves whose midpoint
                // is present. Interior tile edges still require the whole leaf to fit within the tile.
                if (tile.ModeInfoColumnEnd == this.picture.Parent.Common.ModeInfoColumnCount)
                {
                    columnsRequired = (blockColumns >> 1) + 1;
                    verticalColumnsRequired = (blockColumns >> 2) + 1;
                }

                if (tile.ModeInfoRowEnd == this.picture.Parent.Common.ModeInfoRowCount)
                {
                    rowsRequired = (blockColumns >> 1) + 1;
                    horizontalRowsRequired = (blockColumns >> 2) + 1;
                }
            }

            bool fitsColumns = column + columnsRequired <= tile.ModeInfoColumnEnd;
            bool fitsRows = row + rowsRequired <= tile.ModeInfoRowEnd;
            Av1PartitionType partition = Av1PartitionType.Split;
            if (width == 16 && aboveThreshold && pruneSixteenSplit &&
                maximumVariance - minimumVariance <= (threshold16 << 2))
            {
                // Similar child variances permit one sixteen-by-sixteen block even when its own variance
                // is high. Its parent must still split, as recorded independently above.
                if (fitsColumns && fitsRows)
                {
                    partition = Av1PartitionType.None;
                }
            }
            else if (!node.ForceParentSplit)
            {
                if (fitsColumns && fitsRows && node.Variance < threshold)
                {
                    partition = Av1PartitionType.None;
                }
                else
                {
                    ref VariancePartitionNode topLeft = ref nodes[firstChild];
                    ref VariancePartitionNode topRight = ref nodes[firstChild + 1];
                    ref VariancePartitionNode bottomLeft = ref nodes[firstChild + 2];
                    ref VariancePartitionNode bottomRight = ref nodes[firstChild + 3];
                    int halfLog2Count = log2Count - 1;
                    Av1BlockSize verticalSize = Av1PartitionType.Vertical.GetBlockSubSize(blockSize);
                    Av1BlockSize horizontalSize = Av1PartitionType.Horizontal.GetBlockSubSize(blockSize);
                    bool subX = this.source.ChromaSubsamplingX != 0;
                    bool subY = this.source.ChromaSubsamplingY != 0;
                    if (fitsRows && column + verticalColumnsRequired <= tile.ModeInfoColumnEnd &&
                        verticalSize.GetSubsampled(subX, subY) != Av1BlockSize.Invalid &&
                        GetPartitionVariance(topLeft.Sum + bottomLeft.Sum, topLeft.SumSquares + bottomLeft.SumSquares, halfLog2Count) < threshold &&
                        GetPartitionVariance(topRight.Sum + bottomRight.Sum, topRight.SumSquares + bottomRight.SumSquares, halfLog2Count) < threshold)
                    {
                        partition = Av1PartitionType.Vertical;
                    }
                    else if (fitsColumns && row + horizontalRowsRequired <= tile.ModeInfoRowEnd &&
                        horizontalSize.GetSubsampled(subX, subY) != Av1BlockSize.Invalid &&
                        GetPartitionVariance(topLeft.Sum + topRight.Sum, topLeft.SumSquares + topRight.SumSquares, halfLog2Count) < threshold &&
                        GetPartitionVariance(bottomLeft.Sum + bottomRight.Sum, bottomLeft.SumSquares + bottomRight.SumSquares, halfLog2Count) < threshold)
                    {
                        partition = Av1PartitionType.Horizontal;
                    }
                }
            }

            this.superblock.Workspace.PartitionSearchTypes[nodeIndex] = (byte)partition;
        }
    }
}
