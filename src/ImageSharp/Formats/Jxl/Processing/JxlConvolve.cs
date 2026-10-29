// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
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
    /// Weighted sum of 1x5 pixels around ix, iy with [wx2, wx1, wx0, wx1, wx2].
    /// </summary>
    public static float WeightedSumBorder(
        JxlPlane<float> input,
        Func<long, long, int> wrapY,
        long ix,
        long iy,
        int width,
        int height,
        float wx0,
        float wx1,
        float wx2)
    {
        ReadOnlySpan<float> row = input.GetRow(wrapY(iy, height));

        float inM2 = row[WrapMirror(ix - 2, width)];
        float inP2 = row[WrapMirror(ix + 2, width)];
        float inM1 = row[WrapMirror(ix - 1, width)];
        float inP1 = row[WrapMirror(ix + 1, width)];
        float in00 = row[(int)ix];

        float sum2 = wx2 * (inM2 + inP2);
        float sum1 = wx1 * (inM1 + inP1);
        float sum0 = wx0 * in00;

        return sum2 + (sum1 + sum0);
    }

    public static Vector<float> WeightedSum(
        JxlPlane<float> input,
        Func<long, long, int> wrapY,
        int ix,
        long iy,
        int height,
        Vector<float> wx0,
        Vector<float> wx1,
        Vector<float> wx2)
    {
        ReadOnlySpan<float> center = input.GetRow(wrapY(iy, height))[ix..];
        ref float centerRef = ref MemoryMarshal.GetReference(center);

        Vector<float> inM2 = Vector.LoadUnsafe(ref Unsafe.Subtract(ref centerRef, 2));
        Vector<float> inP2 = Vector.LoadUnsafe(ref Unsafe.Add(ref centerRef, 2));
        Vector<float> inM1 = Vector.LoadUnsafe(ref Unsafe.Subtract(ref centerRef, 1));
        Vector<float> inP1 = Vector.LoadUnsafe(ref Unsafe.Add(ref centerRef, 1));
        Vector<float> in00 = Vector.LoadUnsafe(ref centerRef);

        Vector<float> sum2 = wx2 * (inM2 + inP2);
        Vector<float> sum1 = wx1 * (inM1 + inP1);
        Vector<float> sum0 = wx0 * in00;

        return sum2 + (sum1 + sum0);
    }

    public static float Symmetric5Border(JxlPlane<float> input, Func<long, long, int> wrapY, long ix, long iy, ref JxlWeightsSymmetric5 weights)
    {
        float w0 = weights.GetCVector()[0];
        float w1 = weights.GetRVector()[0];
        float w2 = weights.GetR2Vector()[0];
        float w4 = weights.GetDVector()[0];
        float w5 = weights.GetCVector()[0];
        float w8 = weights.GetD2Vector()[0];

        int width = input.XSize;
        int height = input.YSize;

        float sum0 = WeightedSumBorder(input, wrapY, ix, iy, width, height, w0, w1, w2)
            + WeightedSumBorder(input, wrapY, ix, iy - 2, width, height, w2, w5, w8);

        float sum1 = WeightedSumBorder(input, wrapY, ix, iy + 2, width, height, w2, w5, w8);

        sum0 += WeightedSumBorder(input, wrapY, ix, iy + 1, width, height, w1, w4, w5);
        sum1 += WeightedSumBorder(input, wrapY, ix, iy - 1, width, height, w1, w4, w5);

        return sum0 + sum1;
    }

    public static void Symmetric5Interior(
        JxlPlane<float> image,
        int ix,
        Func<long, long, int> wrapY,
        int rix,
        long iy,
        ref JxlWeightsSymmetric5 weights,
        Span<float> rowOut)
    {
        Vector<float> w0 = JxlSimdUtils.LoadDuplicate128(weights.GetCVector());  // c
        Vector<float> w1 = JxlSimdUtils.LoadDuplicate128(weights.GetRVector());  // r
        Vector<float> w2 = JxlSimdUtils.LoadDuplicate128(weights.GetR2Vector()); // R
        Vector<float> w4 = JxlSimdUtils.LoadDuplicate128(weights.GetDVector());  // d
        Vector<float> w5 = JxlSimdUtils.LoadDuplicate128(weights.GetLVector());  // L
        Vector<float> w8 = JxlSimdUtils.LoadDuplicate128(weights.GetD2Vector()); // D

        int height = image.YSize;
        Vector<float> sum0 = WeightedSum(image, wrapY, ix, iy, height, w0, w1, w2)
            + WeightedSum(image, wrapY, ix, iy - 2, height, w2, w5, w8);

        Vector<float> sum1 = WeightedSum(image, wrapY, ix, iy + 2, height, w2, w5, w8);

        sum0 += WeightedSum(image, wrapY, ix, iy - 1, height, w1, w4, w5);
        sum1 += WeightedSum(image, wrapY, ix, iy + 1, height, w1, w4, w5);

        (sum0 + sum1).StoreUnsafe(ref Unsafe.Add(ref MemoryMarshal.GetReference(rowOut), rix));
    }

    public static void Symmetric5Row(
        JxlPlane<float> image,
        Func<long, long, int> wrapY,
        in Rectangle rect,
        long iy,
        ref JxlWeightsSymmetric5 weights,
        Span<float> rowOut)
    {
        const int radius = 2;
        int xEnd = rect.Right;

        int rix = 0;
        int ix = rect.X;

        int n = Vector<float>.Count;
        int alignedX = RoundUpTo(radius, n);

        for (; ix < Math.Min(alignedX, xEnd); ix++, rix++)
        {
            rowOut[rix] = Symmetric5Border(image, wrapY, ix, iy, ref weights);
        }

        for (; ix + n + radius <= xEnd; ix += n, rix += n)
        {
            Symmetric5Interior(image, ix, wrapY, rix, iy, ref weights, rowOut);
        }

        for (; ix < xEnd; ix++, rix++)
        {
            rowOut[rix] = Symmetric5Border(image, wrapY, ix, iy, ref weights);
        }
    }

    public static bool Symmetric5(
        JxlPlane<float> input,
        in Rectangle rectangle,
        ref JxlWeightsSymmetric5 weights,
        JxlPlane<float> output,
        Rectangle outputRect)
    {
        if (rectangle.Width != outputRect.Width || rectangle.Height != outputRect.Height)
        {
            return false;
        }

        int height = rectangle.Height;

        for (int riy = 0; riy < height; riy++)
        {
            int iy = rectangle.Y + riy;

            if (iy < 2 || iy >= rectangle.Height - 2)
            {
                Symmetric5Row(input, WrapMirror, in rectangle, iy, weights, output.GetRow(outputRect, riy));
            }
            else
            {
                Symmetric5Row(input, in rectangle, iy, weights, output.GetRow(outputRect, riy));
            }
        }

        return true;
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
            WrapMirror,
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
                WrapUnchanged,
                wrapY);
        }

        outputRow[width - 1] = SlowSymmetric3Pixel(
            image,
            width - 1,
            y,
            width,
            height,
            weights,
            WrapMirror,
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
                    WrapMirror);
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
                    WrapUnchanged);
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
            int sy = WrapMirror(rect.Y + y + dy, height);
            ReadOnlySpan<float> row = image.GetRow(sy);

            for (int dx = -radius; dx <= radius; dx++)
            {
                float wx = horzWeights[Math.Abs(dx) * 4];
                int sx = WrapMirror(rect.X + x + dx, width);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RoundUpTo(int value, int multiple) => ((value + multiple - 1) / multiple) * multiple;
}
