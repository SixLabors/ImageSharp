// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.ColorProfiles.Companding;

/// <summary>
/// Contains the vector-width operations used by the shared companding formulas.
/// </summary>
internal static class CompandingVectorOperations
{
    /// <summary>
    /// Defines the lane-wise operations required by the shared SIMD companding formulas.
    /// </summary>
    /// <typeparam name="TVector">The SIMD vector type.</typeparam>
    public interface ITransferVectorOperations<TVector>
        where TVector : struct
    {
        /// <summary>
        /// Creates a vector whose lanes contain the specified value.
        /// </summary>
        /// <param name="value">The value copied to every lane.</param>
        /// <returns>The created vector.</returns>
        public static abstract TVector Create(float value);

        /// <summary>
        /// Adds corresponding vector lanes.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>The lane-wise sum.</returns>
        public static abstract TVector Add(TVector left, TVector right);

        /// <summary>
        /// Subtracts corresponding vector lanes.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>The lane-wise difference.</returns>
        public static abstract TVector Subtract(TVector left, TVector right);

        /// <summary>
        /// Multiplies corresponding vector lanes.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>The lane-wise product.</returns>
        public static abstract TVector Multiply(TVector left, TVector right);

        /// <summary>
        /// Multiplies corresponding vector lanes and adds an addend using the fastest supported estimate.
        /// </summary>
        /// <param name="left">The left multiplication operand.</param>
        /// <param name="right">The right multiplication operand.</param>
        /// <param name="addend">The value added to the product.</param>
        /// <returns>The lane-wise multiply-add result.</returns>
        public static abstract TVector MultiplyAddEstimate(TVector left, TVector right, TVector addend);

        /// <summary>
        /// Divides corresponding vector lanes.
        /// </summary>
        /// <param name="left">The dividend.</param>
        /// <param name="right">The divisor.</param>
        /// <returns>The lane-wise quotient.</returns>
        public static abstract TVector Divide(TVector left, TVector right);

        /// <summary>
        /// Negates every vector lane.
        /// </summary>
        /// <param name="value">The input vector.</param>
        /// <returns>The negated vector.</returns>
        public static abstract TVector Negate(TVector value);

        /// <summary>
        /// Selects the smaller value in each pair of lanes.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>The lane-wise minimum.</returns>
        public static abstract TVector Min(TVector left, TVector right);

        /// <summary>
        /// Selects the larger value in each pair of lanes.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>The lane-wise maximum.</returns>
        public static abstract TVector Max(TVector left, TVector right);

        /// <summary>
        /// Clamps each lane to the inclusive bounds and maps NaN to the lower bound.
        /// </summary>
        /// <param name="value">The components to clamp.</param>
        /// <param name="min">The inclusive lower bounds.</param>
        /// <param name="max">The inclusive upper bounds.</param>
        /// <returns>The clamped components.</returns>
        public static abstract TVector Clamp(TVector value, TVector min, TVector max);

        /// <summary>
        /// Compares whether each left lane is less than its right lane.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>The comparison mask.</returns>
        public static abstract TVector LessThan(TVector left, TVector right);

        /// <summary>
        /// Compares whether each left lane is less than or equal to its right lane.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>The comparison mask.</returns>
        public static abstract TVector LessThanOrEqual(TVector left, TVector right);

        /// <summary>
        /// Selects lanes from two vectors according to a comparison mask.
        /// </summary>
        /// <param name="condition">The comparison mask.</param>
        /// <param name="left">The value selected for set mask lanes.</param>
        /// <param name="right">The value selected for clear mask lanes.</param>
        /// <returns>The selected lanes.</returns>
        public static abstract TVector ConditionalSelect(TVector condition, TVector left, TVector right);

        /// <summary>
        /// Computes the natural exponential of every vector lane.
        /// </summary>
        /// <param name="value">The input vector.</param>
        /// <returns>The lane-wise exponential.</returns>
        public static abstract TVector Exp(TVector value);

        /// <summary>
        /// Computes the natural logarithm of every vector lane.
        /// </summary>
        /// <param name="value">The input vector.</param>
        /// <returns>The lane-wise logarithm.</returns>
        public static abstract TVector Log(TVector value);

        /// <summary>
        /// Computes the square root of every vector lane.
        /// </summary>
        /// <param name="value">The input vector.</param>
        /// <returns>The lane-wise square root.</returns>
        public static abstract TVector Sqrt(TVector value);

        /// <summary>
        /// Takes the magnitude of each component.
        /// </summary>
        /// <param name="value">The signed components.</param>
        /// <returns>The component magnitudes.</returns>
        public static abstract TVector Abs(TVector value);

        /// <summary>
        /// Restores each component's original sign.
        /// </summary>
        /// <param name="value">The result magnitudes.</param>
        /// <param name="sign">The original signed components.</param>
        /// <returns>The signed results.</returns>
        public static abstract TVector CopySign(TVector value, TVector sign);

        /// <summary>
        /// Raises nonnegative components to a fixed positive power.
        /// </summary>
        /// <param name="value">The nonnegative bases.</param>
        /// <param name="exponent">The positive exponent selected by the transfer formula.</param>
        /// <returns>The component powers.</returns>
        public static abstract TVector Pow(TVector value, float exponent);
    }

