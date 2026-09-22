// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <content>
/// Reduces one patch to the least-squares system that a refinement step solves.
/// </content>
/// <remarks>
/// The two methods here are reductions rather than lane-wise transforms: each folds a whole patch
/// into a few scalars. A reduction has no per-width arithmetic to name, so it is written directly
/// with a vector path and a scalar path instead of behind an operator. The lane-wise work that the
/// reductions consume does sit behind an operator, in <see cref="IAv1CubicOperator"/>.
/// </remarks>
internal static partial class Av1DenseFlowSolver
{
    /// <summary>
    /// Sums the products of the gradients of one patch into the least-squares normal matrix.
    /// </summary>
    /// <param name="dx">The horizontal gradients.</param>
    /// <param name="dy">The vertical gradients.</param>
    /// <param name="m0">Receives the sum of the squared horizontal gradients.</param>
    /// <param name="m1">Receives the sum of the gradient products.</param>
    /// <param name="m3">Receives the sum of the squared vertical gradients.</param>
    /// <remarks>
    /// The matrix is symmetric, so its two off-diagonal entries are one value. A regularization of
    /// one is added to each diagonal entry. That is what guarantees a determinant of at least one,
    /// so no test for invertibility is needed, and it keeps every entry a whole number.
    /// Reference: compute_flow_matrix().
    /// </remarks>
    private static void ComputeNormalMatrix(
        ReadOnlySpan<short> dx,
        ReadOnlySpan<short> dy,
        out double m0,
        out double m1,
        out double m3)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            ref short dxBase = ref MemoryMarshal.GetReference(dx);
            ref short dyBase = ref MemoryMarshal.GetReference(dy);

            // A gradient reaches about six thousand, so a product needs thirty-two bit lanes.
            Vector128<int> sum0 = Vector128<int>.Zero;
            Vector128<int> sum1 = Vector128<int>.Zero;
            Vector128<int> sum3 = Vector128<int>.Zero;
            for (int row = 0; row < PatchSize; row++)
            {
                // One patch row is eight gradients, which is one vector of sixteen-bit lanes. Both
                // halves of the widened row accumulate into the same vectors. The total is taken at
                // the end and integer addition is associative, so the lane order cannot change it.
                (Vector128<int> lowX, Vector128<int> highX) = Vector128.Widen(Vector128.LoadUnsafe(ref dxBase, (nuint)(row * PatchSize)));

                (Vector128<int> lowY, Vector128<int> highY) = Vector128.Widen(Vector128.LoadUnsafe(ref dyBase, (nuint)(row * PatchSize)));

                sum0 += (lowX * lowX) + (highX * highX);
                sum1 += (lowX * lowY) + (highX * highY);
                sum3 += (lowY * lowY) + (highY * highY);
            }

