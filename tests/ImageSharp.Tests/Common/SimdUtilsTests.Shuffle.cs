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
                SimdUtils.Shuffle.XYZW,
                SimdUtils.Shuffle.ZYXW
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
                        TestShuffle(
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

        private static void TestShuffle(
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
    }
}
