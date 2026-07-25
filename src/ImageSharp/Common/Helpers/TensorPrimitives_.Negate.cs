// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Common.Helpers;

internal static partial class TensorPrimitives_
{
    /// <summary>
    /// Defines an element-wise unary operation.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    private interface IUnaryOperator<T>
    {
        /// <summary>
        /// Gets a value indicating whether the operation supports vector execution.
        /// </summary>
        public static abstract bool Vectorizable { get; }

        /// <summary>
        /// Applies the operation to a scalar value.
        /// </summary>
        /// <param name="x">The input value.</param>
        /// <returns>The operation result.</returns>
        public static abstract T Invoke(T x);

        /// <summary>
        /// Applies the operation to a 128-bit vector.
        /// </summary>
        /// <param name="x">The input vector.</param>
        /// <returns>The operation result.</returns>
        public static abstract Vector128<T> Invoke(Vector128<T> x);

        /// <summary>
        /// Applies the operation to a 256-bit vector.
        /// </summary>
        /// <param name="x">The input vector.</param>
        /// <returns>The operation result.</returns>
        public static abstract Vector256<T> Invoke(Vector256<T> x);

        /// <summary>
        /// Applies the operation to a 512-bit vector.
        /// </summary>
        /// <param name="x">The input vector.</param>
        /// <returns>The operation result.</returns>
        public static abstract Vector512<T> Invoke(Vector512<T> x);
    }

    /// <summary>
    /// Computes the element-wise negation of the values in <paramref name="x"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="x">The values to negate.</param>
    /// <param name="destination">The destination for the negated values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Negate<T>(ReadOnlySpan<T> x, Span<T> destination)
        where T : IUnaryNegationOperators<T, T>
        => InvokeSpanIntoSpan<T, NegateOperator<T>>(x, destination);

    /// <summary>
    /// Performs an element-wise unary operation over a span.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TOperator">The operation to apply.</typeparam>
    /// <param name="x">The input values.</param>
    /// <param name="destination">The destination values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InvokeSpanIntoSpan<T, TOperator>(ReadOnlySpan<T> x, Span<T> destination)
        where TOperator : struct, IUnaryOperator<T>
    {
        ref T xRef = ref MemoryMarshal.GetReference(x);
        ref T destinationRef = ref MemoryMarshal.GetReference(destination);
        nuint length = (uint)x.Length;

        // The dispatch matches the other compatibility pipelines: AVX-512 is reserved for large spans because
        // its setup cost is not recovered by the short image-processing buffers that dominate ImageSharp.
        if (TOperator.Vectorizable
            && Vector512.IsHardwareAccelerated
            && Vector512<T>.IsSupported
            && length >= 512)
        {
            InvokeUnaryVectorized512<T, TOperator>(ref xRef, ref destinationRef, length);
            return;
        }

        if (TOperator.Vectorizable && Vector256.IsHardwareAccelerated && Vector256<T>.IsSupported && length >= (uint)Vector256<T>.Count)
        {
            InvokeUnaryVectorized256<T, TOperator>(ref xRef, ref destinationRef, length);
            return;
        }

        if (TOperator.Vectorizable && Vector128.IsHardwareAccelerated && Vector128<T>.IsSupported && length >= (uint)Vector128<T>.Count)
        {
            InvokeUnaryVectorized128<T, TOperator>(ref xRef, ref destinationRef, length);
            return;
        }

        for (nuint i = 0; i < length; i++)
        {
            Unsafe.Add(ref destinationRef, i) = TOperator.Invoke(Unsafe.Add(ref xRef, i));
        }
    }

