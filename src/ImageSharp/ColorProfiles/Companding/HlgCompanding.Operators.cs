// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.ColorProfiles.Companding;

/// <content>
/// Provides the shared component formulas and signed scalar and SIMD operators.
/// </content>
public static partial class HlgCompanding
{
    /// <summary>
    /// The HLG logarithmic scale.
    /// </summary>
    private const float A = 0.17883277F;

    /// <summary>
    /// The HLG logarithmic offset.
    /// </summary>
    private const float B = 0.28466892F;

    /// <summary>
    /// The HLG output offset.
    /// </summary>
    private const float C = 0.55991073F;

    /// <summary>
    /// Decodes nonnegative HLG scene components.
    /// </summary>
    /// <param name="value">The nonnegative component magnitude, or independent SIMD component magnitudes.</param>
    /// <returns>The converted component magnitudes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float ExpandMagnitude(float value)
    {
        // The quadratic lower segment meets the logarithmic upper segment at signal 0.5 (scene light 1/12).
        return value <= 0.5F
            ? (value * value) / 3F
            : (MathF.Exp((value - C) / A) + B) / 12F;
    }

    /// <summary>
    /// Decodes nonnegative HLG scene components.
    /// </summary>
    /// <param name="value">The nonnegative component magnitude, or independent SIMD component magnitudes.</param>
    /// <returns>The converted component magnitudes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<float> ExpandMagnitude(Vector128<float> value)
    {
        Vector128<float> linear = Vector128.Divide(Vector128.Multiply(value, value), Vector128.Create(3F));
        Vector128<float> exponent = Vector128.Divide(Vector128.Subtract(value, Vector128.Create(C)), Vector128.Create(A));
        Vector128<float> logarithmic = Vector128.Divide(Vector128.Add(Vector128.Exp(exponent), Vector128.Create(B)), Vector128.Create(12F));
        return Vector128.ConditionalSelect(Vector128.LessThanOrEqual(value, Vector128.Create(0.5F)), linear, logarithmic);
    }

    /// <summary>
    /// Decodes nonnegative HLG scene components.
    /// </summary>
    /// <param name="value">The nonnegative component magnitude, or independent SIMD component magnitudes.</param>
    /// <returns>The converted component magnitudes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector256<float> ExpandMagnitude(Vector256<float> value)
    {
        Vector256<float> linear = Vector256.Divide(Vector256.Multiply(value, value), Vector256.Create(3F));
        Vector256<float> exponent = Vector256.Divide(Vector256.Subtract(value, Vector256.Create(C)), Vector256.Create(A));
        Vector256<float> logarithmic = Vector256.Divide(Vector256.Add(Vector256.Exp(exponent), Vector256.Create(B)), Vector256.Create(12F));
        return Vector256.ConditionalSelect(Vector256.LessThanOrEqual(value, Vector256.Create(0.5F)), linear, logarithmic);
    }

    /// <summary>
    /// Decodes nonnegative HLG scene components.
    /// </summary>
    /// <param name="value">The nonnegative component magnitude, or independent SIMD component magnitudes.</param>
    /// <returns>The converted component magnitudes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector512<float> ExpandMagnitude(Vector512<float> value)
    {
        Vector512<float> linear = Vector512.Divide(Vector512.Multiply(value, value), Vector512.Create(3F));
        Vector512<float> exponent = Vector512.Divide(Vector512.Subtract(value, Vector512.Create(C)), Vector512.Create(A));
        Vector512<float> logarithmic = Vector512.Divide(Vector512.Add(Vector512.Exp(exponent), Vector512.Create(B)), Vector512.Create(12F));
        return Vector512.ConditionalSelect(Vector512.LessThanOrEqual(value, Vector512.Create(0.5F)), linear, logarithmic);
    }

    /// <summary>
    /// Encodes nonnegative HLG scene components.
    /// </summary>
    /// <param name="value">The nonnegative component magnitude, or independent SIMD component magnitudes.</param>
    /// <returns>The converted component magnitudes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float CompressMagnitude(float value)
    {
        return value <= 1F / 12F
            ? MathF.Sqrt(3F * value)
            : (A * MathF.Log((12F * value) - B)) + C;
    }

