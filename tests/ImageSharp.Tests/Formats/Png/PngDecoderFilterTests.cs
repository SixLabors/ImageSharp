// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Png.Filters;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Png;

[Trait("Format", "Png")]
public class PngDecoderFilterTests
{
    private static void RunAverageFilterTest()
    {
        // arrange
        byte[] scanline =
        [
            3, 39, 39, 39, 0, 4, 4, 4, 0, 1, 1, 1, 0, 1, 1, 1, 0, 2, 2, 2, 0, 2, 2, 2, 0, 2, 2, 2, 0, 4, 4, 4,
            0, 2, 2, 2, 0, 3, 3, 3, 0, 1, 1, 1, 0, 3, 3, 3, 0, 3, 3, 3, 0, 1, 1, 1, 0, 3, 3, 3, 0, 2, 2, 2, 0,
            1, 1, 1, 0, 3, 3, 3, 0, 1, 1, 1, 0, 3, 3, 3, 0, 1, 1, 1, 0, 3, 3, 3, 0, 3, 3, 3, 0, 254, 254, 254,
            0, 6, 6, 6, 14, 71, 71, 71, 157, 254, 254, 254, 28, 251, 251, 251, 0, 4, 4, 4, 0, 2, 2, 2, 0, 11,
            11, 11, 0, 226, 226, 226, 0, 255, 128, 234
        ];

        byte[] previousScanline =
        [
            3, 74, 74, 74, 0, 73, 73, 73, 0, 73, 73, 73, 0, 74, 74, 74, 0, 74, 74, 74, 0, 73, 73, 73, 0, 72, 72,
            72, 0, 72, 72, 72, 0, 73, 73, 73, 0, 74, 74, 74, 0, 73, 73, 73, 0, 72, 72, 72, 0, 72, 72, 72, 0, 74,
            74, 74, 0, 72, 72, 72, 0, 73, 73, 73, 0, 75, 75, 75, 0, 73, 73, 73, 0, 74, 74, 74, 0, 72, 72, 72, 0,
            73, 73, 73, 0, 73, 73, 73, 0, 72, 72, 72, 0, 74, 74, 74, 0, 61, 61, 61, 0, 101, 101, 101, 78, 197,
            197, 197, 251, 152, 152, 152, 255, 155, 155, 155, 255, 162, 162, 162, 255, 175, 175, 175, 255, 160,
            160, 160, 255, 139, 128, 134
        ];

        byte[] expected =
        [
            3, 76, 76, 76, 0, 78, 78, 78, 0, 76, 76, 76, 0, 76, 76, 76, 0, 77, 77, 77, 0, 77, 77, 77, 0, 76, 76,
            76, 0, 78, 78, 78, 0, 77, 77, 77, 0, 78, 78, 78, 0, 76, 76, 76, 0, 77, 77, 77, 0, 77, 77, 77, 0, 76,
            76, 76, 0, 77, 77, 77, 0, 77, 77, 77, 0, 77, 77, 77, 0, 78, 78, 78, 0, 77, 77, 77, 0, 77, 77, 77, 0,
            76, 76, 76, 0, 77, 77, 77, 0, 77, 77, 77, 0, 73, 73, 73, 0, 73, 73, 73, 14, 158, 158, 158, 203, 175,
            175, 175, 255, 158, 158, 158, 255, 160, 160, 160, 255, 163, 163, 163, 255, 180, 180, 180, 255, 140,
            140, 140, 255, 138, 6, 115
        ];

        // act
        AverageFilter.Decode(scanline, previousScanline, 4);

        // assert
        Assert.Equal(expected, scanline);
    }

    private static void RunUpFilterTest()
    {
        // arrange
        byte[] scanline =
        [
            62, 23, 186, 150, 174, 4, 205, 59, 153, 134, 158, 86, 240, 173, 191, 58, 111, 183, 77, 37, 85, 23,
            93, 204, 110, 139, 9, 20, 87, 154, 176, 54, 207, 214, 40, 11, 179, 199, 7, 219, 174, 242, 112, 220,
            149, 5, 9, 110, 103, 107, 231, 241, 13, 70, 216, 39, 186, 237, 39, 34, 251, 185, 228, 254
        ];

        byte[] previousScanline =
        [
            214, 103, 135, 26, 133, 179, 134, 168, 175, 114, 118, 99, 167, 129, 55, 105, 129, 154, 173, 235,
            179, 191, 41, 137, 253, 0, 81, 198, 159, 228, 224, 245, 14, 113, 5, 45, 126, 239, 233, 179, 229, 62,
            66, 155, 207, 117, 128, 56, 181, 190, 160, 96, 11, 248, 74, 23, 62, 253, 29, 132, 98, 192, 9, 202
        ];

        byte[] expected =
        [
            62, 126, 65, 176, 51, 183, 83, 227, 72, 248, 20, 185, 151, 46, 246, 163, 240, 81, 250, 16, 8, 214,
            134, 85, 107, 139, 90, 218, 246, 126, 144, 43, 221, 71, 45, 56, 49, 182, 240, 142, 147, 48, 178,
            119, 100, 122, 137, 166, 28, 41, 135, 81, 24, 62, 34, 62, 248, 234, 68, 166, 93, 121, 237, 200
        ];

        // act
        UpFilter.Decode(scanline, previousScanline);

        // assert
        Assert.Equal(expected, scanline);
    }