    /// <summary>
    /// Applies a unary operation with 128-bit vectors.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TOperator">The operation to apply.</typeparam>
    /// <param name="xRef">The first input element.</param>
    /// <param name="destinationRef">The first destination element.</param>
    /// <param name="length">The number of elements to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InvokeUnaryVectorized128<T, TOperator>(ref T xRef, ref T destinationRef, nuint length)
        where TOperator : struct, IUnaryOperator<T>
    {
        nuint vectorCount = (uint)Vector128<T>.Count;
        nuint vectorsPerLoop = vectorCount * 8;
        nuint index = 0;

        // The final vector overlaps the preceding store when the length is not a vector multiple. Loading it
        // before any stores preserves same-start in-place operation because it captures the original tail.
        Vector128<T> end = default;
        if ((length % vectorCount) != 0)
        {
            end = TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, length - vectorCount));
        }

        while ((length - index) >= vectorsPerLoop)
        {
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 0))).StoreUnsafe(ref destinationRef, index + (vectorCount * 0));
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 1))).StoreUnsafe(ref destinationRef, index + (vectorCount * 1));
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 2))).StoreUnsafe(ref destinationRef, index + (vectorCount * 2));
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 3))).StoreUnsafe(ref destinationRef, index + (vectorCount * 3));
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 4))).StoreUnsafe(ref destinationRef, index + (vectorCount * 4));
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 5))).StoreUnsafe(ref destinationRef, index + (vectorCount * 5));
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 6))).StoreUnsafe(ref destinationRef, index + (vectorCount * 6));
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 7))).StoreUnsafe(ref destinationRef, index + (vectorCount * 7));

            index += vectorsPerLoop;
        }

        while ((length - index) >= vectorCount)
        {
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index)).StoreUnsafe(ref destinationRef, index);
            index += vectorCount;
        }

        if (index != length)
        {
            end.StoreUnsafe(ref destinationRef, length - vectorCount);
        }
    }

    /// <summary>
    /// Applies a unary operation with 256-bit vectors.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TOperator">The operation to apply.</typeparam>
    /// <param name="xRef">The first input element.</param>
    /// <param name="destinationRef">The first destination element.</param>
    /// <param name="length">The number of elements to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InvokeUnaryVectorized256<T, TOperator>(ref T xRef, ref T destinationRef, nuint length)
        where TOperator : struct, IUnaryOperator<T>
    {
        nuint vectorCount = (uint)Vector256<T>.Count;
        nuint vectorsPerLoop = vectorCount * 8;
        nuint index = 0;
        Vector256<T> end = default;

        if ((length % vectorCount) != 0)
        {
            end = TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, length - vectorCount));
        }

        while ((length - index) >= vectorsPerLoop)
        {
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 0))).StoreUnsafe(ref destinationRef, index + (vectorCount * 0));
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 1))).StoreUnsafe(ref destinationRef, index + (vectorCount * 1));
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 2))).StoreUnsafe(ref destinationRef, index + (vectorCount * 2));
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 3))).StoreUnsafe(ref destinationRef, index + (vectorCount * 3));
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 4))).StoreUnsafe(ref destinationRef, index + (vectorCount * 4));
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 5))).StoreUnsafe(ref destinationRef, index + (vectorCount * 5));
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 6))).StoreUnsafe(ref destinationRef, index + (vectorCount * 6));
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 7))).StoreUnsafe(ref destinationRef, index + (vectorCount * 7));

            index += vectorsPerLoop;
        }

        while ((length - index) >= vectorCount)
        {
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index)).StoreUnsafe(ref destinationRef, index);
            index += vectorCount;
        }

        if (index != length)
        {
            end.StoreUnsafe(ref destinationRef, length - vectorCount);
        }
    }

    /// <summary>
    /// Applies a unary operation with 512-bit vectors.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TOperator">The operation to apply.</typeparam>
    /// <param name="xRef">The first input element.</param>
    /// <param name="destinationRef">The first destination element.</param>
    /// <param name="length">The number of elements to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InvokeUnaryVectorized512<T, TOperator>(ref T xRef, ref T destinationRef, nuint length)
        where TOperator : struct, IUnaryOperator<T>
    {
        nuint vectorCount = (uint)Vector512<T>.Count;
        nuint vectorsPerLoop = vectorCount * 8;
        nuint index = 0;
        Vector512<T> end = default;

        if ((length % vectorCount) != 0)
        {
            end = TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, length - vectorCount));
        }

        while ((length - index) >= vectorsPerLoop)
        {
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 0))).StoreUnsafe(ref destinationRef, index + (vectorCount * 0));
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 1))).StoreUnsafe(ref destinationRef, index + (vectorCount * 1));
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 2))).StoreUnsafe(ref destinationRef, index + (vectorCount * 2));
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 3))).StoreUnsafe(ref destinationRef, index + (vectorCount * 3));
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 4))).StoreUnsafe(ref destinationRef, index + (vectorCount * 4));
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 5))).StoreUnsafe(ref destinationRef, index + (vectorCount * 5));
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 6))).StoreUnsafe(ref destinationRef, index + (vectorCount * 6));
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 7))).StoreUnsafe(ref destinationRef, index + (vectorCount * 7));

            index += vectorsPerLoop;
        }

        while ((length - index) >= vectorCount)
        {
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index)).StoreUnsafe(ref destinationRef, index);
            index += vectorCount;
        }

        if (index != length)
        {
            end.StoreUnsafe(ref destinationRef, length - vectorCount);
        }
    }

    /// <summary>
    /// Implements element-wise negation for scalar and SIMD inputs.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    private readonly struct NegateOperator<T> : IUnaryOperator<T>
        where T : IUnaryNegationOperators<T, T>
    {
        /// <inheritdoc />
        public static bool Vectorizable => true;

        /// <inheritdoc />
        public static T Invoke(T x) => -x;

        /// <inheritdoc />
        public static Vector128<T> Invoke(Vector128<T> x) => -x;

        /// <inheritdoc />
        public static Vector256<T> Invoke(Vector256<T> x) => -x;

        /// <inheritdoc />
        public static Vector512<T> Invoke(Vector512<T> x) => -x;
    }
}