    /// <summary>
    /// Encodes nonnegative HLG scene components.
    /// </summary>
    /// <param name="value">The nonnegative component magnitude, or independent SIMD component magnitudes.</param>
    /// <returns>The converted component magnitudes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<float> CompressMagnitude(Vector128<float> value)
    {
        Vector128<float> linear = Vector128.Sqrt(Vector128.Multiply(Vector128.Create(3F), value));

        // SIMD evaluates both branches. Keep inactive logarithm lanes positive without changing the selected branch.
        Vector128<float> logarithmInput = Vector128.Max(
            Vector128.Subtract(Vector128.Multiply(Vector128.Create(12F), value), Vector128.Create(B)),
            Vector128.Create(float.Epsilon));

        Vector128<float> logarithmic = Vector128.Add(Vector128.Multiply(Vector128.Create(A), Vector128.Log(logarithmInput)), Vector128.Create(C));
        return Vector128.ConditionalSelect(Vector128.LessThanOrEqual(value, Vector128.Create(1F / 12F)), linear, logarithmic);
    }

    /// <summary>
    /// Encodes nonnegative HLG scene components.
    /// </summary>
    /// <param name="value">The nonnegative component magnitude, or independent SIMD component magnitudes.</param>
    /// <returns>The converted component magnitudes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector256<float> CompressMagnitude(Vector256<float> value)
    {
        Vector256<float> linear = Vector256.Sqrt(Vector256.Multiply(Vector256.Create(3F), value));

        // SIMD evaluates both branches. Keep inactive logarithm lanes positive without changing the selected branch.
        Vector256<float> logarithmInput = Vector256.Max(
            Vector256.Subtract(Vector256.Multiply(Vector256.Create(12F), value), Vector256.Create(B)),
            Vector256.Create(float.Epsilon));

        Vector256<float> logarithmic = Vector256.Add(Vector256.Multiply(Vector256.Create(A), Vector256.Log(logarithmInput)), Vector256.Create(C));
        return Vector256.ConditionalSelect(Vector256.LessThanOrEqual(value, Vector256.Create(1F / 12F)), linear, logarithmic);
    }

    /// <summary>
    /// Encodes nonnegative HLG scene components.
    /// </summary>
    /// <param name="value">The nonnegative component magnitude, or independent SIMD component magnitudes.</param>
    /// <returns>The converted component magnitudes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector512<float> CompressMagnitude(Vector512<float> value)
    {
        Vector512<float> linear = Vector512.Sqrt(Vector512.Multiply(Vector512.Create(3F), value));

        // SIMD evaluates both branches. Keep inactive logarithm lanes positive without changing the selected branch.
        Vector512<float> logarithmInput = Vector512.Max(
            Vector512.Subtract(Vector512.Multiply(Vector512.Create(12F), value), Vector512.Create(B)),
            Vector512.Create(float.Epsilon));

        Vector512<float> logarithmic = Vector512.Add(Vector512.Multiply(Vector512.Create(A), Vector512.Log(logarithmInput)), Vector512.Create(C));
        return Vector512.ConditionalSelect(Vector512.LessThanOrEqual(value, Vector512.Create(1F / 12F)), linear, logarithmic);
    }

    /// <summary>
    /// Decodes signed HLG component magnitudes before final result bounds.
    /// </summary>
    internal readonly struct ExpandOperator : TensorPrimitives_.IUnaryOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float value)
        {
            float magnitude = ExpandMagnitude(MathF.Abs(value));
            return MathF.CopySign(magnitude, value);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
        {
            Vector128<float> magnitude = ExpandMagnitude(Vector128.Abs(value));
            return Vector128.CopySign(magnitude, value);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
        {
            Vector256<float> magnitude = ExpandMagnitude(Vector256.Abs(value));
            return Vector256.CopySign(magnitude, value);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
        {
            Vector512<float> magnitude = ExpandMagnitude(Vector512.Abs(value));
            return Vector512.CopySign(magnitude, value);
        }
    }

    /// <summary>
    /// Encodes signed HLG component magnitudes before final result bounds.
    /// </summary>
    internal readonly struct CompressOperator : TensorPrimitives_.IUnaryOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float value)
        {
            float magnitude = CompressMagnitude(MathF.Abs(value));
            return MathF.CopySign(magnitude, value);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
        {
            Vector128<float> magnitude = CompressMagnitude(Vector128.Abs(value));
            return Vector128.CopySign(magnitude, value);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
        {
            Vector256<float> magnitude = CompressMagnitude(Vector256.Abs(value));
            return Vector256.CopySign(magnitude, value);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
        {
            Vector512<float> magnitude = CompressMagnitude(Vector512.Abs(value));
            return Vector512.CopySign(magnitude, value);
        }
    }
}
