// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Modular;

/// <summary>
/// Reverses modular color arithmetic on three planar integer rows.
/// </summary>
/// <remarks>
/// The frame owner validates operation codes and matching channel dimensions before calling this kernel.
/// Channel permutations change plane ownership and do not form part of the row arithmetic.
/// </remarks>
internal static class JxlModularColorTransform
{
    /// <summary>
    /// Defines inverse modular color arithmetic independently of row traversal.
    /// </summary>
    private interface IColorTransformOperator
    {
        /// <summary>
        /// Restores one column's three channel values.
        /// </summary>
        /// <param name="first">The first input and output component.</param>
        /// <param name="second">The second input and output component.</param>
        /// <param name="third">The third input and output component.</param>
        static abstract void Apply(ref int first, ref int second, ref int third);

        /// <summary>
        /// Restores four independent columns, one column per signed integer lane.
        /// </summary>
        /// <param name="first">The first channel's input and output lanes.</param>
        /// <param name="second">The second channel's input and output lanes.</param>
        /// <param name="third">The third channel's input and output lanes.</param>
        static abstract void Apply(ref Vector128<int> first, ref Vector128<int> second, ref Vector128<int> third);

        /// <summary>
        /// Restores eight independent columns, one column per signed integer lane.
        /// </summary>
        /// <param name="first">The first channel's input and output lanes.</param>
        /// <param name="second">The second channel's input and output lanes.</param>
        /// <param name="third">The third channel's input and output lanes.</param>
        static abstract void Apply(ref Vector256<int> first, ref Vector256<int> second, ref Vector256<int> third);

        /// <summary>
        /// Restores sixteen independent columns, one column per signed integer lane.
        /// </summary>
        /// <param name="first">The first channel's input and output lanes.</param>
        /// <param name="second">The second channel's input and output lanes.</param>
        /// <param name="third">The third channel's input and output lanes.</param>
        static abstract void Apply(ref Vector512<int> first, ref Vector512<int> second, ref Vector512<int> third);
    }

    /// <summary>
    /// Applies one inverse reversible color transform in place.
    /// </summary>
    /// <param name="operation">The validated arithmetic operation, from zero through six. Zero leaves all rows unchanged.</param>
    /// <param name="first">The first channel row.</param>
    /// <param name="second">The second channel row, with the same length as the first.</param>
    /// <param name="third">The third channel row, with the same length as the first.</param>
    public static void Apply(int operation, Span<int> first, Span<int> second, Span<int> third)
        => Apply<int>(operation, first, second, third);

