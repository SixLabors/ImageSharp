// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Tests.Helpers;

public class ColorNumericsTests
{
    /// <summary>
    /// Grayscale luminance retains the source value even when it is outside the unit interval.
    /// </summary>
    /// <param name="value">The grayscale component value.</param>
    [Theory]
    [InlineData(-2F)]
    [InlineData(0.5F)]
    [InlineData(2F)]
    public void GetBT709Luminance_PreservesUnboundedGrayscale(float value)
    {
        Vector4 vector = new(value, value, value, 1F);

        Assert.Equal(value, ColorNumerics.GetBT709Luminance(vector));
    }

    [Theory]
    [InlineData((ushort)0, (byte)0)]
    [InlineData((ushort)128, (byte)0)]
    [InlineData((ushort)129, (byte)1)]
    [InlineData((ushort)257, (byte)1)]
    [InlineData((ushort)32896, (byte)128)]
    [InlineData(ushort.MaxValue, byte.MaxValue)]
    public void From16BitTo8Bit_ReturnsExpectedValue(ushort component, byte expected)
    {
        byte actual = ColorNumerics.From16BitTo8Bit(component);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void From16BitTo8Bit_RoundTripsAllExpanded8BitValues()
    {
        for (int i = 0; i <= byte.MaxValue; i++)
        {
            byte expected = (byte)i;
            ushort component = ColorNumerics.From8BitTo16Bit(expected);

            byte actual = ColorNumerics.From16BitTo8Bit(component);

            Assert.Equal(expected, actual);
        }
    }

    [Theory]
    [InlineData(0U, (byte)0)]
    [InlineData(8421504U, (byte)0)]
    [InlineData(8421505U, (byte)1)]
    [InlineData(16843009U, (byte)1)]
    [InlineData(2155905152U, (byte)128)]
    [InlineData(uint.MaxValue, byte.MaxValue)]
    public void From32BitTo8Bit_ReturnsExpectedValue(uint component, byte expected)
    {
        byte actual = ColorNumerics.From32BitTo8Bit(component);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void From32BitTo8Bit_RoundTripsAllExpanded8BitValues()
    {
        for (int i = 0; i <= byte.MaxValue; i++)
        {
            byte expected = (byte)i;
            uint component = ColorNumerics.From8BitTo32Bit(expected);

            byte actual = ColorNumerics.From32BitTo8Bit(component);

            Assert.Equal(expected, actual);
        }
    }

    /// <summary>
    /// Applies a bulk operation with two arguments to a span.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <typeparam name="TArg">The first argument type.</typeparam>
    /// <typeparam name="TArg1">The second argument type.</typeparam>
    /// <param name="span">The components to update.</param>
    /// <param name="arg">The first argument.</param>
    /// <param name="arg1">The second argument.</param>
    private delegate void SpanAction<T, in TArg, in TArg1>(Span<T> span, TArg arg, TArg1 arg1);

    /// <summary>
    /// Checks that single-precision span clamping matches scalar clamping for complete registers and remainders.
    /// </summary>
    /// <param name="length">The component count.</param>
    /// <param name="min">The finite lower bound.</param>
    /// <param name="max">The finite upper bound.</param>
    [Theory]
    [InlineData(64, 36, 96)]
    [InlineData(128, 16, 196)]
    [InlineData(567, 18, 142)]
    [InlineData(1024, 0, 255)]
    public void ClampToFiniteFloat(int length, float min, float max)
    {
        TestClampSpan(
            length,
            min,
            max,
            (s, m1, m2) => ColorNumerics.ClampToFinite(s, m1, m2),
            (v, m1, m2) => ColorNumerics.ClampToFinite(v, m1, m2));
    }

    /// <summary>
    /// Checks that double-precision span clamping matches scalar clamping for complete registers and remainders.
    /// </summary>
    /// <param name="length">The component count.</param>
    /// <param name="min">The finite lower bound.</param>
    /// <param name="max">The finite upper bound.</param>
    [Theory]
    [InlineData(64, 36, 96)]
    [InlineData(128, 16, 196)]
    [InlineData(567, 18, 142)]
    [InlineData(1024, 0, 255)]
    public void ClampToFiniteDouble(int length, double min, double max)
    {
        TestClampSpan(
            length,
            min,
            max,
            (s, m1, m2) => ColorNumerics.ClampToFinite(s, m1, m2),
            (v, m1, m2) => ColorNumerics.ClampToFinite(v, m1, m2));
    }

    /// <summary>
    /// Scalar, SIMD, and span clamps map nonfinite values to the requested bounds.
    /// </summary>
    /// <param name="min">The lower bound.</param>
    /// <param name="max">The upper bound.</param>
    [Theory]
    [InlineData(0F, 1F)]
    [InlineData(-2F, 3F)]
    [InlineData(.25F, .75F)]
    public void ClampToFiniteSingle_NormalizesNonfiniteValues(float min, float max)
    {
        float midpoint = (min + max) / 2;
        float[] inputs = [float.NaN, float.PositiveInfinity, float.NegativeInfinity, midpoint];
        float[] normalized = [min, max, min, midpoint];
        float[] values = new float[65];
        float[] expected = new float[values.Length];

        // The length includes complete registers and remainders for every supported SIMD width.
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = inputs[i % inputs.Length];
            expected[i] = normalized[i % normalized.Length];
            Assert.Equal(expected[i], ColorNumerics.ClampToFinite(values[i], min, max));
        }

        Vector4 input = new(inputs[0], inputs[1], inputs[2], inputs[3]);
        Vector4 result = new(normalized[0], normalized[1], normalized[2], normalized[3]);
        Assert.Equal(result, ColorNumerics.ClampToFinite(input, new Vector4(min), new Vector4(max)));
        Assert.Equal(new Vector2(min, max), ColorNumerics.ClampToFinite(new Vector2(float.NaN, float.PositiveInfinity), new Vector2(min), new Vector2(max)));

        Vector128<float> vector128 = Vector128.LoadUnsafe(ref values[0]);
        Assert.Equal(Vector128.LoadUnsafe(ref expected[0]), ColorNumerics.ClampToFinite(vector128, Vector128.Create(min), Vector128.Create(max)));

        Vector256<float> vector256 = Vector256.LoadUnsafe(ref values[0]);
        Assert.Equal(Vector256.LoadUnsafe(ref expected[0]), ColorNumerics.ClampToFinite(vector256, Vector256.Create(min), Vector256.Create(max)));

        Vector512<float> vector512 = Vector512.LoadUnsafe(ref values[0]);
        Assert.Equal(Vector512.LoadUnsafe(ref expected[0]), ColorNumerics.ClampToFinite(vector512, Vector512.Create(min), Vector512.Create(max)));

        ColorNumerics.ClampToFinite(values, min, max);
        Assert.Equal(expected, values);
    }

    /// <summary>
    /// Scalar, SIMD, and span clamps map nonfinite values to the requested bounds.
    /// </summary>
    /// <param name="min">The lower bound.</param>
    /// <param name="max">The upper bound.</param>
    [Theory]
    [InlineData(0D, 1D)]
    [InlineData(-2D, 3D)]
    [InlineData(.25D, .75D)]
    public void ClampToFiniteDouble_NormalizesNonfiniteValues(double min, double max)
    {
        double midpoint = (min + max) / 2;
        double[] inputs = [double.NaN, double.PositiveInfinity, double.NegativeInfinity, midpoint];
        double[] normalized = [min, max, min, midpoint];
        double[] values = new double[65];
        double[] expected = new double[values.Length];

        // The length includes complete registers and remainders for every supported SIMD width.
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = inputs[i % inputs.Length];
            expected[i] = normalized[i % normalized.Length];
            Assert.Equal(expected[i], ColorNumerics.ClampToFinite(values[i], min, max));
        }

        Vector128<double> vector128 = Vector128.LoadUnsafe(ref values[0]);
        Assert.Equal(Vector128.LoadUnsafe(ref expected[0]), ColorNumerics.ClampToFinite(vector128, Vector128.Create(min), Vector128.Create(max)));

        Vector256<double> vector256 = Vector256.LoadUnsafe(ref values[0]);
        Assert.Equal(Vector256.LoadUnsafe(ref expected[0]), ColorNumerics.ClampToFinite(vector256, Vector256.Create(min), Vector256.Create(max)));

        Vector512<double> vector512 = Vector512.LoadUnsafe(ref values[0]);
        Assert.Equal(Vector512.LoadUnsafe(ref expected[0]), ColorNumerics.ClampToFinite(vector512, Vector512.Create(min), Vector512.Create(max)));

        ColorNumerics.ClampToFinite(values, min, max);
        Assert.Equal(expected, values);
    }

