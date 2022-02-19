// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;
using SharpCrc32 = ICSharpCode.SharpZipLib.Checksum.Crc32;

namespace SixLabors.ImageSharp.Tests.Formats.Png
{
    [Trait("Format", "Png")]
    public class Crc32Tests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void CalculateCrc_ReturnsCorrectResultWhenEmpty(uint input) => Assert.Equal(input, Crc32.Calculate(input, default));

        [Theory]
        [InlineData(0)]
        [InlineData(8)]
        [InlineData(215)]
        [InlineData(1024)]
        [InlineData(1024 + 15)]
        [InlineData(2034)]
        [InlineData(4096)]
        public void CalculateCrc_MatchesReference(int length) => CalculateCrcAndCompareToReference(length);

        private static void CalculateCrcAndCompareToReference(int length)
        {
            // arrange
            byte[] data = GetBuffer(length);
            var crc = new SharpCrc32();
            crc.Update(data);
            long expected = crc.Value;

            // act
            long actual = Crc32.Calculate(data);

            // assert
            Assert.Equal(expected, actual);
        }

        private static byte[] GetBuffer(int length)
        {
            byte[] data = new byte[length];
            new Random(1).NextBytes(data);

            return data;
        }

#if SUPPORTS_RUNTIME_INTRINSICS
        [Fact]
        public void RunCalculateCrcTest_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunCalculateCrcTest, HwIntrinsics.AllowAll);

        [Fact]
        public void RunCalculateCrcTest_WithoutHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunCalculateCrcTest, HwIntrinsics.DisableHWIntrinsic);

        private static void RunCalculateCrcTest()
        {
            int[] testData = { 0, 8, 215, 1024, 1024 + 15, 2034, 4096 };
            for (int i = 0; i < testData.Length; i++)
            {
                CalculateCrcAndCompareToReference(testData[i]);
            }
        }
#endif
    }
}
