// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
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
        /// <summary>
        /// Resolves inter partition thresholds from quantization, resolution, source activity, and frame role.
        /// </summary>
        /// <param name="blockSad">The collocated 64x64 temporal source SAD.</param>
        /// <param name="boostedSegment">Whether cyclic refresh boosts this superblock.</param>
        /// <param name="quantizer">The superblock's effective quantizer index.</param>
        /// <param name="thresholds">The 128x128, 64x64, 32x32, and 16x16 thresholds.</param>
        private void GetInterVarianceThresholds(ulong blockSad, bool boostedSegment, int quantizer, Span<long> thresholds)
        {
            const int resolution288 = 352 * 288;
            const int resolution480 = 640 * 480;
            const int resolution720 = 1280 * 720;
            const int resolution1080 = 1920 * 1080;
            const int resolution1440 = 2560 * 1440;
            const int largeBlockQuantizer = 100;
            Av1PictureParentControlSet parent = this.picture.Parent;
            int speed = (int)parent.EncodingSpeed;
            bool screen = parent.IsScreenContent;
            bool nonReferenceFrame = parent.FrameHeader.RefreshFrameFlags == 0;
            int pixels = parent.FrameHeader.FrameSize.FrameWidth * parent.FrameHeader.FrameSize.FrameHeight;
            int frameQuantizer = this.quantization.QIndex[0];
            long basis = Av1QuantizationLookup.GetAcQuant(quantizer, 0, this.bitDepth);
            if (nonReferenceFrame && parent.FrameSourceSad != 0)
            {
                basis = (3 * basis) >> 1;
            }

            if (speed >= 8)
            {
                basis = (5 * basis) >> 2;
            }

            int splitShift = screen && speed >= 9 ? 10 : speed >= 9 ? 9 : speed >= 8 ? 8 : 7;
            thresholds[0] = basis >> 1;
            thresholds[1] = basis;
            thresholds[3] = basis << splitShift;
            if (pixels >= resolution720)
            {
                thresholds[3] <<= 1;
            }

            if (pixels <= resolution288)
            {
                int low = speed >= 9 ? 200 : speed >= 8 ? 170 : 200;
                int high = speed >= 9 ? 220 : speed >= 8 ? 220 : 210;
                if (frameQuantizer >= high)
                {
                    basis = (5 * basis) >> 1;
                    thresholds[1] = basis >> 3;
                    thresholds[2] = basis << 2;
                    thresholds[3] = basis << 5;
                }
                else if (frameQuantizer < low)
                {
                    thresholds[1] = basis >> 3;
                    thresholds[2] = basis >> 1;
                    thresholds[3] = basis << 3;
                }
                else
                {
                    long fromLow = frameQuantizer - low;
                    long fromHigh = high - frameQuantizer;
                    int interval = high - low;
                    long highBasis = (5 * basis) >> 1;
                    basis = ((fromLow * highBasis) + (fromHigh * basis)) / interval;
                    thresholds[1] = basis >> 3;
                    thresholds[2] = ((fromLow * basis) + (fromHigh * (basis >> 1))) / interval;
                    thresholds[3] = ((fromLow * (basis << 5)) + (fromHigh * (basis << 3))) / interval;
                }
            }
            else if (pixels < resolution720)
            {
                thresholds[2] = (5 * basis) >> 2;
            }
            else if (pixels < resolution1080)
            {
                thresholds[2] = basis << 1;
            }
            else
            {
                thresholds[2] = screen
                    ? ((pixels < resolution1440 ? 5 : 7) * basis) >> 1
                    : (speed > 7 ? 6 : 3) * basis;
            }

            int preference = parent.SpeedSettings.GetVariancePartitionPreference(screen && parent.HighSourceSad, nonReferenceFrame);
            if (preference >= 3)
            {
                // These quantizer weights change in integer steps. Keeping the division integral
                // preserves the threshold transitions at the ends of the quantizer window.
                double weight = frameQuantizer < largeBlockQuantizer - 20 ? 1
                    : frameQuantizer > largeBlockQuantizer + 20 ? 0
                    : 1D - ((frameQuantizer - largeBlockQuantizer + 20) / 40);

                if (pixels > resolution480)
                {
                    for (int index = 0; index < 4; index++)
                    {
                        thresholds[index] <<= 1;
                    }
                }

                if (pixels <= resolution288)
                {
                    thresholds[3] = long.MaxValue;
                    thresholds[1] <<= boostedSegment ? 1 : 2;
                    thresholds[2] <<= boostedSegment ? 3 : this.sourceSadLevel <= Av1SourceSadLevel.Low ? 5 : 4;
                    if (!boostedSegment && parent.AverageSourceSad < 25000 && blockSad > 25000 && blockSad < 50000 &&
                        !this.sourceLightingChange)
                    {
                        thresholds[2] = (3 * thresholds[2]) >> 2;
                        thresholds[3] = thresholds[2] << 3;
                    }
                }
                else if (pixels > resolution480 && !boostedSegment &&
                    (this.sourceSadLevel != Av1SourceSadLevel.High || parent.AverageSourceSad > 50000))
                {
                    thresholds[0] = (3 * thresholds[0]) >> 1;
                    thresholds[3] = long.MaxValue;
                    if (frameQuantizer > largeBlockQuantizer)
                    {
                        thresholds[1] = (int)(((1 - weight) * (thresholds[1] << 1)) + (weight * thresholds[1]));
                        thresholds[2] = (int)(((1 - weight) * (thresholds[2] << 1)) + (weight * thresholds[2]));
                    }
                }
                else if (frameQuantizer > largeBlockQuantizer && !boostedSegment &&
                    (this.sourceSadLevel != Av1SourceSadLevel.High || parent.AverageSourceSad > 50000))
                {
                    thresholds[1] = (int)(((1 - weight) * (thresholds[1] << 2)) + (weight * thresholds[1]));
                    thresholds[2] = (int)(((1 - weight) * (thresholds[2] << 4)) + (weight * thresholds[2]));
                    thresholds[3] = long.MaxValue;
                }
            }
            else if (preference >= 2)
            {
                if (this.sourceSadLevel <= Av1SourceSadLevel.Low)
                {
                    thresholds[1] <<= 2;
                    thresholds[2] *= 3;
                }
            }
            else if (preference >= 1)
            {
                int shift = this.sourceSadLevel <= Av1SourceSadLevel.Low ? 2 : 1;
                double weight = frameQuantizer < largeBlockQuantizer - 45 ? 1
                    : frameQuantizer > largeBlockQuantizer + 45 ? 0
                    : 1D - ((frameQuantizer - largeBlockQuantizer + 45) / 90);

                thresholds[1] = (int)(((1 - weight) * (thresholds[1] << 1)) + (weight * thresholds[1]));
                thresholds[2] = (int)(((1 - weight) * (thresholds[2] << 1)) + (weight * thresholds[2]));
                thresholds[3] = (int)(((1 - weight) * (thresholds[3] << shift)) + (weight * thresholds[3]));
            }
        }

        /// <summary>
        /// Builds temporal partition moments and selects square or rectangular leaves in coding order.
        /// </summary>
        /// <param name="macroBlock">The containing tile and frame boundaries.</param>
        /// <param name="superblockOrigin">The origin of the supplied prediction.</param>
        /// <param name="blockOrigin">The current square's luma origin.</param>
        /// <param name="blockSize">The current square size.</param>
        /// <param name="nodeIndex">The breadth-first moment-tree position.</param>
        /// <param name="prediction">The selected superblock prediction, including its edge padding.</param>
        /// <param name="predictionStride">The prediction row stride.</param>
        /// <param name="thresholds">The four square-size variance thresholds.</param>
        /// <param name="nodes">Scratch moments retained until low-variance flags have been derived.</param>
        private void BuildInterVariancePartitions(
            Av1MacroBlockD macroBlock,
            Point superblockOrigin,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int nodeIndex,
            ReadOnlySpan<TSample> prediction,
            int predictionStride,
            ReadOnlySpan<long> thresholds,
            Span<VariancePartitionNode> nodes)
        {
            int width = blockSize.GetWidth();
            ref VariancePartitionNode node = ref nodes[nodeIndex];
            node = default;
            Buffer2DRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            if (width == 8)
            {
                if (blockOrigin.X < sourcePlane.Width && blockOrigin.Y < sourcePlane.Height)
                {
                    int x = blockOrigin.X - superblockOrigin.X;
                    int y = blockOrigin.Y - superblockOrigin.Y;
                    int sourceAverage = TOperator.GetAverage8x8(
                        Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, blockOrigin), sourcePlane.Stride);

                    int predictionAverage = TOperator.GetAverage8x8(prediction[((y * predictionStride) + x)..], predictionStride);
                    node.Sum = sourceAverage - predictionAverage;
                    node.SumSquares = (uint)(node.Sum * node.Sum);
                }

                this.superblock.Workspace.PartitionSearchTypes[nodeIndex] = (byte)Av1PartitionType.None;
                return;
            }

            int firstChild = (nodeIndex * 4) + 1;
            int half = width >> 1;
            Av1BlockSize childSize = Av1PartitionType.Split.GetBlockSubSize(blockSize);
            int minimum = int.MaxValue;
            int maximum = 0;
            int childVarianceSum = 0;
            for (int child = 0; child < 4; child++)
            {
                Point childOrigin = blockOrigin + new Size((child & 1) * half, (child >> 1) * half);
                this.BuildInterVariancePartitions(
                    macroBlock,
                    superblockOrigin,
                    childOrigin,
                    childSize,
                    firstChild + child,
                    prediction,
                    predictionStride,
                    thresholds,
                    nodes);

                ref VariancePartitionNode childNode = ref nodes[firstChild + child];
                node.Sum += childNode.Sum;
                node.SumSquares += childNode.SumSquares;
                node.ForceParentSplit |= childNode.ForceParentSplit;
                minimum = Math.Min(minimum, childNode.Variance);
                maximum = Math.Max(maximum, childNode.Variance);
                childVarianceSum += childNode.Variance;
            }

            int sizeLog2 = BitOperations.Log2((uint)width);
            int countLog2 = 2 * (sizeLog2 - 3);
            node.Variance = GetPartitionVariance(node.Sum, node.SumSquares, countLog2);
            long threshold = thresholds[7 - sizeLog2];
            Av1PictureParentControlSet parent = this.picture.Parent;
            int pixels = parent.FrameHeader.FrameSize.FrameWidth * parent.FrameHeader.FrameSize.FrameHeight;
            int preference = parent.SpeedSettings.GetVariancePartitionPreference(
                parent.IsScreenContent && parent.HighSourceSad, parent.FrameHeader.RefreshFrameFlags == 0);

            // A forced child split propagates upward before variance-based merges. A parent's
            // low mean variance must not erase a small moving region inside one of its children.
            if (width == 16)
            {
                node.ForceParentSplit |= node.Variance > threshold;
            }
            else if (width == 32)
            {
                node.ForceParentSplit |= node.Variance > threshold ||
                    (node.Variance > (threshold >> 1) && node.Variance > (childVarianceSum >> 1));

                if (pixels <= 640 * 360)
                {
                    node.ForceParentSplit |= (maximum - minimum > (threshold >> 1) && maximum > threshold) ||
                        (preference != 0 && this.sourceSadLevel > Av1SourceSadLevel.Low && parent.FrameSourceSad < 20000 &&
                            maximum > (threshold >> 4) && maximum > (minimum << 2));
                }
            }
            else if (width == 64)
            {
                node.ForceParentSplit |= preference != 0 && maximum - minimum > 3 * (threshold >> 3) && maximum > (threshold >> 1);
            }
            else
            {
                node.ForceParentSplit |= node.Variance > ((9 * childVarianceSum) >> 5) ||
                    (maximum - minimum > 3 * (threshold >> 3) && maximum > (threshold >> 1));
            }

            Av1TileInfo tile = macroBlock.Tile;
            int blockColumns = width >> Av1Constants.ModeInfoSizeLog2;
            int column = blockOrigin.X >> Av1Constants.ModeInfoSizeLog2;
            int row = blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
            int columnsRequired = blockColumns;
            int rowsRequired = blockColumns;
            int verticalColumnsRequired = blockColumns >> 1;
            int horizontalRowsRequired = blockColumns >> 1;
            if (this.picture.Sequence.SequenceHeader.SuperblockSize == Av1BlockSize.Block64x64)
            {
                if (tile.ModeInfoColumnEnd == parent.Common.ModeInfoColumnCount)
                {
                    columnsRequired = (blockColumns >> 1) + 1;
                    verticalColumnsRequired = (blockColumns >> 2) + 1;
                }

                if (tile.ModeInfoRowEnd == parent.Common.ModeInfoRowCount)
                {
                    rowsRequired = (blockColumns >> 1) + 1;
                    horizontalRowsRequired = (blockColumns >> 2) + 1;
                }
            }

            bool fitsColumns = column + columnsRequired <= tile.ModeInfoColumnEnd;
            bool fitsRows = row + rowsRequired <= tile.ModeInfoRowEnd;
            Av1PartitionType partition = Av1PartitionType.Split;
            if (!node.ForceParentSplit && (width != 128 || (fitsColumns && fitsRows)))
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
                    Av1BlockSize verticalSize = Av1PartitionType.Vertical.GetBlockSubSize(blockSize);
                    Av1BlockSize horizontalSize = Av1PartitionType.Horizontal.GetBlockSubSize(blockSize);
                    bool subX = this.source.ChromaSubsamplingX != 0;
                    bool subY = this.source.ChromaSubsamplingY != 0;
                    if (fitsRows && column + verticalColumnsRequired <= tile.ModeInfoColumnEnd &&
                        verticalSize.GetSubsampled(subX, subY) != Av1BlockSize.Invalid &&
                        GetPartitionVariance(topLeft.Sum + bottomLeft.Sum, topLeft.SumSquares + bottomLeft.SumSquares, countLog2 - 1) < threshold &&
                        GetPartitionVariance(topRight.Sum + bottomRight.Sum, topRight.SumSquares + bottomRight.SumSquares, countLog2 - 1) < threshold)
                    {
                        partition = Av1PartitionType.Vertical;
                    }
                    else if (fitsColumns && row + horizontalRowsRequired <= tile.ModeInfoRowEnd &&
                        horizontalSize.GetSubsampled(subX, subY) != Av1BlockSize.Invalid &&
                        GetPartitionVariance(topLeft.Sum + topRight.Sum, topLeft.SumSquares + topRight.SumSquares, countLog2 - 1) < threshold &&
                        GetPartitionVariance(bottomLeft.Sum + bottomRight.Sum, bottomLeft.SumSquares + bottomRight.SumSquares, countLog2 - 1) < threshold)
                    {
                        partition = Av1PartitionType.Horizontal;
                    }
                }
            }

            this.superblock.Workspace.PartitionSearchTypes[nodeIndex] = (byte)partition;
        }

        /// <summary>
        /// Retains low temporal variance for the selected square and rectangular partition leaves.
        /// </summary>
        /// <param name="origin">The superblock's luma origin.</param>
        /// <param name="nodes">The complete temporal moment tree.</param>
        /// <param name="thresholds">The four square-size partition thresholds.</param>
        private void SetInterLowVarianceFlags(Point origin, ReadOnlySpan<VariancePartitionNode> nodes, ReadOnlySpan<long> thresholds)
        {
            Span<byte> flags = this.superblock.Workspace.LowVarianceFlags;
            flags.Clear();
            ReadOnlySpan<byte> partitions = this.superblock.Workspace.PartitionSearchTypes;
            int side = this.picture.Sequence.SequenceHeader.SuperblockSize.GetWidth();
            Av1PartitionType root = (Av1PartitionType)partitions[0];
            if (root == Av1PartitionType.None)
            {
                flags[0] = (byte)(nodes[0].Variance < (thresholds[0] >> 1) ? 1 : 0);
                return;
            }

            if (root is Av1PartitionType.Horizontal or Av1PartitionType.Vertical)
            {
                int start = root == Av1PartitionType.Horizontal ? 1 : 3;
                for (int half = 0; half < 2; half++)
                {
                    int variance = GetPartitionHalfVariance(nodes, 0, side, root, half);
                    flags[start + half] = (byte)(variance < (thresholds[0] >> 2) ? 1 : 0);
                }

                return;
            }

            bool small = side == 64;
            int count64 = small ? 1 : 4;
            int frameWidth = this.picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2;
            int frameHeight = this.picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2;
            for (int index64 = 0; index64 < count64; index64++)
            {
                Point origin64 = origin + new Size((index64 & 1) * 64, (index64 >> 1) * 64);
                if (origin64.X >= frameWidth || origin64.Y >= frameHeight)
                {
                    continue;
                }

                int node64 = small ? 0 : 1 + index64;
                Av1PartitionType partition64 = (Av1PartitionType)partitions[node64];
                long threshold64 = (5 * thresholds[1]) >> 3;
                if (!small && partition64 == Av1PartitionType.None)
                {
                    flags[5 + index64] = (byte)(nodes[node64].Variance < threshold64 ? 1 : 0);
                    continue;
                }

                if (!small && partition64 is Av1PartitionType.Horizontal or Av1PartitionType.Vertical)
                {
                    int start = (partition64 == Av1PartitionType.Horizontal ? 9 : 17) + (index64 * 2);
                    for (int half = 0; half < 2; half++)
                    {
                        int variance = GetPartitionHalfVariance(nodes, node64, 64, partition64, half);
                        flags[start + half] = (byte)(variance < (threshold64 >> 1) ? 1 : 0);
                    }

                    continue;
                }

                for (int index32 = 0; index32 < 4; index32++)
                {
                    Point origin32 = origin64 + new Size((index32 & 1) * 32, (index32 >> 1) * 32);
                    if (origin32.X >= frameWidth || origin32.Y >= frameHeight)
                    {
                        continue;
                    }

                    int node32 = (node64 * 4) + 1 + index32;
                    Av1PartitionType partition32 = (Av1PartitionType)partitions[node32];
                    if (partition32 == Av1PartitionType.None)
                    {
                        long threshold32 = (5 * thresholds[small ? 1 : 2]) >> 3;
                        int flag = small ? 5 + index32 : 25 + (index64 * 4) + index32;
                        flags[flag] = (byte)(nodes[node32].Variance < threshold32 ? 1 : 0);
                    }
                    else
                    {
                        int first16 = (node32 * 4) + 1;

                        // The first leaf selects whether this 32x32 region uses sixteen-sample
                        // activity flags. Rectangles consume the flag at their top-left 16x16 cell.
                        if (partition32 is Av1PartitionType.Horizontal or Av1PartitionType.Vertical ||
                            partitions[first16] == (byte)Av1PartitionType.None)
                        {
                            int start = small ? 9 + (index32 * 4) : 41 + (index64 * 16) + (index32 * 4);
                            long threshold16 = thresholds[small ? 2 : 3] >> 8;
                            for (int index16 = 0; index16 < 4; index16++)
                            {
                                flags[start + index16] = (byte)(nodes[first16 + index16].Variance < threshold16 ? 1 : 0);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Reads the retained temporal-variance decision for one coding block.
        /// </summary>
        /// <param name="origin">The coding block's luma origin.</param>
        /// <param name="blockSize">The selected coding-block size.</param>
        /// <returns>Whether the block can omit secondary-reference motion search.</returns>
        private bool HasLowTemporalVariance(Point origin, Av1BlockSize blockSize)
        {
            ReadOnlySpan<byte> flags = this.superblock.Workspace.LowVarianceFlags;
            int x = origin.X & 127;
            int y = origin.Y & 127;
            int index64 = ((y >> 6) * 2) + (x >> 6);
            int index32 = (((y & 63) >> 5) * 2) + ((x & 63) >> 5);
            int index16 = (((y & 31) >> 4) * 2) + ((x & 31) >> 4);
            int index;
            if (this.picture.Sequence.SequenceHeader.SuperblockSize == Av1BlockSize.Block64x64)
            {
                index = blockSize switch
                {
                    Av1BlockSize.Block64x64 => 0,
                    Av1BlockSize.Block64x32 => 1 + ((y & 63) >> 5),
                    Av1BlockSize.Block32x64 => 3 + ((x & 63) >> 5),
                    Av1BlockSize.Block32x32 => 5 + index32,
                    Av1BlockSize.Block32x16 or Av1BlockSize.Block16x32 or Av1BlockSize.Block16x16 => 9 + (index32 * 4) + index16,
                    _ => -1
                };
            }
            else
            {
                index = blockSize switch
                {
                    Av1BlockSize.Block128x128 => 0,
                    Av1BlockSize.Block128x64 => 1 + (y >> 6),
                    Av1BlockSize.Block64x128 => 3 + (x >> 6),
                    Av1BlockSize.Block64x64 => 5 + index64,
                    Av1BlockSize.Block64x32 => 9 + (index64 * 2) + ((y & 63) >> 5),
                    Av1BlockSize.Block32x64 => 17 + (index64 * 2) + ((x & 63) >> 5),
                    Av1BlockSize.Block32x32 => 25 + (index64 * 4) + index32,
                    Av1BlockSize.Block32x16 or Av1BlockSize.Block16x32 or Av1BlockSize.Block16x16
                        => 41 + (index64 * 16) + (index32 * 4) + index16,
                    _ => -1
                };
            }

            return index >= 0 && flags[index] != 0;
        }

        /// <summary>
        /// Combines the two child moment sets belonging to a horizontal or vertical half.
        /// </summary>
        /// <param name="nodes">The complete moment tree.</param>
        /// <param name="nodeIndex">The square parent's tree position.</param>
        /// <param name="side">The parent width in samples.</param>
        /// <param name="partition">The half orientation.</param>
        /// <param name="half">Zero for the first half or one for the second.</param>
        /// <returns>The centered variance of the selected half.</returns>
        private static int GetPartitionHalfVariance(
            ReadOnlySpan<VariancePartitionNode> nodes,
            int nodeIndex,
            int side,
            Av1PartitionType partition,
            int half)
        {
            bool horizontal = partition == Av1PartitionType.Horizontal;
            int first = (nodeIndex * 4) + 1 + (horizontal ? half * 2 : half);
            int second = first + (horizontal ? 1 : 2);
            int countLog2 = (2 * (BitOperations.Log2((uint)side) - 3)) - 1;
            return GetPartitionVariance(
                nodes[first].Sum + nodes[second].Sum, nodes[first].SumSquares + nodes[second].SumSquares, countLog2);
        }

        /// <summary>
        /// Selects the temporal predictor used by partition variance and measures its chroma activity.
        /// </summary>
        /// <param name="macroBlock">The superblock's neighboring syntax and frame edges.</param>
        /// <param name="origin">The superblock's luma origin.</param>
        /// <param name="predictionStride">The row stride of the returned luma prediction.</param>
        /// <param name="lastSad">The selected LAST prediction's absolute-difference sum.</param>
        /// <returns>The borrowed luma prediction for partition moments.</returns>
        private ReadOnlySpan<TSample> PrepareInterVariancePrediction(
            Av1MacroBlockD macroBlock, Point origin, out int predictionStride, out uint lastSad)
        {
            Av1PictureParentControlSet parent = this.picture.Parent;
            Av1BlockSize blockSize = this.picture.Sequence.SequenceHeader.SuperblockSize;
            int side = blockSize.GetWidth();
            Size frameSize = new(parent.FrameHeader.FrameSize.FrameWidth, parent.FrameHeader.FrameSize.FrameHeight);
            Buffer2DRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            Buffer2DRegion<TSample> lastPlane = this.reference.GetPlane(Av1Plane.Y);
            ReadOnlySpan<TSample> source = Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, origin);
            ReadOnlySpan<TSample> lastStorage = lastPlane.Buffer.DangerousGetSingleSpan();
            int lastOrigin = ((lastPlane.Bounds.Y + origin.Y) * lastPlane.Stride) + lastPlane.Bounds.X + origin.X;
            int precisionShift = this.bitDepth.GetBitCount() - 8;
            uint spatialVariance = this.sourceSadLevel > Av1SourceSadLevel.Low ? (uint)this.GetSourceVariance(origin, blockSize) : uint.MaxValue;
            uint goldenSad = uint.MaxValue;
            if (this.hasDistinctGoldenReference && this.sourceSadLevel != Av1SourceSadLevel.Zero)
            {
                Buffer2DRegion<TSample> golden = this.goldenReference.GetPlane(Av1Plane.Y);
                goldenSad = (uint)TOperator.SumAbsoluteDifferences(
                    source, sourcePlane.Stride, Av1TransformBlockEncoder.GetPlaneSpan(golden, origin), golden.Stride, side, side, 1) >> precisionShift;
            }

            // ALTREF takes part when the non-RD search uses it, alone or in the LAST_ALTREF compound pair.
            // Reference: use_alt_ref in setup_planes().
            uint alternateSad = uint.MaxValue;
            bool useAlternate = parent.SpeedSettings.UseEstimatedAlternateReference || parent.SpeedSettings.UseEstimatedCompound;
            if (useAlternate && (parent.AvailableReferenceMask & (1 << (int)Av1ReferenceFrameType.Alternate)) != 0 &&
                this.sourceSadLevel != Av1SourceSadLevel.Zero)
            {
                Buffer2DRegion<TSample> alternate = this.references.Span[(int)Av1ReferenceFrameType.Alternate].CodedView.GetPlane(Av1Plane.Y);
                alternateSad = (uint)TOperator.SumAbsoluteDifferences(
                    source, sourcePlane.Stride, Av1TransformBlockEncoder.GetPlaneSpan(alternate, origin), alternate.Stride, side, side, 1) >> precisionShift;
            }

            this.partitionMotion = default;
            this.usePartitionMotion = false;
            int motionLevel = precisionShift != 0 ? 0 : parent.IsScreenContent ? 1 : parent.EncodingSpeed >= HeifEncodingSpeed.Level9 ? 3 : 2;
            if (motionLevel > 2 && this.sourceSadLevel > Av1SourceSadLevel.Medium)
            {
                motionLevel = 2;
            }

            Rectangle frameBounds = Av1MotionVector.GetFrameSearchBounds(
                new Rectangle(origin, new Size(side, side)),
                new Size(sourcePlane.Width, sourcePlane.Height),
                Math.Min(lastPlane.Bounds.X, lastPlane.Bounds.Y));

            Rectangle integerBounds = default(Av1MotionVector).GetFullPixelSearchBounds(frameBounds);
            lastSad = uint.MaxValue;
            if ((motionLevel == 1 || motionLevel == 2) && macroBlock.ToRightEdge >= 0 && macroBlock.ToBottomEdge >= 0 &&
                spatialVariance > 100 && this.sourceSadLevel > Av1SourceSadLevel.Low)
            {
                bool largeSearch = parent.IsScreenContent ||
                    (this.sourceSadLevel > Av1SourceSadLevel.Medium && (long)frameSize.Width * frameSize.Height > 1280 * 720);

                int maximumRange = parent.IsScreenContent ? 512 : 256;
                int horizontalRange = largeSearch ? this.sourceSadLevel > Av1SourceSadLevel.Medium ? maximumRange : 96 : side >> 1;
                int verticalRange = largeSearch ? this.sourceSadLevel > Av1SourceSadLevel.Medium ? maximumRange : 192 : side >> 1;
                if ((long)frameSize.Width * frameSize.Height >= 3840 * 2160)
                {
                    horizontalRange <<= 1;
                    verticalRange <<= 1;
                }

                lastSad = Av1MotionSearchBase.SearchProjection(
                    MemoryMarshal.Cast<TSample, byte>(source),
                    sourcePlane.Stride,
                    MemoryMarshal.Cast<TSample, byte>(lastStorage),
                    lastPlane.Stride,
                    lastOrigin,
                    new Rectangle(origin, new Size(side, side)),
                    frameSize,
                    Math.Min(lastPlane.Bounds.X, lastPlane.Bounds.Y),
                    horizontalRange,
                    verticalRange,
                    parent.IsScreenContent,
                    largeSearch,
                    integerBounds,
                    MemoryMarshal.Cast<int, short>(this.blockWorkspace.TransformCoefficients),
                    out this.partitionMotion,
                    out uint zeroSad);

                if (largeSearch)
                {
                    this.usePartitionMotion = lastSad < (zeroSad >> 1) && lastSad < (side == 128 ? 50000U : 20000U);
                    this.superblockMotion = this.partitionMotion;
                    if (!this.usePartitionMotion)
                    {
                        this.partitionMotion = default;
                        lastSad = zeroSad;
                    }
                }
            }

            if (lastSad == uint.MaxValue)
            {
                lastSad = (uint)TOperator.SumAbsoluteDifferences(
                    source, sourcePlane.Stride, lastStorage[lastOrigin..], lastPlane.Stride, side, side, 1) >> precisionShift;
            }

            if (motionLevel >= 2)
            {
                InlineArray2<Av1MotionVector> neighbors = default;
                InlineArray2<uint> errors = default;
                errors[0] = uint.MaxValue;
                errors[1] = uint.MaxValue;
                Rectangle fractionalBounds = default(Av1MotionVector).GetSubpixelSearchBounds(frameBounds);
                Point position = new(origin.X >> Av1Constants.ModeInfoSizeLog2, origin.Y >> Av1Constants.ModeInfoSizeLog2);
                for (int index = 0; index < 2; index++)
                {
                    bool available = index == 0 ? macroBlock.IsUpAvailable : macroBlock.IsLeftAvailable;
                    if (!available)
                    {
                        continue;
                    }

                    Av1EncoderBlockModeInfo neighbor = macroBlock.GetRelativeModeInfo(index == 0 ? -macroBlock.ModeInfoStride : -1).Block;
                    if (neighbor.Mode < Av1PredictionMode.SingleInterModeStart || neighbor.ReferenceFrame != Av1ReferenceFrameType.Last)
                    {
                        continue;
                    }

                    Point neighborPosition = position + (index == 0 ? new Size(0, -1) : new Size(-1, 0));

                    // get_fullmv_from_mv() rounds the clamped vector to the nearest full sample (GET_MV_RAWPEL).
                    Av1MotionVector motion = this.picture.GetDisplacementVector(neighborPosition);
                    int row = Math.Clamp(motion.Row, fractionalBounds.Top, fractionalBounds.Bottom - 1);
                    int column = Math.Clamp(motion.Column, fractionalBounds.Left, fractionalBounds.Right - 1);
                    motion = new Av1MotionVector(
                        ((row + 3 + (row >= 0 ? 1 : 0)) >> 3) << 3,
                        ((column + 3 + (column >= 0 ? 1 : 0)) >> 3) << 3);

                    neighbors[index] = motion;
                    if (motion == this.partitionMotion || (index == 1 && motion == neighbors[0]))
                    {
                        continue;
                    }

                    int offset = lastOrigin + ((motion.Row >> 3) * lastPlane.Stride) + (motion.Column >> 3);
                    errors[index] = (uint)TOperator.SumAbsoluteDifferences(
                        source, sourcePlane.Stride, lastStorage[offset..], lastPlane.Stride, side, side, 1) >> precisionShift;
                }

                uint multiplier = motionLevel > 2 && this.sourceSadLevel > Av1SourceSadLevel.Low ? 7U : 8U;
                if (errors[0] < ((multiplier * lastSad) >> 3) && errors[0] < errors[1])
                {
                    lastSad = errors[0];
                    this.partitionMotion = neighbors[0];
                }

                if (errors[1] < ((multiplier * lastSad) >> 3) && errors[1] < errors[0])
                {
                    lastSad = errors[1];
                    this.partitionMotion = neighbors[1];
                }
            }

            this.partitionReference = Av1ReferenceFrameType.Last;
            this.estimatedReferencePruning = parent.SpeedSettings.GetEstimatedReferencePruningLevel(parent.IsScreenContent);

            // set_ref_frame_for_partition(): GOLDEN or ALTREF, whichever has the lower error, replaces LAST when
            // its error is below 0.9 of LAST's.
            if (goldenSad < 0.9 * lastSad && goldenSad < alternateSad)
            {
                this.partitionReference = Av1ReferenceFrameType.Golden;
            }
            else if (alternateSad < 0.9 * lastSad && alternateSad < goldenSad)
            {
                this.partitionReference = Av1ReferenceFrameType.Alternate;
            }

            if (this.partitionReference != Av1ReferenceFrameType.Last)
            {
                this.partitionMotion = default;
                this.usePartitionMotion = false;
                this.estimatedReferencePruning = 0;
            }

            Av1EncoderInterPredictionWorkspace<TSample> workspace = this.blockWorkspace.GetInterPredictionWorkspace<TSample>();
            bool zeroMotion = this.partitionMotion.IsZero;
            Av1EncoderFrame<TSample>.PlanarView selected = this.partitionReference switch
            {
                Av1ReferenceFrameType.Golden => this.goldenReference,
                Av1ReferenceFrameType.Alternate => this.references.Span[(int)Av1ReferenceFrameType.Alternate].CodedView,
                _ => this.reference
            };

            if (!zeroMotion)
            {
                int planes = this.source.IsMonochrome ? 1 : 3;
                for (int planeIndex = 0; planeIndex < planes; planeIndex++)
                {
                    Av1Plane plane = (Av1Plane)planeIndex;
                    int subX = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingX;
                    int subY = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingY;
                    Span<TSample> prediction = planeIndex == 0 ? workspace.LumaPrediction
                        : planeIndex == 1 ? workspace.BluePrediction : workspace.RedPrediction;

                    this.PrepareInterPlanePrediction(
                        this.partitionMotion,
                        default,
                        plane,
                        Av1PredictionMode.NearestMotionVector,
                        this.partitionReference,
                        Av1ReferenceFrameType.None,
                        false,
                        Av1CompoundType.Average,
                        0,
                        false,
                        default,
                        Av1InterpolationFilter.Bilinear,
                        Av1InterpolationFilter.Bilinear,
                        selected.GetPlane(plane),
                        selected.GetPlane(plane),
                        origin,
                        subX,
                        subY,
                        blockSize,
                        prediction,
                        workspace.Residual);
                }
            }

            this.superblockColorSensitivity[..].Clear();
            this.goldenColorSensitivity[..].Clear();
            this.alternateColorSensitivity[..].Clear();
            this.superblockChromaSad[..].Clear();
            if (!this.source.IsMonochrome)
            {
                // The screen branches of chroma_check() test the screen tune content, which libavif never sets,
                // not the detected screen content. Reference: tune_cfg.content == AOM_CONTENT_SCREEN.
                int upperShift = 1;
                int lowerShift = 3;
                long pixels = (long)frameSize.Width * frameSize.Height;
                int chromaFactor = pixels >= 1920 * 1080 ? 3 : 5;
                if (this.sourceSadLevel >= Av1SourceSadLevel.Medium && spatialVariance > 500 && pixels >= 640 * 360)
                {
                    upperShift = 2;
                    lowerShift = this.sourceSadLevel > Av1SourceSadLevel.Medium ? 5 : 4;
                }

                int subX = this.source.ChromaSubsamplingX;
                int subY = this.source.ChromaSubsamplingY;
                Point chromaOrigin = new(origin.X >> subX, origin.Y >> subY);
                int width = side >> subX;
                int height = side >> subY;
                for (int index = 0; index < 2; index++)
                {
                    Av1Plane plane = index == 0 ? Av1Plane.U : Av1Plane.V;
                    Buffer2DRegion<TSample> chromaSource = this.source.GetPlane(plane);

                    // chroma_check() measures zero motion against LAST, whichever reference the partition chose:
                    // pre[0] when that is LAST, otherwise the LAST buffer through setup_pred_plane().
                    Buffer2DRegion<TSample> chromaReference = this.references.Span[(int)Av1ReferenceFrameType.Last].CodedView.GetPlane(plane);
                    ReadOnlySpan<TSample> prediction = zeroMotion
                        ? Av1TransformBlockEncoder.GetPlaneSpan(chromaReference, chromaOrigin)
                        : index == 0 ? workspace.BluePrediction : workspace.RedPrediction;

                    uint sad = (uint)TOperator.SumAbsoluteDifferences(
                        Av1TransformBlockEncoder.GetPlaneSpan(chromaSource, chromaOrigin),
                        chromaSource.Stride,
                        prediction,
                        zeroMotion ? chromaReference.Stride : width,
                        width,
                        height,
                        1) >> precisionShift;

                    this.superblockChromaSad[index] = sad;
                    this.superblockColorSensitivity[index] = (byte)(sad > (lastSad >> upperShift) ? 1 : sad < (lastSad >> lowerShift) ? 0 : 2);
                    if (goldenSad != uint.MaxValue)
                    {
                        Buffer2DRegion<TSample> golden = this.goldenReference.GetPlane(plane);
                        uint sadGolden = (uint)TOperator.SumAbsoluteDifferences(
                            Av1TransformBlockEncoder.GetPlaneSpan(chromaSource, chromaOrigin),
                            chromaSource.Stride,
                            Av1TransformBlockEncoder.GetPlaneSpan(golden, chromaOrigin),
                            golden.Stride,
                            width,
                            height,
                            1) >> precisionShift;

                        this.goldenColorSensitivity[index] = (byte)(sadGolden > goldenSad / chromaFactor ? 1 : 0);
                    }

                    if (alternateSad != uint.MaxValue)
                    {
                        Buffer2DRegion<TSample> alternate = this.references.Span[(int)Av1ReferenceFrameType.Alternate].CodedView.GetPlane(plane);
                        uint sadAlternate = (uint)TOperator.SumAbsoluteDifferences(
                            Av1TransformBlockEncoder.GetPlaneSpan(chromaSource, chromaOrigin),
                            chromaSource.Stride,
                            Av1TransformBlockEncoder.GetPlaneSpan(alternate, chromaOrigin),
                            alternate.Stride,
                            width,
                            height,
                            1) >> precisionShift;

                        this.alternateColorSensitivity[index] = (byte)(sadAlternate > alternateSad / chromaFactor ? 1 : 0);
                    }
                }
            }

            if (zeroMotion)
            {
                Buffer2DRegion<TSample> plane = selected.GetPlane(Av1Plane.Y);
                predictionStride = plane.Stride;
                return Av1TransformBlockEncoder.GetPlaneSpan(plane, origin);
            }

            predictionStride = side;
            return workspace.LumaPrediction;
        }

        /// <summary>
        /// Filters a low-motion source superblock in place before partition and prediction analysis.
        /// </summary>
        /// <param name="macroBlock">The superblock's already-encoded neighbors.</param>
        /// <param name="origin">The superblock's luma origin.</param>
        private void FilterTemporalSource(Av1MacroBlockD macroBlock, Point origin)
        {
            Point position = new(origin.X >> Av1Constants.ModeInfoSizeLog2, origin.Y >> Av1Constants.ModeInfoSizeLog2);
            for (int index = 0; index < 2; index++)
            {
                bool available = index == 0 ? macroBlock.IsUpAvailable : macroBlock.IsLeftAvailable;
                if (available)
                {
                    Av1EncoderBlockModeInfo neighbor = macroBlock.GetRelativeModeInfo(index == 0 ? -macroBlock.ModeInfoStride : -1).Block;
                    if (neighbor.ReferenceFrame > Av1ReferenceFrameType.Intra)
                    {
                        Av1MotionVector vector = this.picture.GetDisplacementVector(position + (index == 0 ? new Size(0, -1) : new Size(-1, 0)));
                        if (Math.Abs(vector.Row) > 24 || Math.Abs(vector.Column) > 24)
                        {
                            return;
                        }
                    }
                }
            }

            Av1PictureParentControlSet parent = this.picture.Parent;
            int side = this.picture.Sequence.SequenceHeader.SuperblockSize.GetWidth();
            Buffer2DRegion<TSample> luma = this.source.GetPlane(Av1Plane.Y);
            Buffer2DRegion<byte> previousLuma = parent.PreviousSource.GetPlane(Av1Plane.Y);
            ReadOnlySpan<byte> source = MemoryMarshal.Cast<TSample, byte>(Av1TransformBlockEncoder.GetPlaneSpan(luma, origin));
            ReadOnlySpan<byte> previous = previousLuma.Buffer.DangerousGetSingleSpan();
            int previousOffset = ((previousLuma.Bounds.Y + origin.Y) * previousLuma.Stride) + previousLuma.Bounds.X + origin.X;
            int columns = (parent.FrameHeader.ModeInfoColumnCount + 15) >> 4;
            uint stationarySad = (uint)parent.SourceBlockSad.Span[((origin.Y >> 6) * columns) + (origin.X >> 6)];
            uint threshold = (5 * stationarySad) >> 3;
            ReadOnlySpan<int> offsets = [-previousLuma.Stride, -1, 1, previousLuma.Stride];
            foreach (int offset in offsets)
            {
                uint sad = (uint)Av1MotionSearchBase.ByteOperator.SumAbsoluteDifferences(
                    source, luma.Stride, previous[(previousOffset + offset)..], previousLuma.Stride, side, side, 1);

                if (threshold >= sad)
                {
                    return;
                }
            }

            // The source owner is rotated only after the frame finishes. The preceding source stays
            // read-only throughout this pass; averaging changes the current planes without an extra frame.
            int planeCount = this.source.IsMonochrome ? 1 : 3;
            for (int index = 0; index < planeCount; index++)
            {
                int subX = index == 0 ? 0 : this.source.ChromaSubsamplingX;
                int subY = index == 0 ? 0 : this.source.ChromaSubsamplingY;
                Point planeOrigin = new(origin.X >> subX, origin.Y >> subY);
                Buffer2DRegion<TSample> currentPlane = this.source.GetPlane((Av1Plane)index);
                Buffer2DRegion<byte> previousPlane = parent.PreviousSource.GetPlane((Av1Plane)index);
                Span<byte> currentSamples = MemoryMarshal.Cast<TSample, byte>(Av1TransformBlockEncoder.GetPlaneSpan(currentPlane, planeOrigin));
                ReadOnlySpan<byte> previousSamples = Av1TransformBlockEncoder.GetPlaneSpan(previousPlane, planeOrigin);
                int width = side >> subX;
                int height = side >> subY;
                for (int y = 0; y < height; y++)
                {
                    Span<byte> currentRow = currentSamples.Slice(y * currentPlane.Stride, width);
                    ReadOnlySpan<byte> previousRow = previousSamples.Slice(y * previousPlane.Stride, width);
                    for (int x = 0; x < width; x += Vector128<byte>.Count)
                    {
                        Vector128<byte> current = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(currentRow), (nuint)x);
                        Vector128<byte> prior = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(previousRow), (nuint)x);

                        // Eight unsigned sixteen-bit lanes hold each half's sums, bounded by 510.
                        // Shifting before narrowing gives the floor average required at odd sums.
                        Vector128<ushort> low = (Vector128.WidenLower(current) + Vector128.WidenLower(prior)) >> 1;
                        Vector128<ushort> high = (Vector128.WidenUpper(current) + Vector128.WidenUpper(prior)) >> 1;
                        Vector128.Narrow(low, high).StoreUnsafe(ref MemoryMarshal.GetReference(currentRow), (nuint)x);
                    }
                }
            }
        }

        private void PrepareVariancePartitions(Av1MacroBlockD macroBlock, Point superblockOrigin)
        {
            if (!this.picture.Parent.FrameHeader.IsIntra)
            {
                if (this.filterTemporalSource)
                {
                    this.FilterTemporalSource(macroBlock, superblockOrigin);
                }

                Av1PictureParentControlSet parent = this.picture.Parent;
                int side = this.picture.Sequence.SequenceHeader.SuperblockSize.GetWidth();
                int columns = (parent.FrameHeader.FrameSize.FrameWidth + 63) >> 6;
                ulong sourceSad = parent.SourceBlockSad.Span[((superblockOrigin.Y >> 6) * columns) + (superblockOrigin.X >> 6)];
                InlineArray4<long> thresholds = default;
                this.GetInterVarianceThresholds(sourceSad, false, this.superblockQIndex, thresholds);

                ReadOnlySpan<TSample> prediction = this.PrepareInterVariancePrediction(
                    macroBlock, superblockOrigin, out int predictionStride, out uint lastSad);

                this.forceZeroMotionLevel = 0;
                if (parent.IsScreenContent && parent.EncodingSpeed >= HeifEncodingSpeed.Level9 &&
                    parent.FramesSinceKey > 30 && this.partitionReference == Av1ReferenceFrameType.Last &&
                    this.partitionMotion.IsZero && this.sourceSadLevel == Av1SourceSadLevel.Zero)
                {
                    uint lumaThreshold = side == 128 ? 10000U : 5000U;
                    uint chromaThreshold = (3 * lumaThreshold) >> 2;
                    int column = superblockOrigin.X >> Av1Constants.ModeInfoSizeLog2;
                    int row = superblockOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
                    int modeInfoSide = side >> Av1Constants.ModeInfoSizeLog2;
                    if (column + modeInfoSide <= macroBlock.Tile.ModeInfoColumnEnd &&
                        row + modeInfoSide <= macroBlock.Tile.ModeInfoRowEnd && lastSad < lumaThreshold &&
                        this.superblockChromaSad[0] < chromaThreshold && this.superblockChromaSad[1] < chromaThreshold)
                    {
                        this.forceZeroMotionLevel = 1;
                        this.superblock.Workspace.PartitionSearchTypes[0] = (byte)Av1PartitionType.None;
                        return;
                    }

                    // Stationary source can still contain reconstruction error. Keep partitioning
                    // in that case and let individual leaves establish their own zero-motion skip.
                    this.forceZeroMotionLevel = 2;
                }

                Span<VariancePartitionNode> temporalNodes = MemoryMarshal.Cast<int, VariancePartitionNode>(
                    this.blockWorkspace.PartitionAnalysisScratch);

                this.BuildInterVariancePartitions(
                    macroBlock,
                    superblockOrigin,
                    superblockOrigin,
                    this.picture.Sequence.SequenceHeader.SuperblockSize,
                    0,
                    prediction,
                    predictionStride,
                    thresholds,
                    temporalNodes);

                if (parent.SpeedSettings.UseEstimatedLowTemporalVariance && this.partitionReference == Av1ReferenceFrameType.Last)
                {
                    this.SetInterLowVarianceFlags(superblockOrigin, temporalNodes, thresholds);
                }

                return;
            }

            int speed = (int)this.picture.Parent.EncodingSpeed;
            int frameWidth = this.picture.Parent.FrameHeader.FrameSize.FrameWidth;
            int frameHeight = this.picture.Parent.FrameHeader.FrameSize.FrameHeight;
            long threshold = 120L * Av1QuantizationLookup.GetAcQuant(this.superblockQIndex, 0, this.bitDepth);
            bool stillPicture = this.picture.Sequence.SequenceHeader.IsStillPicture;
            bool largePartitions = stillPicture
                ? speed >= 8 && Math.Min(frameWidth, frameHeight) >= 720
                : speed >= 9 && Math.Min(frameWidth, frameHeight) < 720;

            if (largePartitions)
            {
                int splitShift = stillPicture ? speed == 8 ? 8 : 7 : this.picture.Parent.IsScreenContent ? 10 : 9;
                threshold <<= splitShift - (stillPicture ? 7 : 8);
            }

            bool smallFrame = (long)frameWidth * frameHeight < 1280 * 720;
            int largeFrameShift = largePartitions ? stillPicture ? 1 : 0 : 2;
            long threshold32 = smallFrame ? threshold / 3 : threshold >> largeFrameShift;
            long threshold16 = threshold >> (smallFrame ? 1 : largeFrameShift);

            // Partition analysis precedes residual coding. Its compact moment tree borrows coefficient scratch
            // only for this pass; the selected partition bytes survive after transform trials reuse that storage.
            Span<VariancePartitionNode> nodes = MemoryMarshal.Cast<int, VariancePartitionNode>(this.blockWorkspace.PartitionAnalysisScratch);
            this.BuildVariancePartitions(
                this.source.GetPlane(Av1Plane.Y),
                macroBlock.Tile,
                superblockOrigin,
                this.picture.Sequence.SequenceHeader.SuperblockSize,
                0,
                nodes,
                threshold32,
                threshold16,
                stillPicture && speed >= 9);
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

            // Similar child variances permit one sixteen-by-sixteen block even when its own variance
            // is high. Its parent must still split, as recorded independently above. When the block does
            // not fit, the shape evaluation continues, unless the variance is very high for a key frame.
            // Reference: get_part_eval_based_on_sub_blk_var(), and the PART_EVAL_ONLY_NONE path through
            // set_vt_partitioning().
            bool onlyNone = width == 16 && aboveThreshold && pruneSixteenSplit &&
                maximumVariance - minimumVariance <= (threshold16 << 2);

            if (onlyNone && fitsColumns && fitsRows)
            {
                partition = Av1PartitionType.None;
            }
            else if (onlyNone ? node.Variance <= (threshold << 4) : !node.ForceParentSplit)
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