    /// <summary>
    /// Maps the shared transfer-function formulas to 128-bit vector operations.
    /// </summary>
    public readonly struct Vector128Operations : ITransferVectorOperations<Vector128<float>>
    {
        /// <inheritdoc/>
        public static Vector128<float> Create(float value) => Vector128.Create(value);

        /// <inheritdoc/>
        public static Vector128<float> Add(Vector128<float> left, Vector128<float> right) => left + right;

        /// <inheritdoc/>
        public static Vector128<float> Subtract(Vector128<float> left, Vector128<float> right) => left - right;

        /// <inheritdoc/>
        public static Vector128<float> Multiply(Vector128<float> left, Vector128<float> right) => left * right;

        /// <inheritdoc/>
        public static Vector128<float> MultiplyAddEstimate(Vector128<float> left, Vector128<float> right, Vector128<float> addend)
            => Vector128.MultiplyAddEstimate(left, right, addend);

        /// <inheritdoc/>
        public static Vector128<float> Divide(Vector128<float> left, Vector128<float> right) => left / right;

        /// <inheritdoc/>
        public static Vector128<float> Negate(Vector128<float> value) => -value;

        /// <inheritdoc/>
        public static Vector128<float> Min(Vector128<float> left, Vector128<float> right) => Vector128.Min(left, right);

        /// <inheritdoc/>
        public static Vector128<float> Max(Vector128<float> left, Vector128<float> right) => Vector128.Max(left, right);

        /// <inheritdoc/>
        public static Vector128<float> Clamp(Vector128<float> value, Vector128<float> min, Vector128<float> max)
            => Numerics.ClampNumber(value, min, max);

        /// <inheritdoc/>
        public static Vector128<float> LessThan(Vector128<float> left, Vector128<float> right) => Vector128.LessThan(left, right);

        /// <inheritdoc/>
        public static Vector128<float> LessThanOrEqual(Vector128<float> left, Vector128<float> right) => Vector128.LessThanOrEqual(left, right);

        /// <inheritdoc/>
        public static Vector128<float> ConditionalSelect(Vector128<float> condition, Vector128<float> left, Vector128<float> right)
            => Vector128.ConditionalSelect(condition, left, right);

        /// <inheritdoc/>
        public static Vector128<float> Exp(Vector128<float> value) => Vector128.Exp(value);

        /// <inheritdoc/>
        public static Vector128<float> Log(Vector128<float> value) => Vector128.Log(value);

        /// <inheritdoc/>
        public static Vector128<float> Sqrt(Vector128<float> value) => Vector128.Sqrt(value);

        /// <inheritdoc/>
        public static Vector128<float> Abs(Vector128<float> value) => Vector128.Abs(value);

        /// <inheritdoc/>
        public static Vector128<float> CopySign(Vector128<float> value, Vector128<float> sign) => Vector128.CopySign(value, sign);

        /// <inheritdoc/>
        public static Vector128<float> Pow(Vector128<float> value, float exponent) => Vector128_.Pow(value, exponent);
    }

