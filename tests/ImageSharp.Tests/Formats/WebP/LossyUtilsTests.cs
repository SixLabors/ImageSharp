// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Webp.Lossy;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.WebP
{
    [Trait("Format", "Webp")]
    public class LossyUtilsTests
    {
        private static void RunHadamardTransformTest()
        {
            byte[] a =
            {
                27, 27, 28, 29, 29, 28, 27, 27, 27, 28, 28, 29, 29, 28, 28, 27, 129, 129, 129, 129, 129, 129, 129,
                129, 128, 128, 128, 128, 128, 128, 128, 128, 27, 27, 27, 27, 27, 27, 27, 27, 27, 28, 28, 29, 29, 28,
                28, 27, 129, 129, 129, 129, 129, 129, 129, 129, 128, 128, 128, 128, 128, 128, 128, 128, 27, 27, 26,
                26, 26, 26, 27, 27, 27, 28, 28, 29, 29, 28, 28, 27, 129, 129, 129, 129, 129, 129, 129, 129, 128,
                128, 128, 128, 128, 128, 128, 128, 28, 27, 27, 26, 26, 27, 27, 28, 27, 28, 28, 29, 29, 28, 28, 27
            };

            byte[] b =
            {
                28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 204, 204, 204, 204, 204, 204, 204,
                204, 204, 204, 204, 204, 204, 204, 204, 204, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28,
                28, 28, 204, 204, 204, 204, 204, 204, 204, 204, 204, 204, 204, 204, 204, 204, 204, 204, 28, 28, 28,
                28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 204, 204, 204, 204, 204, 204, 204, 204, 204,
                204, 204, 204, 204, 204, 204, 204, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28
            };

            ushort[] w = { 38, 32, 20, 9, 32, 28, 17, 7, 20, 17, 10, 4, 9, 7, 4, 2 };
            int expected = 2;

            int actual = LossyUtils.Vp8Disto4X4(a, b, w, new int[16]);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HadamardTransform_Works() => RunHadamardTransformTest();

#if SUPPORTS_RUNTIME_INTRINSICS
        [Fact]
        public void HadamardTransform_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunHadamardTransformTest, HwIntrinsics.AllowAll);

        [Fact]
        public void HadamardTransform_WithoutHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunHadamardTransformTest, HwIntrinsics.DisableHWIntrinsic);
#endif

    }
}