            m0 = Vector128.Sum(sum0) + 1;
            m1 = Vector128.Sum(sum1);
            m3 = Vector128.Sum(sum3) + 1;
            return;
        }

        int scalar0 = 0;
        int scalar1 = 0;
        int scalar3 = 0;
        for (int index = 0; index < PatchSize * PatchSize; index++)
        {
            scalar0 += dx[index] * dx[index];
            scalar1 += dx[index] * dy[index];
            scalar3 += dy[index] * dy[index];
        }

        m0 = scalar0 + 1;
        m1 = scalar1;
        m3 = scalar3 + 1;
    }

    /// <summary>
    /// Loads eight samples of one plane row as two vectors of thirty-two bit lanes.
    /// </summary>
    /// <param name="plane">The first sample of the storage of the level.</param>
    /// <param name="offset">The sample offset, which is never negative.</param>
    /// <returns>The first four samples and the second four samples.</returns>
    private static (Vector128<int> Low, Vector128<int> High) WidenSamplesToInt32(ref byte plane, nuint offset)
        => Vector128.Widen(WidenSamples(ref plane, offset));

    /// <summary>
    /// Accumulates the gradient-weighted error of one patch against the warped reference.
    /// </summary>
    /// <typeparam name="TOperator">The cubic interpolation arithmetic.</typeparam>
    /// <remarks>
    /// The reference patch is warped by the current flow with a separable cubic interpolation, and
    /// the difference from the source is then weighted by each gradient. The fetch position is
    /// clamped so that every read stays inside the border of the level while remaining far enough
    /// out that the border samples themselves cannot change the result.
    /// Reference: compute_flow_vector().
    /// </remarks>
    private static class Warp<TOperator>
        where TOperator : struct, IAv1CubicOperator
    {
        /// <summary>
        /// The number of intermediate rows, one above the patch and two below it.
        /// </summary>
        private const int IntermediateRows = PatchSize + 3;

        /// <summary>
        /// Accumulates the gradient-weighted error of one patch.
        /// </summary>
        /// <param name="source">The whole storage of the source level.</param>
        /// <param name="reference">The whole storage of the reference level.</param>
        /// <param name="origin">The index of the first coded sample of each level.</param>
        /// <param name="width">The width of the level.</param>
        /// <param name="height">The height of the level.</param>
        /// <param name="stride">The row stride of the level.</param>
        /// <param name="x">The first column of the patch.</param>
        /// <param name="y">The first row of the patch.</param>
        /// <param name="u">The horizontal flow.</param>
        /// <param name="v">The vertical flow.</param>
        /// <param name="dx">The horizontal gradients.</param>
        /// <param name="dy">The vertical gradients.</param>
        /// <param name="b0">Receives the horizontal component of the right-hand side.</param>
        /// <param name="b1">Receives the vertical component.</param>
        public static void ComputeResidual(
            ReadOnlySpan<byte> source,
            ReadOnlySpan<byte> reference,
            int origin,
            int width,
            int height,
            int stride,
            int x,
            int y,
            double u,
            double v,
            ReadOnlySpan<short> dx,
            ReadOnlySpan<short> dy,
            out int b0,
            out int b1)
        {
            int integerU = (int)Math.Floor(u);
            int integerV = (int)Math.Floor(v);
            Span<int> horizontalKernel = stackalloc int[4];
            Span<int> verticalKernel = stackalloc int[4];
            GetCubicKernel(u - integerU, horizontalKernel);
            GetCubicKernel(v - integerV, verticalKernel);

            // The patch is eight samples square and the cubic kernel reaches one sample before and
            // two after, so the extreme reads are one before the patch origin and nine past it.
            int x0 = Math.Clamp(x + integerU, -9, width);
            int y0 = Math.Clamp(y + integerV, -9, height);

            // Eleven rows of intermediate, beginning one row above the patch.
            Span<int> intermediate = stackalloc int[PatchSize * IntermediateRows];

            // The second pass rounds off the bits that the first pass kept, but it retains the
            // fractional bits of the gradients so that the difference and the gradients share a
            // single scale.
            int roundBits = InterpolationBits + IntermediateBits - DerivativeScaleLog2;

            if (Vector128.IsHardwareAccelerated)
            {
                ComputeResidualVector128(
                    source,
                    reference,
                    origin,
                    stride,
                    x,
                    y,
                    x0,
                    y0,
                    horizontalKernel,
                    verticalKernel,
                    roundBits,
                    intermediate,
                    dx,
                    dy,
                    out b0,
                    out b1);

                return;
            }

            ComputeResidualScalar(
                source,
                reference,
                origin,
                stride,
                x,
                y,
                x0,
                y0,
                horizontalKernel,
                verticalKernel,
                roundBits,
                intermediate,
                dx,
                dy,
                out b0,
                out b1);
        }

        /// <summary>
        /// Accumulates the gradient-weighted error of one patch with 128-bit lanes.
        /// </summary>
        /// <param name="source">The whole storage of the source level.</param>
        /// <param name="reference">The whole storage of the reference level.</param>
        /// <param name="origin">The index of the first coded sample of each level.</param>
        /// <param name="stride">The row stride of the level.</param>
        /// <param name="x">The first column of the patch.</param>
        /// <param name="y">The first row of the patch.</param>
        /// <param name="x0">The clamped first column of the warped fetch.</param>
        /// <param name="y0">The clamped first row of the warped fetch.</param>
        /// <param name="horizontalKernel">The horizontal cubic taps.</param>
        /// <param name="verticalKernel">The vertical cubic taps.</param>
        /// <param name="roundBits">The shift applied after the second pass.</param>
        /// <param name="intermediate">Scratch for the eleven intermediate rows.</param>
        /// <param name="dx">The horizontal gradients.</param>
        /// <param name="dy">The vertical gradients.</param>
        /// <param name="b0">Receives the horizontal component of the right-hand side.</param>
        /// <param name="b1">Receives the vertical component.</param>
        /// <remarks>
        /// A patch row is eight samples and the intermediate is thirty-two bits wide, so one row
        /// occupies two vectors and both passes run as two halves. The four cubic taps of the first
        /// pass are four overlapping loads of one reference row, which leaves the pass with no
        /// horizontal data movement at all.
        /// </remarks>
        private static void ComputeResidualVector128(
            ReadOnlySpan<byte> source,
            ReadOnlySpan<byte> reference,
            int origin,
            int stride,
            int x,
            int y,
            int x0,
            int y0,
            ReadOnlySpan<int> horizontalKernel,
            ReadOnlySpan<int> verticalKernel,
            int roundBits,
            Span<int> intermediate,
            ReadOnlySpan<short> dx,
            ReadOnlySpan<short> dy,
            out int b0,
            out int b1)
        {
            ref byte referenceBase = ref MemoryMarshal.GetReference(reference);
            ref byte sourceBase = ref MemoryMarshal.GetReference(source);
            ref int intermediateBase = ref MemoryMarshal.GetReference(intermediate);
            ref short dxBase = ref MemoryMarshal.GetReference(dx);
            ref short dyBase = ref MemoryMarshal.GetReference(dy);

            Vector128<int> tap0 = Vector128.Create(horizontalKernel[0]);
            Vector128<int> tap1 = Vector128.Create(horizontalKernel[1]);
            Vector128<int> tap2 = Vector128.Create(horizontalKernel[2]);
            Vector128<int> tap3 = Vector128.Create(horizontalKernel[3]);
            for (int row = -1; row < PatchSize + 2; row++)
            {
                nuint rowOffset = (nuint)(origin + ((y0 + row) * stride) + x0);
                (Vector128<int> lowA, Vector128<int> highA) = WidenSamplesToInt32(ref referenceBase, rowOffset - 1);
                (Vector128<int> lowB, Vector128<int> highB) = WidenSamplesToInt32(ref referenceBase, rowOffset);
                (Vector128<int> lowC, Vector128<int> highC) = WidenSamplesToInt32(ref referenceBase, rowOffset + 1);
                (Vector128<int> lowD, Vector128<int> highD) = WidenSamplesToInt32(ref referenceBase, rowOffset + 2);

                // The first pass keeps six fractional bits, which is the most the intermediate of
                // the reference tolerates, so both passes match it bit for bit.
                int firstPassBits = InterpolationBits - IntermediateBits;
                TOperator.Filter(lowA, lowB, lowC, lowD, tap0, tap1, tap2, tap3, firstPassBits)
                    .StoreUnsafe(ref intermediateBase, (nuint)((row + 1) * PatchSize));

                TOperator.Filter(highA, highB, highC, highD, tap0, tap1, tap2, tap3, firstPassBits)
                    .StoreUnsafe(ref intermediateBase, (nuint)(((row + 1) * PatchSize) + 4));
            }

            Vector128<int> vertical0 = Vector128.Create(verticalKernel[0]);
            Vector128<int> vertical1 = Vector128.Create(verticalKernel[1]);
            Vector128<int> vertical2 = Vector128.Create(verticalKernel[2]);
            Vector128<int> vertical3 = Vector128.Create(verticalKernel[3]);
            Vector128<int> accumulated0 = Vector128<int>.Zero;
            Vector128<int> accumulated1 = Vector128<int>.Zero;
            for (int row = 0; row < PatchSize; row++)
            {
                (Vector128<int> gradientLowX, Vector128<int> gradientHighX) = Vector128.Widen(
                    Vector128.LoadUnsafe(ref dxBase, (nuint)(row * PatchSize)));

                (Vector128<int> gradientLowY, Vector128<int> gradientHighY) = Vector128.Widen(
                    Vector128.LoadUnsafe(ref dyBase, (nuint)(row * PatchSize)));

                for (int half = 0; half < 2; half++)
                {
                    // The four vertical taps are four rows of the intermediate, which is contiguous
                    // at a stride of one patch row, so each tap is a plain offset load.
                    nuint offset = (nuint)((row * PatchSize) + (half * 4));
                    Vector128<int> above = Vector128.LoadUnsafe(ref intermediateBase, offset);
                    Vector128<int> centre = Vector128.LoadUnsafe(ref intermediateBase, offset + PatchSize);
                    Vector128<int> below = Vector128.LoadUnsafe(ref intermediateBase, offset + (2 * PatchSize));
                    Vector128<int> beyond = Vector128.LoadUnsafe(ref intermediateBase, offset + (3 * PatchSize));

                    Vector128<int> warped = TOperator.Filter(
                        above, centre, below, beyond, vertical0, vertical1, vertical2, vertical3, roundBits);

                    // The source is raised to the scale of the gradients so that the difference
                    // shares it. Only the half that this iteration covers is taken.
                    nuint sourceOffset = (nuint)(origin + ((y + row) * stride) + x + (half * 4));
                    (Vector128<int> sourceLow, _) = WidenSamplesToInt32(ref sourceBase, sourceOffset);
                    Vector128<int> difference = warped - (sourceLow << DerivativeScaleLog2);

                    Vector128<int> gradientX = half == 0 ? gradientLowX : gradientHighX;
                    Vector128<int> gradientY = half == 0 ? gradientLowY : gradientHighY;
                    accumulated0 += gradientX * difference;
                    accumulated1 += gradientY * difference;
                }
            }

            b0 = Vector128.Sum(accumulated0);
            b1 = Vector128.Sum(accumulated1);
        }

        /// <summary>
        /// Accumulates the gradient-weighted error of one patch one sample at a time.
        /// </summary>
        /// <param name="source">The whole storage of the source level.</param>
        /// <param name="reference">The whole storage of the reference level.</param>
        /// <param name="origin">The index of the first coded sample of each level.</param>
        /// <param name="stride">The row stride of the level.</param>
        /// <param name="x">The first column of the patch.</param>
        /// <param name="y">The first row of the patch.</param>
        /// <param name="x0">The clamped first column of the warped fetch.</param>
        /// <param name="y0">The clamped first row of the warped fetch.</param>
        /// <param name="horizontalKernel">The horizontal cubic taps.</param>
        /// <param name="verticalKernel">The vertical cubic taps.</param>
        /// <param name="roundBits">The shift applied after the second pass.</param>
        /// <param name="intermediate">Scratch for the eleven intermediate rows.</param>
        /// <param name="dx">The horizontal gradients.</param>
        /// <param name="dy">The vertical gradients.</param>
        /// <param name="b0">Receives the horizontal component of the right-hand side.</param>
        /// <param name="b1">Receives the vertical component.</param>
        private static void ComputeResidualScalar(
            ReadOnlySpan<byte> source,
            ReadOnlySpan<byte> reference,
            int origin,
            int stride,
            int x,
            int y,
            int x0,
            int y0,
            ReadOnlySpan<int> horizontalKernel,
            ReadOnlySpan<int> verticalKernel,
            int roundBits,
            Span<int> intermediate,
            ReadOnlySpan<short> dx,
            ReadOnlySpan<short> dy,
            out int b0,
            out int b1)
        {
            int firstPassBits = InterpolationBits - IntermediateBits;
            for (int row = -1; row < PatchSize + 2; row++)
            {
                int rowOffset = origin + ((y0 + row) * stride) + x0;
                for (int column = 0; column < PatchSize; column++)
                {
                    intermediate[((row + 1) * PatchSize) + column] = TOperator.Filter(
                        reference[rowOffset + column - 1],
                        reference[rowOffset + column],
                        reference[rowOffset + column + 1],
                        reference[rowOffset + column + 2],
                        horizontalKernel[0],
                        horizontalKernel[1],
                        horizontalKernel[2],
                        horizontalKernel[3],
                        firstPassBits);
                }
            }

            b0 = 0;
            b1 = 0;
            for (int row = 0; row < PatchSize; row++)
            {
                for (int column = 0; column < PatchSize; column++)
                {
                    int start = (row * PatchSize) + column;
                    int warped = TOperator.Filter(
                        intermediate[start],
                        intermediate[start + PatchSize],
                        intermediate[start + (2 * PatchSize)],
                        intermediate[start + (3 * PatchSize)],
                        verticalKernel[0],
                        verticalKernel[1],
                        verticalKernel[2],
                        verticalKernel[3],
                        roundBits);

                    int sourceSample = source[origin + ((y + row) * stride) + x + column] << DerivativeScaleLog2;
                    int difference = warped - sourceSample;
                    b0 += dx[start] * difference;
                    b1 += dy[start] * difference;
                }
            }
        }
    }
}
