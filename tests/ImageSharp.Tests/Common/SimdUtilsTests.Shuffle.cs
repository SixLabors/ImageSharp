// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Common;

public partial class SimdUtilsTests
{
    [Theory]
    [MemberData(nameof(ArraySizesDivisibleBy4))]
    public void BulkShuffleFloat4Channel(int count)
    {
        static void RunTest(string serialized)
        {
            // No need to test multiple shuffle controls as the
            // pipeline is always the same.
            int size = FeatureTestRunner.Deserialize<int>(serialized);
            const byte control = SimdUtils.Shuffle.MMShuffle0123;

            TestShuffleFloat4Channel(
                size,
                (s, d) => SimdUtils.Shuffle4(s.Span, d.Span, control),
                control);
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            count,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX | HwIntrinsics.DisableSSE);
    }

    [Theory]
    [MemberData(nameof(ArraySizesDivisibleBy4))]
    public void BulkShuffleByte4Channel(int count)
    {
        static void RunTest(string serialized)
        {
            int size = FeatureTestRunner.Deserialize<int>(serialized);

            // These cannot be expressed as a theory as you cannot
            // use RemoteExecutor within generic methods nor pass
            // IShuffle4 to the generic utils method.
            WXYZShuffle4 wxyz = default;
            TestShuffleByte4Channel(
                size,
                (s, d) => SimdUtils.Shuffle4(s.Span, d.Span, wxyz),
                SimdUtils.Shuffle.MMShuffle2103);

            WZYXShuffle4 wzyx = default;
            TestShuffleByte4Channel(
                size,
                (s, d) => SimdUtils.Shuffle4(s.Span, d.Span, wzyx),
                SimdUtils.Shuffle.MMShuffle0123);

            YZWXShuffle4 yzwx = default;
            TestShuffleByte4Channel(
                size,
                (s, d) => SimdUtils.Shuffle4(s.Span, d.Span, yzwx),
                SimdUtils.Shuffle.MMShuffle0321);

            ZYXWShuffle4 zyxw = default;
            TestShuffleByte4Channel(
                size,
                (s, d) => SimdUtils.Shuffle4(s.Span, d.Span, zyxw),
                SimdUtils.Shuffle.MMShuffle3012);

            DefaultShuffle4 xwyz = new(SimdUtils.Shuffle.MMShuffle2130);
            TestShuffleByte4Channel(
                size,
                (s, d) => SimdUtils.Shuffle4(s.Span, d.Span, xwyz),
                xwyz.Control);

            DefaultShuffle4 yyyy = new(SimdUtils.Shuffle.MMShuffle1111);
            TestShuffleByte4Channel(
                size,
                (s, d) => SimdUtils.Shuffle4(s.Span, d.Span, yyyy),
                yyyy.Control);

            DefaultShuffle4 wwww = new(SimdUtils.Shuffle.MMShuffle3333);
            TestShuffleByte4Channel(
                size,
                (s, d) => SimdUtils.Shuffle4(s.Span, d.Span, wwww),
                wwww.Control);
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            count,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2 | HwIntrinsics.DisableSSE);
    }

    [Theory]
    [MemberData(nameof(ArraySizesDivisibleBy3))]
    public void BulkShuffleByte3Channel(int count)
    {
        static void RunTest(string serialized)
        {
            int size = FeatureTestRunner.Deserialize<int>(serialized);

            // These cannot be expressed as a theory as you cannot
            // use RemoteExecutor within generic methods nor pass
            // IShuffle3 to the generic utils method.
            DefaultShuffle3 zyx = new(SimdUtils.Shuffle.MMShuffle3012);
            TestShuffleByte3Channel(
                size,
                (s, d) => SimdUtils.Shuffle3(s.Span, d.Span, zyx),
                zyx.Control);

            DefaultShuffle3 xyz = new(SimdUtils.Shuffle.MMShuffle3210);
            TestShuffleByte3Channel(
                size,
                (s, d) => SimdUtils.Shuffle3(s.Span, d.Span, xyz),
                xyz.Control);

            DefaultShuffle3 yyy = new(SimdUtils.Shuffle.MMShuffle3111);
            TestShuffleByte3Channel(
                size,
                (s, d) => SimdUtils.Shuffle3(s.Span, d.Span, yyy),
                yyy.Control);

            DefaultShuffle3 zzz = new(SimdUtils.Shuffle.MMShuffle3222);
            TestShuffleByte3Channel(
                size,
                (s, d) => SimdUtils.Shuffle3(s.Span, d.Span, zzz),
                zzz.Control);
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            count,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2 | HwIntrinsics.DisableSSE);
    }

