// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// Convolution filters
/// </summary>
internal static class JxlConvolve
{
    /// <summary>
    /// Weighted sum of 1x5 pixels around ix, iy with [wx2, wx1, wx0, wx1, wx2].
    /// </summary>
    public static float WeightedSumBorder(
        JxlImageF input,
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
        JxlImageF input,
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

    public static float Symmetric5Border(JxlImageF input, Func<long, long, int> wrapY, long ix, long iy, JxlWeightsSymmetric5 weights)
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
        JxlImageF image,
        int ix,
        Func<long, long, int> wrapY,
        int rix,
        long iy,
        JxlWeightsSymmetric5 weights,
        Span<float> rowOut)
    {
        Vector<float> w0 = LoadDuplicate128(weights.GetCVector());  // c
        Vector<float> w1 = LoadDuplicate128(weights.GetRVector());  // r
        Vector<float> w2 = LoadDuplicate128(weights.GetR2Vector()); // R
        Vector<float> w4 = LoadDuplicate128(weights.GetDVector());  // d
        Vector<float> w5 = LoadDuplicate128(weights.GetLVector());  // L
        Vector<float> w8 = LoadDuplicate128(weights.GetD2Vector()); // D

        int height = image.YSize;
        Vector<float> sum0 = WeightedSum(image, wrapY, ix, iy, height, w0, w1, w2)
            + WeightedSum(image, wrapY, ix, iy - 2, height, w2, w5, w8);

        Vector<float> sum1 = WeightedSum(image, wrapY, ix, iy + 2, height, w2, w5, w8);

        sum0 += WeightedSum(image, wrapY, ix, iy - 1, height, w1, w4, w5);
        sum1 += WeightedSum(image, wrapY, ix, iy + 1, height, w1, w4, w5);

        (sum0 + sum1).StoreUnsafe(ref Unsafe.Add(ref MemoryMarshal.GetReference(rowOut), rix));
    }

    public static void Symmetric5Row(
        JxlImageF image,
        Func<long, long, int> wrapY,
        in Rectangle rect,
        long iy,
        JxlWeightsSymmetric5 weights,
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
            rowOut[rix] = Symmetric5Border(image, wrapY, ix, iy, weights);
        }

        for (; ix + n + radius <= xEnd; ix += n, rix += n)
        {
            Symmetric5Interior(image, ix, wrapY, rix, iy, weights, rowOut);
        }

        for (; ix < xEnd; ix++, rix++)
        {
            rowOut[rix] = Symmetric5Border(image, wrapY, ix, iy, weights);
        }
    }

    public static bool Symmetric5(
        JxlImageF input,
        in Rectangle rectangle,
        JxlWeightsSymmetric5 weights,
        JxlImageF output,
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

    public static float SlowSymmetric3Pixel(
        JxlImageF image,
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
        JxlImageF image,
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
        JxlImageF input,
        Rectangle rect,
        JxlWeightsSymmetric3 weights,
        JxlImageF output)
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
        JxlImageF image,
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
        JxlImageF input,
        Rectangle inputRect,
        JxlWeightsSeparable5 weights,
        JxlImageF output,
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
        JxlImageF input,
        Rectangle inputRect,
        JxlWeightsSeparable5 weights,
        JxlImageF output,
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
    /// A SIMD utility method which takes in the 128 bit vector
    /// and duplicates its values to fit in the CPU vector size.
    /// For example,
    ///
    ///     128 bit vectors: A B C D    (as-is)
    ///     256 bit vectors: A B C D A B C D (duplicate once)
    ///     512 bit vectors: A B C D A B C D A B C D A B C D (duplicate three times)
    ///
    /// Vector&lt;T&gt; has support for arbitrarily large
    /// vector sizes. For example, some ARM CPUs support 2048-bit
    /// vectors through Vector&lt;T&gt;. In that specific case, this
    /// method can be used for future-proofing.
    ///
    /// Note that this method, albeit future-proof, may be considered
    /// slow for smaller vector sizes (think CPUs with 256bit vectors).
    /// </summary>
    /// <param name="vec">Vector to duplicate.</param>
    /// <returns>New vector that is duplicated across the width.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<float> LoadDuplicate128(Vector128<float> vec)
    {
        Span<float> value = stackalloc float[Vector<float>.Count];
        for (int i = 0; i < Vector<float>.Count; i += 4)
        {
            vec.CopyTo(value[i..]);
        }

        return new(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RoundUpTo(int value, int multiple) => ((value + multiple - 1) / multiple) * multiple;
}
