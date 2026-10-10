// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Scales the rate multiplier of each block by the luma activity of its 16x16 blocks, for the SSIM and image tunes.
/// </content>
internal static partial class Av1IntraSuperblockEncoder
{
    /// <summary>
    /// Measures the rate multiplier scaling factor of every 16x16 luma block of a frame. The factor is the mean per-sample variance of
    /// the 8x8 blocks, mapped through a fitted exponential curve and divided by the geometric mean of the frame.
    /// </summary>
    /// <typeparam name="TSample">The sample storage type.</typeparam>
    /// <typeparam name="TOperator">The closed sample operations.</typeparam>
    /// <param name="picture">The frame receiving the factors.</param>
    /// <param name="source">The source frame.</param>
    public static void SetSsimRateMultiplierScaling<TSample, TOperator>(Av1PictureControlSet picture, Av1EncoderFrame<TSample> source)
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
        Av1PictureParentControlSet parent = picture.Parent;
        int modeInfoColumns = parent.Common.ModeInfoColumnCount;
        int modeInfoRows = parent.Common.ModeInfoRowCount;
        double[] factors = new double[((modeInfoColumns + 3) / 4) * ((modeInfoRows + 3) / 4)];
        SetSsimRateMultiplierScaling<TSample, TOperator>(
            factors,
            source,
            modeInfoColumns,
            modeInfoRows,
            picture.Sequence.SequenceHeader.ColorConfig.BitDepth);

        parent.SsimRateMultiplierFactors = factors;
    }

    /// <summary>
    /// Measures the rate multiplier scaling factor of every 16x16 luma block in a grid of the given size. It reads from the top-left
    /// corner of the source and writes to the front of the factor array, in rows of the grid column count. The encoder measures before
    /// it sets the new frame size. As a result, a frame with a new size uses the factors of the earlier grid, but looks them up with
    /// its own grid.
    /// </summary>
    /// <typeparam name="TSample">The sample storage type.</typeparam>
    /// <typeparam name="TOperator">The closed sample operations.</typeparam>
    /// <param name="factors">The factor array. The entries beyond the grid keep their values.</param>
    /// <param name="source">The source frame, at least as large as the grid.</param>
    /// <param name="modeInfoColumns">The column count of the grid, in 4x4 units.</param>
    /// <param name="modeInfoRows">The row count of the grid, in 4x4 units.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    public static void SetSsimRateMultiplierScaling<TSample, TOperator>(
        double[] factors,
        Av1EncoderFrame<TSample> source,
        int modeInfoColumns,
        int modeInfoRows,
        Av1BitDepth bitDepth)
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
        int columns = (modeInfoColumns + 3) / 4;
        int rows = (modeInfoRows + 3) / 4;
        Av1PlaneRegion<TSample> luma = source.CodedView.GetPlane(Av1Plane.Y);
        ReadOnlySpan<TSample> lumaSamples = luma.Samples;
        int shift = bitDepth.GetBitCount() - 8;
        Span<TSample> midpoint = stackalloc TSample[8];
        midpoint.Fill(TOperator.CreateSample(128 << shift));
        double logSum = 0.0;
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                double variance = 0.0;
                double count = 0.0;
                for (int modeInfoRow = row * 4; modeInfoRow < modeInfoRows && modeInfoRow < (row + 1) * 4; modeInfoRow += 2)
                {
                    for (int modeInfoColumn = column * 4; modeInfoColumn < modeInfoColumns && modeInfoColumn < (column + 1) * 4; modeInfoColumn += 2)
                    {
                        // Measures the sum and the sum of squares of an 8x8 luma block relative to a flat midpoint block.
                        // The variance below does not change with that offset.
                        TOperator.GetMoments(
                            lumaSamples[luma.GetOffset(modeInfoColumn << 2, modeInfoRow << 2)..],
                            luma.Stride,
                            midpoint,
                            0,
                            8,
                            8,
                            out int sum,
                            out long squares);

                        // Rounds the moments of a high bit depth block to the 8-bit scale, so the curve below applies to every bit depth.
                        long normalizedSum = sum;
                        long normalizedSquares = squares;
                        if (shift > 0)
                        {
                            normalizedSum = (sum + (1 << (shift - 1))) >> shift;
                            normalizedSquares = (squares + (1L << ((2 * shift) - 1))) >> (2 * shift);
                        }

                        long blockVariance = Math.Max(normalizedSquares - ((normalizedSum * normalizedSum) / 64), 0);
                        variance += (uint)((blockVariance + 32) >> 6);
                        count += 1.0;
                    }
                }

                variance /= count;

                // Curve fitted with an exponential model on the 16x16 blocks of the midres set.
                variance = (67.035434 * (1 - Math.Exp(-0.0021489 * variance))) + 17.492222;
                factors[(row * columns) + column] = variance;
                logSum += Math.Log(variance);
            }
        }

        logSum = Math.Exp(logSum / (rows * columns));
        for (int index = 0; index < rows * columns; index++)
        {
            factors[index] /= logSum;
        }
    }

    internal partial struct ModeDecision<TSample, TOperator>
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
        /// <summary>
        /// Scales a rate multiplier by the geometric mean of the factors of the 16x16 blocks that a block covers.
        /// </summary>
        /// <param name="rateMultiplier">The rate multiplier.</param>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="blockSize">The block size.</param>
        /// <returns>The scaled rate multiplier.</returns>
        private readonly int ScaleSsimRateMultiplier(int rateMultiplier, Point blockOrigin, Av1BlockSize blockSize)
        {
            double[] factors = this.picture.Parent.SsimRateMultiplierFactors!;
            int modeInfoColumns = this.picture.Parent.Common.ModeInfoColumnCount;
            int modeInfoRows = this.picture.Parent.Common.ModeInfoRowCount;
            int columns = (modeInfoColumns + 3) / 4;
            int rows = (modeInfoRows + 3) / 4;
            int blockColumns = (blockSize.Get4x4WideCount() + 3) / 4;
            int blockRows = (blockSize.Get4x4HighCount() + 3) / 4;
            int modeInfoRow = blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
            int modeInfoColumn = blockOrigin.X >> Av1Constants.ModeInfoSizeLog2;
            double count = 0.0;
            double product = 1.0;

            // Each step moves one 16x16 block, which is four 4x4 units on each axis.
            for (int row = modeInfoRow / 4; row < rows && row < (modeInfoRow / 4) + blockRows; row++)
            {
                for (int column = modeInfoColumn / 4; column < columns && column < (modeInfoColumn / 4) + blockColumns; column++)
                {
                    product *= factors[(row * columns) + column];
                    count += 1.0;
                }
            }

            product = Math.Pow(product, 1.0 / count);
            return Math.Max((int)((rateMultiplier * product) + 0.5), 0);
        }
    }
}
