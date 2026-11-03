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
