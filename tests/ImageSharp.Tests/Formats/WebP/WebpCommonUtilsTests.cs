// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
#if SUPPORTS_RUNTIME_INTRINSICS
using SixLabors.ImageSharp.Tests.TestUtilities;
#endif

namespace SixLabors.ImageSharp.Tests.Formats.Webp
{
    [Trait("Format", "Webp")]
    public class WebpCommonUtilsTests
    {
        [Fact]
        public void CheckNonOpaque_WithOpaquePixels_Works() => RunCheckNoneOpaqueWithOpaquePixelsTest();

        [Fact]
        public void CheckNonOpaque_WithNoneOpaquePixels_Works() => RunCheckNoneOpaqueWithNoneOpaquePixelsTest();

#if SUPPORTS_RUNTIME_INTRINSICS
        [Fact]
        public void CheckNonOpaque_WithOpaquePixels_WithHardwareIntrinsics_Works()
            => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunCheckNoneOpaqueWithOpaquePixelsTest, HwIntrinsics.AllowAll);

        [Fact]
        public void CheckNonOpaque_WithOpaquePixels_WithoutSse2_Works()
            => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunCheckNoneOpaqueWithOpaquePixelsTest, HwIntrinsics.DisableSSE2);

        [Fact]
        public void CheckNonOpaque_WithOpaquePixels_WithoutAvx2_Works()
            => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunCheckNoneOpaqueWithOpaquePixelsTest, HwIntrinsics.DisableAVX2);

        [Fact]
        public void CheckNonOpaque_WithNoneOpaquePixels_WithHardwareIntrinsics_Works()
            => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunCheckNoneOpaqueWithNoneOpaquePixelsTest, HwIntrinsics.AllowAll);

        [Fact]
        public void CheckNonOpaque_WithNoneOpaquePixels_WithoutSse2_Works()
            => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunCheckNoneOpaqueWithNoneOpaquePixelsTest, HwIntrinsics.DisableSSE2);

        [Fact]
        public void CheckNonOpaque_WithNoneOpaquePixels_WithoutAvx2_Works()
            => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunCheckNoneOpaqueWithNoneOpaquePixelsTest, HwIntrinsics.DisableAVX2);
#endif

        private static void RunCheckNoneOpaqueWithNoneOpaquePixelsTest()
        {
            // arrange
            byte[] rowBytes =
            {
                122, 120, 101, 255,
                171, 165, 151, 255,
                209, 208, 210, 255,
                174, 183, 189, 255,
                148, 158, 158, 255,
                122, 120, 101, 255,
                171, 165, 151, 255,
                209, 208, 210, 255,
                174, 183, 189, 255,
                148, 158, 158, 255,
                171, 165, 151, 255,
                209, 208, 210, 255,
                174, 183, 189, 255,
                148, 158, 158, 255,
                171, 165, 151, 255,
                209, 208, 210, 255,
                174, 183, 189, 255,
                148, 158, 158, 255,
                171, 165, 151, 255,
                209, 208, 210, 255,
                174, 183, 189, 255,
                148, 158, 158, 255,
                148, 158, 158, 10,
                171, 165, 151, 255,
                209, 208, 210, 255,
                171, 165, 151, 255,
                209, 208, 210, 255,
                174, 183, 189, 255,
                148, 158, 158, 255,
                171, 165, 151, 255,
                209, 208, 210, 255,
                174, 183, 189, 255,
                148, 158, 158, 10,
                171, 165, 151, 255,
                209, 208, 210, 255,
                174, 183, 189, 255,
                148, 158, 158, 255,
                171, 165, 151, 255,
                209, 208, 210, 255,
                174, 183, 189, 255,
                148, 158, 158, 255,
                209, 208, 210, 0,
                174, 183, 189, 255,
                148, 158, 158, 255,
                148, 158, 158, 255,
                171, 165, 151, 255,
                209, 208, 210, 255,
                174, 183, 189, 0,
                148, 158, 158, 255,
                148, 158, 158, 255,
                171, 165, 151, 255,
                209, 208, 210, 255,
                174, 183, 189, 255,
                148, 158, 158, 255,
                148, 158, 158, 100,
                171, 165, 151, 0,
                209, 208, 210, 100,
                174, 183, 189, 255,
                148, 158, 158, 255,
            };
            Span<Bgra32> row = MemoryMarshal.Cast<byte, Bgra32>(rowBytes);

            bool noneOpaque;
            for (int length = 8; length < row.Length; length += 8)
            {
                // act
                noneOpaque = WebpCommonUtils.CheckNonOpaque(row);

                // assert
                Assert.True(noneOpaque);
            }

            // One last test with the complete row.
            noneOpaque = WebpCommonUtils.CheckNonOpaque(row);
            Assert.True(noneOpaque);
        }

        private static void RunCheckNoneOpaqueWithOpaquePixelsTest()
        {
            // arrange
            byte[] rowBytes =
            {
                122, 120, 101, 255,
                171, 165, 151, 255,
                209, 208, 210, 255,
                174, 183, 189, 255,
                148, 158, 158, 255,
                122, 120, 101, 255,
                171, 165, 151, 255,
                209, 208, 210, 255,
                174, 183, 189, 255,
                148, 158, 158, 255,
                171, 165, 151, 255,
                209, 208, 210, 255,
                174, 183, 189, 255,
                148, 158, 158, 255,
                171, 165, 151, 255,
                209, 208, 210, 255,
                174, 183, 189, 255,
                148, 158, 158, 255,
                171, 165, 151, 255,
                209, 208, 210, 255,
                174, 183, 189, 255,
                148, 158, 158, 255,
                148, 158, 158, 255,
                171, 165, 151, 255,
                209, 208, 210, 255,
                171, 165, 151, 255,
                209, 208, 210, 255,
                174, 183, 189, 255,
                148, 158, 158, 255,
                171, 165, 151, 255,
                209, 208, 210, 255,
                174, 183, 189, 255,
                148, 158, 158, 255,
                171, 165, 151, 255,
                209, 208, 210, 255,
                174, 183, 189, 255,
                148, 158, 158, 255,
                171, 165, 151, 255,
                209, 208, 210, 255,
                174, 183, 189, 255,
                148, 158, 158, 255,
                209, 208, 210, 255,
                174, 183, 189, 255,
                148, 158, 158, 255,
                148, 158, 158, 255,
                171, 165, 151, 255,
                209, 208, 210, 255,
                174, 183, 189, 255,
                148, 158, 158, 255,
                148, 158, 158, 255,
                171, 165, 151, 255,
                209, 208, 210, 255,
                174, 183, 189, 255,
                148, 158, 158, 255,
                148, 158, 158, 255,
                171, 165, 151, 255,
                209, 208, 210, 255,
                174, 183, 189, 255,
                148, 158, 158, 255,
            };
            Span<Bgra32> row = MemoryMarshal.Cast<byte, Bgra32>(rowBytes);

            bool noneOpaque;
            for (int length = 8; length < row.Length; length += 8)
            {
                // act
                noneOpaque = WebpCommonUtils.CheckNonOpaque(row.Slice(0, length));

                // assert
                Assert.False(noneOpaque);
            }

            // One last test with the complete row.
            noneOpaque = WebpCommonUtils.CheckNonOpaque(row);
            Assert.False(noneOpaque);
        }
    }
}
