// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <content>
/// Provides the vector kernels of the dense inverse search solver.
/// </content>
internal static partial class Av1DenseFlowSolver
{
    /// <summary>
    /// Fills one patch's gradients in one direction.
    /// </summary>
    /// <param name="source">The patch's first sample.</param>
    /// <param name="sourceStride">The level row stride.</param>
    /// <param name="destination">Receives the patch's gradients, eight per row.</param>
    /// <param name="derivativeHorizontal">
    /// Whether the derivative runs along the rows, which gives the horizontal gradient.
    /// </param>
    /// <remarks>
    /// The filter is separable: a three-tap derivative on one axis and a three-tap smoothing on the
    /// other. The derivative is <c>sample[-1] - sample[+1]</c>, which is minus twice the true
    /// derivative, and the smoothing taps total four, so the pair carries the eight-fold scale the
    /// gradients are defined to have; the sign is absorbed by the residual.
    ///
    /// A patch row is eight samples, so one row of the intermediate and of the result is exactly one
    /// vector of sixteen-bit lanes. The horizontal pass covers ten rows, one above and one below the
    /// patch, because the vertical pass reads a row on each side.
    /// Reference: sobel_filter() in disflow.c.
    /// </remarks>
    private static void SobelFilter(
        ReadOnlySpan<byte> source,
        int sourceStride,
        Span<short> destination,
        bool derivativeHorizontal)
    {
        // Ten rows of intermediate, offset by one so that row -1 is addressable.
        Span<short> intermediate = stackalloc short[PatchSize * (PatchSize + 2)];
        ref byte sourceBase = ref MemoryMarshal.GetReference(source);

        for (int row = -1; row < PatchSize + 1; row++)
        {
            nint rowOffset = (nint)row * sourceStride;

            // The three taps are the samples one to the left, at, and one to the right of each
            // column, so they are three overlapping loads of the same row.
            Vector128<short> left = WidenSamples(ref sourceBase, rowOffset - 1);
            Vector128<short> centre = WidenSamples(ref sourceBase, rowOffset);
            Vector128<short> right = WidenSamples(ref sourceBase, rowOffset + 1);

            Vector128<short> filtered = derivativeHorizontal
                ? left - right
                : left + centre + centre + right;

            filtered.StoreUnsafe(ref MemoryMarshal.GetReference(intermediate), (nuint)((row + 1) * PatchSize));
        }

        ref short intermediateBase = ref MemoryMarshal.GetReference(intermediate);
        ref short destinationBase = ref MemoryMarshal.GetReference(destination);
        for (int row = 0; row < PatchSize; row++)
        {
            // Row r of the result reads intermediate rows r-1, r and r+1, which are stored at r, r+1
            // and r+2 because the intermediate is offset by one row.
            Vector128<short> above = Vector128.LoadUnsafe(ref intermediateBase, (nuint)(row * PatchSize));
            Vector128<short> centre = Vector128.LoadUnsafe(ref intermediateBase, (nuint)((row + 1) * PatchSize));
            Vector128<short> below = Vector128.LoadUnsafe(ref intermediateBase, (nuint)((row + 2) * PatchSize));

            Vector128<short> filtered = derivativeHorizontal
                ? above + centre + centre + below
                : above - below;

            filtered.StoreUnsafe(ref destinationBase, (nuint)(row * PatchSize));
        }
    }

