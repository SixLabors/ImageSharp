// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Linq;
using SixLabors.ImageSharp.Formats.Webp.Lossy;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.WebP
{
    [Trait("Format", "Webp")]
    public class Vp8EncodingTests
    {
        private static void RunInverseTransformTest()
        {
            // arrange
            byte[] reference =
            {
                128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 129, 129, 129, 129,
                129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 128, 128, 128, 128, 128, 128, 128, 128,
                128, 128, 128, 128, 128, 128, 128, 128, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129,
                129, 129, 129, 129, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128,
                129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 128, 128, 128, 128,
                128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 129, 129, 129, 129, 129, 129, 129, 129,
                129, 129, 129, 129, 129, 129, 129, 129
            };
            short[] input = { 177, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 177, -24, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            byte[] dst = new byte[128];
            byte[] expected =
            {
                150, 150, 150, 150, 146, 149, 152, 154, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 150, 150, 150, 150, 146, 149, 152, 154, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 150, 150, 150, 150, 146, 149, 152, 154, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 150, 150, 150, 150, 146, 149, 152, 154, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
            };
            int[] scratch = new int[16];

            // act
            Vp8Encoding.ITransform(reference, input, dst, true, scratch);

            // assert
            Assert.True(dst.SequenceEqual(expected));
        }

        [Fact]
        public void InverseTransform_Works() => RunInverseTransformTest();

#if SUPPORTS_RUNTIME_INTRINSICS
        [Fact]
        public void InverseTransform_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunInverseTransformTest, HwIntrinsics.AllowAll);

        [Fact]
        public void InverseTransform_WithoutHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunInverseTransformTest, HwIntrinsics.DisableHWIntrinsic);
#endif
    }
}
