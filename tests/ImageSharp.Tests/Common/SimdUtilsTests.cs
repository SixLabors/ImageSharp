// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Common;

public partial class SimdUtilsTests
{
    private ITestOutputHelper Output { get; }

    public SimdUtilsTests(ITestOutputHelper output) => this.Output = output;

    private static int R(float f) => (int)Math.Round(f, MidpointRounding.AwayFromZero);

    private static int Re(float f) => (int)Math.Round(f, MidpointRounding.ToEven);

    // TODO: Move this to a proper test class!
    [Theory]
    [InlineData(0.32, 54.5, -3.5, -4.1)]
    [InlineData(5.3, 536.4, 4.5, 8.1)]
    public void PseudoRound(float x, float y, float z, float w)
    {
        Vector4 v = new(x, y, z, w);

        Vector4 actual = v.PseudoRound();

        Assert.Equal(R(v.X), (int)actual.X);
        Assert.Equal(R(v.Y), (int)actual.Y);
        Assert.Equal(R(v.Z), (int)actual.Z);
        Assert.Equal(R(v.W), (int)actual.W);
    }

    private static Vector<float> CreateExactTestVector1()
    {
        float[] data = new float[Vector<float>.Count];

        data[0] = 0.1f;
        data[1] = 0.4f;
        data[2] = 0.5f;
        data[3] = 0.9f;

        for (int i = 4; i < Vector<float>.Count; i++)
        {
            data[i] = data[i - 4] + 100f;
        }

        return new Vector<float>(data);
    }

    private static Vector<float> CreateRandomTestVector(int seed, float min, float max)
    {
        float[] data = new float[Vector<float>.Count];

        Random rnd = new(seed);

        for (int i = 0; i < Vector<float>.Count; i++)
        {
            float v = ((float)rnd.NextDouble() * (max - min)) + min;
            data[i] = v;
        }

        return new Vector<float>(data);
    }

    [Fact]
    public void FastRound()
    {
        Vector<float> v = CreateExactTestVector1();
        Vector<float> r = v.FastRound();

        this.Output.WriteLine(r.ToString());

        AssertEvenRoundIsCorrect(r, v);
    }

    [Theory]
    [InlineData(1, 1f)]
    [InlineData(1, 10f)]
    [InlineData(1, 1000f)]
    [InlineData(42, 1f)]
    [InlineData(42, 10f)]
    [InlineData(42, 1000f)]
    public void FastRound_RandomValues(int seed, float scale)
    {
        Vector<float> v = CreateRandomTestVector(seed, -scale * 0.5f, scale * 0.5f);
        Vector<float> r = v.FastRound();

        this.Output.WriteLine(v.ToString());
        this.Output.WriteLine(r.ToString());

        AssertEvenRoundIsCorrect(r, v);
    }

    private bool SkipOnNonAvx2([CallerMemberName] string testCaseName = null)
    {
        if (!SimdUtils.HasVector8)
        {
            this.Output.WriteLine("Skipping AVX2 specific test case: " + testCaseName);
            return true;
        }

        return false;
    }

    public static readonly TheoryData<int> ArraySizesDivisibleBy8 = new() { 0, 8, 16, 1024 };
    public static readonly TheoryData<int> ArraySizesDivisibleBy4 = new() { 0, 4, 8, 28, 1020 };
    public static readonly TheoryData<int> ArraySizesDivisibleBy3 = new() { 0, 3, 9, 36, 957 };
    public static readonly TheoryData<int> ArraySizesDivisibleBy32 = new() { 0, 32, 512 };
    public static readonly TheoryData<int> ArraySizesDivisibleBy64 = new() { 0, 64, 512 };

    public static readonly TheoryData<int> ArbitraryArraySizes = new() { 0, 1, 2, 3, 4, 7, 8, 9, 15, 16, 17, 63, 64, 255, 511, 512, 513, 514, 515, 516, 517, 518, 519, 520 };