    /// <summary>
    /// Applies one inverse reversible color transform using the stream's signed sample storage.
    /// </summary>
    /// <typeparam name="TSample">The selected short or int storage carrier.</typeparam>
    /// <param name="operation">The validated arithmetic operation, from zero through six.</param>
    /// <param name="first">The first channel row.</param>
    /// <param name="second">The second channel row, with the same length as the first.</param>
    /// <param name="third">The third channel row, with the same length as the first.</param>
    public static void Apply<TSample>(int operation, Span<TSample> first, Span<TSample> second, Span<TSample> third)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        // Select arithmetic once per row. Closed operator types remove operation-code tests from every SIMD and scalar iteration.
        switch (operation)
        {
            case 0:
                return;
            case 1:
                Apply<TSample, AddFirstToThirdOperator>(first, second, third);
                break;
            case 2:
                Apply<TSample, AddFirstToSecondOperator>(first, second, third);
                break;
            case 3:
                Apply<TSample, AddFirstToSecondAndThirdOperator>(first, second, third);
                break;
            case 4:
                Apply<TSample, AddFirstThirdAverageToSecondOperator>(first, second, third);
                break;
            case 5:
                Apply<TSample, AddFirstToThirdAndAverageToSecondOperator<TSample>>(first, second, third);
                break;
            case 6:
                Apply<TSample, YCoCgToRgbOperator>(first, second, third);
                break;
        }
    }

    /// <summary>
    /// Traverses equally sized channel rows with one selected arithmetic operator.
    /// </summary>
    /// <typeparam name="TSample">The selected short or int storage carrier.</typeparam>
    /// <typeparam name="TOperator">The inverse color arithmetic selected before traversal.</typeparam>
    /// <param name="first">The first channel row.</param>
    /// <param name="second">The second channel row.</param>
    /// <param name="third">The third channel row.</param>
    private static void Apply<TSample, TOperator>(Span<TSample> first, Span<TSample> second, Span<TSample> third)
        where TSample : unmanaged, IBinaryInteger<TSample>
        where TOperator : struct, IColorTransformOperator
    {
        int i = 0;

        // Each int lane contains one column from the three planes. Short loads read only that many samples and sign-extend them.
        // Unaligned loads accept arbitrary row origins; the widest path and its tails do not overread a short row.
        // Load all three channels before storing: later outputs can depend on an input whose plane is overwritten in place.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = first.Length.Vector512Count<int>();
            for (nuint block = 0; block < count; block++, i += Vector512<int>.Count)
            {
                Vector512<int> a = JxlModularChannel.Load512<TSample>(first, i);
                Vector512<int> b = JxlModularChannel.Load512<TSample>(second, i);
                Vector512<int> c = JxlModularChannel.Load512<TSample>(third, i);

                TOperator.Apply(ref a, ref b, ref c);

                JxlModularChannel.Store512(first, i, a);
                JxlModularChannel.Store512(second, i, b);
                JxlModularChannel.Store512(third, i, c);
            }
        }

        // Descending widths consume the remaining complete blocks. No pass rereads columns handled by a wider vector.
        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (first.Length - i).Vector256Count<int>();
            for (nuint block = 0; block < count; block++, i += Vector256<int>.Count)
            {
                Vector256<int> a = JxlModularChannel.Load256<TSample>(first, i);
                Vector256<int> b = JxlModularChannel.Load256<TSample>(second, i);
                Vector256<int> c = JxlModularChannel.Load256<TSample>(third, i);

                TOperator.Apply(ref a, ref b, ref c);

                JxlModularChannel.Store256(first, i, a);
                JxlModularChannel.Store256(second, i, b);
                JxlModularChannel.Store256(third, i, c);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (first.Length - i).Vector128Count<int>();
            for (nuint block = 0; block < count; block++, i += Vector128<int>.Count)
            {
                Vector128<int> a = JxlModularChannel.Load128<TSample>(first, i);
                Vector128<int> b = JxlModularChannel.Load128<TSample>(second, i);
                Vector128<int> c = JxlModularChannel.Load128<TSample>(third, i);

                TOperator.Apply(ref a, ref b, ref c);

                JxlModularChannel.Store128(first, i, a);
                JxlModularChannel.Store128(second, i, b);
                JxlModularChannel.Store128(third, i, c);
            }
        }

        // Loads widen short samples before arithmetic. Stores retain the low storage bits in both SIMD and scalar paths.
        // This also handles complete rows when hardware acceleration is disabled.
        for (; i < first.Length; i++)
        {
            int a = int.CreateTruncating(first[i]);
            int b = int.CreateTruncating(second[i]);
            int c = int.CreateTruncating(third[i]);

            TOperator.Apply(ref a, ref b, ref c);

            first[i] = TSample.CreateTruncating(a);
            second[i] = TSample.CreateTruncating(b);
            third[i] = TSample.CreateTruncating(c);
        }
    }

    /// <summary>
    /// Restores the third channel by adding the first channel.
    /// </summary>
    private readonly struct AddFirstToThirdOperator : IColorTransformOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(ref int first, ref int second, ref int third)
        {
            third = unchecked(third + first);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(ref Vector128<int> first, ref Vector128<int> second, ref Vector128<int> third)
        {
            third += first;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(ref Vector256<int> first, ref Vector256<int> second, ref Vector256<int> third)
        {
            third += first;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(ref Vector512<int> first, ref Vector512<int> second, ref Vector512<int> third)
        {
            third += first;
        }
    }

    /// <summary>
    /// Restores the second channel by adding the first channel.
    /// </summary>
    private readonly struct AddFirstToSecondOperator : IColorTransformOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(ref int first, ref int second, ref int third)
        {
            second = unchecked(second + first);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(ref Vector128<int> first, ref Vector128<int> second, ref Vector128<int> third)
        {
            second += first;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(ref Vector256<int> first, ref Vector256<int> second, ref Vector256<int> third)
        {
            second += first;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(ref Vector512<int> first, ref Vector512<int> second, ref Vector512<int> third)
        {
            second += first;
        }
    }

    /// <summary>
    /// Restores the second and third channels by adding the first channel to each.
    /// </summary>
    private readonly struct AddFirstToSecondAndThirdOperator : IColorTransformOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(ref int first, ref int second, ref int third)
        {
            second = unchecked(second + first);
            third = unchecked(third + first);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(ref Vector128<int> first, ref Vector128<int> second, ref Vector128<int> third)
        {
            second += first;
            third += first;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(ref Vector256<int> first, ref Vector256<int> second, ref Vector256<int> third)
        {
            second += first;
            third += first;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(ref Vector512<int> first, ref Vector512<int> second, ref Vector512<int> third)
        {
            second += first;
            third += first;
        }
    }

    /// <summary>
    /// Restores the second channel from the signed average of the first and third channels.
    /// </summary>
    private readonly struct AddFirstThirdAverageToSecondOperator : IColorTransformOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(ref int first, ref int second, ref int third)
        {
            // The sum wraps before its arithmetic right shift. A negative odd sum rounds toward negative infinity, not toward zero.
            second = unchecked(second + (unchecked(first + third) >> 1));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(ref Vector128<int> first, ref Vector128<int> second, ref Vector128<int> third)
        {
            second += (first + third) >> 1;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(ref Vector256<int> first, ref Vector256<int> second, ref Vector256<int> third)
        {
            second += (first + third) >> 1;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(ref Vector512<int> first, ref Vector512<int> second, ref Vector512<int> third)
        {
            second += (first + third) >> 1;
        }
    }

    /// <summary>
    /// Restores the third channel, then restores the second channel from the updated signed average.
    /// </summary>
    /// <typeparam name="TSample">The short or int carrier whose updated third component participates in the average.</typeparam>
    private readonly struct AddFirstToThirdAndAverageToSecondOperator<TSample> : IColorTransformOperator
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(ref int first, ref int second, ref int third)
        {
            // The second channel depends on the restored third channel. Preserve this order and wrap the sum before shifting.
            third = unchecked(third + first);
            if (typeof(TSample) == typeof(short))
            {
                // Rust rct_impl_i16 wraps the updated third component before avg_i16 reads it.
                // The widened sum used for that average then preserves its mathematical signed half-sum.
                third = unchecked((short)third);
            }

            second = unchecked(second + (unchecked(first + third) >> 1));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(ref Vector128<int> first, ref Vector128<int> second, ref Vector128<int> third)
        {
            third += first;
            if (typeof(TSample) == typeof(short))
            {
                // Sign extension after discarding the high bits reproduces the short carrier's wrap.
                third = (third << 16) >> 16;
            }

            second += (first + third) >> 1;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(ref Vector256<int> first, ref Vector256<int> second, ref Vector256<int> third)
        {
            third += first;
            if (typeof(TSample) == typeof(short))
            {
                // Sign extension after discarding the high bits reproduces the short carrier's wrap.
                third = (third << 16) >> 16;
            }

            second += (first + third) >> 1;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(ref Vector512<int> first, ref Vector512<int> second, ref Vector512<int> third)
        {
            third += first;
            if (typeof(TSample) == typeof(short))
            {
                // Sign extension after discarding the high bits reproduces the short carrier's wrap.
                third = (third << 16) >> 16;
            }

            second += (first + third) >> 1;
        }
    }

    /// <summary>
    /// Restores RGB from reversible YCoCg components.
    /// </summary>
    private readonly struct YCoCgToRgbOperator : IColorTransformOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(ref int first, ref int second, ref int third)
        {
            // Inputs are Y, Co and Cg. Subtract half Cg first, then half Co; signed shifts retain the reversible integer rounding.
            int center = unchecked(first - (third >> 1));
            int green = unchecked(third + center);
            int blue = unchecked(center - (second >> 1));

            first = unchecked(blue + second);
            second = green;
            third = blue;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(ref Vector128<int> first, ref Vector128<int> second, ref Vector128<int> third)
        {
            Vector128<int> center = first - (third >> 1);
            Vector128<int> green = third + center;
            Vector128<int> blue = center - (second >> 1);

            first = blue + second;
            second = green;
            third = blue;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(ref Vector256<int> first, ref Vector256<int> second, ref Vector256<int> third)
        {
            Vector256<int> center = first - (third >> 1);
            Vector256<int> green = third + center;
            Vector256<int> blue = center - (second >> 1);

            first = blue + second;
            second = green;
            third = blue;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(ref Vector512<int> first, ref Vector512<int> second, ref Vector512<int> third)
        {
            Vector512<int> center = first - (third >> 1);
            Vector512<int> green = third + center;
            Vector512<int> blue = center - (second >> 1);

            first = blue + second;
            second = green;
            third = blue;
        }
    }
}
