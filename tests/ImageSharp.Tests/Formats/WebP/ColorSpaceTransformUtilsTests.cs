// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Webp.Lossless;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Webp;

[Trait("Format", "Webp")]
public class ColorSpaceTransformUtilsTests
{
    private static void RunCollectColorBlueTransformsTest()
    {
        uint[] pixelData =
        {
            3074, 256, 256, 256, 0, 65280, 65280, 65280, 256, 256, 0, 256, 0, 65280, 0, 65280, 16711680, 256,
            256, 0, 65024, 0, 256, 256, 0, 65280, 0, 65280, 0, 256, 0, 256
        };

        int[] expectedOutput =
        {
            31, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
        };

        int[] histo = new int[256];
        ColorSpaceTransformUtils.CollectColorBlueTransforms(pixelData, 0, 32, 1, 0, 0, histo);

        Assert.Equal(expectedOutput, histo);
    }

    private static void RunCollectColorRedTransformsTest()
    {
        uint[] pixelData =
        {
            3074, 256, 256, 256, 0, 65280, 65280, 65280, 256, 256, 0, 256, 0, 65280, 0, 65280, 16711680, 256,
            256, 0, 65024, 0, 256, 256, 0, 65280, 0, 65280, 0, 256, 0, 256
        };

        int[] expectedOutput =
        {
            31, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1
        };

        int[] histo = new int[256];
        ColorSpaceTransformUtils.CollectColorRedTransforms(pixelData, 0, 32, 1, 0, histo);

        Assert.Equal(expectedOutput, histo);
    }

    [Fact]
    public void CollectColorBlueTransforms_Works() => RunCollectColorBlueTransformsTest();

    [Fact]
    public void CollectColorRedTransforms_Works() => RunCollectColorRedTransformsTest();

    [Fact]
    public void CollectColorBlueTransforms_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunCollectColorBlueTransformsTest, HwIntrinsics.AllowAll);

    [Fact]
    public void CollectColorBlueTransforms_WithoutVector128_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunCollectColorBlueTransformsTest, HwIntrinsics.DisableSSE41);

    [Fact]
    public void CollectColorBlueTransforms_WithoutVector256_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunCollectColorBlueTransformsTest, HwIntrinsics.DisableAVX2);

    [Fact]
    public void CollectColorRedTransforms_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunCollectColorRedTransformsTest, HwIntrinsics.AllowAll);

    [Fact]
    public void CollectColorRedTransforms_WithoutVector128_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunCollectColorRedTransformsTest, HwIntrinsics.DisableSSE41);

    [Fact]
    public void CollectColorRedTransforms_WithoutVector256_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunCollectColorRedTransformsTest, HwIntrinsics.DisableAVX2);
}