    /// <summary>
    /// Clamping an in-range zero preserves its sign in scalar and bulk conversions.
    /// </summary>
    [Fact]
    public void ClampToFinite_PreservesInRangeSignedZero()
    {
        float[] singles = new float[65];
        double[] doubles = new double[65];

        for (int i = 0; i < singles.Length; i++)
        {
            singles[i] = i % 2 == 0 ? -0F : 0F;
            doubles[i] = i % 2 == 0 ? -0D : 0D;
            Assert.Equal(BitConverter.SingleToInt32Bits(singles[i]), BitConverter.SingleToInt32Bits(ColorNumerics.ClampToFinite(singles[i], 0F, 1F)));
            Assert.Equal(BitConverter.DoubleToInt64Bits(doubles[i]), BitConverter.DoubleToInt64Bits(ColorNumerics.ClampToFinite(doubles[i], 0D, 1D)));
        }

        ColorNumerics.ClampToFinite(singles, 0F, 1F);
        ColorNumerics.ClampToFinite(doubles, 0D, 1D);

        for (int i = 0; i < singles.Length; i++)
        {
            Assert.Equal(BitConverter.SingleToInt32Bits(i % 2 == 0 ? -0F : 0F), BitConverter.SingleToInt32Bits(singles[i]));
            Assert.Equal(BitConverter.DoubleToInt64Bits(i % 2 == 0 ? -0D : 0D), BitConverter.DoubleToInt64Bits(doubles[i]));
        }
    }

    /// <summary>
    /// Checks the bulk result against the scalar result for the same component values.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="length">The component count.</param>
    /// <param name="min">The finite lower bound.</param>
    /// <param name="max">The finite upper bound.</param>
    /// <param name="clampAction">The bulk clamp.</param>
    /// <param name="refClampFunc">The scalar clamp.</param>
    private static void TestClampSpan<T>(
        int length,
        T min,
        T max,
        SpanAction<T, T, T> clampAction,
        Func<T, T, T, T> refClampFunc)
        where T : unmanaged, IComparable<T>
    {
        Span<T> actual = new T[length];

        Random r = new();
        for (int i = 0; i < length; i++)
        {
            actual[i] = (T)Convert.ChangeType(r.Next(byte.MinValue, byte.MaxValue), typeof(T));
        }

        Span<T> expected = new T[length];
        actual.CopyTo(expected);

        for (int i = 0; i < expected.Length; i++)
        {
            ref T v = ref expected[i];
            v = refClampFunc(v, min, max);
        }

        clampAction(actual, min, max);

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], actual[i]);
        }
    }
}
