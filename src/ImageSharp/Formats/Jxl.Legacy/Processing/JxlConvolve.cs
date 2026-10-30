// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// Convolution filters
/// </summary>
internal static class JxlConvolve
{
    /// <summary>
    /// Applies a symmetric separable five-by-five kernel in one traversal without intermediate sample allocations.
    /// </summary>
    /// <param name="input">The borrowed source plane.</param>
    /// <param name="rectangle">The source rectangle. Its horizontal edges define the reflected row bounds.</param>
    /// <param name="weights">The symmetric horizontal and vertical weights in four-value groups.</param>
    /// <param name="output">The distinct output plane, with logical dimensions equal to the source rectangle.</param>
    /// <returns>Whether the output dimensions match the source rectangle.</returns>
    /// <remarks>Vertical neighbors reflect at the full input image edges. The caller supplies a rectangle inside the input plane.</remarks>
    public static bool Separable5(JxlPlane<float> input, in Rectangle rectangle, in JxlWeightsSeparable5 weights, JxlPlane<float> output)
    {
        if (rectangle.Width != output.XSize || rectangle.Height != output.YSize)
        {
            return false;
        }

        // Borrow once at each logical origin. Storage prefixes and different physical row strides remain outside the operation.
        ReadOnlySpan<float> source = input.GetRow(0);
        Span<float> destination = output.GetRow(0);
        int inputStride = input.PixelsPerRow;
        int outputStride = output.PixelsPerRow;

        for (int y = 0; y < rectangle.Height; y++)
        {
            int sourceY = rectangle.Y + y;

            // Reflect at image edges. Mirror also handles repeated reflections for images only one to four rows high.
            ReadOnlySpan<float> top2 = source.Slice((JxlImageOperations.Mirror(sourceY - 2, input.YSize) * inputStride) + rectangle.X, rectangle.Width);
            ReadOnlySpan<float> top1 = source.Slice((JxlImageOperations.Mirror(sourceY - 1, input.YSize) * inputStride) + rectangle.X, rectangle.Width);
            ReadOnlySpan<float> middle = source.Slice((sourceY * inputStride) + rectangle.X, rectangle.Width);
            ReadOnlySpan<float> bottom1 = source.Slice((JxlImageOperations.Mirror(sourceY + 1L, input.YSize) * inputStride) + rectangle.X, rectangle.Width);
            ReadOnlySpan<float> bottom2 = source.Slice((JxlImageOperations.Mirror(sourceY + 2L, input.YSize) * inputStride) + rectangle.X, rectangle.Width);
            Span<float> rowOutput = destination.Slice(y * outputStride, rectangle.Width);

            // Horizontal results stay in registers. Combine the five rows directly into the output without a row cache or another image pass.
            JxlConvolveSeparable5.ConvolveRow(top2, top1, middle, bottom1, bottom2, weights, rowOutput);
        }

        return true;
    }

