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
    /// Transforms a component span in place with the widest supported SIMD width.
    /// </summary>
    /// <remarks>
    /// Every supplied component is transformed, regardless of its position in the span.
    /// </remarks>
    /// <typeparam name="TOperator">The operation configured before traversal.</typeparam>
    /// <param name="values">The independent components to transform.</param>
    /// <param name="operation">The scalar and SIMD forms of the operation.</param>
    public static void Invoke<TOperator>(Span<float> values, TOperator operation)
        where TOperator : struct, IUnaryOperator
    {
        ref float first = ref MemoryMarshal.GetReference(values);
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (values.Length - i).Vector512Count<float>();

            // Each lane holds one independent component. Complete registers do not overlap.
            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                Vector512<float> input = Vector512.LoadUnsafe(ref first, (uint)i);
                Vector512<float> output = operation.Invoke(input);
                output.StoreUnsafe(ref first, (uint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (values.Length - i).Vector256Count<float>();

            // Continue with complete smaller registers, then process the remaining components below.
            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                Vector256<float> input = Vector256.LoadUnsafe(ref first, (uint)i);
                Vector256<float> output = operation.Invoke(input);
                output.StoreUnsafe(ref first, (uint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (values.Length - i).Vector128Count<float>();

            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                Vector128<float> input = Vector128.LoadUnsafe(ref first, (uint)i);
                Vector128<float> output = operation.Invoke(input);
                output.StoreUnsafe(ref first, (uint)i);
            }
        }

        // The scalar remainder applies the same operation to each component.
        for (; i < values.Length; i++)
        {
            Unsafe.Add(ref first, (uint)i) = operation.Invoke(Unsafe.Add(ref first, (uint)i));
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
