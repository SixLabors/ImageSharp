// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Webp.Lossless;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Webp;

[Trait("Format", "Webp")]
public class PredictorEncoderTests
{
    [Fact]
    public static void ColorSpaceTransform_WithBikeImage_ProducesExpectedData()
        => RunColorSpaceTransformTestWithBikeImage();

    [Fact]
    public static void ColorSpaceTransform_WithPeakImage_ProducesExpectedData()
        => RunColorSpaceTransformTestWithPeakImage();

    [Fact]
    public void ColorSpaceTransform_WithPeakImage_WithHardwareIntrinsics_Works()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ColorSpaceTransform_WithPeakImage_ProducesExpectedData, HwIntrinsics.AllowAll);

    [Fact]
    public void ColorSpaceTransform_WithPeakImage_WithoutSSE41_Works()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ColorSpaceTransform_WithPeakImage_ProducesExpectedData, HwIntrinsics.DisableSSE41);

    [Fact]
    public void ColorSpaceTransform_WithBikeImage_WithHardwareIntrinsics_Works()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ColorSpaceTransform_WithBikeImage_ProducesExpectedData, HwIntrinsics.AllowAll);

    [Fact]
    public void ColorSpaceTransform_WithBikeImage_WithoutSSE41_Works()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ColorSpaceTransform_WithBikeImage_ProducesExpectedData, HwIntrinsics.DisableSSE41);

    [Fact]
    public void ColorSpaceTransform_WithBikeImage_WithoutAvx2_Works()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ColorSpaceTransform_WithBikeImage_ProducesExpectedData, HwIntrinsics.DisableAVX2);

    // Test image: Input\Webp\peak.png
    private static void RunColorSpaceTransformTestWithPeakImage()
    {
        // arrange
        uint[] expectedData =
        [
            4278191104, 4278191104, 4278191104, 4278191104, 4278191104, 4278191104, 4278191104, 4294577152,
            4294707200, 4294707200, 4294707200, 4294707200, 4294837248, 4294837248, 4293926912, 4294316544,
            4278191104, 4278191104, 4294837248, 4294837248, 4280287232, 4280350720, 4294447104, 4294707200,
            4294838272, 4278516736, 4294837248, 4294837248, 4278516736, 4294707200, 4279298048, 4294837248,
            4294837248, 4294837248, 4294837248, 4280287232, 4280287232, 4292670464, 4279633408, 4294838272,
            4294837248, 4278516736, 4278516736, 4278516736, 4278516736, 4278516736, 4278778880, 4278193152,
            4278191104, 4280287232, 4280287232, 4280287232, 4280287232, 4293971968, 4280612864, 4292802560,
            4294837760, 4278516736, 4278516736, 4294837760, 4294707712, 4278516736, 4294837248, 4278193152,
            4280287232, 4278984704, 4280287232, 4278243328, 4280287232, 4278244352, 4280287232, 4280025088,
            4280025088, 4294837760, 4278192128, 4294838784, 4294837760, 4294707712, 4278778880, 4278324224,
            4280287232, 4280287232, 4278202368, 4279115776, 4280287232, 4278243328, 4280287232, 4280287232,
            4280025088, 4280287232, 4278192128, 4294838272, 4294838272, 4294837760, 4278190592, 4278778880,
            4280875008, 4280287232, 4279896576, 4281075712, 4281075712, 4280287232, 4280287232, 4280287232,
            4280287232, 4280287232, 4278190592, 4294709248, 4278516736, 4278516736, 4278584832, 4278909440,
            4280287232, 4280287232, 4294367744, 4294621184, 4279115776, 4280287232, 4280287232, 4280351744,
            4280287232, 4280287232, 4280287232, 4278513664, 4278516736, 4278716416, 4278584832, 4280291328,
            4293062144, 4280287232, 4280287232, 4280287232, 4294456320, 4280291328, 4280287232, 4280287232,
            4280287232, 4280287232, 4280287232, 4280287232, 4278513152, 4278716416, 4278584832, 4280291328,
            4278198272, 4278198272, 4278589952, 4278198272, 4278198272, 4280287232, 4278765568, 4280287232,
            4280287232, 4280287232, 4280287232, 4294712832, 4278513152, 4278716640, 4279300608, 4278584832,
            4280156672, 4279373312, 4278589952, 4279373312, 4278328832, 4278328832, 4278328832, 4279634432,
            4280287232, 4280287232, 4280287232, 4280287232, 4278457344, 4280483328, 4278584832, 4278385664,
            4279634432, 4279373312, 4279634432, 4280287232, 4280287232, 4280156672, 4278589952, 4278328832,
            4278198272, 4280156672, 4280483328, 4294363648, 4280287232, 4278376448, 4280287232, 4278647808,
            4280287232, 4280287232, 4279373312, 4280287232, 4280287232, 4280156672, 4280287232, 4278198272,
            4278198272, 4280156672, 4280287232, 4280287232, 4293669888, 4278765568, 4278765568, 4280287232,
            4280287232, 4280287232, 4279634432, 4279634432, 4280287232, 4280287232, 4280287232, 4280287232,
            4280287232, 4280287232, 4280287232, 4280287232, 4279373312, 4279764992, 4293539328, 4279896576,
            4280287232, 4280287232, 4280287232, 4279634432, 4278198272, 4279634432, 4280287232, 4280287232,
            4280287232, 4280287232, 4280287232, 4280287232, 4280287232, 4279503872, 4279503872, 4280288256,
            4280287232, 4280287232, 4280287232, 4280287232, 4280287232, 4280287232, 4280287232, 4280287232,
            4280287232, 4280287232, 4280287232, 4280287232, 4280287232, 4280287232, 4280287232, 4280287232
        ];

        // Convert image pixels to bgra array.
        byte[] imgBytes = File.ReadAllBytes(TestImageFullPath(TestImages.Webp.Peak));
        using Image<Rgba32> image = Image.Load<Rgba32>(imgBytes);
        uint[] bgra = ToBgra(image);

        const int colorTransformBits = 3;
        int transformWidth = LosslessUtils.SubSampleSize(image.Width, colorTransformBits);
        int transformHeight = LosslessUtils.SubSampleSize(image.Height, colorTransformBits);
        uint[] transformData = new uint[transformWidth * transformHeight];
        int[] scratch = new int[256];

        // act
        PredictorEncoder.ColorSpaceTransform(image.Width, image.Height, colorTransformBits, 75, bgra, transformData, scratch);

        // assert
        Assert.Equal(expectedData, transformData);
    }

    private static void RunColorSpaceTransformTestWithBikeImage()
    {
        // arrange
        uint[] expectedData =
        [
            4278714368, 4278192876, 4278198304, 4278198304, 4278190304, 4278190080, 4278190080, 4278198272,
            4278197760, 4278198816, 4278197794, 4278197774, 4278190080, 4278190080, 4278198816, 4278197281,
            4278197280, 4278197792, 4278200353, 4278191343, 4278190304, 4294713873, 4278198784, 4294844416,
            4278201578, 4278200044, 4278191343, 4278190288, 4294705200, 4294717139, 4278203628, 4278201064,
            4278201586, 4278197792, 4279240909
        ];

        // Convert image pixels to bgra array.
        byte[] imgBytes = File.ReadAllBytes(TestImageFullPath(TestImages.Webp.Lossy.BikeSmall));
        using Image<Rgba32> image = Image.Load<Rgba32>(imgBytes);
        uint[] bgra = ToBgra(image);

        const int colorTransformBits = 4;
        int transformWidth = LosslessUtils.SubSampleSize(image.Width, colorTransformBits);
        int transformHeight = LosslessUtils.SubSampleSize(image.Height, colorTransformBits);
        uint[] transformData = new uint[transformWidth * transformHeight];
        int[] scratch = new int[256];

        // act
        PredictorEncoder.ColorSpaceTransform(image.Width, image.Height, colorTransformBits, 75, bgra, transformData, scratch);

        // assert
        Assert.Equal(expectedData, transformData);
    }

    private static uint[] ToBgra<TPixel>(Image<TPixel> image)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        uint[] bgra = new uint[image.Width * image.Height];
        image.ProcessPixelRows(accessor =>
        {
            int idx = 0;
            for (int y = 0; y < accessor.Height; y++)
            {
                Span<TPixel> rowSpan = accessor.GetRowSpan(y);
                for (int x = 0; x < rowSpan.Length; x++)
                {
                    bgra[idx++] = ToBgra32(rowSpan[x]).PackedValue;
                }
            }
        });

        return bgra;
    }

    private static Bgra32 ToBgra32<TPixel>(TPixel color)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Rgba32 rgba = color.ToRgba32();
        return new Bgra32(rgba.R, rgba.G, rgba.B, rgba.A);
    }

    private static string TestImageFullPath(string path)
        => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, path);
}