    /// <summary>
    /// Applies a symmetric five-by-five kernel to distinct source and destination rectangles.
    /// </summary>
    /// <param name="input">The borrowed source plane. Reflection uses its full logical dimensions.</param>
    /// <param name="rectangle">The source range, inside the input plane.</param>
    /// <param name="weights">The six symmetric weights, each repeated in a four-value group.</param>
    /// <param name="output">The distinct destination plane.</param>
    /// <param name="outputRect">The destination range, with the same dimensions as the source range.</param>
    /// <returns>Whether both rectangles have matching dimensions.</returns>
    public static bool Symmetric5(JxlPlane<float> input, in Rectangle rectangle, ref JxlWeightsSymmetric5 weights, JxlPlane<float> output, Rectangle outputRect)
    {
        if (rectangle.Width != outputRect.Width || rectangle.Height != outputRect.Height)
        {
            return false;
        }

        // Each row has three horizontal weights. The adjacent and outer rows use different weights, so this kernel
        // is not separable. Prepare those operations once and retain all five horizontal results in registers.
        WeightedSumOperator center = new(weights.C[0], weights.R[0], weights.R2[0]);
        WeightedSumOperator adjacent = new(weights.R[0], weights.D[0], weights.L[0]);
        WeightedSumOperator outer = new(weights.R2[0], weights.L[0], weights.D2[0]);
        ReadOnlySpan<float> source = input.GetRow(0);
        Span<float> destination = output.GetRow(0);
        int inputStride = input.PixelsPerRow;
        int outputStride = output.PixelsPerRow;

        for (int y = 0; y < rectangle.Height; y++)
        {
            int sourceY = rectangle.Y + y;
            ReadOnlySpan<float> top2 = source.Slice(JxlImageOperations.Mirror(sourceY - 2L, input.YSize) * inputStride, input.XSize);
            ReadOnlySpan<float> top1 = source.Slice(JxlImageOperations.Mirror(sourceY - 1L, input.YSize) * inputStride, input.XSize);
            ReadOnlySpan<float> middle = source.Slice(sourceY * inputStride, input.XSize);
            ReadOnlySpan<float> bottom1 = source.Slice(JxlImageOperations.Mirror(sourceY + 1L, input.YSize) * inputStride, input.XSize);
            ReadOnlySpan<float> bottom2 = source.Slice(JxlImageOperations.Mirror(sourceY + 2L, input.YSize) * inputStride, input.XSize);
            Span<float> row = destination.Slice(((outputRect.Y + y) * outputStride) + outputRect.X, rectangle.Width);
            int x = rectangle.X;
            int end = rectangle.Right;

            // Only image edges need reflection. A cropped rectangle can read its two-sample halo from the input image.
            // Unaligned loads keep one independent output center in each lane; physical padding is never a halo.
            for (; x < Math.Min(2, end); x++)
            {
                row[x - rectangle.X] = Symmetric5Pixel(top2, top1, middle, bottom1, bottom2, x, center, adjacent, outer);
            }

            int interiorEnd = Math.Min(end, input.XSize - 2);

            if (Vector512.IsHardwareAccelerated)
            {
                for (; x <= interiorEnd - Vector512<float>.Count; x += Vector512<float>.Count)
                {
                    Vector512<float> sum0 = WeightedSum512(middle, x, center) + WeightedSum512(top2, x, outer);
                    Vector512<float> sum1 = WeightedSum512(bottom2, x, outer);
                    sum0 += WeightedSum512(top1, x, adjacent);
                    sum1 += WeightedSum512(bottom1, x, adjacent);
                    (sum0 + sum1).StoreUnsafe(ref MemoryMarshal.GetReference(row), (uint)(x - rectangle.X));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x <= interiorEnd - Vector256<float>.Count; x += Vector256<float>.Count)
                {
                    Vector256<float> sum0 = WeightedSum256(middle, x, center) + WeightedSum256(top2, x, outer);
                    Vector256<float> sum1 = WeightedSum256(bottom2, x, outer);
                    sum0 += WeightedSum256(top1, x, adjacent);
                    sum1 += WeightedSum256(bottom1, x, adjacent);
                    (sum0 + sum1).StoreUnsafe(ref MemoryMarshal.GetReference(row), (uint)(x - rectangle.X));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x <= interiorEnd - Vector128<float>.Count; x += Vector128<float>.Count)
                {
                    Vector128<float> sum0 = WeightedSum128(middle, x, center) + WeightedSum128(top2, x, outer);
                    Vector128<float> sum1 = WeightedSum128(bottom2, x, outer);
                    sum0 += WeightedSum128(top1, x, adjacent);
                    sum1 += WeightedSum128(bottom1, x, adjacent);
                    (sum0 + sum1).StoreUnsafe(ref MemoryMarshal.GetReference(row), (uint)(x - rectangle.X));
                }
            }

            // Scalar tails use the identical row grouping. The last two image columns reflect their neighbors.
            for (; x < end; x++)
            {
                row[x - rectangle.X] = Symmetric5Pixel(top2, top1, middle, bottom1, bottom2, x, center, adjacent, outer);
            }
        }

        return true;
    }

