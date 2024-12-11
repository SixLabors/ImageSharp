// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.ColorSpaces.Conversion.Icc;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.ColorProfiles.Icc;

public class IccProfileConverterTests
{
    private static readonly PngEncoder Encoder = new();

    [Theory(Skip = "Skip for now while we refactor the library")]
    [WithFile(TestImages.Jpeg.ICC.AdobeRgb, PixelTypes.Rgb24)]
    [WithFile(TestImages.Jpeg.ICC.AppleRGB, PixelTypes.Rgb24)]
    [WithFile(TestImages.Jpeg.ICC.ColorMatch, PixelTypes.Rgb24)]
    [WithFile(TestImages.Jpeg.ICC.WideRGB, PixelTypes.Rgb24)]
    [WithFile(TestImages.Jpeg.ICC.SRgb, PixelTypes.Rgb24)]
    [WithFile(TestImages.Jpeg.ICC.ProPhoto, PixelTypes.Rgb24)]
    [WithFile(TestImages.Jpeg.ICC.CMYK, PixelTypes.Rgb24)]
    public void CanRoundTripProfile<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();

        IccProfile profile = image.Metadata.IccProfile;

        TPixel expected = image[0, 0];

        IccProfileConverter.Convert(image, profile, profile);

        image.DebugSave(provider, extension: "png", appendPixelTypeToFileName: false, appendSourceFileOrDescription: true, encoder: Encoder);

        TPixel actual = image[0, 0];

        Assert.Equal(expected, actual);
    }

    [Theory(Skip = "Skip for now while we refactor the library")]
    [WithFile(TestImages.Jpeg.ICC.AdobeRgb, PixelTypes.Rgb24)]
    [WithFile(TestImages.Jpeg.ICC.AppleRGB, PixelTypes.Rgb24)]
    [WithFile(TestImages.Jpeg.ICC.ColorMatch, PixelTypes.Rgb24)]
    [WithFile(TestImages.Jpeg.ICC.WideRGB, PixelTypes.Rgb24)]
    [WithFile(TestImages.Jpeg.ICC.SRgb, PixelTypes.Rgb24)]
    [WithFile(TestImages.Jpeg.ICC.ProPhoto, PixelTypes.Rgb24)]
    [WithFile(TestImages.Jpeg.ICC.CMYK, PixelTypes.Rgb24)]
    public void CanConvertToSRGB<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        IccProfile profile = image.Metadata.IccProfile;

        IccProfile sRGBProfile = SrgbV4Profile.GetProfile();

        IccProfileConverter.Convert(image, profile, sRGBProfile);

        Assert.Equal(image.Metadata.IccProfile, sRGBProfile);

        image.DebugSave(provider, extension: "png", appendPixelTypeToFileName: false, appendSourceFileOrDescription: true, encoder: Encoder);

        // Mac reports a difference of 0.0000%
        image.CompareToReferenceOutput(ImageComparer.Tolerant(0.0001F), provider, appendPixelTypeToFileName: false);
    }
}
