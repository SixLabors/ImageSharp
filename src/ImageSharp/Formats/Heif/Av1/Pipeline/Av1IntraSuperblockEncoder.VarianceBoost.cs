// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Chooses the superblock quantizer of the Variance Boost delta quantizer mode.
/// </content>
internal static partial class Av1IntraSuperblockEncoder
{
    internal partial struct ModeDecision<TSample, TOperator>
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
        /// <summary>
        /// Gets the Variance Boost quantizer index of the superblock at an origin. Reference:
        /// av1_get_variance_boost_block_variance() and av1_get_sbq_variance_boost().
        /// </summary>
        /// <param name="origin">The superblock origin in luma samples.</param>
        /// <param name="baseQIndex">The frame quantizer index.</param>
        /// <returns>The superblock quantizer index before the delta resolution.</returns>
        private readonly int GetVarianceBoostQIndex(Point origin, int baseQIndex)
        {
            // Each 8x8 variance is measured against zero samples, reading the replicated border past the frame
            // edge, and truncated to a per-sample value.
            Span<uint> variances = stackalloc uint[Av1VarianceBoost.SubblockCount];
            Span<TSample> zeros = stackalloc TSample[Av1VarianceBoost.SubblockSize];
            zeros.Clear();
            Buffer2DRegion<TSample> luma = this.source.GetPlane(Av1Plane.Y);
            int shift = this.bitDepth.GetBitCount() - 8;
            int index = 0;
            for (int row = 0; row < Av1VarianceBoost.SuperblockSize; row += Av1VarianceBoost.SubblockSize)
            {
                for (int column = 0; column < Av1VarianceBoost.SuperblockSize; column += Av1VarianceBoost.SubblockSize)
                {
                    TOperator.GetMoments(
                        Av1TransformBlockEncoder.GetPlaneSpan(luma, new Point(origin.X + column, origin.Y + row)),
                        luma.Stride,
                        zeros,
                        0,
                        Av1VarianceBoost.SubblockSize,
                        Av1VarianceBoost.SubblockSize,
                        out int sum,
                        out long squares);

                    // High bit depths round both moments to eight-bit precision first. Reference:
                    // aom_highbd_10_variance8x8() and aom_highbd_12_variance8x8().
                    long normalizedSum = sum;
                    long normalizedSquares = squares;
                    if (shift > 0)
                    {
                        normalizedSum = (sum + (1 << (shift - 1))) >> shift;
                        normalizedSquares = (squares + (1L << ((2 * shift) - 1))) >> (2 * shift);
                    }

                    long variance = normalizedSquares - ((normalizedSum * normalizedSum) / 64);
                    variances[index++] = (uint)Math.Max(variance, 0) / 64;
                }
            }

            uint blockVariance = Av1VarianceBoost.GetBlockVariance(variances);
            return Av1VarianceBoost.GetSuperblockQIndex(blockVariance, baseQIndex, this.bitDepth);
        }
    }
}