    /// <summary>
    /// Evaluates one center with full-image reflection and the native five-row addition order.
    /// </summary>
    /// <param name="top2">The logical row two positions above the center.</param>
    /// <param name="top1">The logical row one position above the center.</param>
    /// <param name="middle">The logical center row.</param>
    /// <param name="bottom1">The logical row one position below the center.</param>
    /// <param name="bottom2">The logical row two positions below the center.</param>
    /// <param name="x">The center column in the full input image.</param>
    /// <param name="center">The center row's horizontal weighting.</param>
    /// <param name="adjacent">The adjacent rows' horizontal weighting.</param>
    /// <param name="outer">The outer rows' horizontal weighting.</param>
    /// <returns>The weighted sample.</returns>
    private static float Symmetric5Pixel(
        ReadOnlySpan<float> top2,
        ReadOnlySpan<float> top1,
        ReadOnlySpan<float> middle,
        ReadOnlySpan<float> bottom1,
        ReadOnlySpan<float> bottom2,
        int x,
        WeightedSumOperator center,
        WeightedSumOperator adjacent,
        WeightedSumOperator outer)
    {
        int left1 = x - 1;
        int left2 = x - 2;
        int right1 = x + 1;
        int right2 = x + 2;

        if (x < 2 || x >= middle.Length - 2)
        {
            // Reflect at the edge sample (-1 maps to 0). Compute indices once for all five rows, including width one.
            left1 = JxlImageOperations.Mirror(left1, middle.Length);
            left2 = JxlImageOperations.Mirror(left2, middle.Length);
            right1 = JxlImageOperations.Mirror(right1, middle.Length);
            right2 = JxlImageOperations.Mirror(right2, middle.Length);
        }

        float sum0 = center.Invoke(middle[x], middle[left1] + middle[right1], middle[left2] + middle[right2]);
        sum0 += outer.Invoke(top2[x], top2[left1] + top2[right1], top2[left2] + top2[right2]);
        float sum1 = outer.Invoke(bottom2[x], bottom2[left1] + bottom2[right1], bottom2[left2] + bottom2[right2]);
        sum0 += adjacent.Invoke(top1[x], top1[left1] + top1[right1], top1[left2] + top1[right2]);
        sum1 += adjacent.Invoke(bottom1[x], bottom1[left1] + bottom1[right1], bottom1[left2] + bottom1[right2]);
        return sum0 + sum1;
    }

    /// <summary>
    /// Weights complete interior centers without reading outside their two-sample logical halo.
    /// </summary>
    /// <param name="row">The full logical source row.</param>
    /// <param name="x">The first center index. Both halo samples and every result lane must exist.</param>
    /// <param name="operation">The row's center, distance-one and distance-two weights.</param>
    /// <returns>One horizontal weighted sum per lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> WeightedSum128(ReadOnlySpan<float> row, int x, WeightedSumOperator operation)
    {
        ref float first = ref MemoryMarshal.GetReference(row);
        Vector128<float> center = Vector128.LoadUnsafe(ref first, (uint)x);
        Vector128<float> near = Vector128.LoadUnsafe(ref first, (uint)(x - 1)) + Vector128.LoadUnsafe(ref first, (uint)(x + 1));
        Vector128<float> far = Vector128.LoadUnsafe(ref first, (uint)(x - 2)) + Vector128.LoadUnsafe(ref first, (uint)(x + 2));
        return operation.Invoke(center, near, far);
    }

    /// <summary>
    /// Weights complete interior centers without reading outside their two-sample logical halo.
    /// </summary>
    /// <param name="row">The full logical source row.</param>
    /// <param name="x">The first center index. Both halo samples and every result lane must exist.</param>
    /// <param name="operation">The row's center, distance-one and distance-two weights.</param>
    /// <returns>One horizontal weighted sum per lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> WeightedSum256(ReadOnlySpan<float> row, int x, WeightedSumOperator operation)
    {
        ref float first = ref MemoryMarshal.GetReference(row);
        Vector256<float> center = Vector256.LoadUnsafe(ref first, (uint)x);
        Vector256<float> near = Vector256.LoadUnsafe(ref first, (uint)(x - 1)) + Vector256.LoadUnsafe(ref first, (uint)(x + 1));
        Vector256<float> far = Vector256.LoadUnsafe(ref first, (uint)(x - 2)) + Vector256.LoadUnsafe(ref first, (uint)(x + 2));
        return operation.Invoke(center, near, far);
    }

