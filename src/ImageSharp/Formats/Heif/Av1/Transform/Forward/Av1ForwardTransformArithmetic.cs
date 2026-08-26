// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <summary>
/// Provides the sample-type and vector-width arithmetic used by the shared AV1 forward-transform stage networks.
/// </summary>
/// <typeparam name="TValue">The scalar or SIMD value containing independent transform axes.</typeparam>
internal static class Av1ForwardTransformArithmetic<TValue>
    where TValue : struct
{
    /// <summary>
    /// Creates the rounding value used by fixed-point transform multiplications.
    /// </summary>
    /// <param name="cosBit">The number of fractional bits in the transform constants.</param>
    /// <returns>The rounding value in the widened lane shape used by <typeparamref name="TValue"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Av1TransformRounding CreateRounding(int cosBit)
    {
        int value = 1 << (cosBit - 1);
        Av1TransformRounding rounding = default;

        if (typeof(TValue) == typeof(Vector128<short>) || typeof(TValue) == typeof(Vector128<int>))
        {
            rounding.Vector128 = Vector128.Create(value);
        }
        else if (typeof(TValue) == typeof(Vector256<short>) || typeof(TValue) == typeof(Vector256<int>))
        {
            rounding.Vector256 = Vector256.Create(value);
        }
        else if (typeof(TValue) == typeof(Vector512<short>) || typeof(TValue) == typeof(Vector512<int>))
        {
            rounding.Vector512 = Vector512.Create(value);
        }
        else
        {
            rounding.Scalar = value;
        }

        return rounding;
    }

    /// <summary>
    /// Adds two transform values using the lane arithmetic required by the selected sample type.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The lane-wise sum.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TValue Add(TValue left, TValue right)
    {
        if (typeof(TValue) == typeof(short))
        {
            int resultValue = As<TValue, short>(left) + As<TValue, short>(right);
            short result = (short)Math.Clamp(resultValue, short.MinValue, short.MaxValue);
            return As<short, TValue>(result);
        }

        if (typeof(TValue) == typeof(int))
        {
            int result = As<TValue, int>(left) + As<TValue, int>(right);
            return As<int, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector128<short>))
        {
            Vector128<short> result = Vector128.AddSaturate(As<TValue, Vector128<short>>(left), As<TValue, Vector128<short>>(right));
            return As<Vector128<short>, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector128<int>))
        {
            Vector128<int> result = As<TValue, Vector128<int>>(left) + As<TValue, Vector128<int>>(right);
            return As<Vector128<int>, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector256<short>))
        {
            Vector256<short> result = Vector256.AddSaturate(As<TValue, Vector256<short>>(left), As<TValue, Vector256<short>>(right));
            return As<Vector256<short>, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector256<int>))
        {
            Vector256<int> result = As<TValue, Vector256<int>>(left) + As<TValue, Vector256<int>>(right);
            return As<Vector256<int>, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector512<short>))
        {
            Vector512<short> result = Vector512.AddSaturate(As<TValue, Vector512<short>>(left), As<TValue, Vector512<short>>(right));
            return As<Vector512<short>, TValue>(result);
        }

        Vector512<int> vector = As<TValue, Vector512<int>>(left) + As<TValue, Vector512<int>>(right);
        return As<Vector512<int>, TValue>(vector);
    }

    /// <summary>
    /// Subtracts one transform value from another using the lane arithmetic required by the selected sample type.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The lane-wise difference.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TValue Subtract(TValue left, TValue right)
    {
        if (typeof(TValue) == typeof(short))
        {
            int resultValue = As<TValue, short>(left) - As<TValue, short>(right);
            short result = (short)Math.Clamp(resultValue, short.MinValue, short.MaxValue);
            return As<short, TValue>(result);
        }

        if (typeof(TValue) == typeof(int))
        {
            int result = As<TValue, int>(left) - As<TValue, int>(right);
            return As<int, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector128<short>))
        {
            Vector128<short> result = Vector128.SubtractSaturate(As<TValue, Vector128<short>>(left), As<TValue, Vector128<short>>(right));
            return As<Vector128<short>, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector128<int>))
        {
            Vector128<int> result = As<TValue, Vector128<int>>(left) - As<TValue, Vector128<int>>(right);
            return As<Vector128<int>, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector256<short>))
        {
            Vector256<short> result = Vector256.SubtractSaturate(As<TValue, Vector256<short>>(left), As<TValue, Vector256<short>>(right));
            return As<Vector256<short>, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector256<int>))
        {
            Vector256<int> result = As<TValue, Vector256<int>>(left) - As<TValue, Vector256<int>>(right);
            return As<Vector256<int>, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector512<short>))
        {
            Vector512<short> result = Vector512.SubtractSaturate(As<TValue, Vector512<short>>(left), As<TValue, Vector512<short>>(right));
            return As<Vector512<short>, TValue>(result);
        }

        Vector512<int> vector = As<TValue, Vector512<int>>(left) - As<TValue, Vector512<int>>(right);
        return As<Vector512<int>, TValue>(vector);
    }

    /// <summary>
    /// Negates a transform value using wrapping lane arithmetic.
    /// </summary>
    /// <param name="value">The value to negate.</param>
    /// <returns>The lane-wise negated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TValue Negate(TValue value)
    {
        if (typeof(TValue) == typeof(short))
        {
            short result = unchecked((short)-As<TValue, short>(value));
            return As<short, TValue>(result);
        }

        if (typeof(TValue) == typeof(int))
        {
            int result = -As<TValue, int>(value);
            return As<int, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector128<short>))
        {
            Vector128<short> result = Vector128<short>.Zero - As<TValue, Vector128<short>>(value);
            return As<Vector128<short>, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector128<int>))
        {
            Vector128<int> result = -As<TValue, Vector128<int>>(value);
            return As<Vector128<int>, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector256<short>))
        {
            Vector256<short> result = Vector256<short>.Zero - As<TValue, Vector256<short>>(value);
            return As<Vector256<short>, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector256<int>))
        {
            Vector256<int> result = -As<TValue, Vector256<int>>(value);
            return As<Vector256<int>, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector512<short>))
        {
            Vector512<short> result = Vector512<short>.Zero - As<TValue, Vector512<short>>(value);
            return As<Vector512<short>, TValue>(result);
        }

        Vector512<int> vector = -As<TValue, Vector512<int>>(value);
        return As<Vector512<int>, TValue>(vector);
    }

    /// <summary>
    /// Adds and subtracts two transform values, saturating only the signed sixteen-bit representations.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <param name="sum">The lane-wise sum.</param>
    /// <param name="difference">The lane-wise difference.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddSubtract(TValue left, TValue right, out TValue sum, out TValue difference)
    {
        if (typeof(TValue) == typeof(short))
        {
            int leftValue = As<TValue, short>(left);
            int rightValue = As<TValue, short>(right);
            short sumValue = (short)Math.Clamp(leftValue + rightValue, short.MinValue, short.MaxValue);
            short differenceValue = (short)Math.Clamp(leftValue - rightValue, short.MinValue, short.MaxValue);

            sum = As<short, TValue>(sumValue);
            difference = As<short, TValue>(differenceValue);
            return;
        }

        if (typeof(TValue) == typeof(int))
        {
            int leftValue = As<TValue, int>(left);
            int rightValue = As<TValue, int>(right);
            int sumValue = leftValue + rightValue;
            int differenceValue = leftValue - rightValue;

            sum = As<int, TValue>(sumValue);
            difference = As<int, TValue>(differenceValue);
            return;
        }

        if (typeof(TValue) == typeof(Vector128<short>))
        {
            Vector128<short> leftValue = As<TValue, Vector128<short>>(left);
            Vector128<short> rightValue = As<TValue, Vector128<short>>(right);
            Vector128<short> sumValue = Vector128.AddSaturate(leftValue, rightValue);
            Vector128<short> differenceValue = Vector128.SubtractSaturate(leftValue, rightValue);

            sum = As<Vector128<short>, TValue>(sumValue);
            difference = As<Vector128<short>, TValue>(differenceValue);
            return;
        }

        if (typeof(TValue) == typeof(Vector128<int>))
        {
            Vector128<int> leftValue = As<TValue, Vector128<int>>(left);
            Vector128<int> rightValue = As<TValue, Vector128<int>>(right);

            sum = As<Vector128<int>, TValue>(leftValue + rightValue);
            difference = As<Vector128<int>, TValue>(leftValue - rightValue);
            return;
        }

        if (typeof(TValue) == typeof(Vector256<short>))
        {
            Vector256<short> leftValue = As<TValue, Vector256<short>>(left);
            Vector256<short> rightValue = As<TValue, Vector256<short>>(right);
            Vector256<short> sumValue = Vector256.AddSaturate(leftValue, rightValue);
            Vector256<short> differenceValue = Vector256.SubtractSaturate(leftValue, rightValue);

            sum = As<Vector256<short>, TValue>(sumValue);
            difference = As<Vector256<short>, TValue>(differenceValue);
            return;
        }

        if (typeof(TValue) == typeof(Vector256<int>))
        {
            Vector256<int> leftValue = As<TValue, Vector256<int>>(left);
            Vector256<int> rightValue = As<TValue, Vector256<int>>(right);

            sum = As<Vector256<int>, TValue>(leftValue + rightValue);
            difference = As<Vector256<int>, TValue>(leftValue - rightValue);
            return;
        }

        if (typeof(TValue) == typeof(Vector512<short>))
        {
            Vector512<short> leftValue = As<TValue, Vector512<short>>(left);
            Vector512<short> rightValue = As<TValue, Vector512<short>>(right);
            Vector512<short> sumValue = Vector512.AddSaturate(leftValue, rightValue);
            Vector512<short> differenceValue = Vector512.SubtractSaturate(leftValue, rightValue);

            sum = As<Vector512<short>, TValue>(sumValue);
            difference = As<Vector512<short>, TValue>(differenceValue);
            return;
        }

        Vector512<int> leftVector = As<TValue, Vector512<int>>(left);
        Vector512<int> rightVector = As<TValue, Vector512<int>>(right);

        sum = As<Vector512<int>, TValue>(leftVector + rightVector);
        difference = As<Vector512<int>, TValue>(leftVector - rightVector);
    }

    /// <summary>
    /// Calculates both outputs of a rounded, weighted two-input butterfly.
    /// </summary>
    /// <param name="weight0">The first fixed-point weight.</param>
    /// <param name="weight1">The second fixed-point weight.</param>
    /// <param name="input0">The first transform value.</param>
    /// <param name="input1">The second transform value.</param>
    /// <param name="output0">The first rounded result.</param>
    /// <param name="output1">The second rounded result.</param>
    /// <param name="cosBit">The number of fractional bits in each weight.</param>
    /// <param name="rounding">The rounding value created for this transform.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Butterfly(
        int weight0,
        int weight1,
        TValue input0,
        TValue input1,
        out TValue output0,
        out TValue output1,
        int cosBit,
        in Av1TransformRounding rounding)
    {
        if (typeof(TValue) == typeof(Vector128<short>))
        {
            Vector128<short> left = As<TValue, Vector128<short>>(input0);
            Vector128<short> right = As<TValue, Vector128<short>>(input1);

            Av1Transform1dMath.Butterfly(weight0, weight1, in left, in right, out Vector128<short> result0, out Vector128<short> result1, cosBit, in rounding.Vector128);
            output0 = As<Vector128<short>, TValue>(result0);
            output1 = As<Vector128<short>, TValue>(result1);
            return;
        }

        if (typeof(TValue) == typeof(Vector256<short>))
        {
            Vector256<short> left = As<TValue, Vector256<short>>(input0);
            Vector256<short> right = As<TValue, Vector256<short>>(input1);

            Av1Transform1dMath.Butterfly(weight0, weight1, in left, in right, out Vector256<short> result0, out Vector256<short> result1, cosBit, in rounding.Vector256);
            output0 = As<Vector256<short>, TValue>(result0);
            output1 = As<Vector256<short>, TValue>(result1);
            return;
        }

        if (typeof(TValue) == typeof(Vector512<short>))
        {
            Vector512<short> left = As<TValue, Vector512<short>>(input0);
            Vector512<short> right = As<TValue, Vector512<short>>(input1);

            Av1Transform1dMath.Butterfly(weight0, weight1, in left, in right, out Vector512<short> result0, out Vector512<short> result1, cosBit, in rounding.Vector512);
            output0 = As<Vector512<short>, TValue>(result0);
            output1 = As<Vector512<short>, TValue>(result1);
            return;
        }

        output0 = HalfButterfly(weight0, input0, weight1, input1, cosBit, in rounding);
        output1 = HalfButterfly(weight1, input0, -weight0, input1, cosBit, in rounding);
    }

    /// <summary>
    /// Calculates one output of a rounded, weighted two-input butterfly.
    /// </summary>
    /// <param name="weight0">The first fixed-point weight.</param>
    /// <param name="input0">The first transform value.</param>
    /// <param name="weight1">The second fixed-point weight.</param>
    /// <param name="input1">The second transform value.</param>
    /// <param name="cosBit">The number of fractional bits in each weight.</param>
    /// <param name="rounding">The rounding value created for this transform.</param>
    /// <returns>The rounded lane-wise weighted sum.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TValue HalfButterfly(
        int weight0,
        TValue input0,
        int weight1,
        TValue input1,
        int cosBit,
        in Av1TransformRounding rounding)
    {
        if (typeof(TValue) == typeof(short))
        {
            int weighted = (weight0 * As<TValue, short>(input0)) + (weight1 * As<TValue, short>(input1));
            short result = (short)Math.Clamp((weighted + rounding.Scalar) >> cosBit, short.MinValue, short.MaxValue);
            return As<short, TValue>(result);
        }

        if (typeof(TValue) == typeof(int))
        {
            int weighted = (weight0 * As<TValue, int>(input0)) + (weight1 * As<TValue, int>(input1));
            int result = (weighted + rounding.Scalar) >> cosBit;
            return As<int, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector128<short>))
        {
            Vector128<short> result = MultiplyRound(
                As<TValue, Vector128<short>>(input0),
                weight0,
                As<TValue, Vector128<short>>(input1),
                weight1,
                cosBit,
                rounding.Vector128);

            return As<Vector128<short>, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector128<int>))
        {
            Vector128<int> result = ((As<TValue, Vector128<int>>(input0) * Vector128.Create(weight0))
                + (As<TValue, Vector128<int>>(input1) * Vector128.Create(weight1))
                + rounding.Vector128) >> cosBit;

            return As<Vector128<int>, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector256<short>))
        {
            Vector256<short> result = MultiplyRound(
                As<TValue, Vector256<short>>(input0),
                weight0,
                As<TValue, Vector256<short>>(input1),
                weight1,
                cosBit,
                rounding.Vector256);

            return As<Vector256<short>, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector256<int>))
        {
            Vector256<int> result = ((As<TValue, Vector256<int>>(input0) * Vector256.Create(weight0))
                + (As<TValue, Vector256<int>>(input1) * Vector256.Create(weight1))
                + rounding.Vector256) >> cosBit;

            return As<Vector256<int>, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector512<short>))
        {
            Vector512<short> result = MultiplyRound(
                As<TValue, Vector512<short>>(input0),
                weight0,
                As<TValue, Vector512<short>>(input1),
                weight1,
                cosBit,
                rounding.Vector512);

            return As<Vector512<short>, TValue>(result);
        }

        Vector512<int> vector = ((As<TValue, Vector512<int>>(input0) * Vector512.Create(weight0))
            + (As<TValue, Vector512<int>>(input1) * Vector512.Create(weight1))
            + rounding.Vector512) >> cosBit;

        return As<Vector512<int>, TValue>(vector);
    }

    /// <summary>
    /// Multiplies a transform value by a fixed-point constant and applies the requested rounding shift.
    /// </summary>
    /// <param name="value">The transform value.</param>
    /// <param name="multiplier">The fixed-point multiplier.</param>
    /// <param name="shift">The number of fractional bits in the multiplier.</param>
    /// <returns>The rounded lane-wise product.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TValue MultiplyRound(TValue value, int multiplier, int shift)
    {
        Av1TransformRounding rounding = CreateRounding(shift);
        return HalfButterfly(multiplier, value, 0, default, shift, in rounding);
    }

    /// <summary>
    /// Shifts each transform lane left without saturation.
    /// </summary>
    /// <param name="value">The transform value.</param>
    /// <param name="count">The shift count.</param>
    /// <returns>The shifted lane values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TValue ShiftLeft(TValue value, int count)
    {
        if (typeof(TValue) == typeof(short))
        {
            short result = unchecked((short)(As<TValue, short>(value) << count));
            return As<short, TValue>(result);
        }

        if (typeof(TValue) == typeof(int))
        {
            int result = As<TValue, int>(value) << count;
            return As<int, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector128<short>))
        {
            Vector128<short> result = As<TValue, Vector128<short>>(value) << count;
            return As<Vector128<short>, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector128<int>))
        {
            Vector128<int> result = As<TValue, Vector128<int>>(value) << count;
            return As<Vector128<int>, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector256<short>))
        {
            Vector256<short> result = As<TValue, Vector256<short>>(value) << count;
            return As<Vector256<short>, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector256<int>))
        {
            Vector256<int> result = As<TValue, Vector256<int>>(value) << count;
            return As<Vector256<int>, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector512<short>))
        {
            Vector512<short> result = As<TValue, Vector512<short>>(value) << count;
            return As<Vector512<short>, TValue>(result);
        }

        Vector512<int> vector = As<TValue, Vector512<int>>(value) << count;
        return As<Vector512<int>, TValue>(vector);
    }

    /// <summary>
    /// Calculates one rounded sum containing four independently weighted transform values.
    /// </summary>
    /// <param name="weight0">The first fixed-point weight.</param>
    /// <param name="input0">The first transform value.</param>
    /// <param name="weight1">The second fixed-point weight.</param>
    /// <param name="input1">The second transform value.</param>
    /// <param name="weight2">The third fixed-point weight.</param>
    /// <param name="input2">The third transform value.</param>
    /// <param name="weight3">The fourth fixed-point weight.</param>
    /// <param name="input3">The fourth transform value.</param>
    /// <param name="cosBit">The number of fractional bits in each weight.</param>
    /// <param name="rounding">The rounding value created for this transform.</param>
    /// <returns>The rounded lane-wise weighted sum.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TValue MultiplyAddRound(
        int weight0,
        TValue input0,
        int weight1,
        TValue input1,
        int weight2,
        TValue input2,
        int weight3,
        TValue input3,
        int cosBit,
        in Av1TransformRounding rounding)
    {
        if (typeof(TValue) == typeof(short))
        {
            int weighted = (weight0 * As<TValue, short>(input0))
                + (weight1 * As<TValue, short>(input1))
                + (weight2 * As<TValue, short>(input2))
                + (weight3 * As<TValue, short>(input3));

            short result = (short)Math.Clamp((weighted + rounding.Scalar) >> cosBit, short.MinValue, short.MaxValue);
            return As<short, TValue>(result);
        }

        if (typeof(TValue) == typeof(int))
        {
            int weighted = (weight0 * As<TValue, int>(input0))
                + (weight1 * As<TValue, int>(input1))
                + (weight2 * As<TValue, int>(input2))
                + (weight3 * As<TValue, int>(input3));

            int result = (weighted + rounding.Scalar) >> cosBit;
            return As<int, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector128<short>))
        {
            Vector128<short> result = MultiplyRound(
                As<TValue, Vector128<short>>(input0),
                weight0,
                As<TValue, Vector128<short>>(input1),
                weight1,
                As<TValue, Vector128<short>>(input2),
                weight2,
                As<TValue, Vector128<short>>(input3),
                weight3,
                cosBit,
                rounding.Vector128);

            return As<Vector128<short>, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector128<int>))
        {
            Vector128<int> result = ((As<TValue, Vector128<int>>(input0) * Vector128.Create(weight0))
                + (As<TValue, Vector128<int>>(input1) * Vector128.Create(weight1))
                + (As<TValue, Vector128<int>>(input2) * Vector128.Create(weight2))
                + (As<TValue, Vector128<int>>(input3) * Vector128.Create(weight3))
                + rounding.Vector128) >> cosBit;

            return As<Vector128<int>, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector256<short>))
        {
            Vector256<short> result = MultiplyRound(
                As<TValue, Vector256<short>>(input0),
                weight0,
                As<TValue, Vector256<short>>(input1),
                weight1,
                As<TValue, Vector256<short>>(input2),
                weight2,
                As<TValue, Vector256<short>>(input3),
                weight3,
                cosBit,
                rounding.Vector256);

            return As<Vector256<short>, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector256<int>))
        {
            Vector256<int> result = ((As<TValue, Vector256<int>>(input0) * Vector256.Create(weight0))
                + (As<TValue, Vector256<int>>(input1) * Vector256.Create(weight1))
                + (As<TValue, Vector256<int>>(input2) * Vector256.Create(weight2))
                + (As<TValue, Vector256<int>>(input3) * Vector256.Create(weight3))
                + rounding.Vector256) >> cosBit;

            return As<Vector256<int>, TValue>(result);
        }

        if (typeof(TValue) == typeof(Vector512<short>))
        {
            Vector512<short> result = MultiplyRound(
                As<TValue, Vector512<short>>(input0),
                weight0,
                As<TValue, Vector512<short>>(input1),
                weight1,
                As<TValue, Vector512<short>>(input2),
                weight2,
                As<TValue, Vector512<short>>(input3),
                weight3,
                cosBit,
                rounding.Vector512);

            return As<Vector512<short>, TValue>(result);
        }

        Vector512<int> vector = ((As<TValue, Vector512<int>>(input0) * Vector512.Create(weight0))
            + (As<TValue, Vector512<int>>(input1) * Vector512.Create(weight1))
            + (As<TValue, Vector512<int>>(input2) * Vector512.Create(weight2))
            + (As<TValue, Vector512<int>>(input3) * Vector512.Create(weight3))
            + rounding.Vector512) >> cosBit;

        return As<Vector512<int>, TValue>(vector);
    }

    /// <summary>
    /// Converts one value type to another equal-sized value type without changing its bits.
    /// </summary>
    /// <typeparam name="TFrom">The source value type.</typeparam>
    /// <typeparam name="TTo">The destination value type.</typeparam>
    /// <param name="value">The value to reinterpret.</param>
    /// <returns>The reinterpreted value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TTo As<TFrom, TTo>(TFrom value)
        where TFrom : struct
        where TTo : struct
        => Unsafe.As<TFrom, TTo>(ref value);

    /// <summary>
    /// Calculates and narrows two weighted 128-bit signed sixteen-bit vectors.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<short> MultiplyRound(
        Vector128<short> input0,
        int weight0,
        Vector128<short> input1,
        int weight1,
        int cosBit,
        Vector128<int> rounding)
    {
        // Widening preserves lane order on every Vector128 implementation. The explicit clamp gives Narrow the
        // signed-saturating demotion semantics used by Highway on x86, Arm, and WebAssembly.
        (Vector128<int> input0Lower, Vector128<int> input0Upper) = Vector128.Widen(input0);
        (Vector128<int> input1Lower, Vector128<int> input1Upper) = Vector128.Widen(input1);

        Vector128<int> weight0Vector = Vector128.Create(weight0);
        Vector128<int> weight1Vector = Vector128.Create(weight1);
        Vector128<int> lower = ((input0Lower * weight0Vector) + (input1Lower * weight1Vector) + rounding) >> cosBit;
        Vector128<int> upper = ((input0Upper * weight0Vector) + (input1Upper * weight1Vector) + rounding) >> cosBit;
        Vector128<int> minimum = Vector128.Create((int)short.MinValue);
        Vector128<int> maximum = Vector128.Create((int)short.MaxValue);

        lower = Vector128.Clamp(lower, minimum, maximum);
        upper = Vector128.Clamp(upper, minimum, maximum);
        return Vector128.Narrow(lower, upper);
    }

    /// <summary>
    /// Calculates and narrows two weighted 256-bit signed sixteen-bit vectors.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<short> MultiplyRound(
        Vector256<short> input0,
        int weight0,
        Vector256<short> input1,
        int weight1,
        int cosBit,
        Vector256<int> rounding)
    {
        // The AVX2 path mirrors Highway's WidenMulPairwiseAdd primitive: adjacent Int16 products become Int32
        // sums, then VPACKSSDW restores the original lane width with signed saturation.
        Vector256<short> lowerInputs = Avx2.UnpackLow(input0, input1);
        Vector256<short> upperInputs = Avx2.UnpackHigh(input0, input1);
        Vector256<short> weights = Avx2.UnpackLow(Vector256.Create((short)weight0), Vector256.Create((short)weight1));
        Vector256<int> lower = (Avx2.MultiplyAddAdjacent(lowerInputs, weights) + rounding) >> cosBit;
        Vector256<int> upper = (Avx2.MultiplyAddAdjacent(upperInputs, weights) + rounding) >> cosBit;
        return Avx2.PackSignedSaturate(lower, upper);
    }

    /// <summary>
    /// Calculates and narrows two weighted 512-bit signed sixteen-bit vectors.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<short> MultiplyRound(
        Vector512<short> input0,
        int weight0,
        Vector512<short> input1,
        int weight1,
        int cosBit,
        Vector512<int> rounding)
    {
        Vector512<short> lowerInputs = Avx512BW.UnpackLow(input0, input1);
        Vector512<short> upperInputs = Avx512BW.UnpackHigh(input0, input1);
        Vector512<short> weights = Avx512BW.UnpackLow(Vector512.Create((short)weight0), Vector512.Create((short)weight1));
        Vector512<int> lower = (Avx512BW.MultiplyAddAdjacent(lowerInputs, weights) + rounding) >> cosBit;
        Vector512<int> upper = (Avx512BW.MultiplyAddAdjacent(upperInputs, weights) + rounding) >> cosBit;
        return Avx512BW.PackSignedSaturate(lower, upper);
    }

    /// <summary>
    /// Calculates and narrows four weighted 128-bit signed sixteen-bit vectors.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<short> MultiplyRound(
        Vector128<short> input0,
        int weight0,
        Vector128<short> input1,
        int weight1,
        Vector128<short> input2,
        int weight2,
        Vector128<short> input3,
        int weight3,
        int cosBit,
        Vector128<int> rounding)
    {
        (Vector128<int> input0Lower, Vector128<int> input0Upper) = Vector128.Widen(input0);
        (Vector128<int> input1Lower, Vector128<int> input1Upper) = Vector128.Widen(input1);
        (Vector128<int> input2Lower, Vector128<int> input2Upper) = Vector128.Widen(input2);
        (Vector128<int> input3Lower, Vector128<int> input3Upper) = Vector128.Widen(input3);

        Vector128<int> lower = ((input0Lower * Vector128.Create(weight0))
            + (input1Lower * Vector128.Create(weight1))
            + (input2Lower * Vector128.Create(weight2))
            + (input3Lower * Vector128.Create(weight3))
            + rounding) >> cosBit;

        Vector128<int> upper = ((input0Upper * Vector128.Create(weight0))
            + (input1Upper * Vector128.Create(weight1))
            + (input2Upper * Vector128.Create(weight2))
            + (input3Upper * Vector128.Create(weight3))
            + rounding) >> cosBit;

        Vector128<int> minimum = Vector128.Create((int)short.MinValue);
        Vector128<int> maximum = Vector128.Create((int)short.MaxValue);
        lower = Vector128.Clamp(lower, minimum, maximum);
        upper = Vector128.Clamp(upper, minimum, maximum);
        return Vector128.Narrow(lower, upper);
    }

    /// <summary>
    /// Calculates and narrows four weighted 256-bit signed sixteen-bit vectors.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<short> MultiplyRound(
        Vector256<short> input0,
        int weight0,
        Vector256<short> input1,
        int weight1,
        Vector256<short> input2,
        int weight2,
        Vector256<short> input3,
        int weight3,
        int cosBit,
        Vector256<int> rounding)
    {
        Vector256<short> weights01 = Avx2.UnpackLow(Vector256.Create((short)weight0), Vector256.Create((short)weight1));
        Vector256<short> weights23 = Avx2.UnpackLow(Vector256.Create((short)weight2), Vector256.Create((short)weight3));
        Vector256<int> lower = Avx2.MultiplyAddAdjacent(Avx2.UnpackLow(input0, input1), weights01)
            + Avx2.MultiplyAddAdjacent(Avx2.UnpackLow(input2, input3), weights23);

        Vector256<int> upper = Avx2.MultiplyAddAdjacent(Avx2.UnpackHigh(input0, input1), weights01)
            + Avx2.MultiplyAddAdjacent(Avx2.UnpackHigh(input2, input3), weights23);

        lower = (lower + rounding) >> cosBit;
        upper = (upper + rounding) >> cosBit;
        return Avx2.PackSignedSaturate(lower, upper);
    }

    /// <summary>
    /// Calculates and narrows four weighted 512-bit signed sixteen-bit vectors.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<short> MultiplyRound(
        Vector512<short> input0,
        int weight0,
        Vector512<short> input1,
        int weight1,
        Vector512<short> input2,
        int weight2,
        Vector512<short> input3,
        int weight3,
        int cosBit,
        Vector512<int> rounding)
    {
        Vector512<short> weights01 = Avx512BW.UnpackLow(Vector512.Create((short)weight0), Vector512.Create((short)weight1));
        Vector512<short> weights23 = Avx512BW.UnpackLow(Vector512.Create((short)weight2), Vector512.Create((short)weight3));
        Vector512<int> lower = Avx512BW.MultiplyAddAdjacent(Avx512BW.UnpackLow(input0, input1), weights01)
            + Avx512BW.MultiplyAddAdjacent(Avx512BW.UnpackLow(input2, input3), weights23);

        Vector512<int> upper = Avx512BW.MultiplyAddAdjacent(Avx512BW.UnpackHigh(input0, input1), weights01)
            + Avx512BW.MultiplyAddAdjacent(Avx512BW.UnpackHigh(input2, input3), weights23);

        lower = (lower + rounding) >> cosBit;
        upper = (upper + rounding) >> cosBit;
        return Avx512BW.PackSignedSaturate(lower, upper);
    }
}
