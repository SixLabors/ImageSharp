// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Tests.Helpers;

public class NumericsTests
{
    private delegate void SpanAction<T, in TArg, in TArg1>(Span<T> span, TArg arg, TArg1 arg1);

    private readonly ApproximateFloatComparer approximateFloatComparer = new(1e-6f);

    /// <summary>
    /// Gets lengths that straddle the Vector4 block boundaries used by the 128-, 256-, and 512-bit kernels.
    /// </summary>
    public static TheoryData<int> AssociationSpanLengths => new() { 0, 1, 2, 3, 4, 5, 7, 8, 9, 15, 16, 17, 31, 32, 33, 63 };

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(100)]
    [InlineData(123)]
    [InlineData(53436353)]
    public void Modulo2(int x)
    {
        int actual = Numerics.Modulo2(x);
        Assert.Equal(x % 2, actual);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(100)]
    [InlineData(123)]
    [InlineData(53436353)]
    public void Modulo4(int x)
    {
        int actual = Numerics.Modulo4(x);
        Assert.Equal(x % 4, actual);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(100)]
    [InlineData(123)]
    [InlineData(53436353)]
    [InlineData(975)]
    public void Modulo8(int x)
    {
        int actual = Numerics.Modulo8(x);
        Assert.Equal(x % 8, actual);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    [InlineData(0, 4)]
    [InlineData(3, 4)]
    [InlineData(5, 4)]
    [InlineData(5, 8)]
    [InlineData(8, 8)]
    [InlineData(8, 16)]
    [InlineData(15, 16)]
    [InlineData(17, 16)]
    [InlineData(17, 32)]
    [InlineData(31, 32)]
    [InlineData(32, 32)]
    [InlineData(33, 32)]
    public void Modulo2P(int x, int m)
    {
        int actual = Numerics.ModuloP2(x, m);
        Assert.Equal(x % m, actual);
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(-17)]
    [InlineData(-12856)]
    [InlineData(-32)]
    [InlineData(-7425)]
    [InlineData(5)]
    [InlineData(17)]
    [InlineData(12856)]
    [InlineData(32)]
    [InlineData(7425)]
    public void Abs(int x)
    {
        int expected = Math.Abs(x);
        Assert.Equal(expected, Numerics.Abs(x));
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(-17)]
    [InlineData(-12856)]
    [InlineData(-32)]
    [InlineData(-7425)]
    [InlineData(5)]
    [InlineData(17)]
    [InlineData(12856)]
    [InlineData(32)]
    [InlineData(7425)]
    public void Pow2(float x)
    {
        float expected = (float)Math.Pow(x, 2);
        Assert.Equal(expected, Numerics.Pow2(x));
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(-17)]
    [InlineData(-12856)]
    [InlineData(-32)]
    [InlineData(5)]
    [InlineData(17)]
    [InlineData(12856)]
    [InlineData(32)]
    public void Pow3(float x)
    {
        float expected = (float)Math.Pow(x, 3);
        Assert.Equal(expected, Numerics.Pow3(x));
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(1, 42, 1)]
    [InlineData(10, 8, 2)]
    [InlineData(12, 18, 6)]
    [InlineData(4536, 1000, 8)]
    [InlineData(1600, 1024, 64)]
    public void GreatestCommonDivisor(int a, int b, int expected)
    {
        int actual = Numerics.GreatestCommonDivisor(a, b);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(1, 42, 42)]
    [InlineData(3, 4, 12)]
    [InlineData(6, 4, 12)]
    [InlineData(1600, 1024, 25600)]
    [InlineData(3264, 100, 81600)]
    public void LeastCommonMultiple(int a, int b, int expected)
    {
        int actual = Numerics.LeastCommonMultiple(a, b);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(AssociationSpanLengths))]
    public void PremultiplyVectorSpan(int length)
    {
        Random rnd = new(42);
        Vector4[] source = rnd.GenerateRandomVectorArray(length, 0, 1);

        for (int i = 0; i < source.Length; i++)
        {
            // Exact zero and one exercise the special alpha boundaries alongside the random fractional values.
            source[i].W = i % 5 switch
            {
                0 => 0F,
                1 => 1F,
                _ => source[i].W
            };
        }

        Vector4[] expected =
        [
            .. source.Select(v =>
            {
                Numerics.Premultiply(ref v);
                return v;
            })
        ];

        Numerics.Premultiply(source);

        Assert.Equal(expected, source, this.approximateFloatComparer);

        for (int i = 0; i < source.Length; i++)
        {
            // Alpha is storage metadata here and must survive each SIMD width without any floating-point transformation.
            Assert.Equal(BitConverter.SingleToInt32Bits(expected[i].W), BitConverter.SingleToInt32Bits(source[i].W));
        }
    }

    [Theory]
    [MemberData(nameof(AssociationSpanLengths))]
    public void UnPremultiplyVectorSpan(int length)
    {
        Random rnd = new(42);
        Vector4[] source = rnd.GenerateRandomVectorArray(length, 0, 1);

        for (int i = 0; i < source.Length; i++)
        {
            // A zero alpha preserves the complete source vector by contract; one also verifies the identity case.
            source[i].W = i % 5 switch
            {
                0 => 0F,
                1 => 1F,
                _ => source[i].W
            };
        }

        Vector4[] expected =
        [
            .. source.Select(v =>
            {
                Numerics.UnPremultiply(ref v);
                return v;
            })
        ];

        Numerics.UnPremultiply(source);

        Assert.Equal(expected, source, this.approximateFloatComparer);

        for (int i = 0; i < source.Length; i++)
        {
            // Alpha is the divisor and must still survive each SIMD width without any floating-point transformation.
            Assert.Equal(BitConverter.SingleToInt32Bits(expected[i].W), BitConverter.SingleToInt32Bits(source[i].W));
        }
    }

    [Theory]
    [InlineData(64, 36, 96)]
    [InlineData(128, 16, 196)]
    [InlineData(567, 18, 142)]
    [InlineData(1024, 0, 255)]
    public void ClampByte(int length, byte min, byte max)
    {
        TestClampSpan(
            length,
            min,
            max,
            (s, m1, m2) => Numerics.Clamp(s, m1, m2),
            (v, m1, m2) => Numerics.Clamp(v, m1, m2));
    }

    [Theory]
    [InlineData(64, 36, 96)]
    [InlineData(128, 16, 196)]
    [InlineData(567, 18, 142)]
    [InlineData(1024, 0, 255)]
    public void ClampInt(int length, int min, int max)
    {
        TestClampSpan(
            length,
            min,
            max,
            (s, m1, m2) => Numerics.Clamp(s, m1, m2),
            (v, m1, m2) => Numerics.Clamp(v, m1, m2));
    }

    [Theory]
    [InlineData(64, 36, 96)]
    [InlineData(128, 16, 196)]
    [InlineData(567, 18, 142)]
    [InlineData(1024, 0, 255)]
    public void ClampUInt(int length, uint min, uint max)
    {
        TestClampSpan(
            length,
            min,
            max,
            (s, m1, m2) => Numerics.Clamp(s, m1, m2),
            (v, m1, m2) => Numerics.Clamp(v, m1, m2));
    }

    [Theory]
    [InlineData(64, 36, 96)]
    [InlineData(128, 16, 196)]
    [InlineData(567, 18, 142)]
    [InlineData(1024, 0, 255)]
    public void ClampFloat(int length, float min, float max)
    {
        TestClampSpan(
            length,
            min,
            max,
            (s, m1, m2) => Numerics.Clamp(s, m1, m2),
            (v, m1, m2) => Numerics.Clamp(v, m1, m2));
    }

    [Theory]
    [InlineData(64, 36, 96)]
    [InlineData(128, 16, 196)]
    [InlineData(567, 18, 142)]
    [InlineData(1024, 0, 255)]
    public void ClampDouble(int length, double min, double max)
    {
        TestClampSpan(
            length,
            min,
            max,
            (s, m1, m2) => Numerics.Clamp(s, m1, m2),
            (v, m1, m2) => Numerics.Clamp(v, m1, m2));
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
    public void ClampSingle_NormalizesNonfiniteValues(float min, float max)
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
            Assert.Equal(expected[i], Numerics.Clamp(values[i], min, max));
        }

        Vector4 input = new(inputs[0], inputs[1], inputs[2], inputs[3]);
        Vector4 result = new(normalized[0], normalized[1], normalized[2], normalized[3]);
        Assert.Equal(result, Numerics.Clamp(input, new Vector4(min), new Vector4(max)));
        Assert.Equal(new Vector2(min, max), Numerics.Clamp(new Vector2(float.NaN, float.PositiveInfinity), new Vector2(min), new Vector2(max)));

        Vector128<float> vector128 = Vector128.LoadUnsafe(ref values[0]);
        Assert.Equal(Vector128.LoadUnsafe(ref expected[0]), Numerics.Clamp(vector128, Vector128.Create(min), Vector128.Create(max)));

        Vector256<float> vector256 = Vector256.LoadUnsafe(ref values[0]);
        Assert.Equal(Vector256.LoadUnsafe(ref expected[0]), Numerics.Clamp(vector256, Vector256.Create(min), Vector256.Create(max)));

        Vector512<float> vector512 = Vector512.LoadUnsafe(ref values[0]);
        Assert.Equal(Vector512.LoadUnsafe(ref expected[0]), Numerics.Clamp(vector512, Vector512.Create(min), Vector512.Create(max)));

        Numerics.Clamp(values, min, max);
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
    public void ClampDouble_NormalizesNonfiniteValues(double min, double max)
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
            Assert.Equal(expected[i], Numerics.Clamp(values[i], min, max));
        }

        Vector128<double> vector128 = Vector128.LoadUnsafe(ref values[0]);
        Assert.Equal(Vector128.LoadUnsafe(ref expected[0]), Numerics.Clamp(vector128, Vector128.Create(min), Vector128.Create(max)));

        Vector256<double> vector256 = Vector256.LoadUnsafe(ref values[0]);
        Assert.Equal(Vector256.LoadUnsafe(ref expected[0]), Numerics.Clamp(vector256, Vector256.Create(min), Vector256.Create(max)));

        Vector512<double> vector512 = Vector512.LoadUnsafe(ref values[0]);
        Assert.Equal(Vector512.LoadUnsafe(ref expected[0]), Numerics.Clamp(vector512, Vector512.Create(min), Vector512.Create(max)));

        Numerics.Clamp(values, min, max);
        Assert.Equal(expected, values);
    }

    /// <summary>
    /// Clamping an in-range zero preserves its sign in scalar and bulk conversions.
    /// </summary>
    [Fact]
    public void Clamp_PreservesInRangeSignedZero()
    {
        float[] singles = new float[65];
        double[] doubles = new double[65];

        for (int i = 0; i < singles.Length; i++)
        {
            singles[i] = i % 2 == 0 ? -0F : 0F;
            doubles[i] = i % 2 == 0 ? -0D : 0D;
            Assert.Equal(BitConverter.SingleToInt32Bits(singles[i]), BitConverter.SingleToInt32Bits(Numerics.Clamp(singles[i], 0F, 1F)));
            Assert.Equal(BitConverter.DoubleToInt64Bits(doubles[i]), BitConverter.DoubleToInt64Bits(Numerics.Clamp(doubles[i], 0D, 1D)));
        }

        Numerics.Clamp(singles, 0F, 1F);
        Numerics.Clamp(doubles, 0D, 1D);

        for (int i = 0; i < singles.Length; i++)
        {
            Assert.Equal(BitConverter.SingleToInt32Bits(i % 2 == 0 ? -0F : 0F), BitConverter.SingleToInt32Bits(singles[i]));
            Assert.Equal(BitConverter.DoubleToInt64Bits(i % 2 == 0 ? -0D : 0D), BitConverter.DoubleToInt64Bits(doubles[i]));
        }
    }

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
