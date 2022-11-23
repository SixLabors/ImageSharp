// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Tests.Processing;

public class IntegralImageTests : BaseImageOperationsExtensionTest
{
    [Theory]
    [WithFile(TestImages.Png.Bradley01, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.Bradley02, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.Ducky, PixelTypes.Rgba32)]
    public void CalculateIntegralImage_Rgba32Works(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage();

        // Act:
        Buffer2D<ulong> integralBuffer = image.CalculateIntegralImage();

        // Assert:
        VerifySumValues(provider, integralBuffer, (Rgba32 pixel) =>
        {
            L8 outputPixel = default;

            outputPixel.FromRgba32(pixel);

            return outputPixel.PackedValue;
        });
    }

    [Theory]
    [WithFile(TestImages.Png.Bradley01, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.Bradley02, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.Ducky, PixelTypes.Rgba32)]
    public void CalculateIntegralImage_WithBounds_Rgba32Works(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage();

        Rectangle interest = new(image.Width / 4, image.Height / 4, image.Width / 2, image.Height / 2);

        // Act:
        Buffer2D<ulong> integralBuffer = image.CalculateIntegralImage(interest);

        // Assert:
        VerifySumValues(provider, integralBuffer, interest, (Rgba32 pixel) =>
        {
            L8 outputPixel = default;

            outputPixel.FromRgba32(pixel);

            return outputPixel.PackedValue;
        });
    }

    [Theory]
    [WithFile(TestImages.Png.Bradley01, PixelTypes.L8)]
    [WithFile(TestImages.Png.Bradley02, PixelTypes.L8)]
    public void CalculateIntegralImage_L8Works(TestImageProvider<L8> provider)
    {
        using Image<L8> image = provider.GetImage();

        // Act:
        Buffer2D<ulong> integralBuffer = image.CalculateIntegralImage();

        // Assert:
        VerifySumValues(provider, integralBuffer, (L8 pixel) => pixel.PackedValue);
    }

    [Theory]
    [WithFile(TestImages.Png.Bradley01, PixelTypes.L8)]
    [WithFile(TestImages.Png.Bradley02, PixelTypes.L8)]
    public void CalculateIntegralImage_WithBounds_L8Works(TestImageProvider<L8> provider)
    {
        using Image<L8> image = provider.GetImage();

        Rectangle interest = new(image.Width / 4, image.Height / 4, image.Width / 2, image.Height / 2);

        // Act:
        Buffer2D<ulong> integralBuffer = image.CalculateIntegralImage(interest);

        // Assert:
        VerifySumValues(provider, integralBuffer, interest, (L8 pixel) => pixel.PackedValue);
    }

    private static void VerifySumValues<TPixel>(
        TestImageProvider<TPixel> provider,
        Buffer2D<ulong> integralBuffer,
        System.Func<TPixel, ulong> getPixel)
        where TPixel : unmanaged, IPixel<TPixel>
        => VerifySumValues(provider, integralBuffer, integralBuffer.Bounds(), getPixel);

    private static void VerifySumValues<TPixel>(
        TestImageProvider<TPixel> provider,
        Buffer2D<ulong> integralBuffer,
        Rectangle bounds,
        System.Func<TPixel, ulong> getPixel)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Buffer2DRegion<TPixel> image = provider.GetImage().GetRootFramePixelBuffer().GetRegion(bounds);

        // Check top-left corner
        Assert.Equal(getPixel(image[0, 0]), integralBuffer[0, 0]);

        ulong pixelValues = 0;

        pixelValues += getPixel(image[0, 0]);
        pixelValues += getPixel(image[1, 0]);
        pixelValues += getPixel(image[0, 1]);
        pixelValues += getPixel(image[1, 1]);

        // Check top-left 2x2 pixels
        Assert.Equal(pixelValues, integralBuffer[1, 1]);

        pixelValues = 0;

        pixelValues += getPixel(image[image.Width - 3, 0]);
        pixelValues += getPixel(image[image.Width - 2, 0]);
        pixelValues += getPixel(image[image.Width - 1, 0]);
        pixelValues += getPixel(image[image.Width - 3, 1]);
        pixelValues += getPixel(image[image.Width - 2, 1]);
        pixelValues += getPixel(image[image.Width - 1, 1]);

        // Check top-right 3x2 pixels
        Assert.Equal(pixelValues, integralBuffer[image.Width - 1, 1] + 0 - 0 - integralBuffer[image.Width - 4, 1]);

        pixelValues = 0;

        pixelValues += getPixel(image[0, image.Height - 3]);
        pixelValues += getPixel(image[0, image.Height - 2]);
        pixelValues += getPixel(image[0, image.Height - 1]);
        pixelValues += getPixel(image[1, image.Height - 3]);
        pixelValues += getPixel(image[1, image.Height - 2]);
        pixelValues += getPixel(image[1, image.Height - 1]);

        // Check bottom-left 2x3 pixels
        Assert.Equal(pixelValues, integralBuffer[1, image.Height - 1] + 0 - integralBuffer[1, image.Height - 4] - 0);

        pixelValues = 0;

        pixelValues += getPixel(image[image.Width - 3, image.Height - 3]);
        pixelValues += getPixel(image[image.Width - 2, image.Height - 3]);
        pixelValues += getPixel(image[image.Width - 1, image.Height - 3]);
        pixelValues += getPixel(image[image.Width - 3, image.Height - 2]);
        pixelValues += getPixel(image[image.Width - 2, image.Height - 2]);
        pixelValues += getPixel(image[image.Width - 1, image.Height - 2]);
        pixelValues += getPixel(image[image.Width - 3, image.Height - 1]);
        pixelValues += getPixel(image[image.Width - 2, image.Height - 1]);
        pixelValues += getPixel(image[image.Width - 1, image.Height - 1]);

        // Check bottom-right 3x3 pixels
        Assert.Equal(pixelValues, integralBuffer[image.Width - 1, image.Height - 1] + integralBuffer[image.Width - 4, image.Height - 4] - integralBuffer[image.Width - 1, image.Height - 4] - integralBuffer[image.Width - 4, image.Height - 1]);
    }
}
