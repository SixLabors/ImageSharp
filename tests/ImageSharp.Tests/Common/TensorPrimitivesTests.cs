// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Common;

public class TensorPrimitivesTests
{
    private static readonly int[] SpanLengthValues =
    [
        0,
        1,
        3,
        4,
        5,
        7,
        8,
        9,
        15,
        16,
        17,
        31,
        32,
        33,
        63,
        64,
        65,
        127,
        128,
        129,
        2048
    ];

    /// <summary>
    /// Gets lengths that exercise scalar execution, every SIMD width, overlapping tails, and the unrolled loop.
    /// </summary>
    public static TheoryData<int> SpanLengths => new(SpanLengthValues);

    /// <summary>
    /// Verifies every compatibility operation while forcing the supported SIMD feature tiers in isolated processes.
    /// </summary>
    [Fact]
    public void OperationsMatchScalarFormulasAcrossHardwareIntrinsicFeatures()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunOperationsAcrossHardwareIntrinsicFeatures,
            HwIntrinsics.AllowAll
                | HwIntrinsics.DisableAVX512F
                | HwIntrinsics.DisableAVX
                | HwIntrinsics.DisableArm64Sve
                | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Verifies that span-to-span operations require equal input lengths before accessing either input.
    /// </summary>
    [Fact]
    public void AddSpanSpanRejectsMismatchedInputLengths()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => TensorPrimitives_.Add<int>(new int[4], new int[3], new int[4]));

        Assert.Null(exception.ParamName);
    }

    /// <summary>
    /// Verifies that every operation rejects a destination that cannot hold all input elements.
    /// </summary>
    [Fact]
    public void OperationsRejectShortDestinations()
    {
        int[] integers = new int[4];
        float[] singles = new float[4];

        Assert.Equal("destination", Assert.Throws<ArgumentException>(() => TensorPrimitives_.Add<int>(integers, integers, new int[3])).ParamName);
        Assert.Equal("destination", Assert.Throws<ArgumentException>(() => TensorPrimitives_.Add<int>(integers, 1, new int[3])).ParamName);
        Assert.Equal("destination", Assert.Throws<ArgumentException>(() => TensorPrimitives_.Multiply<float>(singles, 2F, new float[3])).ParamName);
        Assert.Equal("destination", Assert.Throws<ArgumentException>(() => TensorPrimitives_.Divide<float>(singles, 2F, new float[3])).ParamName);
        Assert.Equal("destination", Assert.Throws<ArgumentException>(() => TensorPrimitives_.Max<float>(singles, 2F, new float[3])).ParamName);
        Assert.Equal("destination", Assert.Throws<ArgumentException>(() => TensorPrimitives_.Clamp<float>(singles, 0F, 1F, new float[3])).ParamName);
        Assert.Equal("destination", Assert.Throws<ArgumentException>(() => TensorPrimitives_.Negate<float>(singles, new float[3])).ParamName);
    }

    /// <summary>
    /// Verifies that every operation rejects shifted input and destination overlap.
    /// </summary>
    [Fact]
    public void OperationsRejectShiftedOverlap()
    {
        int[] integers = new int[5];
        int[] separateIntegers = new int[4];
        float[] singles = new float[5];

        // Both inputs need independent validation because either one may alias a shifted destination.
        Assert.Equal(
            "destination",
            Assert.Throws<ArgumentException>(
                () => TensorPrimitives_.Add<int>(integers.AsSpan(0, 4), separateIntegers, integers.AsSpan(1, 4))).ParamName);

        Assert.Equal(
            "destination",
            Assert.Throws<ArgumentException>(
                () => TensorPrimitives_.Add<int>(separateIntegers, integers.AsSpan(0, 4), integers.AsSpan(1, 4))).ParamName);

        Assert.Equal(
            "destination",
            Assert.Throws<ArgumentException>(
                () => TensorPrimitives_.Add<int>(integers.AsSpan(0, 4), 1, integers.AsSpan(1, 4))).ParamName);

        Assert.Equal(
            "destination",
            Assert.Throws<ArgumentException>(
                () => TensorPrimitives_.Multiply<float>(singles.AsSpan(0, 4), 2F, singles.AsSpan(1, 4))).ParamName);

        Assert.Equal(
            "destination",
            Assert.Throws<ArgumentException>(
                () => TensorPrimitives_.Divide<float>(singles.AsSpan(0, 4), 2F, singles.AsSpan(1, 4))).ParamName);

        Assert.Equal(
            "destination",
            Assert.Throws<ArgumentException>(
                () => TensorPrimitives_.Max<float>(singles.AsSpan(0, 4), 2F, singles.AsSpan(1, 4))).ParamName);

        Assert.Equal(
            "destination",
            Assert.Throws<ArgumentException>(
                () => TensorPrimitives_.Clamp<float>(singles.AsSpan(0, 4), 0F, 1F, singles.AsSpan(1, 4))).ParamName);

        Assert.Equal(
            "destination",
            Assert.Throws<ArgumentException>(
                () => TensorPrimitives_.Negate<float>(singles.AsSpan(0, 4), singles.AsSpan(1, 4))).ParamName);
    }

    /// <summary>
    /// Runs the TensorPrimitives compatibility assertions inside a process configured for one hardware-intrinsic tier.
    /// </summary>
    private static void RunOperationsAcrossHardwareIntrinsicFeatures()
    {
        TensorPrimitivesTests tests = new();

        // Reuse the focused assertions so the remote feature matrix cannot drift from the normal test coverage.
        foreach (int length in SpanLengthValues)
        {
            tests.AddByteMatchesScalarFormula(length);
            tests.AddUInt32MatchesScalarFormula(length);
            tests.AddScalarInt32MatchesScalarFormula(length);
            tests.NegateSingleMatchesScalarFormula(length);
            tests.NegateDoubleMatchesScalarFormula(length);
            tests.ClampInt32MatchesScalarFormula(length);
            tests.ClampSingleMatchesRuntimeFormula(length);
            tests.DivideSingleMatchesScalarFormula(length);
            tests.MaxSingleMatchesRuntimeFormula(length);
            tests.MultiplySingleMatchesScalarFormula(length);
            tests.NormalizeMatchesScalarFormula(length);
        }

        tests.ClampSinglePreservesRuntimeSpecialValueSemantics();
        tests.ClampDoublePreservesRuntimeSpecialValueSemantics();
    }

    /// <summary>
    /// Verifies that byte addition wraps modulo 256 and supports either input as the in-place destination.
    /// </summary>
    /// <param name="length">The input length.</param>
    [Theory]
    [MemberData(nameof(SpanLengths))]
    public void AddByteMatchesScalarFormula(int length)
    {
        byte[] x = new byte[length];
        byte[] y = new byte[length];
        byte[] expected = new byte[length];

        for (int i = 0; i < length; i++)
        {
            x[i] = (byte)((i * 23) + 197);
            y[i] = (byte)((i * 41) + 113);
            expected[i] = unchecked((byte)(x[i] + y[i]));
        }

        byte[] destination = new byte[length];
        TensorPrimitives_.Add<byte>(x, y, destination);
        Assert.Equal(expected, destination);

        byte[] xInPlace = (byte[])x.Clone();
        TensorPrimitives_.Add<byte>(xInPlace, y, xInPlace);
        Assert.Equal(expected, xInPlace);

        byte[] yInPlace = (byte[])y.Clone();
        TensorPrimitives_.Add<byte>(x, yInPlace, yInPlace);
        Assert.Equal(expected, yInPlace);
    }

    /// <summary>
    /// Verifies that unsigned integer addition preserves unchecked histogram accumulation semantics.
    /// </summary>
    /// <param name="length">The input length.</param>
    [Theory]
    [MemberData(nameof(SpanLengths))]
    public void AddUInt32MatchesScalarFormula(int length)
    {
        uint[] x = new uint[length];
        uint[] y = new uint[length];
        uint[] expected = new uint[length];

        for (int i = 0; i < length; i++)
        {
            x[i] = ((uint)i * 1_234_567U) + 0xF0000000U;
            y[i] = ((uint)i * 7_654_321U) + 0x30000000U;
            expected[i] = unchecked(x[i] + y[i]);
        }

        TensorPrimitives_.Add<uint>(x, y, x);
        Assert.Equal(expected, x);
    }

    /// <summary>
    /// Verifies that scalar integer addition produces identical results for separate and in-place destinations.
    /// </summary>
    /// <param name="length">The input length.</param>
    [Theory]
    [MemberData(nameof(SpanLengths))]
    public void AddScalarInt32MatchesScalarFormula(int length)
    {
        int[] source = new int[length];
        int[] expected = new int[length];
        const int addend = 17;

        for (int i = 0; i < length; i++)
        {
            source[i] = (i * 37) - 200;
            expected[i] = source[i] + addend;
        }

        int[] destination = new int[length];
        TensorPrimitives_.Add(source, addend, destination);
        Assert.Equal(expected, destination);

        int[] inPlace = (int[])source.Clone();
        TensorPrimitives_.Add(inPlace, addend, inPlace);
        Assert.Equal(expected, inPlace);
    }

    /// <summary>
    /// Verifies that floating-point negation preserves the scalar operator's exact bit-level behavior.
    /// </summary>
    /// <param name="length">The input length.</param>
    [Theory]
    [MemberData(nameof(SpanLengths))]
    public void NegateSingleMatchesScalarFormula(int length)
    {
        float[] values =
        {
            float.NaN,
            -0F,
            0F,
            -1F,
            1F,
            float.NegativeInfinity,
            float.PositiveInfinity
        };

        float[] source = new float[length];
        float[] expected = new float[length];

        for (int i = 0; i < source.Length; i++)
        {
            source[i] = values[i % values.Length];
            expected[i] = -source[i];
        }

        float[] destination = new float[length];
        TensorPrimitives_.Negate<float>(source, destination);
        AssertSingleBitsEqual(expected, destination);

        TensorPrimitives_.Negate<float>(source, source);
        AssertSingleBitsEqual(expected, source);
    }

    /// <summary>
    /// Verifies that double-precision negation preserves the scalar operator's exact bit-level behavior.
    /// </summary>
    /// <param name="length">The input length.</param>
    [Theory]
    [MemberData(nameof(SpanLengths))]
    public void NegateDoubleMatchesScalarFormula(int length)
    {
        double[] values =
        {
            double.NaN,
            -0D,
            0D,
            -1D,
            1D,
            double.NegativeInfinity,
            double.PositiveInfinity
        };

        double[] source = new double[length];
        double[] expected = new double[length];

        for (int i = 0; i < source.Length; i++)
        {
            source[i] = values[i % values.Length];
            expected[i] = -source[i];
        }

        double[] destination = new double[length];
        TensorPrimitives_.Negate<double>(source, destination);
        AssertDoubleBitsEqual(expected, destination);

        TensorPrimitives_.Negate<double>(source, source);
        AssertDoubleBitsEqual(expected, source);
    }

    /// <summary>
    /// Verifies that integer clamping produces identical results for separate and in-place destinations.
    /// </summary>
    /// <param name="length">The input length.</param>
    [Theory]
    [MemberData(nameof(SpanLengths))]
    public void ClampInt32MatchesScalarFormula(int length)
    {
        int[] source = new int[length];
        int[] expected = new int[length];

        for (int i = 0; i < source.Length; i++)
        {
            source[i] = ((i * 37) % 401) - 200;
            expected[i] = Math.Clamp(source[i], -73, 91);
        }

        int[] destination = new int[length];
        TensorPrimitives_.Clamp<int>(source, -73, 91, destination);
        Assert.Equal(expected, destination);

        int[] inPlace = (int[])source.Clone();
        TensorPrimitives_.Clamp<int>(inPlace, -73, 91, inPlace);
        Assert.Equal(expected, inPlace);
    }

    /// <summary>
    /// Verifies that floating-point clamping matches the runtime tensor formula for special values and unordered bounds.
    /// </summary>
    /// <param name="length">The input length.</param>
    [Theory]
    [MemberData(nameof(SpanLengths))]
    public void ClampSingleMatchesRuntimeFormula(int length)
    {
        float[] values =
        {
            float.NaN,
            -0F,
            0F,
            -1F,
            1F,
            float.NegativeInfinity,
            float.PositiveInfinity
        };

        float[] source = new float[length];
        float[] expected = new float[length];

        for (int i = 0; i < source.Length; i++)
        {
            source[i] = values[i % values.Length];

            // Runtime main follows Min(Max(x, min), max) for vectorizable types, including unordered bounds.
            expected[i] = float.Min(float.Max(source[i], 2F), -2F);
        }

        TensorPrimitives_.Clamp<float>(source, 2F, -2F, source);
        AssertSingleBitsEqual(expected, source);
    }

    /// <summary>
    /// Verifies that single-precision clamping preserves the runtime's signed-zero and NaN behavior.
    /// </summary>
    [Fact]
    public void ClampSinglePreservesRuntimeSpecialValueSemantics()
    {
        float[] values =
        {
            float.NaN,
            float.NegativeInfinity,
            -0F,
            0F,
            float.PositiveInfinity
        };

        float[] actual = new float[129];
        float[] expected = new float[actual.Length];

        for (int i = 0; i < actual.Length; i++)
        {
            actual[i] = values[i % values.Length];
            expected[i] = float.Min(float.Max(actual[i], -0F), 0F);
        }

        TensorPrimitives_.Clamp<float>(actual, -0F, 0F, actual);
        AssertSingleBitsEqual(expected, actual);
    }

    /// <summary>
    /// Verifies that double-precision clamping preserves the runtime's signed-zero and NaN behavior.
    /// </summary>
    [Fact]
    public void ClampDoublePreservesRuntimeSpecialValueSemantics()
    {
        double[] values =
        {
            double.NaN,
            double.NegativeInfinity,
            -0D,
            0D,
            double.PositiveInfinity
        };

        double[] actual = new double[65];
        double[] expected = new double[actual.Length];

        for (int i = 0; i < actual.Length; i++)
        {
            actual[i] = values[i % values.Length];
            expected[i] = double.Min(double.Max(actual[i], -0D), 0D);
        }

        TensorPrimitives_.Clamp<double>(actual, -0D, 0D, actual);
        AssertDoubleBitsEqual(expected, actual);
    }

    /// <summary>
    /// Verifies that division produces identical results for separate and in-place destinations.
    /// </summary>
    /// <param name="length">The input length.</param>
    [Theory]
    [MemberData(nameof(SpanLengths))]
    public void DivideSingleMatchesScalarFormula(int length)
    {
        float[] source = new float[length];
        float[] expected = new float[length];

        for (int i = 0; i < source.Length; i++)
        {
            source[i] = (i - 65.25F) * 1.75F;
            expected[i] = source[i] / 3.25F;
        }

        float[] destination = new float[length];
        TensorPrimitives_.Divide<float>(source, 3.25F, destination);
        AssertSingleBitsEqual(expected, destination);

        float[] inPlace = (float[])source.Clone();
        TensorPrimitives_.Divide<float>(inPlace, 3.25F, inPlace);
        AssertSingleBitsEqual(expected, inPlace);
    }

    /// <summary>
    /// Verifies that maximum selection preserves the runtime's NaN and signed-zero semantics.
    /// </summary>
    /// <param name="length">The input length.</param>
    [Theory]
    [MemberData(nameof(SpanLengths))]
    public void MaxSingleMatchesRuntimeFormula(int length)
    {
        float[] values =
        {
            float.NaN,
            float.NegativeInfinity,
            -1F,
            -0F,
            0F,
            1F,
            float.PositiveInfinity
        };

        float[] actual = new float[length];
        float[] expected = new float[length];

        for (int i = 0; i < length; i++)
        {
            actual[i] = values[i % values.Length];
            expected[i] = float.Max(actual[i], -0F);
        }

        TensorPrimitives_.Max<float>(actual, -0F, actual);
        AssertSingleBitsEqual(expected, actual);
    }

    /// <summary>
    /// Verifies that multiplication produces identical results for separate and in-place destinations.
    /// </summary>
    /// <param name="length">The input length.</param>
    [Theory]
    [MemberData(nameof(SpanLengths))]
    public void MultiplySingleMatchesScalarFormula(int length)
    {
        float[] source = new float[length];
        float[] expected = new float[length];

        for (int i = 0; i < source.Length; i++)
        {
            source[i] = (i - 65.25F) * 1.75F;
            expected[i] = source[i] * 0.375F;
        }

        float[] destination = new float[length];
        TensorPrimitives_.Multiply<float>(source, 0.375F, destination);
        AssertSingleBitsEqual(expected, destination);

        TensorPrimitives_.Multiply<float>(source, 0.375F, source);
        AssertSingleBitsEqual(expected, source);
    }

    /// <summary>
    /// Verifies that the normalization compatibility call preserves its element-wise division contract.
    /// </summary>
    /// <param name="length">The input length.</param>
    [Theory]
    [MemberData(nameof(SpanLengths))]
    public void NormalizeMatchesScalarFormula(int length)
    {
        float[] actual = new float[length];
        float[] expected = new float[length];

        for (int i = 0; i < actual.Length; i++)
        {
            actual[i] = (i + 1) * 0.125F;
            expected[i] = actual[i] / 7.5F;
        }

        Numerics.Normalize(actual, 7.5F);
        AssertSingleBitsEqual(expected, actual);
    }

    /// <summary>
    /// Compares floating-point results while preserving signed-zero behavior.
    /// </summary>
    /// <param name="expected">The expected values.</param>
    /// <param name="actual">The actual values.</param>
    private static void AssertSingleBitsEqual(ReadOnlySpan<float> expected, ReadOnlySpan<float> actual)
    {
        Assert.Equal(expected.Length, actual.Length);

        for (int i = 0; i < expected.Length; i++)
        {
            if (float.IsNaN(expected[i]))
            {
                Assert.True(float.IsNaN(actual[i]));
            }
            else
            {
                Assert.Equal(BitConverter.SingleToInt32Bits(expected[i]), BitConverter.SingleToInt32Bits(actual[i]));
            }
        }
    }

    /// <summary>
    /// Compares double-precision results while preserving signed-zero behavior.
    /// </summary>
    /// <param name="expected">The expected values.</param>
    /// <param name="actual">The actual values.</param>
    private static void AssertDoubleBitsEqual(ReadOnlySpan<double> expected, ReadOnlySpan<double> actual)
    {
        Assert.Equal(expected.Length, actual.Length);

        for (int i = 0; i < expected.Length; i++)
        {
            if (double.IsNaN(expected[i]))
            {
                Assert.True(double.IsNaN(actual[i]));
            }
            else
            {
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i]), BitConverter.DoubleToInt64Bits(actual[i]));
            }
        }
    }
}
