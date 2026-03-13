// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms;

[Trait("Category", "Processors")]
[GroupOutput("Transforms")]
public class CropTest
{
    [Theory]
    [WithTestPatternImages(70, 30, PixelTypes.Rgba32, 0, 0, 70, 30)]
    [WithTestPatternImages(30, 70, PixelTypes.Rgba32, 7, 13, 20, 50)]
    public void Crop<TPixel>(TestImageProvider<TPixel> provider, int x, int y, int w, int h)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Rectangle rect = new(x, y, w, h);
        FormattableString info = $"X{x}Y{y}.W{w}H{h}";
        provider.RunValidatingProcessorTest(
            ctx => ctx.Crop(rect),
            info,
            comparer: ImageComparer.Exact,
            appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithTestPatternImages(100, 100, PixelTypes.Rgba32)]
    public void CropUpdatesSubject<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();

        image.Metadata.ExifProfile = new ExifProfile();
        image.Metadata.ExifProfile.SetValue(ExifTag.SubjectLocation, [5, 15]);
        image.Metadata.ExifProfile.SetValue(ExifTag.SubjectArea, [5, 15, 50, 50]);

        image.Mutate(ctx => ctx.Crop(Rectangle.FromLTRB(20, 20, 50, 50)));

        // The new subject area is now relative to the cropped area.
        // overhanging pixels are constrained to the dimensions of the image.
        Assert.Equal(
            [0, 0],
            image.Metadata.ExifProfile.GetValue(ExifTag.SubjectLocation).Value);

        Assert.Equal(
            [0, 0, 30, 30],
            image.Metadata.ExifProfile.GetValue(ExifTag.SubjectArea).Value);
    }
}
