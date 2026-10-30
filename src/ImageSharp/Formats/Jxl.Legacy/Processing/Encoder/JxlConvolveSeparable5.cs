// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

/// <summary>
/// Applies a separable five-tap kernel during one traversal of an output row.
/// </summary>
internal static class JxlConvolveSeparable5
{
    /// <summary>
    /// Convolves five borrowed source rows without allocating intermediate rows.
    /// </summary>
    /// <param name="top2">The vertically mirrored row two positions above the output.</param>
    /// <param name="top1">The vertically mirrored row one position above the output.</param>
    /// <param name="middle">The source row at the output position.</param>
    /// <param name="bottom1">The vertically mirrored row one position below the output.</param>
    /// <param name="bottom2">The vertically mirrored row two positions below the output.</param>
    /// <param name="weights">The horizontal and vertical weights in four-value groups.</param>
    /// <param name="output">The distinct output row. All rows have the rectangle's logical width.</param>
    public static void ConvolveRow(
        ReadOnlySpan<float> top2,
        ReadOnlySpan<float> top1,
        ReadOnlySpan<float> middle,
        ReadOnlySpan<float> bottom1,
        ReadOnlySpan<float> bottom2,
        in JxlWeightsSeparable5 weights,
        Span<float> output)
    {
        float horizontal0 = weights.Horizontal[0];
        float horizontal1 = weights.Horizontal[4];
        float horizontal2 = weights.Horizontal[8];
        float vertical0 = weights.Vertical[0];
        float vertical1 = weights.Vertical[4];
        float vertical2 = weights.Vertical[8];
        int width = output.Length;
        int x = 0;

        // The first two samples need reflected horizontal neighbors. This also handles rows narrower than the kernel.
        for (; x < Math.Min(2, width); x++)
        {
            output[x] = ConvolvePixel(top2, top1, middle, bottom1, bottom2, x, horizontal0, horizontal1, horizontal2, vertical0, vertical1, vertical2);
        }

        ref float top2Ref = ref MemoryMarshal.GetReference(top2);
        ref float top1Ref = ref MemoryMarshal.GetReference(top1);
        ref float middleRef = ref MemoryMarshal.GetReference(middle);
        ref float bottom1Ref = ref MemoryMarshal.GetReference(bottom1);
        ref float bottom2Ref = ref MemoryMarshal.GetReference(bottom2);
        ref float outputRef = ref MemoryMarshal.GetReference(output);

        // Each lane produces one adjacent output sample. Complete blocks need two real samples on either side.
        // Neighbor loads are unaligned. Their bounds exclude row padding, so the final block never reads past logical samples.
        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<float> horizontal0Vector = Vector512.Create(horizontal0);
            Vector512<float> horizontal1Vector = Vector512.Create(horizontal1);
            Vector512<float> horizontal2Vector = Vector512.Create(horizontal2);
            Vector512<float> vertical0Vector = Vector512.Create(vertical0);
            Vector512<float> vertical1Vector = Vector512.Create(vertical1);
            Vector512<float> vertical2Vector = Vector512.Create(vertical2);

            for (; x + Vector512<float>.Count + 2 <= width; x += Vector512<float>.Count)
            {
                Vector512<float> center = HorizontalConvolve(ref Unsafe.Add(ref middleRef, x), horizontal0Vector, horizontal1Vector, horizontal2Vector);
                Vector512<float> top = HorizontalConvolve(ref Unsafe.Add(ref top1Ref, x), horizontal0Vector, horizontal1Vector, horizontal2Vector);
                Vector512<float> bottom = HorizontalConvolve(ref Unsafe.Add(ref bottom1Ref, x), horizontal0Vector, horizontal1Vector, horizontal2Vector);
                Vector512<float> result = Vector512.MultiplyAddEstimate(top + bottom, vertical1Vector, center * vertical0Vector);
                top = HorizontalConvolve(ref Unsafe.Add(ref top2Ref, x), horizontal0Vector, horizontal1Vector, horizontal2Vector);
                bottom = HorizontalConvolve(ref Unsafe.Add(ref bottom2Ref, x), horizontal0Vector, horizontal1Vector, horizontal2Vector);
                result = Vector512.MultiplyAddEstimate(top + bottom, vertical2Vector, result);
                result.StoreUnsafe(ref outputRef, (nuint)x);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> horizontal0Vector = Vector256.Create(horizontal0);
            Vector256<float> horizontal1Vector = Vector256.Create(horizontal1);
            Vector256<float> horizontal2Vector = Vector256.Create(horizontal2);
            Vector256<float> vertical0Vector = Vector256.Create(vertical0);
            Vector256<float> vertical1Vector = Vector256.Create(vertical1);
            Vector256<float> vertical2Vector = Vector256.Create(vertical2);

            for (; x + Vector256<float>.Count + 2 <= width; x += Vector256<float>.Count)
            {
                Vector256<float> center = HorizontalConvolve(ref Unsafe.Add(ref middleRef, x), horizontal0Vector, horizontal1Vector, horizontal2Vector);
                Vector256<float> top = HorizontalConvolve(ref Unsafe.Add(ref top1Ref, x), horizontal0Vector, horizontal1Vector, horizontal2Vector);
                Vector256<float> bottom = HorizontalConvolve(ref Unsafe.Add(ref bottom1Ref, x), horizontal0Vector, horizontal1Vector, horizontal2Vector);
                Vector256<float> result = Vector256.MultiplyAddEstimate(top + bottom, vertical1Vector, center * vertical0Vector);
                top = HorizontalConvolve(ref Unsafe.Add(ref top2Ref, x), horizontal0Vector, horizontal1Vector, horizontal2Vector);
                bottom = HorizontalConvolve(ref Unsafe.Add(ref bottom2Ref, x), horizontal0Vector, horizontal1Vector, horizontal2Vector);
                result = Vector256.MultiplyAddEstimate(top + bottom, vertical2Vector, result);
                result.StoreUnsafe(ref outputRef, (nuint)x);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> horizontal0Vector = Vector128.Create(horizontal0);
            Vector128<float> horizontal1Vector = Vector128.Create(horizontal1);
            Vector128<float> horizontal2Vector = Vector128.Create(horizontal2);
            Vector128<float> vertical0Vector = Vector128.Create(vertical0);
            Vector128<float> vertical1Vector = Vector128.Create(vertical1);
            Vector128<float> vertical2Vector = Vector128.Create(vertical2);

            for (; x + Vector128<float>.Count + 2 <= width; x += Vector128<float>.Count)
            {
                Vector128<float> center = HorizontalConvolve(ref Unsafe.Add(ref middleRef, x), horizontal0Vector, horizontal1Vector, horizontal2Vector);
                Vector128<float> top = HorizontalConvolve(ref Unsafe.Add(ref top1Ref, x), horizontal0Vector, horizontal1Vector, horizontal2Vector);
                Vector128<float> bottom = HorizontalConvolve(ref Unsafe.Add(ref bottom1Ref, x), horizontal0Vector, horizontal1Vector, horizontal2Vector);
                Vector128<float> result = Vector128.MultiplyAddEstimate(top + bottom, vertical1Vector, center * vertical0Vector);
                top = HorizontalConvolve(ref Unsafe.Add(ref top2Ref, x), horizontal0Vector, horizontal1Vector, horizontal2Vector);
                bottom = HorizontalConvolve(ref Unsafe.Add(ref bottom2Ref, x), horizontal0Vector, horizontal1Vector, horizontal2Vector);
                result = Vector128.MultiplyAddEstimate(top + bottom, vertical2Vector, result);
                result.StoreUnsafe(ref outputRef, (nuint)x);
            }
        }

        // Narrower SIMD blocks finish complete interior groups. Reflect the final scalar samples and right border.
        for (; x < width; x++)
        {
            output[x] = ConvolvePixel(top2, top1, middle, bottom1, bottom2, x, horizontal0, horizontal1, horizontal2, vertical0, vertical1, vertical2);
        }
    }

    /// <summary>
    /// Convolves one output sample with reflected neighbors and the same row grouping as the SIMD path.
    /// </summary>
    /// <param name="top2">The vertically mirrored row two positions above the output.</param>
    /// <param name="top1">The vertically mirrored row one position above the output.</param>
    /// <param name="middle">The row at the output position.</param>
    /// <param name="bottom1">The vertically mirrored row one position below the output.</param>
    /// <param name="bottom2">The vertically mirrored row two positions below the output.</param>
    /// <param name="x">The horizontal output position.</param>
    /// <param name="horizontal0">The horizontal center weight.</param>
    /// <param name="horizontal1">The horizontal distance-one weight.</param>
    /// <param name="horizontal2">The horizontal distance-two weight.</param>
    /// <param name="vertical0">The vertical center weight.</param>
    /// <param name="vertical1">The vertical distance-one weight.</param>
    /// <param name="vertical2">The vertical distance-two weight.</param>
    /// <returns>The weighted output sample.</returns>
    private static float ConvolvePixel(
        ReadOnlySpan<float> top2,
        ReadOnlySpan<float> top1,
        ReadOnlySpan<float> middle,
        ReadOnlySpan<float> bottom1,
        ReadOnlySpan<float> bottom2,
        int x,
        float horizontal0,
        float horizontal1,
        float horizontal2,
        float vertical0,
        float vertical1,
        float vertical2)
    {
        // Reflect at the edge sample: -1 maps to 0, -2 maps to 1. Mirror also handles repeated reflections at width one.
        // All five rows use the same neighbor indices, so compute them once per border sample.
        int left1 = JxlImageOperations.Mirror(x - 1, middle.Length);
        int left2 = JxlImageOperations.Mirror(x - 2, middle.Length);
        int right1 = JxlImageOperations.Mirror((long)x + 1, middle.Length);
        int right2 = JxlImageOperations.Mirror((long)x + 2, middle.Length);
        float center = HorizontalConvolve(middle, x, left1, left2, right1, right2, horizontal0, horizontal1, horizontal2);
        float top = HorizontalConvolve(top1, x, left1, left2, right1, right2, horizontal0, horizontal1, horizontal2);
        float bottom = HorizontalConvolve(bottom1, x, left1, left2, right1, right2, horizontal0, horizontal1, horizontal2);
        float result = float.MultiplyAddEstimate(top + bottom, vertical1, center * vertical0);
        top = HorizontalConvolve(top2, x, left1, left2, right1, right2, horizontal0, horizontal1, horizontal2);
        bottom = HorizontalConvolve(bottom2, x, left1, left2, right1, right2, horizontal0, horizontal1, horizontal2);
        return float.MultiplyAddEstimate(top + bottom, vertical2, result);
    }

    /// <summary>
    /// Convolves one horizontal sample using reflected neighbor indices.
    /// </summary>
    /// <param name="row">The borrowed logical row.</param>
    /// <param name="x">The center index.</param>
    /// <param name="left1">The reflected distance-one index to the left.</param>
    /// <param name="left2">The reflected distance-two index to the left.</param>
    /// <param name="right1">The reflected distance-one index to the right.</param>
    /// <param name="right2">The reflected distance-two index to the right.</param>
    /// <param name="weight0">The center weight.</param>
    /// <param name="weight1">The distance-one weight.</param>
    /// <param name="weight2">The distance-two weight.</param>
    /// <returns>The horizontal weighted sum.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float HorizontalConvolve(
        ReadOnlySpan<float> row,
        int x,
        int left1,
        int left2,
        int right1,
        int right2,
        float weight0,
        float weight1,
        float weight2)
    {
        float result = float.MultiplyAddEstimate(row[left1] + row[right1], weight1, row[x] * weight0);
        return float.MultiplyAddEstimate(row[left2] + row[right2], weight2, result);
    }

