// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Common
{
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
                byte control = default(WZYXShuffle4).Control;

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
                    wxyz.Control);

                WZYXShuffle4 wzyx = default;
                TestShuffleByte4Channel(
                    size,
                    (s, d) => SimdUtils.Shuffle4(s.Span, d.Span, wzyx),
                    wzyx.Control);

                YZWXShuffle4 yzwx = default;
                TestShuffleByte4Channel(
                    size,
                    (s, d) => SimdUtils.Shuffle4(s.Span, d.Span, yzwx),
                    yzwx.Control);

                ZYXWShuffle4 zyxw = default;
                TestShuffleByte4Channel(
                    size,
                    (s, d) => SimdUtils.Shuffle4(s.Span, d.Span, zyxw),
                    zyxw.Control);

                var xwyz = new DefaultShuffle4(2, 1, 3, 0);
                TestShuffleByte4Channel(
                    size,
                    (s, d) => SimdUtils.Shuffle4(s.Span, d.Span, xwyz),
                    xwyz.Control);

                var yyyy = new DefaultShuffle4(1, 1, 1, 1);
                TestShuffleByte4Channel(
                    size,
                    (s, d) => SimdUtils.Shuffle4(s.Span, d.Span, yyyy),
                    yyyy.Control);

                var wwww = new DefaultShuffle4(3, 3, 3, 3);
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
                var zyx = new DefaultShuffle3(0, 1, 2);
                TestShuffleByte3Channel(
                    size,
                    (s, d) => SimdUtils.Shuffle3(s.Span, d.Span, zyx),
                    zyx.Control);

                var xyz = new DefaultShuffle3(2, 1, 0);
                TestShuffleByte3Channel(
                    size,
                    (s, d) => SimdUtils.Shuffle3(s.Span, d.Span, xyz),
                    xyz.Control);

                var yyy = new DefaultShuffle3(1, 1, 1);
                TestShuffleByte3Channel(
                    size,
                    (s, d) => SimdUtils.Shuffle3(s.Span, d.Span, yyy),
                    yyy.Control);

                var zzz = new DefaultShuffle3(2, 2, 2);
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
                    xyzw.Control);

                var xwyz = new DefaultPad3Shuffle4(2, 1, 3, 0);
                TestPad3Shuffle4Channel(
                    size,
                    (s, d) => SimdUtils.Pad3Shuffle4(s.Span, d.Span, xwyz),
                    xwyz.Control);

                var yyyy = new DefaultPad3Shuffle4(1, 1, 1, 1);
                TestPad3Shuffle4Channel(
                    size,
                    (s, d) => SimdUtils.Pad3Shuffle4(s.Span, d.Span, yyyy),
                    yyyy.Control);

                var wwww = new DefaultPad3Shuffle4(3, 3, 3, 3);
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
                    xyzw.Control);

                var xwyz = new DefaultShuffle4Slice3(2, 1, 3, 0);
                TestShuffle4Slice3Channel(
                    size,
                    (s, d) => SimdUtils.Shuffle4Slice3(s.Span, d.Span, xwyz),
                    xwyz.Control);

                var yyyy = new DefaultShuffle4Slice3(1, 1, 1, 1);
                TestShuffle4Slice3Channel(
                    size,
                    (s, d) => SimdUtils.Shuffle4Slice3(s.Span, d.Span, yyyy),
                    yyyy.Control);

                var wwww = new DefaultShuffle4Slice3(3, 3, 3, 3);
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
            var result = new float[count];

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
            var result = new byte[count];

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
            var result = new byte[count];

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

            var result = new byte[count * 4 / 3];

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

            var result = new byte[count * 3 / 4];

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
}