    [Theory]
    [MemberData(nameof(ArraySizesDivisibleBy64))]
    public void HwIntrinsics_BulkConvertByteToNormalizedFloat(int count)
    {
        if (!Sse2.IsSupported && !AdvSimd.IsSupported)
        {
            return;
        }

        static void RunTest(string serialized) => TestImpl_BulkConvertByteToNormalizedFloat(
                FeatureTestRunner.Deserialize<int>(serialized),
                (s, d) => SimdUtils.HwIntrinsics.ByteToNormalizedFloat(s.Span, d.Span));

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            count,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX2 | HwIntrinsics.DisableSSE41);
    }

    [Theory]
    [MemberData(nameof(ArbitraryArraySizes))]
    public void BulkConvertByteToNormalizedFloat(int count) => TestImpl_BulkConvertByteToNormalizedFloat(
            count,
            (s, d) => SimdUtils.ByteToNormalizedFloat(s.Span, d.Span));

    private static void TestImpl_BulkConvertByteToNormalizedFloat(
        int count,
        Action<Memory<byte>, Memory<float>> convert)
    {
        byte[] source = new Random(count).GenerateRandomByteArray(count);
        float[] result = new float[count];
        float[] expected = source.Select(b => b / 255f).ToArray();

        convert(source, result);

        Assert.Equal(expected, result, new ApproximateFloatComparer(1e-5f));
    }

    [Theory]
    [MemberData(nameof(ArraySizesDivisibleBy64))]
    public void HwIntrinsics_BulkConvertNormalizedFloatToByteClampOverflows(int count)
    {
        if (!Sse2.IsSupported && !AdvSimd.IsSupported)
        {
            return;
        }

        static void RunTest(string serialized) => TestImpl_BulkConvertNormalizedFloatToByteClampOverflows(
                FeatureTestRunner.Deserialize<int>(serialized),
                (s, d) => SimdUtils.HwIntrinsics.NormalizedFloatToByteSaturate(s.Span, d.Span));

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            count,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512BW | HwIntrinsics.DisableAVX2);
    }

    [Theory]
    [MemberData(nameof(ArbitraryArraySizes))]
    public void BulkConvertNormalizedFloatToByteClampOverflows(int count)
    {
        TestImpl_BulkConvertNormalizedFloatToByteClampOverflows(count, (s, d) => SimdUtils.NormalizedFloatToByteSaturate(s.Span, d.Span));

        // For small values, let's stress test the implementation a bit:
        if (count is > 0 and < 10)
        {
            for (int i = 0; i < 20; i++)
            {
                TestImpl_BulkConvertNormalizedFloatToByteClampOverflows(
                    count,
                    (s, d) => SimdUtils.NormalizedFloatToByteSaturate(s.Span, d.Span),
                    i + 42);
            }
        }
    }

    [Theory]
    [MemberData(nameof(ArbitraryArraySizes))]
    public void PackFromRgbPlanes_Rgb24(int count) => TestPackFromRgbPlanes<Rgb24>(
            count,
            (r, g, b, actual) =>
                SimdUtils.PackFromRgbPlanes(r, g, b, actual));

    [Theory]
    [MemberData(nameof(ArbitraryArraySizes))]
    public void PackFromRgbPlanes_Rgba32(int count) => TestPackFromRgbPlanes<Rgba32>(
            count,
            (r, g, b, actual) =>
                SimdUtils.PackFromRgbPlanes(r, g, b, actual));

    [Fact]
    public void PackFromRgbPlanesAvx2Reduce_Rgb24()
    {
        if (!Avx2.IsSupported)
        {
            return;
        }

        byte[] r = Enumerable.Range(0, 32).Select(x => (byte)x).ToArray();
        byte[] g = Enumerable.Range(100, 32).Select(x => (byte)x).ToArray();
        byte[] b = Enumerable.Range(200, 32).Select(x => (byte)x).ToArray();
        const int padding = 4;
        Rgb24[] d = new Rgb24[32 + padding];

        ReadOnlySpan<byte> rr = r.AsSpan();
        ReadOnlySpan<byte> gg = g.AsSpan();
        ReadOnlySpan<byte> bb = b.AsSpan();
        Span<Rgb24> dd = d.AsSpan();

        SimdUtils.HwIntrinsics.PackFromRgbPlanesAvx2Reduce(ref rr, ref gg, ref bb, ref dd);

        for (int i = 0; i < 32; i++)
        {
            Assert.Equal(i, d[i].R);
            Assert.Equal(i + 100, d[i].G);
            Assert.Equal(i + 200, d[i].B);
        }

        Assert.Equal(0, rr.Length);
        Assert.Equal(0, gg.Length);
        Assert.Equal(0, bb.Length);
        Assert.Equal(padding, dd.Length);
    }

    [Fact]
    public void PackFromRgbPlanesAvx2Reduce_Rgba32()
    {
        if (!Avx2.IsSupported)
        {
            return;
        }

        byte[] r = Enumerable.Range(0, 32).Select(x => (byte)x).ToArray();
        byte[] g = Enumerable.Range(100, 32).Select(x => (byte)x).ToArray();
        byte[] b = Enumerable.Range(200, 32).Select(x => (byte)x).ToArray();

        Rgba32[] d = new Rgba32[32];

        ReadOnlySpan<byte> rr = r.AsSpan();
        ReadOnlySpan<byte> gg = g.AsSpan();
        ReadOnlySpan<byte> bb = b.AsSpan();
        Span<Rgba32> dd = d.AsSpan();

        SimdUtils.HwIntrinsics.PackFromRgbPlanesAvx2Reduce(ref rr, ref gg, ref bb, ref dd);

        for (int i = 0; i < 32; i++)
        {
            Assert.Equal(i, d[i].R);
            Assert.Equal(i + 100, d[i].G);
            Assert.Equal(i + 200, d[i].B);
            Assert.Equal(255, d[i].A);
        }

        Assert.Equal(0, rr.Length);
        Assert.Equal(0, gg.Length);
        Assert.Equal(0, bb.Length);
        Assert.Equal(0, dd.Length);
    }

    internal static void TestPackFromRgbPlanes<TPixel>(int count, Action<byte[], byte[], byte[], TPixel[]> packMethod)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Random rnd = new(42);
        byte[] r = rnd.GenerateRandomByteArray(count);
        byte[] g = rnd.GenerateRandomByteArray(count);
        byte[] b = rnd.GenerateRandomByteArray(count);

        TPixel[] expected = new TPixel[count];
        for (int i = 0; i < count; i++)
        {
            expected[i] = TPixel.FromRgb24(new Rgb24(r[i], g[i], b[i]));
        }

        TPixel[] actual = new TPixel[count + 3]; // padding for Rgb24 AVX2
        packMethod(r, g, b, actual);

        Assert.True(expected.AsSpan().SequenceEqual(actual.AsSpan()[..count]));
    }

    private static void TestImpl_BulkConvertNormalizedFloatToByteClampOverflows(
        int count,
        Action<Memory<float>,
        Memory<byte>> convert,
        int seed = -1)
    {
        seed = seed > 0 ? seed : count;
        float[] source = new Random(seed).GenerateRandomFloatArray(count, -0.2f, 1.2f);
        byte[] expected = source.Select(NormalizedFloatToByte).ToArray();
        byte[] actual = new byte[count];

        convert(source, actual);

        Assert.Equal(expected, actual);
    }

    private static byte NormalizedFloatToByte(float f) => (byte)Math.Min(255f, Math.Max(0f, (f * 255f) + 0.5f));

    private static void AssertEvenRoundIsCorrect(Vector<float> r, Vector<float> v)
    {
        for (int i = 0; i < Vector<float>.Count; i++)
        {
            int actual = (int)r[i];
            int expected = Re(v[i]);

            Assert.Equal(expected, actual);
        }
    }
}
