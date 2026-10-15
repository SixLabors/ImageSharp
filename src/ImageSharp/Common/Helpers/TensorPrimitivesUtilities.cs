// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
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
    public interface IUnaryOperator
    {
        /// <summary>
        /// Gets a value indicating whether each fourth component is alpha and must remain unchanged.
        /// </summary>
        public static abstract bool PreserveAlpha { get; }

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
    /// <typeparam name="TOperator">The operation configured before traversal.</typeparam>
    /// <param name="values">The components. Operators that preserve alpha require complete RGBA groups.</param>
    /// <param name="operation">The scalar and SIMD forms of the operation.</param>
    public static void Invoke<TOperator>(Span<float> values, TOperator operation)
        where TOperator : struct, IUnaryOperator
    {
        ref float first = ref MemoryMarshal.GetReference(values);
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = Numerics.Vector512Count<float>(values.Length - i);
            Vector512<float> alphaMask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();

            // Each lane holds one component. RGBA operators restore lanes 3, 7, ... from the input.
            // The closed operator type removes this selection entirely for operations without alpha.
            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                Vector512<float> input = Vector512.LoadUnsafe(ref first, (uint)i);
                Vector512<float> output = operation.Invoke(input);
                if (TOperator.PreserveAlpha)
                {
                    output = Vector512.ConditionalSelect(alphaMask, input, output);
                }

                output.StoreUnsafe(ref first, (uint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = Numerics.Vector256Count<float>(values.Length - i);
            Vector256<float> alphaMask = Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle();

            // Each lane holds one component. RGBA operators restore lanes 3, 7, ... from the input.
            // The closed operator type removes this selection entirely for operations without alpha.
            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                Vector256<float> input = Vector256.LoadUnsafe(ref first, (uint)i);
                Vector256<float> output = operation.Invoke(input);
                if (TOperator.PreserveAlpha)
                {
                    output = Vector256.ConditionalSelect(alphaMask, input, output);
                }

                output.StoreUnsafe(ref first, (uint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = Numerics.Vector128Count<float>(values.Length - i);
            Vector128<float> alphaMask = Vector128.Create(0, 0, 0, -1).AsSingle();

            // Each lane holds one component. RGBA operators restore lanes 3, 7, ... from the input.
            // The closed operator type removes this selection entirely for operations without alpha.
            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                Vector128<float> input = Vector128.LoadUnsafe(ref first, (uint)i);
                Vector128<float> output = operation.Invoke(input);
                if (TOperator.PreserveAlpha)
                {
                    output = Vector128.ConditionalSelect(alphaMask, input, output);
                }

                output.StoreUnsafe(ref first, (uint)i);
            }
        }

        // Complete registers never overlap. RGBA input ends on a four-component boundary,
        // so the scalar fallback can transform RGB directly without an alpha test per component.
        if (TOperator.PreserveAlpha)
        {
            for (; i < values.Length; i += 4)
            {
                Unsafe.Add(ref first, (uint)i) = operation.Invoke(Unsafe.Add(ref first, (uint)i));
                Unsafe.Add(ref first, (uint)i + 1) = operation.Invoke(Unsafe.Add(ref first, (uint)i + 1));
                Unsafe.Add(ref first, (uint)i + 2) = operation.Invoke(Unsafe.Add(ref first, (uint)i + 2));
            }
        }
        else
        {
            for (; i < values.Length; i++)
            {
                Unsafe.Add(ref first, (uint)i) = operation.Invoke(Unsafe.Add(ref first, (uint)i));
            }
        }
    }

    /// <summary>
    /// Transforms RGB in place and retains each color's alpha.
    /// </summary>
    /// <typeparam name="TOperator">The companding operation.</typeparam>
    /// <param name="values">The colors to transform.</param>
    /// <param name="operation">The configured transfer curve.</param>
    public static void Invoke<TOperator>(Span<Vector4> values, TOperator operation)
        where TOperator : struct, IUnaryOperator
        => Invoke(MemoryMarshal.Cast<Vector4, float>(values), operation);

    /// <summary>
    /// Transforms one color with the same operator as bulk conversion.
    /// </summary>
    /// <typeparam name="TOperator">The component operation.</typeparam>
    /// <param name="value">The input color.</param>
    /// <param name="operation">The configured operation.</param>
    /// <returns>The transformed color, with alpha retained when the operator requires it.</returns>
    public static Vector4 Invoke<TOperator>(Vector4 value, TOperator operation)
        where TOperator : struct, IUnaryOperator
    {
        if (Vector128.IsHardwareAccelerated)
        {
            Vector4 result = operation.Invoke(value.AsVector128()).AsVector4();
            if (TOperator.PreserveAlpha)
            {
                result.W = value.W;
            }

            return result;
        }

        return new Vector4(
            operation.Invoke(value.X),
            operation.Invoke(value.Y),
            operation.Invoke(value.Z),
            TOperator.PreserveAlpha ? value.W : operation.Invoke(value.W));
    }
}
