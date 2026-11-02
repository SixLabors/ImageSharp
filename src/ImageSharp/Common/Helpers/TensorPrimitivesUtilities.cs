// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Common.Helpers;

/// <summary>
/// Provides bulk SIMD operations that System.Numerics.Tensors does not supply.
/// </summary>
#pragma warning disable SA1649 // Match the existing Vector128_, Vector256_ and Vector512_ helper names.
internal static partial class TensorPrimitives_
#pragma warning restore SA1649 // File name should match first type name
{
    /// <summary>
    /// Defines scalar and SIMD forms of one component operation.
    /// </summary>
    /// <remarks>
    /// The operation treats every component independently. It does not interpret pixel layout or alpha.
    /// </remarks>
    public interface IUnaryOperator
    {
        /// <summary>
        /// Transforms independent component values.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The transformed components.</returns>
        public float Invoke(float value);

        /// <summary>
        /// Transforms independent component values.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The transformed components.</returns>
        public Vector128<float> Invoke(Vector128<float> value);

        /// <summary>
        /// Transforms independent component values.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The transformed components.</returns>
        public Vector256<float> Invoke(Vector256<float> value);

        /// <summary>
        /// Transforms independent component values.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The transformed components.</returns>
        public Vector512<float> Invoke(Vector512<float> value);
    }

    /// <summary>
    /// Defines scalar and SIMD forms of an operation on two independent component inputs.
    /// </summary>
    public interface IBinaryOperator
    {
        /// <summary>
        /// Transforms two scalar components.
        /// </summary>
        /// <param name="first">The first input.</param>
        /// <param name="second">The second input.</param>
        /// <returns>The transformed component.</returns>
        public float Invoke(float first, float second);

        /// <summary>
        /// Transforms corresponding components in two SIMD registers.
        /// </summary>
        /// <param name="first">The first inputs.</param>
        /// <param name="second">The second inputs.</param>
        /// <returns>The transformed components in their original lane order.</returns>
        public Vector128<float> Invoke(Vector128<float> first, Vector128<float> second);

        /// <inheritdoc cref="Invoke(Vector128{float}, Vector128{float})" />
        public Vector256<float> Invoke(Vector256<float> first, Vector256<float> second);

        /// <inheritdoc cref="Invoke(Vector128{float}, Vector128{float})" />
        public Vector512<float> Invoke(Vector512<float> first, Vector512<float> second);
    }

    /// <summary>
    /// Defines scalar and SIMD forms of an operation on three independent component inputs.
    /// </summary>
    public interface ITernaryOperator
    {
        /// <summary>
        /// Transforms three scalar components.
        /// </summary>
        /// <param name="first">The first input.</param>
        /// <param name="second">The second input.</param>
        /// <param name="third">The third input.</param>
        /// <returns>The transformed component.</returns>
        public float Invoke(float first, float second, float third);

        /// <summary>
        /// Transforms corresponding components in three SIMD registers.
        /// </summary>
        /// <param name="first">The first inputs.</param>
        /// <param name="second">The second inputs.</param>
        /// <param name="third">The third inputs.</param>
        /// <returns>The transformed components in their original lane order.</returns>
        public Vector128<float> Invoke(Vector128<float> first, Vector128<float> second, Vector128<float> third);

        /// <inheritdoc cref="Invoke(Vector128{float}, Vector128{float}, Vector128{float})" />
        public Vector256<float> Invoke(Vector256<float> first, Vector256<float> second, Vector256<float> third);

        /// <inheritdoc cref="Invoke(Vector128{float}, Vector128{float}, Vector128{float})" />
        public Vector512<float> Invoke(Vector512<float> first, Vector512<float> second, Vector512<float> third);
    }

    /// <summary>
    /// Transforms every component in place with the widest supported SIMD width.
    /// </summary>
    /// <typeparam name="TOperator">The operation configured before traversal.</typeparam>
    /// <param name="values">The independent components to transform.</param>
    /// <param name="operation">The scalar and SIMD forms of the operation.</param>
    public static void Invoke<TOperator>(Span<float> values, TOperator operation)
        where TOperator : struct, IUnaryOperator
        => Invoke(values, values, operation);

    /// <summary>
    /// Transforms independent components into equally sized output with the widest supported SIMD width.
    /// </summary>
    /// <typeparam name="TOperator">The operation configured before traversal.</typeparam>
    /// <param name="source">The independent input components.</param>
    /// <param name="destination">The output, with capacity for every input component.</param>
    /// <param name="operation">The scalar and SIMD forms of the operation.</param>
    /// <remarks>Input and output are disjoint or begin at the same address. Each complete register is loaded before its output is stored.</remarks>
    public static void Invoke<TOperator>(ReadOnlySpan<float> source, Span<float> destination, TOperator operation)
        where TOperator : struct, IUnaryOperator
    {
        ref float input = ref MemoryMarshal.GetReference(source);
        ref float output = ref MemoryMarshal.GetReference(destination);
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (source.Length - i).Vector512Count<float>();

            // Each lane owns one component. Smaller registers continue from the first unprocessed component.
            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                Vector512<float> value = Vector512.LoadUnsafe(ref input, (uint)i);
                operation.Invoke(value).StoreUnsafe(ref output, (uint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (source.Length - i).Vector256Count<float>();

            // Each lane owns one component. Smaller registers continue from the first unprocessed component.
            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                Vector256<float> value = Vector256.LoadUnsafe(ref input, (uint)i);
                operation.Invoke(value).StoreUnsafe(ref output, (uint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (source.Length - i).Vector128Count<float>();

            // Each lane owns one component. Smaller registers continue from the first unprocessed component.
            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                Vector128<float> value = Vector128.LoadUnsafe(ref input, (uint)i);
                operation.Invoke(value).StoreUnsafe(ref output, (uint)i);
            }
        }

        // The scalar remainder applies the same equation without reading or writing padding.
        for (; i < source.Length; i++)
        {
            Unsafe.Add(ref output, (uint)i) = operation.Invoke(Unsafe.Add(ref input, (uint)i));
        }
    }

    /// <summary>
    /// Transforms corresponding components from two equally sized spans into caller-owned output.
    /// </summary>
    /// <typeparam name="TOperator">The operation configured before traversal.</typeparam>
    /// <param name="first">The first inputs.</param>
    /// <param name="second">The second inputs, with the same length as first.</param>
    /// <param name="destination">The output, with capacity for every input component.</param>
    /// <param name="operation">The scalar and SIMD forms of the operation.</param>
    /// <remarks>
    /// Inputs and output are disjoint or begin at the same address. Both inputs are loaded before each store,
    /// so an output span can also supply either input. The operation does not interpret pixel layout or alpha.
    /// </remarks>
    public static void Invoke<TOperator>(ReadOnlySpan<float> first, ReadOnlySpan<float> second, Span<float> destination, TOperator operation)
        where TOperator : struct, IBinaryOperator
    {
        ref float input0 = ref MemoryMarshal.GetReference(first);
        ref float input1 = ref MemoryMarshal.GetReference(second);
        ref float output = ref MemoryMarshal.GetReference(destination);
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (first.Length - i).Vector512Count<float>();

            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                Vector512<float> value0 = Vector512.LoadUnsafe(ref input0, (uint)i);
                Vector512<float> value1 = Vector512.LoadUnsafe(ref input1, (uint)i);
                operation.Invoke(value0, value1).StoreUnsafe(ref output, (uint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (first.Length - i).Vector256Count<float>();

            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                Vector256<float> value0 = Vector256.LoadUnsafe(ref input0, (uint)i);
                Vector256<float> value1 = Vector256.LoadUnsafe(ref input1, (uint)i);
                operation.Invoke(value0, value1).StoreUnsafe(ref output, (uint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (first.Length - i).Vector128Count<float>();

            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                Vector128<float> value0 = Vector128.LoadUnsafe(ref input0, (uint)i);
                Vector128<float> value1 = Vector128.LoadUnsafe(ref input1, (uint)i);
                operation.Invoke(value0, value1).StoreUnsafe(ref output, (uint)i);
            }
        }

        for (; i < first.Length; i++)
        {
            float value0 = Unsafe.Add(ref input0, (uint)i);
            float value1 = Unsafe.Add(ref input1, (uint)i);
            Unsafe.Add(ref output, (uint)i) = operation.Invoke(value0, value1);
        }
    }

    /// <summary>
    /// Transforms corresponding components from three equally sized spans into caller-owned output.
    /// </summary>
    /// <typeparam name="TOperator">The operation configured before traversal.</typeparam>
    /// <param name="first">The first inputs.</param>
    /// <param name="second">The second inputs, with the same length as first.</param>
    /// <param name="third">The third inputs, with the same length as first.</param>
    /// <param name="destination">The output, with capacity for every input component.</param>
    /// <param name="operation">The scalar and SIMD forms of the operation.</param>
    /// <remarks>
    /// Inputs and output are disjoint or begin at the same address. All three inputs are loaded before each store,
    /// so an output span can also supply any one of the inputs. The operation does not interpret pixel layout or alpha.
    /// </remarks>
    public static void Invoke<TOperator>(
        ReadOnlySpan<float> first,
        ReadOnlySpan<float> second,
        ReadOnlySpan<float> third,
        Span<float> destination,
        TOperator operation)
        where TOperator : struct, ITernaryOperator
    {
        ref float input0 = ref MemoryMarshal.GetReference(first);
        ref float input1 = ref MemoryMarshal.GetReference(second);
        ref float input2 = ref MemoryMarshal.GetReference(third);
        ref float output = ref MemoryMarshal.GetReference(destination);
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (first.Length - i).Vector512Count<float>();

            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                // Load all corresponding inputs before the store. Same-start in-place calls cannot overwrite a later input block.
                Vector512<float> value0 = Vector512.LoadUnsafe(ref input0, (uint)i);
                Vector512<float> value1 = Vector512.LoadUnsafe(ref input1, (uint)i);
                Vector512<float> value2 = Vector512.LoadUnsafe(ref input2, (uint)i);
                operation.Invoke(value0, value1, value2).StoreUnsafe(ref output, (uint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (first.Length - i).Vector256Count<float>();

            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                // Load all corresponding inputs before the store. Same-start in-place calls cannot overwrite a later input block.
                Vector256<float> value0 = Vector256.LoadUnsafe(ref input0, (uint)i);
                Vector256<float> value1 = Vector256.LoadUnsafe(ref input1, (uint)i);
                Vector256<float> value2 = Vector256.LoadUnsafe(ref input2, (uint)i);
                operation.Invoke(value0, value1, value2).StoreUnsafe(ref output, (uint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (first.Length - i).Vector128Count<float>();

            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                // Load all corresponding inputs before the store. Same-start in-place calls cannot overwrite a later input block.
                Vector128<float> value0 = Vector128.LoadUnsafe(ref input0, (uint)i);
                Vector128<float> value1 = Vector128.LoadUnsafe(ref input1, (uint)i);
                Vector128<float> value2 = Vector128.LoadUnsafe(ref input2, (uint)i);
                operation.Invoke(value0, value1, value2).StoreUnsafe(ref output, (uint)i);
            }
        }

        for (; i < first.Length; i++)
        {
            float value0 = Unsafe.Add(ref input0, (uint)i);
            float value1 = Unsafe.Add(ref input1, (uint)i);
            float value2 = Unsafe.Add(ref input2, (uint)i);
            Unsafe.Add(ref output, (uint)i) = operation.Invoke(value0, value1, value2);
        }
    }

    /// <summary>
    /// Retains each independent component without arithmetic.
    /// </summary>
    public readonly struct IdentityOperator : IUnaryOperator
    {
        /// <inheritdoc />
        public float Invoke(float value) => value;

        /// <inheritdoc />
        public Vector128<float> Invoke(Vector128<float> value) => value;

        /// <inheritdoc />
        public Vector256<float> Invoke(Vector256<float> value) => value;

        /// <inheritdoc />
        public Vector512<float> Invoke(Vector512<float> value) => value;
    }
}
