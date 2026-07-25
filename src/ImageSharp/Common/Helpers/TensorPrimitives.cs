// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Common.Helpers;

/// <summary>
/// Provides compatibility implementations for tensor operations that are not available on every target framework.
/// </summary>
/// <remarks>
/// The API shape follows <c>System.Numerics.Tensors.TensorPrimitives</c> so call sites can move to the runtime
/// implementation when ImageSharp no longer supports target frameworks that predate it.
/// </remarks>
#pragma warning disable SA1649 // File name should match first type name
internal static class TensorPrimitives_
#pragma warning restore SA1649 // File name should match first type name
{
    /// <summary>
    /// Defines an element-wise binary operation.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    private interface IBinaryOperator<T>
    {
        /// <summary>
        /// Gets a value indicating whether the operation supports vector execution.
        /// </summary>
        public static abstract bool Vectorizable { get; }

        /// <summary>
        /// Applies the operation to scalar values.
        /// </summary>
        /// <param name="x">The first value.</param>
        /// <param name="y">The second value.</param>
        /// <returns>The operation result.</returns>
        public static abstract T Invoke(T x, T y);

        /// <summary>
        /// Applies the operation to 128-bit vectors.
        /// </summary>
        /// <param name="x">The first vector.</param>
        /// <param name="y">The second vector.</param>
        /// <returns>The operation result.</returns>
        public static abstract Vector128<T> Invoke(Vector128<T> x, Vector128<T> y);

        /// <summary>
        /// Applies the operation to 256-bit vectors.
        /// </summary>
        /// <param name="x">The first vector.</param>
        /// <param name="y">The second vector.</param>
        /// <returns>The operation result.</returns>
        public static abstract Vector256<T> Invoke(Vector256<T> x, Vector256<T> y);

        /// <summary>
        /// Applies the operation to 512-bit vectors.
        /// </summary>
        /// <param name="x">The first vector.</param>
        /// <param name="y">The second vector.</param>
        /// <returns>The operation result.</returns>
        public static abstract Vector512<T> Invoke(Vector512<T> x, Vector512<T> y);
    }

    /// <summary>
    /// Defines an element-wise ternary operation.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    private interface ITernaryOperator<T>
    {
        /// <summary>
        /// Gets a value indicating whether the operation supports vector execution.
        /// </summary>
        public static abstract bool Vectorizable { get; }

        /// <summary>
        /// Applies the operation to scalar values.
        /// </summary>
        /// <param name="x">The first value.</param>
        /// <param name="y">The second value.</param>
        /// <param name="z">The third value.</param>
        /// <returns>The operation result.</returns>
        public static abstract T Invoke(T x, T y, T z);

        /// <summary>
        /// Applies the operation to 128-bit vectors.
        /// </summary>
        /// <param name="x">The first vector.</param>
        /// <param name="y">The second vector.</param>
        /// <param name="z">The third vector.</param>
        /// <returns>The operation result.</returns>
        public static abstract Vector128<T> Invoke(Vector128<T> x, Vector128<T> y, Vector128<T> z);

        /// <summary>
        /// Applies the operation to 256-bit vectors.
        /// </summary>
        /// <param name="x">The first vector.</param>
        /// <param name="y">The second vector.</param>
        /// <param name="z">The third vector.</param>
        /// <returns>The operation result.</returns>
        public static abstract Vector256<T> Invoke(Vector256<T> x, Vector256<T> y, Vector256<T> z);

        /// <summary>
        /// Applies the operation to 512-bit vectors.
        /// </summary>
        /// <param name="x">The first vector.</param>
        /// <param name="y">The second vector.</param>
        /// <param name="z">The third vector.</param>
        /// <returns>The operation result.</returns>
        public static abstract Vector512<T> Invoke(Vector512<T> x, Vector512<T> y, Vector512<T> z);
    }

    /// <summary>
    /// Computes the element-wise result of clamping <paramref name="x"/> to the inclusive range specified
    /// by <paramref name="min"/> and <paramref name="max"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="x">The values to clamp.</param>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    /// <param name="destination">The destination for the clamped values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Clamp<T>(ReadOnlySpan<T> x, T min, T max, Span<T> destination)
        where T : INumber<T>
        => InvokeSpanScalarScalarIntoSpan<T, ClampOperator<T>>(x, min, max, destination);

    /// <summary>
    /// Computes the element-wise sum of the values in <paramref name="x"/> and <paramref name="y"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="x">The first addends.</param>
    /// <param name="y">The second addends.</param>
    /// <param name="destination">The destination for the sums.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Add<T>(ReadOnlySpan<T> x, ReadOnlySpan<T> y, Span<T> destination)
        where T : IAdditionOperators<T, T, T>, IAdditiveIdentity<T, T>
        => InvokeSpanSpanIntoSpan<T, AddOperator<T>>(x, y, destination);

    /// <summary>
    /// Computes the element-wise sum of the values in <paramref name="x"/> and the scalar <paramref name="y"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="x">The first addends.</param>
    /// <param name="y">The scalar second addend.</param>
    /// <param name="destination">The destination for the sums.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Add<T>(ReadOnlySpan<T> x, T y, Span<T> destination)
        where T : IAdditionOperators<T, T, T>, IAdditiveIdentity<T, T>
        => InvokeSpanScalarIntoSpan<T, AddOperator<T>>(x, y, destination);

    /// <summary>
    /// Computes the element-wise result of dividing the values in <paramref name="x"/> by <paramref name="y"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="x">The dividend values.</param>
    /// <param name="y">The divisor.</param>
    /// <param name="destination">The destination for the quotient values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Divide<T>(ReadOnlySpan<T> x, T y, Span<T> destination)
        where T : IDivisionOperators<T, T, T>
        => InvokeSpanScalarIntoSpanForDivision<T, DivideOperator<T>>(x, y, destination);

    /// <summary>
    /// Computes the element-wise maximum of the values in <paramref name="x"/> and <paramref name="y"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="x">The values to compare.</param>
    /// <param name="y">The value to compare with each element.</param>
    /// <param name="destination">The destination for the maximum values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Max<T>(ReadOnlySpan<T> x, T y, Span<T> destination)
        where T : INumber<T>
        => InvokeSpanScalarIntoSpan<T, MaxOperator<T>>(x, y, destination);

    /// <summary>
    /// Computes the element-wise product of the values in <paramref name="x"/> and <paramref name="y"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="x">The multiplicands.</param>
    /// <param name="y">The multiplier.</param>
    /// <param name="destination">The destination for the products.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Multiply<T>(ReadOnlySpan<T> x, T y, Span<T> destination)
        where T : IMultiplyOperators<T, T, T>, IMultiplicativeIdentity<T, T>
        => InvokeSpanScalarIntoSpan<T, MultiplyOperator<T>>(x, y, destination);

    /// <summary>
    /// Performs an element-wise binary operation between two spans.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TOperator">The operation to apply.</typeparam>
    /// <param name="x">The first input values.</param>
    /// <param name="y">The second input values.</param>
    /// <param name="destination">The destination values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InvokeSpanSpanIntoSpan<T, TOperator>(
        ReadOnlySpan<T> x,
        ReadOnlySpan<T> y,
        Span<T> destination)
        where TOperator : struct, IBinaryOperator<T>
    {
        ref T xRef = ref MemoryMarshal.GetReference(x);
        ref T yRef = ref MemoryMarshal.GetReference(y);
        ref T destinationRef = ref MemoryMarshal.GetReference(destination);
        nuint length = (uint)x.Length;

        // AVX-512 setup only pays off for larger multi-byte inputs. Byte addition remains on AVX2 because direct
        // PNG/WebP measurements show that its higher lane count does not recover the wider dispatch cost.
        // Each pipeline preloads its final inputs when a tail overlaps so same-start in-place operation remains correct.
        if (TOperator.Vectorizable
            && Vector512.IsHardwareAccelerated
            && Vector512<T>.IsSupported
            && Unsafe.SizeOf<T>() > 1
            && length >= 512)
        {
            InvokeVectorized512<T, TOperator>(ref xRef, ref yRef, ref destinationRef, length);
            return;
        }

        if (TOperator.Vectorizable && Vector256.IsHardwareAccelerated && Vector256<T>.IsSupported && length >= (uint)Vector256<T>.Count)
        {
            InvokeVectorized256<T, TOperator>(ref xRef, ref yRef, ref destinationRef, length);
            return;
        }

        if (TOperator.Vectorizable && Vector128.IsHardwareAccelerated && Vector128<T>.IsSupported && length >= (uint)Vector128<T>.Count)
        {
            InvokeVectorized128<T, TOperator>(ref xRef, ref yRef, ref destinationRef, length);
            return;
        }

        for (nuint i = 0; i < length; i++)
        {
            Unsafe.Add(ref destinationRef, i) = TOperator.Invoke(Unsafe.Add(ref xRef, i), Unsafe.Add(ref yRef, i));
        }
    }

    /// <summary>
    /// Performs an element-wise binary operation between a span and a scalar.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TOperator">The operation to apply.</typeparam>
    /// <param name="x">The input values.</param>
    /// <param name="y">The scalar input.</param>
    /// <param name="destination">The destination values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InvokeSpanScalarIntoSpan<T, TOperator>(
        ReadOnlySpan<T> x,
        T y,
        Span<T> destination)
        where TOperator : struct, IBinaryOperator<T>
    {
        ref T xRef = ref MemoryMarshal.GetReference(x);
        ref T destinationRef = ref MemoryMarshal.GetReference(destination);
        nuint length = (uint)x.Length;

        // The runtime-style unrolled 512-bit pipeline wins on large inputs, but its setup cost regresses the
        // shorter JPEG and ICC buffers. Measurements put the crossover safely below 512 elements.
        if (TOperator.Vectorizable
            && Vector512.IsHardwareAccelerated
            && Vector512<T>.IsSupported
            && length >= 512)
        {
            InvokeVectorized512<T, TOperator>(ref xRef, y, ref destinationRef, length);
            return;
        }

        if (TOperator.Vectorizable && Vector256.IsHardwareAccelerated && Vector256<T>.IsSupported && length >= (uint)Vector256<T>.Count)
        {
            InvokeVectorized256<T, TOperator>(ref xRef, y, ref destinationRef, length);
            return;
        }

        if (TOperator.Vectorizable && Vector128.IsHardwareAccelerated && Vector128<T>.IsSupported && length >= (uint)Vector128<T>.Count)
        {
            InvokeVectorized128<T, TOperator>(ref xRef, y, ref destinationRef, length);
            return;
        }

        for (nuint i = 0; i < length; i++)
        {
            Unsafe.Add(ref destinationRef, i) = TOperator.Invoke(Unsafe.Add(ref xRef, i), y);
        }
    }

    /// <summary>
    /// Performs element-wise division using thresholds measured for ImageSharp normalization workloads.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TOperator">The division operation to apply.</typeparam>
    /// <param name="x">The input values.</param>
    /// <param name="y">The scalar divisor.</param>
    /// <param name="destination">The destination values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InvokeSpanScalarIntoSpanForDivision<T, TOperator>(
        ReadOnlySpan<T> x,
        T y,
        Span<T> destination)
        where TOperator : struct, IBinaryOperator<T>
    {
        ref T xRef = ref MemoryMarshal.GetReference(x);
        ref T destinationRef = ref MemoryMarshal.GetReference(destination);
        nuint length = (uint)x.Length;

        // AVX-512 only wins once there is enough work to amortize its wider dispatch and division latency.
        // Eight vectors is also the runtime pipeline's unrolled-loop boundary, while shorter inputs retain
        // the lower setup cost of 256-bit vectors.
        if (TOperator.Vectorizable
            && Vector512.IsHardwareAccelerated
            && Vector512<T>.IsSupported
            && length >= (uint)(Vector512<T>.Count * 8))
        {
            InvokeVectorized512<T, TOperator>(ref xRef, y, ref destinationRef, length);
            return;
        }

        if (TOperator.Vectorizable && Vector256.IsHardwareAccelerated && Vector256<T>.IsSupported && length >= (uint)Vector256<T>.Count)
        {
            InvokeVectorized256<T, TOperator>(ref xRef, y, ref destinationRef, length);
            return;
        }

        // Four values fill one 128-bit float vector. Processing exactly one packed prefix before the scalar
        // remainder avoids the overlapping second vector that regresses the common seven-element normalization.
        if (TOperator.Vectorizable
            && Vector128.IsHardwareAccelerated
            && Vector128<T>.IsSupported
            && length >= (uint)Vector128<T>.Count)
        {
            nuint vectorCount = (uint)Vector128<T>.Count;
            Vector128<T> yVector = Vector128.Create(y);
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef), yVector).StoreUnsafe(ref destinationRef);

            for (nuint i = vectorCount; i < length; i++)
            {
                Unsafe.Add(ref destinationRef, i) = TOperator.Invoke(Unsafe.Add(ref xRef, i), y);
            }

            return;
        }

        for (nuint i = 0; i < length; i++)
        {
            Unsafe.Add(ref destinationRef, i) = TOperator.Invoke(Unsafe.Add(ref xRef, i), y);
        }
    }

    /// <summary>
    /// Performs an element-wise ternary operation between a span and two scalars.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TOperator">The operation to apply.</typeparam>
    /// <param name="x">The input values.</param>
    /// <param name="y">The first scalar input.</param>
    /// <param name="z">The second scalar input.</param>
    /// <param name="destination">The destination values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InvokeSpanScalarScalarIntoSpan<T, TOperator>(
        ReadOnlySpan<T> x,
        T y,
        T z,
        Span<T> destination)
        where TOperator : struct, ITernaryOperator<T>
    {
        ref T xRef = ref MemoryMarshal.GetReference(x);
        ref T destinationRef = ref MemoryMarshal.GetReference(destination);
        nuint length = (uint)x.Length;

        // This dispatch mirrors the runtime pipeline: large inputs use the widest available registers while
        // short inputs fall through to a width that fits, keeping the operator contract identical at every length.
        if (TOperator.Vectorizable && Vector512.IsHardwareAccelerated && Vector512<T>.IsSupported && length >= (uint)Vector512<T>.Count)
        {
            InvokeVectorized512<T, TOperator>(ref xRef, y, z, ref destinationRef, length);
            return;
        }

        if (TOperator.Vectorizable && Vector256.IsHardwareAccelerated && Vector256<T>.IsSupported && length >= (uint)Vector256<T>.Count)
        {
            InvokeVectorized256<T, TOperator>(ref xRef, y, z, ref destinationRef, length);
            return;
        }

        if (TOperator.Vectorizable && Vector128.IsHardwareAccelerated && Vector128<T>.IsSupported && length >= (uint)Vector128<T>.Count)
        {
            InvokeVectorized128<T, TOperator>(ref xRef, y, z, ref destinationRef, length);
            return;
        }

        for (nuint i = 0; i < length; i++)
        {
            Unsafe.Add(ref destinationRef, i) = TOperator.Invoke(Unsafe.Add(ref xRef, i), y, z);
        }
    }

    /// <summary>
    /// Applies a binary operation between two spans with 128-bit vectors.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TOperator">The operation to apply.</typeparam>
    /// <param name="xRef">The first element of the first input.</param>
    /// <param name="yRef">The first element of the second input.</param>
    /// <param name="destinationRef">The first destination element.</param>
    /// <param name="length">The number of elements to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InvokeVectorized128<T, TOperator>(
        ref T xRef,
        ref T yRef,
        ref T destinationRef,
        nuint length)
        where TOperator : struct, IBinaryOperator<T>
    {
        nuint vectorCount = (uint)Vector128<T>.Count;
        nuint vectorsPerLoop = vectorCount * 8;
        nuint index = 0;

        // When a tail exists, both final inputs are loaded before any stores. This permits either source to also
        // be the destination when the tail starts inside the range written by the preceding full vector.
        Vector128<T> end = default;
        if ((length % vectorCount) != 0)
        {
            end = TOperator.Invoke(
                Vector128.LoadUnsafe(ref xRef, length - vectorCount),
                Vector128.LoadUnsafe(ref yRef, length - vectorCount));
        }

        while ((length - index) >= vectorsPerLoop)
        {
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 0)), Vector128.LoadUnsafe(ref yRef, index + (vectorCount * 0))).StoreUnsafe(ref destinationRef, index + (vectorCount * 0));
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 1)), Vector128.LoadUnsafe(ref yRef, index + (vectorCount * 1))).StoreUnsafe(ref destinationRef, index + (vectorCount * 1));
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 2)), Vector128.LoadUnsafe(ref yRef, index + (vectorCount * 2))).StoreUnsafe(ref destinationRef, index + (vectorCount * 2));
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 3)), Vector128.LoadUnsafe(ref yRef, index + (vectorCount * 3))).StoreUnsafe(ref destinationRef, index + (vectorCount * 3));
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 4)), Vector128.LoadUnsafe(ref yRef, index + (vectorCount * 4))).StoreUnsafe(ref destinationRef, index + (vectorCount * 4));
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 5)), Vector128.LoadUnsafe(ref yRef, index + (vectorCount * 5))).StoreUnsafe(ref destinationRef, index + (vectorCount * 5));
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 6)), Vector128.LoadUnsafe(ref yRef, index + (vectorCount * 6))).StoreUnsafe(ref destinationRef, index + (vectorCount * 6));
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 7)), Vector128.LoadUnsafe(ref yRef, index + (vectorCount * 7))).StoreUnsafe(ref destinationRef, index + (vectorCount * 7));

            index += vectorsPerLoop;
        }

        while ((length - index) >= vectorCount)
        {
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index), Vector128.LoadUnsafe(ref yRef, index)).StoreUnsafe(ref destinationRef, index);
            index += vectorCount;
        }

        if (index != length)
        {
            end.StoreUnsafe(ref destinationRef, length - vectorCount);
        }
    }

    /// <summary>
    /// Applies a binary operation between two spans with 256-bit vectors.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TOperator">The operation to apply.</typeparam>
    /// <param name="xRef">The first element of the first input.</param>
    /// <param name="yRef">The first element of the second input.</param>
    /// <param name="destinationRef">The first destination element.</param>
    /// <param name="length">The number of elements to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InvokeVectorized256<T, TOperator>(
        ref T xRef,
        ref T yRef,
        ref T destinationRef,
        nuint length)
        where TOperator : struct, IBinaryOperator<T>
    {
        nuint vectorCount = (uint)Vector256<T>.Count;
        nuint vectorsPerLoop = vectorCount * 8;
        nuint index = 0;
        Vector256<T> end = default;
        if ((length % vectorCount) != 0)
        {
            end = TOperator.Invoke(
                Vector256.LoadUnsafe(ref xRef, length - vectorCount),
                Vector256.LoadUnsafe(ref yRef, length - vectorCount));
        }

        while ((length - index) >= vectorsPerLoop)
        {
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 0)), Vector256.LoadUnsafe(ref yRef, index + (vectorCount * 0))).StoreUnsafe(ref destinationRef, index + (vectorCount * 0));
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 1)), Vector256.LoadUnsafe(ref yRef, index + (vectorCount * 1))).StoreUnsafe(ref destinationRef, index + (vectorCount * 1));
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 2)), Vector256.LoadUnsafe(ref yRef, index + (vectorCount * 2))).StoreUnsafe(ref destinationRef, index + (vectorCount * 2));
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 3)), Vector256.LoadUnsafe(ref yRef, index + (vectorCount * 3))).StoreUnsafe(ref destinationRef, index + (vectorCount * 3));
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 4)), Vector256.LoadUnsafe(ref yRef, index + (vectorCount * 4))).StoreUnsafe(ref destinationRef, index + (vectorCount * 4));
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 5)), Vector256.LoadUnsafe(ref yRef, index + (vectorCount * 5))).StoreUnsafe(ref destinationRef, index + (vectorCount * 5));
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 6)), Vector256.LoadUnsafe(ref yRef, index + (vectorCount * 6))).StoreUnsafe(ref destinationRef, index + (vectorCount * 6));
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 7)), Vector256.LoadUnsafe(ref yRef, index + (vectorCount * 7))).StoreUnsafe(ref destinationRef, index + (vectorCount * 7));

            index += vectorsPerLoop;
        }

        while ((length - index) >= vectorCount)
        {
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index), Vector256.LoadUnsafe(ref yRef, index)).StoreUnsafe(ref destinationRef, index);
            index += vectorCount;
        }

        if (index != length)
        {
            end.StoreUnsafe(ref destinationRef, length - vectorCount);
        }
    }

    /// <summary>
    /// Applies a binary operation between two spans with 512-bit vectors.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TOperator">The operation to apply.</typeparam>
    /// <param name="xRef">The first element of the first input.</param>
    /// <param name="yRef">The first element of the second input.</param>
    /// <param name="destinationRef">The first destination element.</param>
    /// <param name="length">The number of elements to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InvokeVectorized512<T, TOperator>(
        ref T xRef,
        ref T yRef,
        ref T destinationRef,
        nuint length)
        where TOperator : struct, IBinaryOperator<T>
    {
        nuint vectorCount = (uint)Vector512<T>.Count;
        nuint vectorsPerLoop = vectorCount * 8;
        nuint index = 0;
        Vector512<T> end = default;
        if ((length % vectorCount) != 0)
        {
            end = TOperator.Invoke(
                Vector512.LoadUnsafe(ref xRef, length - vectorCount),
                Vector512.LoadUnsafe(ref yRef, length - vectorCount));
        }

        while ((length - index) >= vectorsPerLoop)
        {
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 0)), Vector512.LoadUnsafe(ref yRef, index + (vectorCount * 0))).StoreUnsafe(ref destinationRef, index + (vectorCount * 0));
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 1)), Vector512.LoadUnsafe(ref yRef, index + (vectorCount * 1))).StoreUnsafe(ref destinationRef, index + (vectorCount * 1));
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 2)), Vector512.LoadUnsafe(ref yRef, index + (vectorCount * 2))).StoreUnsafe(ref destinationRef, index + (vectorCount * 2));
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 3)), Vector512.LoadUnsafe(ref yRef, index + (vectorCount * 3))).StoreUnsafe(ref destinationRef, index + (vectorCount * 3));
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 4)), Vector512.LoadUnsafe(ref yRef, index + (vectorCount * 4))).StoreUnsafe(ref destinationRef, index + (vectorCount * 4));
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 5)), Vector512.LoadUnsafe(ref yRef, index + (vectorCount * 5))).StoreUnsafe(ref destinationRef, index + (vectorCount * 5));
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 6)), Vector512.LoadUnsafe(ref yRef, index + (vectorCount * 6))).StoreUnsafe(ref destinationRef, index + (vectorCount * 6));
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 7)), Vector512.LoadUnsafe(ref yRef, index + (vectorCount * 7))).StoreUnsafe(ref destinationRef, index + (vectorCount * 7));

            index += vectorsPerLoop;
        }

        while ((length - index) >= vectorCount)
        {
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index), Vector512.LoadUnsafe(ref yRef, index)).StoreUnsafe(ref destinationRef, index);
            index += vectorCount;
        }

        if (index != length)
        {
            end.StoreUnsafe(ref destinationRef, length - vectorCount);
        }
    }

    /// <summary>
    /// Applies a binary operation between a span and a scalar with 128-bit vectors.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TOperator">The operation to apply.</typeparam>
    /// <param name="xRef">The first input element.</param>
    /// <param name="y">The scalar input.</param>
    /// <param name="destinationRef">The first destination element.</param>
    /// <param name="length">The number of elements to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InvokeVectorized128<T, TOperator>(
        ref T xRef,
        T y,
        ref T destinationRef,
        nuint length)
        where TOperator : struct, IBinaryOperator<T>
    {
        nuint vectorCount = (uint)Vector128<T>.Count;
        nuint vectorsPerLoop = vectorCount * 8;
        nuint index = 0;
        Vector128<T> yVector = Vector128.Create(y);

        // When a tail exists, preloading its final vector is required for in-place operation because it must
        // observe the original values before an earlier overlapping store writes them.
        Vector128<T> end = default;
        if ((length % vectorCount) != 0)
        {
            end = TOperator.Invoke(
                Vector128.LoadUnsafe(ref xRef, length - vectorCount),
                yVector);
        }

        while ((length - index) >= vectorsPerLoop)
        {
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 0)), yVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 0));
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 1)), yVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 1));
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 2)), yVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 2));
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 3)), yVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 3));
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 4)), yVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 4));
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 5)), yVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 5));
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 6)), yVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 6));
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 7)), yVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 7));

            index += vectorsPerLoop;
        }

        while ((length - index) >= vectorCount)
        {
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index), yVector).StoreUnsafe(ref destinationRef, index);
            index += vectorCount;
        }

        if (index != length)
        {
            end.StoreUnsafe(ref destinationRef, length - vectorCount);
        }
    }

    /// <summary>
    /// Applies a binary operation with 256-bit vectors.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TOperator">The operation to apply.</typeparam>
    /// <param name="xRef">The first input element.</param>
    /// <param name="y">The scalar input.</param>
    /// <param name="destinationRef">The first destination element.</param>
    /// <param name="length">The number of elements to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InvokeVectorized256<T, TOperator>(
        ref T xRef,
        T y,
        ref T destinationRef,
        nuint length)
        where TOperator : struct, IBinaryOperator<T>
    {
        nuint vectorCount = (uint)Vector256<T>.Count;
        nuint vectorsPerLoop = vectorCount * 8;
        nuint index = 0;
        Vector256<T> yVector = Vector256.Create(y);
        Vector256<T> end = default;
        if ((length % vectorCount) != 0)
        {
            end = TOperator.Invoke(
                Vector256.LoadUnsafe(ref xRef, length - vectorCount),
                yVector);
        }

        while ((length - index) >= vectorsPerLoop)
        {
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 0)), yVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 0));
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 1)), yVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 1));
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 2)), yVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 2));
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 3)), yVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 3));
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 4)), yVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 4));
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 5)), yVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 5));
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 6)), yVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 6));
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 7)), yVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 7));

            index += vectorsPerLoop;
        }

        while ((length - index) >= vectorCount)
        {
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index), yVector).StoreUnsafe(ref destinationRef, index);
            index += vectorCount;
        }

        if (index != length)
        {
            end.StoreUnsafe(ref destinationRef, length - vectorCount);
        }
    }

    /// <summary>
    /// Applies a binary operation with 512-bit vectors.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TOperator">The operation to apply.</typeparam>
    /// <param name="xRef">The first input element.</param>
    /// <param name="y">The scalar input.</param>
    /// <param name="destinationRef">The first destination element.</param>
    /// <param name="length">The number of elements to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InvokeVectorized512<T, TOperator>(
        ref T xRef,
        T y,
        ref T destinationRef,
        nuint length)
        where TOperator : struct, IBinaryOperator<T>
    {
        nuint vectorCount = (uint)Vector512<T>.Count;
        nuint vectorsPerLoop = vectorCount * 8;
        nuint index = 0;
        Vector512<T> yVector = Vector512.Create(y);
        Vector512<T> end = default;
        if ((length % vectorCount) != 0)
        {
            end = TOperator.Invoke(
                Vector512.LoadUnsafe(ref xRef, length - vectorCount),
                yVector);
        }

        while ((length - index) >= vectorsPerLoop)
        {
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 0)), yVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 0));
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 1)), yVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 1));
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 2)), yVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 2));
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 3)), yVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 3));
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 4)), yVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 4));
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 5)), yVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 5));
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 6)), yVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 6));
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 7)), yVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 7));

            index += vectorsPerLoop;
        }

        while ((length - index) >= vectorCount)
        {
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index), yVector).StoreUnsafe(ref destinationRef, index);
            index += vectorCount;
        }

        if (index != length)
        {
            end.StoreUnsafe(ref destinationRef, length - vectorCount);
        }
    }

    /// <summary>
    /// Selects maximum single-precision values with the normalized runtime semantics.
    /// </summary>
    /// <param name="x">The first values.</param>
    /// <param name="y">The second values.</param>
    /// <returns>The maximum values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> MaxSingle(Vector128<float> x, Vector128<float> y)
    {
        // The .NET 8 operation already handles ordered unequal values. Correct its second-operand result for a
        // first-operand NaN, then use bitwise AND for equal values so positive zero wins regardless of operand order.
        Vector128<float> result = Vector128.Max(x, y);
        result = Vector128.ConditionalSelect(~Vector128.Equals(x, x), x, result);

        return Vector128.ConditionalSelect(
            Vector128.Equals(x, y),
            x & y,
            result);
    }

    /// <summary>
    /// Selects maximum single-precision values with the normalized runtime semantics.
    /// </summary>
    /// <param name="x">The first values.</param>
    /// <param name="y">The second values.</param>
    /// <returns>The maximum values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> MaxSingle(Vector256<float> x, Vector256<float> y)
    {
        Vector256<float> result = Vector256.Max(x, y);
        result = Vector256.ConditionalSelect(~Vector256.Equals(x, x), x, result);

        return Vector256.ConditionalSelect(
            Vector256.Equals(x, y),
            x & y,
            result);
    }

    /// <summary>
    /// Selects maximum single-precision values with the normalized runtime semantics.
    /// </summary>
    /// <param name="x">The first values.</param>
    /// <param name="y">The second values.</param>
    /// <returns>The maximum values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> MaxSingle(Vector512<float> x, Vector512<float> y)
    {
        Vector512<float> result = Vector512.Max(x, y);
        result = Vector512.ConditionalSelect(~Vector512.Equals(x, x), x, result);

        return Vector512.ConditionalSelect(
            Vector512.Equals(x, y),
            x & y,
            result);
    }

    /// <summary>
    /// Selects maximum double-precision values with the normalized runtime semantics.
    /// </summary>
    /// <param name="x">The first values.</param>
    /// <param name="y">The second values.</param>
    /// <returns>The maximum values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<double> MaxDouble(Vector128<double> x, Vector128<double> y)
    {
        Vector128<double> result = Vector128.Max(x, y);
        result = Vector128.ConditionalSelect(~Vector128.Equals(x, x), x, result);

        return Vector128.ConditionalSelect(
            Vector128.Equals(x, y),
            x & y,
            result);
    }

    /// <summary>
    /// Selects maximum double-precision values with the normalized runtime semantics.
    /// </summary>
    /// <param name="x">The first values.</param>
    /// <param name="y">The second values.</param>
    /// <returns>The maximum values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<double> MaxDouble(Vector256<double> x, Vector256<double> y)
    {
        Vector256<double> result = Vector256.Max(x, y);
        result = Vector256.ConditionalSelect(~Vector256.Equals(x, x), x, result);

        return Vector256.ConditionalSelect(
            Vector256.Equals(x, y),
            x & y,
            result);
    }

    /// <summary>
    /// Selects maximum double-precision values with the normalized runtime semantics.
    /// </summary>
    /// <param name="x">The first values.</param>
    /// <param name="y">The second values.</param>
    /// <returns>The maximum values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<double> MaxDouble(Vector512<double> x, Vector512<double> y)
    {
        Vector512<double> result = Vector512.Max(x, y);
        result = Vector512.ConditionalSelect(~Vector512.Equals(x, x), x, result);

        return Vector512.ConditionalSelect(
            Vector512.Equals(x, y),
            x & y,
            result);
    }

    /// <summary>
    /// Applies a ternary operation with 128-bit vectors.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TOperator">The operation to apply.</typeparam>
    /// <param name="xRef">The first input element.</param>
    /// <param name="y">The first scalar input.</param>
    /// <param name="z">The second scalar input.</param>
    /// <param name="destinationRef">The first destination element.</param>
    /// <param name="length">The number of elements to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InvokeVectorized128<T, TOperator>(
        ref T xRef,
        T y,
        T z,
        ref T destinationRef,
        nuint length)
        where TOperator : struct, ITernaryOperator<T>
    {
        nuint vectorCount = (uint)Vector128<T>.Count;
        nuint vectorsPerLoop = vectorCount * 8;
        nuint index = 0;
        Vector128<T> yVector = Vector128.Create(y);
        Vector128<T> zVector = Vector128.Create(z);
        Vector128<T> end = default;
        if ((length % vectorCount) != 0)
        {
            end = TOperator.Invoke(
                Vector128.LoadUnsafe(ref xRef, length - vectorCount),
                yVector,
                zVector);
        }

        while ((length - index) >= vectorsPerLoop)
        {
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 0)), yVector, zVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 0));
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 1)), yVector, zVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 1));
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 2)), yVector, zVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 2));
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 3)), yVector, zVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 3));
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 4)), yVector, zVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 4));
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 5)), yVector, zVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 5));
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 6)), yVector, zVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 6));
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index + (vectorCount * 7)), yVector, zVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 7));

            index += vectorsPerLoop;
        }

        while ((length - index) >= vectorCount)
        {
            TOperator.Invoke(Vector128.LoadUnsafe(ref xRef, index), yVector, zVector).StoreUnsafe(ref destinationRef, index);
            index += vectorCount;
        }

        if (index != length)
        {
            end.StoreUnsafe(ref destinationRef, length - vectorCount);
        }
    }

    /// <summary>
    /// Applies a ternary operation with 256-bit vectors.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TOperator">The operation to apply.</typeparam>
    /// <param name="xRef">The first input element.</param>
    /// <param name="y">The first scalar input.</param>
    /// <param name="z">The second scalar input.</param>
    /// <param name="destinationRef">The first destination element.</param>
    /// <param name="length">The number of elements to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InvokeVectorized256<T, TOperator>(
        ref T xRef,
        T y,
        T z,
        ref T destinationRef,
        nuint length)
        where TOperator : struct, ITernaryOperator<T>
    {
        nuint vectorCount = (uint)Vector256<T>.Count;
        nuint vectorsPerLoop = vectorCount * 8;
        nuint index = 0;
        Vector256<T> yVector = Vector256.Create(y);
        Vector256<T> zVector = Vector256.Create(z);
        Vector256<T> end = default;
        if ((length % vectorCount) != 0)
        {
            end = TOperator.Invoke(
                Vector256.LoadUnsafe(ref xRef, length - vectorCount),
                yVector,
                zVector);
        }

        while ((length - index) >= vectorsPerLoop)
        {
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 0)), yVector, zVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 0));
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 1)), yVector, zVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 1));
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 2)), yVector, zVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 2));
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 3)), yVector, zVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 3));
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 4)), yVector, zVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 4));
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 5)), yVector, zVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 5));
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 6)), yVector, zVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 6));
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index + (vectorCount * 7)), yVector, zVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 7));

            index += vectorsPerLoop;
        }

        while ((length - index) >= vectorCount)
        {
            TOperator.Invoke(Vector256.LoadUnsafe(ref xRef, index), yVector, zVector).StoreUnsafe(ref destinationRef, index);
            index += vectorCount;
        }

        if (index != length)
        {
            end.StoreUnsafe(ref destinationRef, length - vectorCount);
        }
    }

    /// <summary>
    /// Applies a ternary operation with 512-bit vectors.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TOperator">The operation to apply.</typeparam>
    /// <param name="xRef">The first input element.</param>
    /// <param name="y">The first scalar input.</param>
    /// <param name="z">The second scalar input.</param>
    /// <param name="destinationRef">The first destination element.</param>
    /// <param name="length">The number of elements to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InvokeVectorized512<T, TOperator>(
        ref T xRef,
        T y,
        T z,
        ref T destinationRef,
        nuint length)
        where TOperator : struct, ITernaryOperator<T>
    {
        nuint vectorCount = (uint)Vector512<T>.Count;
        nuint vectorsPerLoop = vectorCount * 8;
        nuint index = 0;
        Vector512<T> yVector = Vector512.Create(y);
        Vector512<T> zVector = Vector512.Create(z);
        Vector512<T> end = default;
        if ((length % vectorCount) != 0)
        {
            end = TOperator.Invoke(
                Vector512.LoadUnsafe(ref xRef, length - vectorCount),
                yVector,
                zVector);
        }

        while ((length - index) >= vectorsPerLoop)
        {
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 0)), yVector, zVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 0));
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 1)), yVector, zVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 1));
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 2)), yVector, zVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 2));
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 3)), yVector, zVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 3));
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 4)), yVector, zVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 4));
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 5)), yVector, zVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 5));
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 6)), yVector, zVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 6));
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index + (vectorCount * 7)), yVector, zVector).StoreUnsafe(ref destinationRef, index + (vectorCount * 7));

            index += vectorsPerLoop;
        }

        while ((length - index) >= vectorCount)
        {
            TOperator.Invoke(Vector512.LoadUnsafe(ref xRef, index), yVector, zVector).StoreUnsafe(ref destinationRef, index);
            index += vectorCount;
        }

        if (index != length)
        {
            end.StoreUnsafe(ref destinationRef, length - vectorCount);
        }
    }

    /// <summary>
    /// Clamps single-precision values with the normalized runtime semantics.
    /// </summary>
    /// <param name="value">The values to clamp.</param>
    /// <param name="min">The inclusive lower bounds.</param>
    /// <param name="max">The inclusive upper bounds.</param>
    /// <returns>The clamped values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> ClampSingle(
        Vector128<float> value,
        Vector128<float> min,
        Vector128<float> max)
    {
        // Unlike the native x86 min/max instructions, the normalized runtime operations propagate a NaN in the
        // first operand and select negative zero when equal values have different signs.
        Vector128<float> maximum = Vector128.ConditionalSelect(
            Vector128.LessThan(min, value)
            | ~Vector128.Equals(value, value)
            | (Vector128.Equals(value, min) & (min.AsInt32() >> 31).AsSingle()),
            value,
            min);

        return Vector128.ConditionalSelect(
            Vector128.LessThan(maximum, max)
            | ~Vector128.Equals(maximum, maximum)
            | (Vector128.Equals(maximum, max) & (maximum.AsInt32() >> 31).AsSingle()),
            maximum,
            max);
    }

    /// <summary>
    /// Clamps single-precision values with the normalized runtime semantics.
    /// </summary>
    /// <param name="value">The values to clamp.</param>
    /// <param name="min">The inclusive lower bounds.</param>
    /// <param name="max">The inclusive upper bounds.</param>
    /// <returns>The clamped values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> ClampSingle(
        Vector256<float> value,
        Vector256<float> min,
        Vector256<float> max)
    {
        Vector256<float> maximum = Vector256.ConditionalSelect(
            Vector256.LessThan(min, value)
            | ~Vector256.Equals(value, value)
            | (Vector256.Equals(value, min) & (min.AsInt32() >> 31).AsSingle()),
            value,
            min);

        return Vector256.ConditionalSelect(
            Vector256.LessThan(maximum, max)
            | ~Vector256.Equals(maximum, maximum)
            | (Vector256.Equals(maximum, max) & (maximum.AsInt32() >> 31).AsSingle()),
            maximum,
            max);
    }

    /// <summary>
    /// Clamps single-precision values with the normalized runtime semantics.
    /// </summary>
    /// <param name="value">The values to clamp.</param>
    /// <param name="min">The inclusive lower bounds.</param>
    /// <param name="max">The inclusive upper bounds.</param>
    /// <returns>The clamped values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> ClampSingle(
        Vector512<float> value,
        Vector512<float> min,
        Vector512<float> max)
    {
        Vector512<float> maximum = Vector512.ConditionalSelect(
            Vector512.LessThan(min, value)
            | ~Vector512.Equals(value, value)
            | (Vector512.Equals(value, min) & (min.AsInt32() >> 31).AsSingle()),
            value,
            min);

        return Vector512.ConditionalSelect(
            Vector512.LessThan(maximum, max)
            | ~Vector512.Equals(maximum, maximum)
            | (Vector512.Equals(maximum, max) & (maximum.AsInt32() >> 31).AsSingle()),
            maximum,
            max);
    }

    /// <summary>
    /// Clamps double-precision values with the normalized runtime semantics.
    /// </summary>
    /// <param name="value">The values to clamp.</param>
    /// <param name="min">The inclusive lower bounds.</param>
    /// <param name="max">The inclusive upper bounds.</param>
    /// <returns>The clamped values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<double> ClampDouble(
        Vector128<double> value,
        Vector128<double> min,
        Vector128<double> max)
    {
        Vector128<double> maximum = Vector128.ConditionalSelect(
            Vector128.LessThan(min, value)
            | ~Vector128.Equals(value, value)
            | (Vector128.Equals(value, min) & (min.AsInt64() >> 63).AsDouble()),
            value,
            min);

        return Vector128.ConditionalSelect(
            Vector128.LessThan(maximum, max)
            | ~Vector128.Equals(maximum, maximum)
            | (Vector128.Equals(maximum, max) & (maximum.AsInt64() >> 63).AsDouble()),
            maximum,
            max);
    }

    /// <summary>
    /// Clamps double-precision values with the normalized runtime semantics.
    /// </summary>
    /// <param name="value">The values to clamp.</param>
    /// <param name="min">The inclusive lower bounds.</param>
    /// <param name="max">The inclusive upper bounds.</param>
    /// <returns>The clamped values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<double> ClampDouble(
        Vector256<double> value,
        Vector256<double> min,
        Vector256<double> max)
    {
        Vector256<double> maximum = Vector256.ConditionalSelect(
            Vector256.LessThan(min, value)
            | ~Vector256.Equals(value, value)
            | (Vector256.Equals(value, min) & (min.AsInt64() >> 63).AsDouble()),
            value,
            min);

        return Vector256.ConditionalSelect(
            Vector256.LessThan(maximum, max)
            | ~Vector256.Equals(maximum, maximum)
            | (Vector256.Equals(maximum, max) & (maximum.AsInt64() >> 63).AsDouble()),
            maximum,
            max);
    }

    /// <summary>
    /// Clamps double-precision values with the normalized runtime semantics.
    /// </summary>
    /// <param name="value">The values to clamp.</param>
    /// <param name="min">The inclusive lower bounds.</param>
    /// <param name="max">The inclusive upper bounds.</param>
    /// <returns>The clamped values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<double> ClampDouble(
        Vector512<double> value,
        Vector512<double> min,
        Vector512<double> max)
    {
        Vector512<double> maximum = Vector512.ConditionalSelect(
            Vector512.LessThan(min, value)
            | ~Vector512.Equals(value, value)
            | (Vector512.Equals(value, min) & (min.AsInt64() >> 63).AsDouble()),
            value,
            min);

        return Vector512.ConditionalSelect(
            Vector512.LessThan(maximum, max)
            | ~Vector512.Equals(maximum, maximum)
            | (Vector512.Equals(maximum, max) & (maximum.AsInt64() >> 63).AsDouble()),
            maximum,
            max);
    }

    /// <summary>
    /// Determines whether <typeparamref name="T"/> has the same vector division support as <see cref="int"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns><see langword="true"/> when <typeparamref name="T"/> is a 32-bit signed native integer type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsInt32Like<T>()
        => typeof(T) == typeof(int) || (IntPtr.Size == 4 && typeof(T) == typeof(nint));

    /// <summary>
    /// Adds corresponding values.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    private readonly struct AddOperator<T> : IBinaryOperator<T>
        where T : IAdditionOperators<T, T, T>, IAdditiveIdentity<T, T>
    {
        /// <summary>
        /// Gets a value indicating whether this operation supports vector execution.
        /// </summary>
        public static bool Vectorizable => true;

        /// <summary>
        /// Adds scalar values.
        /// </summary>
        /// <param name="x">The first addend.</param>
        /// <param name="y">The second addend.</param>
        /// <returns>The sum.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Invoke(T x, T y) => x + y;

        /// <summary>
        /// Adds 128-bit vectors.
        /// </summary>
        /// <param name="x">The first addends.</param>
        /// <param name="y">The second addends.</param>
        /// <returns>The sums.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<T> Invoke(Vector128<T> x, Vector128<T> y) => x + y;

        /// <summary>
        /// Adds 256-bit vectors.
        /// </summary>
        /// <param name="x">The first addends.</param>
        /// <param name="y">The second addends.</param>
        /// <returns>The sums.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<T> Invoke(Vector256<T> x, Vector256<T> y) => x + y;

        /// <summary>
        /// Adds 512-bit vectors.
        /// </summary>
        /// <param name="x">The first addends.</param>
        /// <param name="y">The second addends.</param>
        /// <returns>The sums.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<T> Invoke(Vector512<T> x, Vector512<T> y) => x + y;
    }

    /// <summary>
    /// Clamps values using the complete runtime tensor contract, including signed-zero correction.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    private readonly struct ClampOperator<T> : ITernaryOperator<T>
        where T : INumber<T>
    {
        /// <summary>
        /// Gets a value indicating whether this operation supports vector execution.
        /// </summary>
        public static bool Vectorizable => true;

        /// <summary>
        /// Clamps a scalar value.
        /// </summary>
        /// <param name="x">The value.</param>
        /// <param name="min">The inclusive lower bound.</param>
        /// <param name="max">The inclusive upper bound.</param>
        /// <returns>The clamped value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Invoke(T x, T min, T max)
            => Vector128<T>.IsSupported ? T.Min(T.Max(x, min), max) : T.Clamp(x, min, max);

        /// <summary>
        /// Clamps a 128-bit vector.
        /// </summary>
        /// <param name="x">The values.</param>
        /// <param name="min">The inclusive lower bounds.</param>
        /// <param name="max">The inclusive upper bounds.</param>
        /// <returns>The clamped values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<T> Invoke(Vector128<T> x, Vector128<T> min, Vector128<T> max)
        {
            if (typeof(T) == typeof(float))
            {
                Vector128<float> result = ClampSingle(
                    Unsafe.As<Vector128<T>, Vector128<float>>(ref x),
                    Unsafe.As<Vector128<T>, Vector128<float>>(ref min),
                    Unsafe.As<Vector128<T>, Vector128<float>>(ref max));

                return Unsafe.As<Vector128<float>, Vector128<T>>(ref result);
            }

            if (typeof(T) == typeof(double))
            {
                Vector128<double> result = ClampDouble(
                    Unsafe.As<Vector128<T>, Vector128<double>>(ref x),
                    Unsafe.As<Vector128<T>, Vector128<double>>(ref min),
                    Unsafe.As<Vector128<T>, Vector128<double>>(ref max));

                return Unsafe.As<Vector128<double>, Vector128<T>>(ref result);
            }

            return Vector128_.Clamp(x, min, max);
        }

        /// <summary>
        /// Clamps a 256-bit vector.
        /// </summary>
        /// <param name="x">The values.</param>
        /// <param name="min">The inclusive lower bounds.</param>
        /// <param name="max">The inclusive upper bounds.</param>
        /// <returns>The clamped values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<T> Invoke(Vector256<T> x, Vector256<T> min, Vector256<T> max)
        {
            if (typeof(T) == typeof(float))
            {
                Vector256<float> result = ClampSingle(
                    Unsafe.As<Vector256<T>, Vector256<float>>(ref x),
                    Unsafe.As<Vector256<T>, Vector256<float>>(ref min),
                    Unsafe.As<Vector256<T>, Vector256<float>>(ref max));

                return Unsafe.As<Vector256<float>, Vector256<T>>(ref result);
            }

            if (typeof(T) == typeof(double))
            {
                Vector256<double> result = ClampDouble(
                    Unsafe.As<Vector256<T>, Vector256<double>>(ref x),
                    Unsafe.As<Vector256<T>, Vector256<double>>(ref min),
                    Unsafe.As<Vector256<T>, Vector256<double>>(ref max));

                return Unsafe.As<Vector256<double>, Vector256<T>>(ref result);
            }

            return Vector256_.Clamp(x, min, max);
        }

        /// <summary>
        /// Clamps a 512-bit vector.
        /// </summary>
        /// <param name="x">The values.</param>
        /// <param name="min">The inclusive lower bounds.</param>
        /// <param name="max">The inclusive upper bounds.</param>
        /// <returns>The clamped values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<T> Invoke(Vector512<T> x, Vector512<T> min, Vector512<T> max)
        {
            if (typeof(T) == typeof(float))
            {
                Vector512<float> result = ClampSingle(
                    Unsafe.As<Vector512<T>, Vector512<float>>(ref x),
                    Unsafe.As<Vector512<T>, Vector512<float>>(ref min),
                    Unsafe.As<Vector512<T>, Vector512<float>>(ref max));

                return Unsafe.As<Vector512<float>, Vector512<T>>(ref result);
            }

            if (typeof(T) == typeof(double))
            {
                Vector512<double> result = ClampDouble(
                    Unsafe.As<Vector512<T>, Vector512<double>>(ref x),
                    Unsafe.As<Vector512<T>, Vector512<double>>(ref min),
                    Unsafe.As<Vector512<T>, Vector512<double>>(ref max));

                return Unsafe.As<Vector512<double>, Vector512<T>>(ref result);
            }

            return Vector512_.Clamp(x, min, max);
        }
    }

    /// <summary>
    /// Selects the maximum corresponding values.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    private readonly struct MaxOperator<T> : IBinaryOperator<T>
        where T : INumber<T>
    {
        /// <summary>
        /// Gets a value indicating whether this operation supports vector execution.
        /// </summary>
        public static bool Vectorizable => true;

        /// <summary>
        /// Selects the maximum scalar value.
        /// </summary>
        /// <param name="x">The first value.</param>
        /// <param name="y">The second value.</param>
        /// <returns>The maximum value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Invoke(T x, T y) => T.Max(x, y);

        /// <summary>
        /// Selects the maximum values from 128-bit vectors.
        /// </summary>
        /// <param name="x">The first values.</param>
        /// <param name="y">The second values.</param>
        /// <returns>The maximum values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<T> Invoke(Vector128<T> x, Vector128<T> y)
        {
            if (typeof(T) == typeof(float))
            {
                Vector128<float> result = MaxSingle(
                    Unsafe.As<Vector128<T>, Vector128<float>>(ref x),
                    Unsafe.As<Vector128<T>, Vector128<float>>(ref y));

                return Unsafe.As<Vector128<float>, Vector128<T>>(ref result);
            }

            if (typeof(T) == typeof(double))
            {
                Vector128<double> result = MaxDouble(
                    Unsafe.As<Vector128<T>, Vector128<double>>(ref x),
                    Unsafe.As<Vector128<T>, Vector128<double>>(ref y));

                return Unsafe.As<Vector128<double>, Vector128<T>>(ref result);
            }

            return Vector128.Max(x, y);
        }

        /// <summary>
        /// Selects the maximum values from 256-bit vectors.
        /// </summary>
        /// <param name="x">The first values.</param>
        /// <param name="y">The second values.</param>
        /// <returns>The maximum values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<T> Invoke(Vector256<T> x, Vector256<T> y)
        {
            if (typeof(T) == typeof(float))
            {
                Vector256<float> result = MaxSingle(
                    Unsafe.As<Vector256<T>, Vector256<float>>(ref x),
                    Unsafe.As<Vector256<T>, Vector256<float>>(ref y));

                return Unsafe.As<Vector256<float>, Vector256<T>>(ref result);
            }

            if (typeof(T) == typeof(double))
            {
                Vector256<double> result = MaxDouble(
                    Unsafe.As<Vector256<T>, Vector256<double>>(ref x),
                    Unsafe.As<Vector256<T>, Vector256<double>>(ref y));

                return Unsafe.As<Vector256<double>, Vector256<T>>(ref result);
            }

            return Vector256.Max(x, y);
        }

        /// <summary>
        /// Selects the maximum values from 512-bit vectors.
        /// </summary>
        /// <param name="x">The first values.</param>
        /// <param name="y">The second values.</param>
        /// <returns>The maximum values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<T> Invoke(Vector512<T> x, Vector512<T> y)
        {
            if (typeof(T) == typeof(float))
            {
                Vector512<float> result = MaxSingle(
                    Unsafe.As<Vector512<T>, Vector512<float>>(ref x),
                    Unsafe.As<Vector512<T>, Vector512<float>>(ref y));

                return Unsafe.As<Vector512<float>, Vector512<T>>(ref result);
            }

            if (typeof(T) == typeof(double))
            {
                Vector512<double> result = MaxDouble(
                    Unsafe.As<Vector512<T>, Vector512<double>>(ref x),
                    Unsafe.As<Vector512<T>, Vector512<double>>(ref y));

                return Unsafe.As<Vector512<double>, Vector512<T>>(ref result);
            }

            return Vector512.Max(x, y);
        }
    }

    /// <summary>
    /// Multiplies corresponding values.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    private readonly struct MultiplyOperator<T> : IBinaryOperator<T>
        where T : IMultiplyOperators<T, T, T>, IMultiplicativeIdentity<T, T>
    {
        /// <summary>
        /// Gets a value indicating whether this operation supports vector execution.
        /// </summary>
        public static bool Vectorizable => true;

        /// <summary>
        /// Multiplies scalar values.
        /// </summary>
        /// <param name="x">The multiplicand.</param>
        /// <param name="y">The multiplier.</param>
        /// <returns>The product.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Invoke(T x, T y) => x * y;

        /// <summary>
        /// Multiplies 128-bit vectors.
        /// </summary>
        /// <param name="x">The multiplicands.</param>
        /// <param name="y">The multipliers.</param>
        /// <returns>The products.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<T> Invoke(Vector128<T> x, Vector128<T> y) => x * y;

        /// <summary>
        /// Multiplies 256-bit vectors.
        /// </summary>
        /// <param name="x">The multiplicands.</param>
        /// <param name="y">The multipliers.</param>
        /// <returns>The products.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<T> Invoke(Vector256<T> x, Vector256<T> y) => x * y;

        /// <summary>
        /// Multiplies 512-bit vectors.
        /// </summary>
        /// <param name="x">The multiplicands.</param>
        /// <param name="y">The multipliers.</param>
        /// <returns>The products.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<T> Invoke(Vector512<T> x, Vector512<T> y) => x * y;
    }

    /// <summary>
    /// Divides values by a scalar.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    private readonly struct DivideOperator<T> : IBinaryOperator<T>
        where T : IDivisionOperators<T, T, T>
    {
        /// <summary>
        /// Gets a value indicating whether this operation supports vector execution.
        /// </summary>
        public static bool Vectorizable => typeof(T) == typeof(float)
                                        || typeof(T) == typeof(double)
                                        || (Vector256.IsHardwareAccelerated && IsInt32Like<T>());

        /// <summary>
        /// Divides scalar values.
        /// </summary>
        /// <param name="x">The dividend.</param>
        /// <param name="y">The divisor.</param>
        /// <returns>The quotient.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Invoke(T x, T y) => x / y;

        /// <summary>
        /// Divides 128-bit vectors.
        /// </summary>
        /// <param name="x">The dividends.</param>
        /// <param name="y">The divisors.</param>
        /// <returns>The quotients.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<T> Invoke(Vector128<T> x, Vector128<T> y) => x / y;

        /// <summary>
        /// Divides 256-bit vectors.
        /// </summary>
        /// <param name="x">The dividends.</param>
        /// <param name="y">The divisors.</param>
        /// <returns>The quotients.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<T> Invoke(Vector256<T> x, Vector256<T> y) => x / y;

        /// <summary>
        /// Divides 512-bit vectors.
        /// </summary>
        /// <param name="x">The dividends.</param>
        /// <param name="y">The divisors.</param>
        /// <returns>The quotients.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<T> Invoke(Vector512<T> x, Vector512<T> y) => x / y;
    }
}
