// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Linq;
using SixLabors.ImageSharp.Formats.Webp.Lossy;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.WebP
{
    [Trait("Format", "Webp")]
    public class LossyUtilsTests
    {
        private static void RunMean16x4Test()
        {
            // arrange
            byte[] input =
            {
                154, 145, 102, 115, 127, 129, 126, 125, 126, 120, 133, 152, 157, 153, 119, 94, 104, 116, 111, 113,
                113, 109, 105, 124, 173, 175, 177, 170, 175, 172, 166, 164, 151, 141, 99, 114, 125, 126, 135, 150,
                133, 115, 127, 149, 141, 168, 100, 54, 110, 117, 115, 116, 119, 115, 117, 130, 174, 174, 174, 157,
                146, 171, 166, 158, 117, 140, 96, 111, 119, 119, 136, 171, 188, 134, 121, 126, 136, 119, 59, 77,
                109, 115, 113, 120, 120, 117, 128, 115, 174, 173, 173, 161, 152, 148, 153, 162, 105, 140, 96, 114,
                115, 122, 141, 173, 190, 190, 142, 106, 151, 78, 66, 141, 110, 117, 123, 136, 118, 124, 127, 114,
                173, 175, 166, 155, 155, 159, 159, 158
            };
            uint[] dc = new uint[4];
            ushort[] tmp = new ushort[8];
            uint[] expectedDc = { 1940, 2139, 2252, 1813 };

            // act
            LossyUtils.Mean16x4(input, dc, tmp);

            // assert
            Assert.True(dc.SequenceEqual(expectedDc));
        }

        [Fact]
        public void Mean16x4_Works() => RunMean16x4Test();

#if SUPPORTS_RUNTIME_INTRINSICS
        [Fact]
        public void Mean16x4_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunMean16x4Test, HwIntrinsics.AllowAll);

        [Fact]
        public void Mean16x4_WithoutSSE2_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunMean16x4Test, HwIntrinsics.DisableSSE2);
#endif
    }
}
