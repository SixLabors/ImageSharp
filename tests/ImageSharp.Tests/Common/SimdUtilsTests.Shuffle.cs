// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Common
{
    public partial class SimdUtilsTests
    {
        public static readonly TheoryData<byte> ShuffleControls =
            new TheoryData<byte>
            {
                SimdUtils.Shuffle.WXYZ,
                SimdUtils.Shuffle.WZYX,
                SimdUtils.Shuffle.XYZW,
                SimdUtils.Shuffle.YZWX,
                SimdUtils.Shuffle.ZYXW,
                SimdUtils.Shuffle.MmShuffle(2, 1, 3, 0),
                SimdUtils.Shuffle.MmShuffle(1, 1, 1, 1),
                SimdUtils.Shuffle.MmShuffle(3, 3, 3, 3)
            };

        [Theory]
        [MemberData(nameof(ShuffleControls))]
        public void BulkShuffleFloat4Channel(byte control)
        {
            static void RunTest(string serialized)
            {
                byte ctrl = FeatureTestRunner.Deserialize<byte>(serialized);
                foreach (var item in ArraySizesDivisibleBy4)
                {
                    foreach (var count in item)
                    {
                        TestShuffleFloat4Channel(
                            (int)count,
                            (s, d) => SimdUtils.Shuffle4Channel(s.Span, d.Span, ctrl),
                            ctrl);
                    }
                }
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                control,
                HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX | HwIntrinsics.DisableSSE);
        }

        [Theory]
        [MemberData(nameof(ShuffleControls))]
        public void BulkShuffleByte4Channel(byte control)
        {
            static void RunTest(string serialized)
            {
                byte ctrl = FeatureTestRunner.Deserialize<byte>(serialized);
                foreach (var item in ArraySizesDivisibleBy4)
                {
                    foreach (var count in item)
                    {
                        TestShuffleByte4Channel(
                            (int)count,
                            (s, d) => SimdUtils.Shuffle4Channel(s.Span, d.Span, ctrl),
                            ctrl);
                    }
                }
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                control,
                HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2 | HwIntrinsics.DisableSSE);
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
    }
}
