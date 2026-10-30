// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    {
        // Select arithmetic once per row. Closed operator types remove operation-code tests from every SIMD and scalar iteration.
        switch (operation)
        {
            case 0:
                return;
            case 1:
                Apply<AddFirstToThirdOperator>(first, second, third);
                break;
            case 2:
                Apply<AddFirstToSecondOperator>(first, second, third);
                break;
            case 3:
                Apply<AddFirstToSecondAndThirdOperator>(first, second, third);
                break;
            case 4:
                Apply<AddFirstThirdAverageToSecondOperator>(first, second, third);
                break;
            case 5:
                Apply<AddFirstToThirdAndAverageToSecondOperator>(first, second, third);
                break;
            case 6:
                Apply<YCoCgToRgbOperator>(first, second, third);
                break;
        }
    }

    /// <summary>
    /// Traverses equally sized channel rows with one selected arithmetic operator.
    /// </summary>
    /// <typeparam name="TOperator">The inverse color arithmetic selected before traversal.</typeparam>
    /// <param name="first">The first channel row.</param>
    /// <param name="second">The second channel row.</param>
    /// <param name="third">The third channel row.</param>
    private static void Apply<TOperator>(Span<int> first, Span<int> second, Span<int> third)
        where TOperator : struct, IColorTransformOperator
    {
        ref int channel0 = ref MemoryMarshal.GetReference(first);
        ref int channel1 = ref MemoryMarshal.GetReference(second);
        ref int channel2 = ref MemoryMarshal.GetReference(third);
        int i = 0;

        // Each lane contains the same column from three separate channel planes. Unaligned loads accept any row origin.
        // Load all three channels before storing: later outputs can depend on an input whose plane is overwritten in place.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = first.Length.Vector512Count<int>();
            for (nuint block = 0; block < count; block++, i += Vector512<int>.Count)
            {
                Vector512<int> a = Vector512.LoadUnsafe(ref channel0, (nuint)i);
                Vector512<int> b = Vector512.LoadUnsafe(ref channel1, (nuint)i);
                Vector512<int> c = Vector512.LoadUnsafe(ref channel2, (nuint)i);

                TOperator.Apply(ref a, ref b, ref c);

                a.StoreUnsafe(ref channel0, (nuint)i);
                b.StoreUnsafe(ref channel1, (nuint)i);
                c.StoreUnsafe(ref channel2, (nuint)i);
            }
        }

        // Descending widths consume the remaining complete blocks. No pass rereads columns handled by a wider vector.
        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (first.Length - i).Vector256Count<int>();
            for (nuint block = 0; block < count; block++, i += Vector256<int>.Count)
            {
                Vector256<int> a = Vector256.LoadUnsafe(ref channel0, (nuint)i);
                Vector256<int> b = Vector256.LoadUnsafe(ref channel1, (nuint)i);
                Vector256<int> c = Vector256.LoadUnsafe(ref channel2, (nuint)i);

                TOperator.Apply(ref a, ref b, ref c);

                a.StoreUnsafe(ref channel0, (nuint)i);
                b.StoreUnsafe(ref channel1, (nuint)i);
                c.StoreUnsafe(ref channel2, (nuint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (first.Length - i).Vector128Count<int>();
            for (nuint block = 0; block < count; block++, i += Vector128<int>.Count)
            {
                Vector128<int> a = Vector128.LoadUnsafe(ref channel0, (nuint)i);
                Vector128<int> b = Vector128.LoadUnsafe(ref channel1, (nuint)i);
                Vector128<int> c = Vector128.LoadUnsafe(ref channel2, (nuint)i);

                TOperator.Apply(ref a, ref b, ref c);

                a.StoreUnsafe(ref channel0, (nuint)i);
                b.StoreUnsafe(ref channel1, (nuint)i);
                c.StoreUnsafe(ref channel2, (nuint)i);
            }
        }

        // Scalar arithmetic uses the same operation order, signed shifts and 32-bit wrapping as the SIMD lanes.
        // This also handles complete rows when hardware acceleration is disabled.
        for (; i < first.Length; i++)
        {
            int a = Unsafe.Add(ref channel0, i);
            int b = Unsafe.Add(ref channel1, i);
            int c = Unsafe.Add(ref channel2, i);

            TOperator.Apply(ref a, ref b, ref c);

            Unsafe.Add(ref channel0, i) = a;
            Unsafe.Add(ref channel1, i) = b;
            Unsafe.Add(ref channel2, i) = c;
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
    private readonly struct AddFirstToThirdAndAverageToSecondOperator : IColorTransformOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(ref int first, ref int second, ref int third)
        {
            // The second channel depends on the restored third channel. Preserve this order and wrap the sum before shifting.
            third = unchecked(third + first);
            second = unchecked(second + (unchecked(first + third) >> 1));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(ref Vector128<int> first, ref Vector128<int> second, ref Vector128<int> third)
        {
            third += first;
            second += (first + third) >> 1;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(ref Vector256<int> first, ref Vector256<int> second, ref Vector256<int> third)
        {
            third += first;
            second += (first + third) >> 1;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Apply(ref Vector512<int> first, ref Vector512<int> second, ref Vector512<int> third)
        {
            third += first;
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