    [Theory]
    [MemberData(nameof(ArraySizesDivisibleBy3))]
    public void BulkPad3Shuffle4Channel(int count)
    {
        static void RunTest(string serialized)
        {
            int size = FeatureTestRunner.Deserialize<int>(serialized);

            // These cannot be expressed as a theory as you cannot
            // use RemoteExecutor within generic methods nor pass
            // IPad3Shuffle4 to the generic utils method.
            XYZWPad3Shuffle4 xyzw = default;
            TestPad3Shuffle4Channel(
                size,
                (s, d) => SimdUtils.Pad3Shuffle4(s.Span, d.Span, xyzw),
                SimdUtils.Shuffle.MMShuffle3210);

            DefaultPad3Shuffle4 xwyz = new(SimdUtils.Shuffle.MMShuffle2130);
            TestPad3Shuffle4Channel(
                size,
                (s, d) => SimdUtils.Pad3Shuffle4(s.Span, d.Span, xwyz),
                xwyz.Control);

            DefaultPad3Shuffle4 yyyy = new(SimdUtils.Shuffle.MMShuffle1111);
            TestPad3Shuffle4Channel(
                size,
                (s, d) => SimdUtils.Pad3Shuffle4(s.Span, d.Span, yyyy),
                yyyy.Control);

            DefaultPad3Shuffle4 wwww = new(SimdUtils.Shuffle.MMShuffle3333);
            TestPad3Shuffle4Channel(
                size,
                (s, d) => SimdUtils.Pad3Shuffle4(s.Span, d.Span, wwww),
                wwww.Control);
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            count,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2 | HwIntrinsics.DisableSSE);
    }

    [Theory]
    [MemberData(nameof(ArraySizesDivisibleBy4))]
    public void BulkShuffle4Slice3Channel(int count)
    {
        static void RunTest(string serialized)
        {
            int size = FeatureTestRunner.Deserialize<int>(serialized);

            // These cannot be expressed as a theory as you cannot
            // use RemoteExecutor within generic methods nor pass
            // IShuffle4Slice3 to the generic utils method.
            XYZWShuffle4Slice3 xyzw = default;
            TestShuffle4Slice3Channel(
                size,
                (s, d) => SimdUtils.Shuffle4Slice3(s.Span, d.Span, xyzw),
                SimdUtils.Shuffle.MMShuffle3210);

            DefaultShuffle4Slice3 xwyz = new(SimdUtils.Shuffle.MMShuffle2130);
            TestShuffle4Slice3Channel(
                size,
                (s, d) => SimdUtils.Shuffle4Slice3(s.Span, d.Span, xwyz),
                xwyz.Control);

            DefaultShuffle4Slice3 yyyy = new(SimdUtils.Shuffle.MMShuffle1111);
            TestShuffle4Slice3Channel(
                size,
                (s, d) => SimdUtils.Shuffle4Slice3(s.Span, d.Span, yyyy),
                yyyy.Control);

            DefaultShuffle4Slice3 wwww = new(SimdUtils.Shuffle.MMShuffle3333);
            TestShuffle4Slice3Channel(
                size,
                (s, d) => SimdUtils.Shuffle4Slice3(s.Span, d.Span, wwww),
                wwww.Control);
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            count,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX | HwIntrinsics.DisableSSE);
    }