    /// <summary>
    /// Sums the products of one patch's gradients into the least-squares normal matrix.
    /// </summary>
    /// <param name="dx">The horizontal gradients.</param>
    /// <param name="dy">The vertical gradients.</param>
    /// <param name="m0">Receives the sum of the squared horizontal gradients.</param>
    /// <param name="m1">Receives the sum of the gradient products.</param>
    /// <param name="m3">Receives the sum of the squared vertical gradients.</param>
    /// <remarks>
    /// The matrix is symmetric, so its off-diagonal entries are one value. A regularization of one is
    /// added to each diagonal entry, which is what guarantees the determinant is at least one and so
    /// removes any need to test for invertibility. It also keeps every entry a whole number.
    /// Reference: compute_flow_matrix() in disflow.c.
    /// </remarks>
    private static void ComputeNormalMatrix(
        ReadOnlySpan<short> dx,
        ReadOnlySpan<short> dy,
        out double m0,
        out double m1,
        out double m3)
    {
        ref short dxBase = ref MemoryMarshal.GetReference(dx);
        ref short dyBase = ref MemoryMarshal.GetReference(dy);

        // One row of products fits one vector of thirty-two bit lanes per half, and the gradients
        // reach about six thousand, so their products need the wider lane.
        Vector128<int> sum0 = Vector128<int>.Zero;
        Vector128<int> sum1 = Vector128<int>.Zero;
        Vector128<int> sum3 = Vector128<int>.Zero;
        for (int row = 0; row < PatchSize; row++)
        {
            Vector128<short> rowX = Vector128.LoadUnsafe(ref dxBase, (nuint)(row * PatchSize));
            Vector128<short> rowY = Vector128.LoadUnsafe(ref dyBase, (nuint)(row * PatchSize));
            (Vector128<int> lowX, Vector128<int> highX) = Widen(rowX);
            (Vector128<int> lowY, Vector128<int> highY) = Widen(rowY);

            // Both halves accumulate into the same vectors. The total is taken at the end, and
            // addition is associative, so the order of the lanes does not affect the sum.
            sum0 += (lowX * lowX) + (highX * highX);
            sum1 += (lowX * lowY) + (highX * highY);
            sum3 += (lowY * lowY) + (highY * highY);
        }

        m0 = Vector128.Sum(sum0) + 1;
        m1 = Vector128.Sum(sum1);
        m3 = Vector128.Sum(sum3) + 1;
    }