    /// <summary>
    /// Weights complete interior centers without reading outside their two-sample logical halo.
    /// </summary>
    /// <param name="row">The full logical source row.</param>
    /// <param name="x">The first center index. Both halo samples and every result lane must exist.</param>
    /// <param name="operation">The row's center, distance-one and distance-two weights.</param>
    /// <returns>One horizontal weighted sum per lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> WeightedSum512(ReadOnlySpan<float> row, int x, WeightedSumOperator operation)
    {
        ref float first = ref MemoryMarshal.GetReference(row);
        Vector512<float> center = Vector512.LoadUnsafe(ref first, (uint)x);
        Vector512<float> near = Vector512.LoadUnsafe(ref first, (uint)(x - 1)) + Vector512.LoadUnsafe(ref first, (uint)(x + 1));
        Vector512<float> far = Vector512.LoadUnsafe(ref first, (uint)(x - 2)) + Vector512.LoadUnsafe(ref first, (uint)(x + 2));
        return operation.Invoke(center, near, far);
    }

    /// <summary>
    /// Applies a symmetric three-by-three stencil to three borrowed rows with an explicit one-sample horizontal halo.
    /// </summary>
    /// <param name="top">The row above the output, including one sample before and after its requested range.</param>
    /// <param name="middle">The center row, with the same halo and logical origin as top.</param>
    /// <param name="bottom">The row below the output, with the same halo and logical origin as top.</param>
    /// <param name="centerWeight">The center sample weight.</param>
    /// <param name="axialWeight">The weight for each horizontal and vertical neighbor.</param>
    /// <param name="diagonalWeight">The weight for each diagonal neighbor.</param>
    /// <param name="output">The exact output range, distinct from all three inputs.</param>
    /// <remarks>
    /// Each input contains output.Length plus two samples. The caller supplies image-edge extension in the halo.
    /// The stencil uses the pairwise sums and nested multiply-add operations from native stage_gaborish.cc.
    /// </remarks>
    public static void Symmetric3Row(
        ReadOnlySpan<float> top,
        ReadOnlySpan<float> middle,
        ReadOnlySpan<float> bottom,
        float centerWeight,
        float axialWeight,
        float diagonalWeight,
        Span<float> output)
    {
        ref float topReference = ref MemoryMarshal.GetReference(top);
        ref float middleReference = ref MemoryMarshal.GetReference(middle);
        ref float bottomReference = ref MemoryMarshal.GetReference(bottom);
        ref float outputReference = ref MemoryMarshal.GetReference(output);
        Symmetric3Operator operation = new(centerWeight, axialWeight, diagonalWeight);
        int x = 0;

        // Input index x+1 is output pixel x. Neighbor loads start at x and x+2, so each full vector consumes only its declared halo.
        // Loads can be unaligned. No output register crosses the requested range, and tails use the identical scalar equation.
        if (Vector512.IsHardwareAccelerated)
        {
            for (; x <= output.Length - Vector512<float>.Count; x += Vector512<float>.Count)
            {
                Vector512<float> center = Vector512.LoadUnsafe(ref middleReference, (uint)(x + 1));
                Vector512<float> horizontal = Vector512.LoadUnsafe(ref middleReference, (uint)x) + Vector512.LoadUnsafe(ref middleReference, (uint)(x + 2));
                Vector512<float> vertical = Vector512.LoadUnsafe(ref topReference, (uint)(x + 1)) + Vector512.LoadUnsafe(ref bottomReference, (uint)(x + 1));
                Vector512<float> topDiagonal = Vector512.LoadUnsafe(ref topReference, (uint)x) + Vector512.LoadUnsafe(ref topReference, (uint)(x + 2));
                Vector512<float> bottomDiagonal = Vector512.LoadUnsafe(ref bottomReference, (uint)x) + Vector512.LoadUnsafe(ref bottomReference, (uint)(x + 2));
                Vector512<float> result = operation.Invoke(center, horizontal + vertical, topDiagonal + bottomDiagonal);
                result.StoreUnsafe(ref outputReference, (uint)x);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; x <= output.Length - Vector256<float>.Count; x += Vector256<float>.Count)
            {
                Vector256<float> center = Vector256.LoadUnsafe(ref middleReference, (uint)(x + 1));
                Vector256<float> horizontal = Vector256.LoadUnsafe(ref middleReference, (uint)x) + Vector256.LoadUnsafe(ref middleReference, (uint)(x + 2));
                Vector256<float> vertical = Vector256.LoadUnsafe(ref topReference, (uint)(x + 1)) + Vector256.LoadUnsafe(ref bottomReference, (uint)(x + 1));
                Vector256<float> topDiagonal = Vector256.LoadUnsafe(ref topReference, (uint)x) + Vector256.LoadUnsafe(ref topReference, (uint)(x + 2));
                Vector256<float> bottomDiagonal = Vector256.LoadUnsafe(ref bottomReference, (uint)x) + Vector256.LoadUnsafe(ref bottomReference, (uint)(x + 2));
                Vector256<float> result = operation.Invoke(center, horizontal + vertical, topDiagonal + bottomDiagonal);
                result.StoreUnsafe(ref outputReference, (uint)x);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; x <= output.Length - Vector128<float>.Count; x += Vector128<float>.Count)
            {
                Vector128<float> center = Vector128.LoadUnsafe(ref middleReference, (uint)(x + 1));
                Vector128<float> horizontal = Vector128.LoadUnsafe(ref middleReference, (uint)x) + Vector128.LoadUnsafe(ref middleReference, (uint)(x + 2));
                Vector128<float> vertical = Vector128.LoadUnsafe(ref topReference, (uint)(x + 1)) + Vector128.LoadUnsafe(ref bottomReference, (uint)(x + 1));
                Vector128<float> topDiagonal = Vector128.LoadUnsafe(ref topReference, (uint)x) + Vector128.LoadUnsafe(ref topReference, (uint)(x + 2));
                Vector128<float> bottomDiagonal = Vector128.LoadUnsafe(ref bottomReference, (uint)x) + Vector128.LoadUnsafe(ref bottomReference, (uint)(x + 2));
                Vector128<float> result = operation.Invoke(center, horizontal + vertical, topDiagonal + bottomDiagonal);
                result.StoreUnsafe(ref outputReference, (uint)x);
            }
        }

        for (; x < output.Length; x++)
        {
            float sum1 = (middle[x] + middle[x + 2]) + (top[x + 1] + bottom[x + 1]);
            float sum2 = (top[x] + top[x + 2]) + (bottom[x] + bottom[x + 2]);
            output[x] = operation.Invoke(middle[x + 1], sum1, sum2);
        }
    }

