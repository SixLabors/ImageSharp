// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.Icc;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Icc;
public class IccProfileConverterTests
{
    private static readonly PngEncoder Encoder = new();

    [Theory]
    [WithFile(TestImages.Jpeg.ICC.AdobeRgb, PixelTypes.Rgb24)]
    [WithFile(TestImages.Jpeg.ICC.AppleRGB, PixelTypes.Rgb24)]
    [WithFile(TestImages.Jpeg.ICC.ColorMatch, PixelTypes.Rgb24)]
    [WithFile(TestImages.Jpeg.ICC.WideRGB, PixelTypes.Rgb24)]
    [WithFile(TestImages.Jpeg.ICC.SRgb, PixelTypes.Rgb24)]
    [WithFile(TestImages.Jpeg.ICC.ProPhoto, PixelTypes.Rgb24)]
    public void CanRoundTripProfile<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();

        IccProfile profile = image.Metadata.IccProfile;

        TPixel expected = image[0, 0];

        IccProfileConverter.Convert(image, profile, profile);

        image.DebugSave(provider, Encoder);

        TPixel actual = image[0, 0];

        Assert.Equal(expected, actual);
    }

    [Theory]
    [WithFile(TestImages.Jpeg.ICC.AdobeRgb, PixelTypes.Rgb24)]
    public void CanConvertToWide<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        IccProfile profile = image.Metadata.IccProfile;

        string file = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Jpeg.ICC.SRgb);
        IImageInfo i = Image.Identify(file);
        IccProfile sRGBProfile = i.Metadata.IccProfile;

        IccProfileConverter.Convert(image, profile, sRGBProfile);

        // TODO: Compare.
        image.DebugSave(provider, Encoder);
    }
}