    /// <summary>
    /// Maps the shared transfer-function formulas to 256-bit vector operations.
    /// </summary>
    public readonly struct Vector256Operations : ITransferVectorOperations<Vector256<float>>
    {
        /// <inheritdoc/>
        public static Vector256<float> Create(float value) => Vector256.Create(value);

        /// <inheritdoc/>
        public static Vector256<float> Add(Vector256<float> left, Vector256<float> right) => left + right;

        /// <inheritdoc/>
        public static Vector256<float> Subtract(Vector256<float> left, Vector256<float> right) => left - right;

        /// <inheritdoc/>
        public static Vector256<float> Multiply(Vector256<float> left, Vector256<float> right) => left * right;

        /// <inheritdoc/>
        public static Vector256<float> MultiplyAddEstimate(Vector256<float> left, Vector256<float> right, Vector256<float> addend)
            => Vector256.MultiplyAddEstimate(left, right, addend);

        /// <inheritdoc/>
        public static Vector256<float> Divide(Vector256<float> left, Vector256<float> right) => left / right;

        /// <inheritdoc/>
        public static Vector256<float> Negate(Vector256<float> value) => -value;

        /// <inheritdoc/>
        public static Vector256<float> Min(Vector256<float> left, Vector256<float> right) => Vector256.Min(left, right);

        /// <inheritdoc/>
        public static Vector256<float> Max(Vector256<float> left, Vector256<float> right) => Vector256.Max(left, right);

        /// <inheritdoc/>
        public static Vector256<float> Clamp(Vector256<float> value, Vector256<float> min, Vector256<float> max)
            => Numerics.ClampNumber(value, min, max);

        /// <inheritdoc/>
        public static Vector256<float> LessThan(Vector256<float> left, Vector256<float> right) => Vector256.LessThan(left, right);

        /// <inheritdoc/>
        public static Vector256<float> LessThanOrEqual(Vector256<float> left, Vector256<float> right) => Vector256.LessThanOrEqual(left, right);

        /// <inheritdoc/>
        public static Vector256<float> ConditionalSelect(Vector256<float> condition, Vector256<float> left, Vector256<float> right)
            => Vector256.ConditionalSelect(condition, left, right);

        /// <inheritdoc/>
        public static Vector256<float> Exp(Vector256<float> value) => Vector256.Exp(value);

        /// <inheritdoc/>
        public static Vector256<float> Log(Vector256<float> value) => Vector256.Log(value);

        /// <inheritdoc/>
        public static Vector256<float> Sqrt(Vector256<float> value) => Vector256.Sqrt(value);

        /// <inheritdoc/>
        public static Vector256<float> Abs(Vector256<float> value) => Vector256.Abs(value);

        /// <inheritdoc/>
        public static Vector256<float> CopySign(Vector256<float> value, Vector256<float> sign) => Vector256.CopySign(value, sign);

        /// <inheritdoc/>
        public static Vector256<float> Pow(Vector256<float> value, float exponent) => Vector256_.Pow(value, exponent);
    }

    /// <summary>
    /// Maps the shared transfer-function formulas to 512-bit vector operations.
    /// </summary>
    public readonly struct Vector512Operations : ITransferVectorOperations<Vector512<float>>
    {
        /// <inheritdoc/>
        public static Vector512<float> Create(float value) => Vector512.Create(value);

        /// <inheritdoc/>
        public static Vector512<float> Add(Vector512<float> left, Vector512<float> right) => left + right;

        /// <inheritdoc/>
        public static Vector512<float> Subtract(Vector512<float> left, Vector512<float> right) => left - right;

        /// <inheritdoc/>
        public static Vector512<float> Multiply(Vector512<float> left, Vector512<float> right) => left * right;

        /// <inheritdoc/>
        public static Vector512<float> MultiplyAddEstimate(Vector512<float> left, Vector512<float> right, Vector512<float> addend)
            => Vector512.MultiplyAddEstimate(left, right, addend);

        /// <inheritdoc/>
        public static Vector512<float> Divide(Vector512<float> left, Vector512<float> right) => left / right;

        /// <inheritdoc/>
        public static Vector512<float> Negate(Vector512<float> value) => -value;

        /// <inheritdoc/>
        public static Vector512<float> Min(Vector512<float> left, Vector512<float> right) => Vector512.Min(left, right);

        /// <inheritdoc/>
        public static Vector512<float> Max(Vector512<float> left, Vector512<float> right) => Vector512.Max(left, right);

        /// <inheritdoc/>
        public static Vector512<float> Clamp(Vector512<float> value, Vector512<float> min, Vector512<float> max)
            => Numerics.ClampNumber(value, min, max);

        /// <inheritdoc/>
        public static Vector512<float> LessThan(Vector512<float> left, Vector512<float> right) => Vector512.LessThan(left, right);

        /// <inheritdoc/>
        public static Vector512<float> LessThanOrEqual(Vector512<float> left, Vector512<float> right) => Vector512.LessThanOrEqual(left, right);

        /// <inheritdoc/>
        public static Vector512<float> ConditionalSelect(Vector512<float> condition, Vector512<float> left, Vector512<float> right)
            => Vector512.ConditionalSelect(condition, left, right);

        /// <inheritdoc/>
        public static Vector512<float> Exp(Vector512<float> value) => Vector512.Exp(value);

        /// <inheritdoc/>
        public static Vector512<float> Log(Vector512<float> value) => Vector512.Log(value);

        /// <inheritdoc/>
        public static Vector512<float> Sqrt(Vector512<float> value) => Vector512.Sqrt(value);

        /// <inheritdoc/>
        public static Vector512<float> Abs(Vector512<float> value) => Vector512.Abs(value);

        /// <inheritdoc/>
        public static Vector512<float> CopySign(Vector512<float> value, Vector512<float> sign) => Vector512.CopySign(value, sign);

        /// <inheritdoc/>
        public static Vector512<float> Pow(Vector512<float> value, float exponent) => Vector512_.Pow(value, exponent);
    }
}