    public static float SlowSymmetric3Pixel(
        JxlPlane<float> image,
        int x,
        int y,
        int width,
        int height,
        JxlWeightsSymmetric3 weights,
        Func<long, long, int> wrapX,
        Func<long, long, int> wrapY)
    {
        float sum = 0.0f;

        float c0 = weights.GetCVector()[0];
        float r0 = weights.GetRVector()[0];
        float d0 = weights.GetDVector()[0];

        for (int ky = -1; ky <= 1; ky++)
        {
            int yy = wrapY(y + ky, height);
            ReadOnlySpan<float> row = image.GetRow(yy);

            float wc = (ky == 0) ? c0 : r0;
            float wlr = (ky == 0) ? r0 : d0;

            int xm1 = wrapX(x - 1, width);
            int xp1 = wrapX(x + 1, width);

            sum += (row[x] * wc) + ((row[xm1] + row[xp1]) * wlr);
        }

        return sum;
    }

    public static void SlowSymmetric3Row(
        JxlPlane<float> image,
        int y,
        int width,
        int height,
        JxlWeightsSymmetric3 weights,
        Span<float> outputRow,
        Func<long, long, int> wrapY)
    {
        outputRow[0] = SlowSymmetric3Pixel(
            image,
            0,
            y,
            width,
            height,
            weights,
            JxlImageOperations.Mirror,
            wrapY);

        for (int x = 1; x < width - 1; x++)
        {
            outputRow[x] = SlowSymmetric3Pixel(
                image,
                x,
                y,
                width,
                height,
                weights,
                JxlImageOperations.Mirror,
                wrapY);
        }

        outputRow[width - 1] = SlowSymmetric3Pixel(
            image,
            width - 1,
            y,
            width,
            height,
            weights,
            JxlImageOperations.Mirror,
            wrapY);
    }