    private static void TestShuffleFloat4Channel(
        int count,
        Action<Memory<float>, Memory<float>> convert,
        byte control)
    {
        float[] source = new Random(count).GenerateRandomFloatArray(count, 0, 256);
        float[] result = new float[count];

        float[] expected = new float[count];

        SimdUtils.Shuffle.InverseMmShuffle(
            control,
            out int p3,
            out int p2,
            out int p1,
            out int p0);

        for (int i = 0; i < expected.Length; i += 4)
        {
            expected[i] = source[p0 + i];
            expected[i + 1] = source[p1 + i];
            expected[i + 2] = source[p2 + i];
            expected[i + 3] = source[p3 + i];
        }

        convert(source, result);

        Assert.Equal(expected, result, new ApproximateFloatComparer(1e-5F));
    }

    private static void TestShuffleByte4Channel(
        int count,
        Action<Memory<byte>, Memory<byte>> convert,
        byte control)
    {
        byte[] source = new byte[count];
        new Random(count).NextBytes(source);
        byte[] result = new byte[count];

        byte[] expected = new byte[count];

        SimdUtils.Shuffle.InverseMmShuffle(
            control,
            out int p3,
            out int p2,
            out int p1,
            out int p0);

        for (int i = 0; i < expected.Length; i += 4)
        {
            expected[i] = source[p0 + i];
            expected[i + 1] = source[p1 + i];
            expected[i + 2] = source[p2 + i];
            expected[i + 3] = source[p3 + i];
        }

        convert(source, result);

        Assert.Equal(expected, result);
    }

    private static void TestShuffleByte3Channel(
        int count,
        Action<Memory<byte>, Memory<byte>> convert,
        byte control)
    {
        byte[] source = new byte[count];
        new Random(count).NextBytes(source);
        byte[] result = new byte[count];

        byte[] expected = new byte[count];

        SimdUtils.Shuffle.InverseMmShuffle(
            control,
            out int _,
            out int p2,
            out int p1,
            out int p0);

        for (int i = 0; i < expected.Length; i += 3)
        {
            expected[i] = source[p0 + i];
            expected[i + 1] = source[p1 + i];
            expected[i + 2] = source[p2 + i];
        }

        convert(source, result);

        Assert.Equal(expected, result);
    }

    private static void TestPad3Shuffle4Channel(
        int count,
        Action<Memory<byte>, Memory<byte>> convert,
        byte control)
    {
        byte[] source = new byte[count];
        new Random(count).NextBytes(source);

        byte[] result = new byte[count * 4 / 3];

        byte[] expected = new byte[result.Length];

        SimdUtils.Shuffle.InverseMmShuffle(
            control,
            out int p3,
            out int p2,
            out int p1,
            out int p0);

        for (int i = 0, j = 0; i < expected.Length; i += 4, j += 3)
        {
            expected[p0 + i] = source[j];
            expected[p1 + i] = source[j + 1];
            expected[p2 + i] = source[j + 2];
            expected[p3 + i] = byte.MaxValue;
        }

        Span<byte> temp = stackalloc byte[4];
        for (int i = 0, j = 0; i < expected.Length; i += 4, j += 3)
        {
            temp[0] = source[j];
            temp[1] = source[j + 1];
            temp[2] = source[j + 2];
            temp[3] = byte.MaxValue;

            expected[i] = temp[p0];
            expected[i + 1] = temp[p1];
            expected[i + 2] = temp[p2];
            expected[i + 3] = temp[p3];
        }

        convert(source, result);

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], result[i]);
        }

        Assert.Equal(expected, result);
    }

    private static void TestShuffle4Slice3Channel(
        int count,
        Action<Memory<byte>, Memory<byte>> convert,
        byte control)
    {
        byte[] source = new byte[count];
        new Random(count).NextBytes(source);

        byte[] result = new byte[count * 3 / 4];

        byte[] expected = new byte[result.Length];

        SimdUtils.Shuffle.InverseMmShuffle(
            control,
            out int _,
            out int p2,
            out int p1,
            out int p0);

        for (int i = 0, j = 0; i < expected.Length; i += 3, j += 4)
        {
            expected[i] = source[p0 + j];
            expected[i + 1] = source[p1 + j];
            expected[i + 2] = source[p2 + j];
        }

        convert(source, result);

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], result[i]);
        }

        Assert.Equal(expected, result);
    }
}