    private static void RunSubFilterTest()
    {
        // arrange
        byte[] scanline =
        [
            62, 23, 186, 150, 174, 4, 205, 59, 153, 134, 158, 86, 240, 173, 191, 58, 111, 183, 77, 37, 85, 23,
            93, 204, 110, 139, 9, 20, 87, 154, 176, 54, 207, 214, 40, 11, 179, 199, 7, 219, 174, 242, 112, 220,
            149, 5, 9, 110, 103, 107, 231, 241, 13, 70, 216, 39, 186, 237, 39, 34, 251, 185, 228, 254
        ];

        byte[] expected =
        [
            62, 23, 186, 150, 174, 27, 135, 209, 71, 161, 37, 39, 55, 78, 228, 97, 166, 5, 49, 134, 251, 28,
            142, 82, 105, 167, 151, 102, 192, 65, 71, 156, 143, 23, 111, 167, 66, 222, 118, 130, 240, 208, 230,
            94, 133, 213, 239, 204, 236, 64, 214, 189, 249, 134, 174, 228, 179, 115, 213, 6, 174, 44, 185, 4
        ];

        // act
        SubFilter.Decode(scanline, 4);

        // assert
        Assert.Equal(expected, scanline);
    }

    private static void RunPaethFilterTest()
    {
        // arrange
        byte[] scanline =
        [
            62, 23, 186, 150, 174, 4, 205, 59, 153, 134, 158, 86, 240, 173, 191, 58, 111, 183, 77, 37, 85, 23,
            93, 204, 110, 139, 9, 20, 87, 154, 176, 54, 207, 214, 40, 11, 179, 199, 7, 219, 174, 242, 112, 220,
            149, 5, 9, 110, 103, 107, 231, 241, 13, 70, 216, 39, 186, 237, 39, 34, 251, 185, 228, 254
        ];

        byte[] previousScanline =
        [
            214, 103, 135, 26, 133, 179, 134, 168, 175, 114, 118, 99, 167, 129, 55, 105, 129, 154, 173, 235,
            179, 191, 41, 137, 253, 0, 81, 198, 159, 228, 224, 245, 14, 113, 5, 45, 126, 239, 233, 179, 229, 62,
            66, 155, 207, 117, 128, 56, 181, 190, 160, 96, 11, 248, 74, 23, 62, 253, 29, 132, 98, 192, 9, 202
        ];

        byte[] expected =
        [
            62, 126, 65, 176, 51, 183, 14, 235, 30, 248, 172, 254, 14, 165, 53, 56, 125, 92, 250, 16, 8, 177,
            10, 220, 118, 139, 50, 240, 205, 126, 144, 43, 221, 71, 45, 54, 144, 182, 240, 142, 147, 48, 178,
            106, 40, 122, 187, 166, 143, 41, 162, 151, 24, 111, 34, 135, 248, 92, 68, 169, 243, 21, 1, 200
        ];

        // act
        PaethFilter.Decode(scanline, previousScanline, 4);

        // assert
        Assert.Equal(expected, scanline);
    }

    [Fact]
    public void AverageFilter_Works() => RunAverageFilterTest();

    [Fact]
    public void UpFilter_Works() => RunUpFilterTest();

    [Fact]
    public void SubFilter_Works() => RunSubFilterTest();

    [Fact]
    public void PaethFilter_Works() => RunPaethFilterTest();

    [Fact]
    public void AverageFilter_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunAverageFilterTest, HwIntrinsics.AllowAll);

    [Fact]
    public void AverageFilter_WithoutHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunAverageFilterTest, HwIntrinsics.DisableHWIntrinsic);

    [Fact]
    public void UpFilter_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunUpFilterTest, HwIntrinsics.AllowAll);

    [Fact]
    public void UpFilter_WithoutAVX2_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunUpFilterTest, HwIntrinsics.DisableAVX2);

    [Fact]
    public void UpFilter_WithoutHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunUpFilterTest, HwIntrinsics.DisableHWIntrinsic);

    [Fact]
    public void SubFilter_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunSubFilterTest, HwIntrinsics.AllowAll);

    [Fact]
    public void SubFilter_WithoutHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunSubFilterTest, HwIntrinsics.DisableHWIntrinsic);

    [Fact]
    public void PaethFilter_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunPaethFilterTest, HwIntrinsics.AllowAll);

    [Fact]
    public void PaethFilter_WithoutSsse3_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunPaethFilterTest, HwIntrinsics.DisableSSE42);

    [Fact]
    public void PaethFilter_WithoutHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunPaethFilterTest, HwIntrinsics.DisableHWIntrinsic);
}