    public static void SlowSymmetric3(
        JxlPlane<float> input,
        Rectangle rect,
        JxlWeightsSymmetric3 weights,
        JxlPlane<float> output)
    {
        int width = rect.Width;
        int height = rect.Height;

        const int radius = 1;

        for (int y = 0; y < height; y++)
        {
            Span<float> rowOut = output.GetRow(y);

            if (y < radius || y >= height - radius)
            {
                SlowSymmetric3Row(
                    input,
                    y,
                    width,
                    height,
                    weights,
                    rowOut,
                    JxlImageOperations.Mirror);
            }
            else
            {
                SlowSymmetric3Row(
                    input,
                    y,
                    width,
                    height,
                    weights,
                    rowOut,
                    JxlImageOperations.Mirror);
            }
        }
    }

    public static float SlowSeparablePixel(
        JxlPlane<float> image,
        Rectangle rect,
        int x,
        int y,
        int radius,
        ReadOnlySpan<float> horzWeights,
        ReadOnlySpan<float> vertWeights)
    {
        int width = image.XSize;
        int height = image.YSize;

        float sum = 0;

        for (int dy = -radius; dy <= radius; dy++)
        {
            float wy = vertWeights[Math.Abs(dy) * 4];
            int sy = JxlImageOperations.Mirror(rect.Y + y + dy, height);
            ReadOnlySpan<float> row = image.GetRow(sy);

            for (int dx = -radius; dx <= radius; dx++)
            {
                float wx = horzWeights[Math.Abs(dx) * 4];
                int sx = JxlImageOperations.Mirror(rect.X + x + dx, width);
                sum += row[sx] * wx * wy;
            }
        }

        return sum;
    }

    public static void SlowSeparable(
        JxlPlane<float> input,
        Rectangle inputRect,
        JxlWeightsSeparable5 weights,
        JxlPlane<float> output,
        Rectangle outputRect,
        int radius)
    {
        ReadOnlySpan<float> horz = weights.Horizontal;
        ReadOnlySpan<float> vert = weights.Vertical;

        for (int y = 0; y < inputRect.Height; y++)
        {
            Span<float> rowOut = output.GetRow(outputRect, y);

            for (int x = 0; x < inputRect.Width; x++)
            {
                rowOut[x] = SlowSeparablePixel(
                    input,
                    inputRect,
                    x,
                    y,
                    radius,
                    horz,
                    vert);
            }
        }
    }

    public static void SlowSeparable5(
        JxlPlane<float> input,
        Rectangle inputRect,
        JxlWeightsSeparable5 weights,
        JxlPlane<float> output,
        Rectangle outputRect)
        => SlowSeparable(input, inputRect, weights, output, outputRect, 2);

    public static void FirstL1(ReadOnlySpan<float> c, Span<float> dst)
    {
        dst[0] = c[0];
        for (int i = 1; i < dst.Length; i++)
        {
            dst[i] = c[i - 1];
        }
    }

    public static void FirstL2(ReadOnlySpan<float> c, Span<float> dst)
    {
        dst[0] = c[1];
        dst[1] = c[0];

        for (int i = 2; i < dst.Length; i++)
        {
            dst[i] = c[i - 2];
        }
    }