    /// <summary>
    /// Convolves 4 adjacent horizontal samples with two valid neighbors on each side.
    /// </summary>
    /// <param name="position">The first center sample. Unaligned loads can read two samples before this reference.</param>
    /// <param name="weight0">The broadcast center weight.</param>
    /// <param name="weight1">The broadcast distance-one weight.</param>
    /// <param name="weight2">The broadcast distance-two weight.</param>
    /// <returns>One horizontal weighted sum per lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> HorizontalConvolve(
        ref float position,
        Vector128<float> weight0,
        Vector128<float> weight1,
        Vector128<float> weight2)
    {
        Vector128<float> center = Vector128.LoadUnsafe(ref position);
        Vector128<float> left1 = Vector128.LoadUnsafe(ref Unsafe.Subtract(ref position, 1));
        Vector128<float> right1 = Vector128.LoadUnsafe(ref Unsafe.Add(ref position, 1));
        Vector128<float> result = Vector128.MultiplyAddEstimate(left1 + right1, weight1, center * weight0);
        Vector128<float> left2 = Vector128.LoadUnsafe(ref Unsafe.Subtract(ref position, 2));
        Vector128<float> right2 = Vector128.LoadUnsafe(ref Unsafe.Add(ref position, 2));
        return Vector128.MultiplyAddEstimate(left2 + right2, weight2, result);
    }

    /// <summary>
    /// Convolves 8 adjacent horizontal samples with two valid neighbors on each side.
    /// </summary>
    /// <param name="position">The first center sample. Unaligned loads can read two samples before this reference.</param>
    /// <param name="weight0">The broadcast center weight.</param>
    /// <param name="weight1">The broadcast distance-one weight.</param>
    /// <param name="weight2">The broadcast distance-two weight.</param>
    /// <returns>One horizontal weighted sum per lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> HorizontalConvolve(
        ref float position,
        Vector256<float> weight0,
        Vector256<float> weight1,
        Vector256<float> weight2)
    {
        Vector256<float> center = Vector256.LoadUnsafe(ref position);
        Vector256<float> left1 = Vector256.LoadUnsafe(ref Unsafe.Subtract(ref position, 1));
        Vector256<float> right1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref position, 1));
        Vector256<float> result = Vector256.MultiplyAddEstimate(left1 + right1, weight1, center * weight0);
        Vector256<float> left2 = Vector256.LoadUnsafe(ref Unsafe.Subtract(ref position, 2));
        Vector256<float> right2 = Vector256.LoadUnsafe(ref Unsafe.Add(ref position, 2));
        return Vector256.MultiplyAddEstimate(left2 + right2, weight2, result);
    }

    /// <summary>
    /// Convolves 16 adjacent horizontal samples with two valid neighbors on each side.
    /// </summary>
    /// <param name="position">The first center sample. Unaligned loads can read two samples before this reference.</param>
    /// <param name="weight0">The broadcast center weight.</param>
    /// <param name="weight1">The broadcast distance-one weight.</param>
    /// <param name="weight2">The broadcast distance-two weight.</param>
    /// <returns>One horizontal weighted sum per lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> HorizontalConvolve(
        ref float position,
        Vector512<float> weight0,
        Vector512<float> weight1,
        Vector512<float> weight2)
    {
        Vector512<float> center = Vector512.LoadUnsafe(ref position);
        Vector512<float> left1 = Vector512.LoadUnsafe(ref Unsafe.Subtract(ref position, 1));
        Vector512<float> right1 = Vector512.LoadUnsafe(ref Unsafe.Add(ref position, 1));
        Vector512<float> result = Vector512.MultiplyAddEstimate(left1 + right1, weight1, center * weight0);
        Vector512<float> left2 = Vector512.LoadUnsafe(ref Unsafe.Subtract(ref position, 2));
        Vector512<float> right2 = Vector512.LoadUnsafe(ref Unsafe.Add(ref position, 2));
        return Vector512.MultiplyAddEstimate(left2 + right2, weight2, result);
    }
}