    /// <summary>
    /// Accumulates the gradient-weighted error of one patch against the warped reference.
    /// </summary>
    /// <param name="source">The source level, from its first coded sample.</param>
    /// <param name="reference">The reference level, from its first coded sample.</param>
    /// <param name="width">The level width.</param>
    /// <param name="height">The level height.</param>
    /// <param name="stride">The level row stride.</param>
    /// <param name="x">The patch's first column.</param>
    /// <param name="y">The patch's first row.</param>
    /// <param name="u">The horizontal flow.</param>
    /// <param name="v">The vertical flow.</param>
    /// <param name="dx">The horizontal gradients.</param>
    /// <param name="dy">The vertical gradients.</param>
    /// <param name="b0">Receives the horizontal component of the right-hand side.</param>
    /// <param name="b1">Receives the vertical component.</param>
    /// <remarks>
    /// The reference patch is warped by the current flow with a separable cubic interpolation, and the
    /// difference from the source is then weighted by each gradient. The fetch position is clamped so
    /// that every read stays inside the level's border while remaining far enough out that the border
    /// samples themselves do not change the result. Reference: compute_flow_vector() in disflow.c.
    /// </remarks>
    private static void ComputeResidual(
        ReadOnlySpan<byte> source,
        ReadOnlySpan<byte> reference,
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

        // The patch is eight samples square and the cubic kernel reaches one sample before and two
        // after, so the extreme reads are one before the patch origin and nine past it.
        int x0 = Math.Clamp(x + integerU, -9, width);
        int y0 = Math.Clamp(y + integerV, -9, height);

        // Eleven rows of intermediate, offset by one so that row -1 is addressable.
        Span<int> intermediate = stackalloc int[PatchSize * (PatchSize + 3)];
        ref byte referenceBase = ref MemoryMarshal.GetReference(reference);
        ref int intermediateBase = ref MemoryMarshal.GetReference(intermediate);

        Vector128<int> tap0 = Vector128.Create(horizontalKernel[0]);
        Vector128<int> tap1 = Vector128.Create(horizontalKernel[1]);
        Vector128<int> tap2 = Vector128.Create(horizontalKernel[2]);
        Vector128<int> tap3 = Vector128.Create(horizontalKernel[3]);
        for (int row = -1; row < PatchSize + 2; row++)
        {
            nint rowOffset = (nint)((y0 + row) * stride) + x0;

            // The four taps are four overlapping loads of the same reference row, so the whole
            // horizontal pass is one set of vertical operations on eight columns at a time.
            WidenSamples(ref referenceBase, rowOffset - 1, out Vector128<int> lowA, out Vector128<int> highA);
            WidenSamples(ref referenceBase, rowOffset, out Vector128<int> lowB, out Vector128<int> highB);
            WidenSamples(ref referenceBase, rowOffset + 1, out Vector128<int> lowC, out Vector128<int> highC);
            WidenSamples(ref referenceBase, rowOffset + 2, out Vector128<int> lowD, out Vector128<int> highD);

            Vector128<int> low = (lowA * tap0) + (lowB * tap1) + (lowC * tap2) + (lowD * tap3);
            Vector128<int> high = (highA * tap0) + (highB * tap1) + (highC * tap2) + (highD * tap3);

            // Six fractional bits are kept between the passes, which is the most the intermediate
            // range tolerates.
            low = RoundShift(low, InterpolationBits - IntermediateBits);
            high = RoundShift(high, InterpolationBits - IntermediateBits);
            low.StoreUnsafe(ref intermediateBase, (nuint)((row + 1) * PatchSize));
            high.StoreUnsafe(ref intermediateBase, (nuint)(((row + 1) * PatchSize) + 4));
        }

        ref short dxBase = ref MemoryMarshal.GetReference(dx);
        ref short dyBase = ref MemoryMarshal.GetReference(dy);
        ref byte sourceBase = ref MemoryMarshal.GetReference(source);
        Vector128<int> vertical0 = Vector128.Create(verticalKernel[0]);
        Vector128<int> vertical1 = Vector128.Create(verticalKernel[1]);
        Vector128<int> vertical2 = Vector128.Create(verticalKernel[2]);
        Vector128<int> vertical3 = Vector128.Create(verticalKernel[3]);

        // The vertical pass rounds off the six bits kept above but retains the gradients' own
        // fractional bits, so that the difference and the gradients share one scale.
        int roundBits = InterpolationBits + IntermediateBits - DerivativeScaleLog2;
        Vector128<int> accumulated0 = Vector128<int>.Zero;
        Vector128<int> accumulated1 = Vector128<int>.Zero;
        for (int row = 0; row < PatchSize; row++)
        {
            Vector128<short> gradientRowX = Vector128.LoadUnsafe(ref dxBase, (nuint)(row * PatchSize));
            Vector128<short> gradientRowY = Vector128.LoadUnsafe(ref dyBase, (nuint)(row * PatchSize));
            (Vector128<int> gradientLowX, Vector128<int> gradientHighX) = Widen(gradientRowX);
            (Vector128<int> gradientLowY, Vector128<int> gradientHighY) = Widen(gradientRowY);

            for (int half = 0; half < 2; half++)
            {
                nuint offset = (nuint)((row * PatchSize) + (half * 4));
                Vector128<int> above = Vector128.LoadUnsafe(ref intermediateBase, offset);
                Vector128<int> centre = Vector128.LoadUnsafe(ref intermediateBase, offset + PatchSize);
                Vector128<int> below = Vector128.LoadUnsafe(ref intermediateBase, offset + (2 * PatchSize));
                Vector128<int> beyond = Vector128.LoadUnsafe(ref intermediateBase, offset + (3 * PatchSize));

                Vector128<int> warped = (above * vertical0) + (centre * vertical1) +
                    (below * vertical2) + (beyond * vertical3);

                warped = RoundShift(warped, roundBits);

                // The source is raised to the gradients' scale so that the difference is in that
                // scale too.
                nint sourceOffset = (nint)((y + row) * stride) + x + (half * 4);
                WidenSamples(ref sourceBase, sourceOffset, out Vector128<int> sourceLow, out _);
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
}