    /// <summary>
    /// Combines a symmetric stencil's center, axial sum and diagonal sum with native Gaborish multiply-add order.
    /// </summary>
    private readonly struct Symmetric3Operator : TensorPrimitives_.ITernaryOperator
    {
        private readonly float centerWeight;
        private readonly float axialWeight;
        private readonly float diagonalWeight;

        /// <summary>
        /// Retains the normalized weights shared by every sample in one output row.
        /// </summary>
        /// <param name="centerWeight">The center sample weight.</param>
        /// <param name="axialWeight">The weight for each sample in the axial sum.</param>
        /// <param name="diagonalWeight">The weight for each sample in the diagonal sum.</param>
        public Symmetric3Operator(float centerWeight, float axialWeight, float diagonalWeight)
        {
            this.centerWeight = centerWeight;
            this.axialWeight = axialWeight;
            this.diagonalWeight = diagonalWeight;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float first, float second, float third)
        {
            float result = float.MultiplyAddEstimate(second, this.axialWeight, first * this.centerWeight);
            return float.MultiplyAddEstimate(third, this.diagonalWeight, result);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> first, Vector128<float> second, Vector128<float> third)
        {
            // Each lane keeps one output sample. Weight the center first, then add axial and diagonal contributions in native order.
            Vector128<float> result = Vector128.MultiplyAddEstimate(second, Vector128.Create(this.axialWeight), first * Vector128.Create(this.centerWeight));
            return Vector128.MultiplyAddEstimate(third, Vector128.Create(this.diagonalWeight), result);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> first, Vector256<float> second, Vector256<float> third)
        {
            // Each lane keeps one output sample. Weight the center first, then add axial and diagonal contributions in native order.
            Vector256<float> result = Vector256.MultiplyAddEstimate(second, Vector256.Create(this.axialWeight), first * Vector256.Create(this.centerWeight));
            return Vector256.MultiplyAddEstimate(third, Vector256.Create(this.diagonalWeight), result);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> first, Vector512<float> second, Vector512<float> third)
        {
            // Each lane keeps one output sample. Weight the center first, then add axial and diagonal contributions in native order.
            Vector512<float> result = Vector512.MultiplyAddEstimate(second, Vector512.Create(this.axialWeight), first * Vector512.Create(this.centerWeight));
            return Vector512.MultiplyAddEstimate(third, Vector512.Create(this.diagonalWeight), result);
        }
    }

    /// <summary>
    /// Weights a row's center and its two symmetric neighbor pairs without fused reassociation.
    /// </summary>
    private readonly struct WeightedSumOperator : TensorPrimitives_.ITernaryOperator
    {
        private readonly float centerWeight;
        private readonly float nearWeight;
        private readonly float farWeight;

        /// <summary>Retains one horizontal row's symmetric weights for all centers and SIMD widths.</summary>
        /// <param name="centerWeight">The center weight.</param>
        /// <param name="nearWeight">The weight of each distance-one neighbor.</param>
        /// <param name="farWeight">The weight of each distance-two neighbor.</param>
        public WeightedSumOperator(float centerWeight, float nearWeight, float farWeight)
        {
            this.centerWeight = centerWeight;
            this.nearWeight = nearWeight;
            this.farWeight = farWeight;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float first, float second, float third)
        {
            // Native convolve_symmetric5.cc adds the inner pair before the outer pair. Keep each product rounded separately.
            float near = second * this.nearWeight;
            float center = first * this.centerWeight;
            float far = third * this.farWeight;
            return far + (near + center);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> first, Vector128<float> second, Vector128<float> third)
        {
            // Native convolve_symmetric5.cc adds the inner pair before the outer pair. Keep each product rounded separately.
            Vector128<float> near = second * Vector128.Create(this.nearWeight);
            Vector128<float> center = first * Vector128.Create(this.centerWeight);
            Vector128<float> far = third * Vector128.Create(this.farWeight);
            return far + (near + center);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> first, Vector256<float> second, Vector256<float> third)
        {
            // Native convolve_symmetric5.cc adds the inner pair before the outer pair. Keep each product rounded separately.
            Vector256<float> near = second * Vector256.Create(this.nearWeight);
            Vector256<float> center = first * Vector256.Create(this.centerWeight);
            Vector256<float> far = third * Vector256.Create(this.farWeight);
            return far + (near + center);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> first, Vector512<float> second, Vector512<float> third)
        {
            // Native convolve_symmetric5.cc adds the inner pair before the outer pair. Keep each product rounded separately.
            Vector512<float> near = second * Vector512.Create(this.nearWeight);
            Vector512<float> center = first * Vector512.Create(this.centerWeight);
            Vector512<float> far = third * Vector512.Create(this.farWeight);
            return far + (near + center);
        }
    }
}
