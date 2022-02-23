// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Png.Filters;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Png
{
    public class PngDecoderFilterTests
    {
        private static void RunAverageFilterTest()
        {
            // arrange
            byte[] scanline =
            {
                3, 39, 39, 39, 0, 4, 4, 4, 0, 1, 1, 1, 0, 1, 1, 1, 0, 2, 2, 2, 0, 2, 2, 2, 0, 2, 2, 2, 0, 4, 4, 4,
                0, 2, 2, 2, 0, 3, 3, 3, 0, 1, 1, 1, 0, 3, 3, 3, 0, 3, 3, 3, 0, 1, 1, 1, 0, 3, 3, 3, 0, 2, 2, 2, 0,
                1, 1, 1, 0, 3, 3, 3, 0, 1, 1, 1, 0, 3, 3, 3, 0, 1, 1, 1, 0, 3, 3, 3, 0, 3, 3, 3, 0, 254, 254, 254,
                0, 6, 6, 6, 14, 71, 71, 71, 157, 254, 254, 254, 28, 251, 251, 251, 0, 4, 4, 4, 0, 2, 2, 2, 0, 11,
                11, 11, 0, 226, 226, 226, 0, 255
            };

            byte[] previousScanline =
            {
                3, 74, 74, 74, 0, 73, 73, 73, 0, 73, 73, 73, 0, 74, 74, 74, 0, 74, 74, 74, 0, 73, 73, 73, 0, 72, 72,
                72, 0, 72, 72, 72, 0, 73, 73, 73, 0, 74, 74, 74, 0, 73, 73, 73, 0, 72, 72, 72, 0, 72, 72, 72, 0, 74,
                74, 74, 0, 72, 72, 72, 0, 73, 73, 73, 0, 75, 75, 75, 0, 73, 73, 73, 0, 74, 74, 74, 0, 72, 72, 72, 0,
                73, 73, 73, 0, 73, 73, 73, 0, 72, 72, 72, 0, 74, 74, 74, 0, 61, 61, 61, 0, 101, 101, 101, 78, 197,
                197, 197, 251, 152, 152, 152, 255, 155, 155, 155, 255, 162, 162, 162, 255, 175, 175, 175, 255, 160,
                160, 160, 255, 139
            };

            byte[] expected =
            {
                3, 76, 76, 76, 0, 78, 78, 78, 0, 76, 76, 76, 0, 76, 76, 76, 0, 77, 77, 77, 0, 77, 77, 77, 0, 76, 76,
                76, 0, 78, 78, 78, 0, 77, 77, 77, 0, 78, 78, 78, 0, 76, 76, 76, 0, 77, 77, 77, 0, 77, 77, 77, 0, 76,
                76, 76, 0, 77, 77, 77, 0, 77, 77, 77, 0, 77, 77, 77, 0, 78, 78, 78, 0, 77, 77, 77, 0, 77, 77, 77, 0,
                76, 76, 76, 0, 77, 77, 77, 0, 77, 77, 77, 0, 73, 73, 73, 0, 73, 73, 73, 14, 158, 158, 158, 203, 175,
                175, 175, 255, 158, 158, 158, 255, 160, 160, 160, 255, 163, 163, 163, 255, 180, 180, 180, 255, 140,
                140, 140, 255, 255
            };

            // act
            AverageFilter.Decode(scanline, previousScanline, 4);

            // assert
            Assert.Equal(scanline, expected);
        }

        [Fact]
        public void AverageInverse_Works() => RunAverageFilterTest();

#if SUPPORTS_RUNTIME_INTRINSICS
        [Fact]
        public void AverageInverse_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunAverageFilterTest, HwIntrinsics.AllowAll);

        [Fact]
        public void AverageInverse__WithoutSSE2_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunAverageFilterTest, HwIntrinsics.DisableSSE2);
#endif
    }
}
