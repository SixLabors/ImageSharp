// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Linq;
using SixLabors.ImageSharp.Formats.Webp.Lossy;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.WebP
{
    [Trait("Format", "Webp")]
    public class QuantEncTests
    {
        private static void RunQuantizeBlockTest()
        {
            // arrange
            short[] input = { 378, 777, -851, 888, 259, 148, 0, -111, -185, -185, -74, -37, 148, 74, 111, 74 };
            short[] output = new short[16];
            ushort[] q = { 42, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37 };
            ushort[] iq = { 3120, 3542, 3542, 3542, 3542, 3542, 3542, 3542, 3542, 3542, 3542, 3542, 3542, 3542, 3542, 3542 };
            uint[] bias =
            {
                49152, 55296, 55296, 55296, 55296, 55296, 55296, 55296, 55296, 55296, 55296, 55296, 55296, 55296,
                55296, 55296
            };
            uint[] zthresh = { 26, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21 };
            short[] expectedOutput = { 9, 21, 7, -5, 4, -23, 24, 0, -5, 4, 2, -2, -3, -1, 3, 2 };
            int expectedResult = 1;
            var vp8Matrix = new Vp8Matrix(q, iq, bias, zthresh, new short[16]);

            // act
            int actualResult = QuantEnc.QuantizeBlock(input, output, vp8Matrix);

            // assert
            Assert.True(output.SequenceEqual(expectedOutput));
            Assert.Equal(expectedResult, actualResult);
        }

        [Fact]
        public void QuantizeBlock_Works() => RunQuantizeBlockTest();

#if SUPPORTS_RUNTIME_INTRINSICS
        [Fact]
        public void QuantizeBlock_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunQuantizeBlockTest, HwIntrinsics.AllowAll);

        [Fact]
        public void QuantizeBlock_WithoutSSE2_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunQuantizeBlockTest, HwIntrinsics.DisableSSE2);

        [Fact]
        public void QuantizeBlock_WithoutSSSE3_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunQuantizeBlockTest, HwIntrinsics.DisableSSSE3);

        [Fact]
        public void QuantizeBlock_WithoutSSE2AndSSSE3_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunQuantizeBlockTest, HwIntrinsics.DisableSSE2 | HwIntrinsics.DisableSSSE3);
#endif
    }
}
