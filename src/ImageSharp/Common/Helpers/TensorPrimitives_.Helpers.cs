// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
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
internal static partial class TensorPrimitives_
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
    /// Validates that an input and destination are either disjoint or begin at the same memory location.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="input">The input values.</param>
    /// <param name="destination">The destination values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateInputOutputSpanNonOverlapping<T>(ReadOnlySpan<T> input, Span<T> destination)
    {
        // Runtime TensorPrimitives permits exact same-start overlap for in-place operation. A shifted overlap is
        // rejected because forward SIMD stores could overwrite input elements before a later load consumes them.
        if (!Unsafe.AreSame(ref MemoryMarshal.GetReference(input), ref MemoryMarshal.GetReference(destination))
            && input.Overlaps(destination))
        {
            ThrowInputAndDestinationSpanMustNotOverlap();
        }
    }

    /// <summary>
    /// Throws when input spans do not have the same length.
    /// </summary>
    [DoesNotReturn]
    private static void ThrowSpansMustHaveSameLength()
        => throw new ArgumentException("Input span arguments must all have the same length.");

    /// <summary>
    /// Throws when the destination cannot hold every result.
    /// </summary>
    [DoesNotReturn]
    private static void ThrowDestinationTooShort()
        => throw new ArgumentException("Destination is too short.", "destination");

    /// <summary>
    /// Throws when an input and destination overlap without beginning at the same memory location.
    /// </summary>
    [DoesNotReturn]
    private static void ThrowInputAndDestinationSpanMustNotOverlap()
        => throw new ArgumentException(
            "The destination span may only overlap with an input span if the two spans start at the same memory location.",
            "destination");

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
        if (x.Length != y.Length)
        {
            ThrowSpansMustHaveSameLength();
        }

        if (x.Length > destination.Length)
        {
            ThrowDestinationTooShort();
        }

        ValidateInputOutputSpanNonOverlapping(x, destination);
        ValidateInputOutputSpanNonOverlapping(y, destination);

        ref T xRef = ref MemoryMarshal.GetReference(x);
        ref T yRef = ref MemoryMarshal.GetReference(y);
        ref T destinationRef = ref MemoryMarshal.GetReference(destination);
        nuint length = (uint)x.Length;

        // Runtime main selects the widest supported pipeline once one complete vector is available.
        // Each pipeline preloads its final inputs when a tail overlaps so same-start in-place operation remains correct.
        if (TOperator.Vectorizable
            && Vector512.IsHardwareAccelerated
            && Vector512<T>.IsSupported
            && length >= (uint)Vector512<T>.Count)
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
        if (x.Length > destination.Length)
        {
            ThrowDestinationTooShort();
        }

        ValidateInputOutputSpanNonOverlapping(x, destination);

        ref T xRef = ref MemoryMarshal.GetReference(x);
        ref T destinationRef = ref MemoryMarshal.GetReference(destination);
        nuint length = (uint)x.Length;

        // Runtime main selects the widest supported pipeline once one complete vector is available.
        if (TOperator.Vectorizable
            && Vector512.IsHardwareAccelerated
            && Vector512<T>.IsSupported
            && length >= (uint)Vector512<T>.Count)
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
    /// Performs element-wise division using the runtime tensor width-selection order.
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
        if (x.Length > destination.Length)
        {
            ThrowDestinationTooShort();
        }

        ValidateInputOutputSpanNonOverlapping(x, destination);

        ref T xRef = ref MemoryMarshal.GetReference(x);
        ref T destinationRef = ref MemoryMarshal.GetReference(destination);
        nuint length = (uint)x.Length;

        // Runtime main selects the widest supported pipeline once one complete vector is available.
        if (TOperator.Vectorizable
            && Vector512.IsHardwareAccelerated
            && Vector512<T>.IsSupported
            && length >= (uint)Vector512<T>.Count)
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
        if (x.Length > destination.Length)
        {
            ThrowDestinationTooShort();
        }

        ValidateInputOutputSpanNonOverlapping(x, destination);

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
}
