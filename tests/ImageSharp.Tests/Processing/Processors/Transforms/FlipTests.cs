// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms;

[Trait("Category", "Processors")]
[GroupOutput("Transforms")]
public class FlipTests
{
    public static readonly TheoryData<FlipMode> FlipValues =
        new()
        {
            FlipMode.None,
            FlipMode.Vertical,
            FlipMode.Horizontal
        };

    [Theory]
    [WithTestPatternImages(nameof(FlipValues), 20, 37, PixelTypes.Rgba32)]
    [WithTestPatternImages(nameof(FlipValues), 53, 37, PixelTypes.Rgba32)]
    [WithTestPatternImages(nameof(FlipValues), 17, 32, PixelTypes.Rgba32)]
    public void Flip<TPixel>(TestImageProvider<TPixel> provider, FlipMode flipMode)
        where TPixel : unmanaged, IPixel<TPixel>
        => provider.RunValidatingProcessorTest(
            ctx => ctx.Flip(flipMode),
            testOutputDetails: flipMode,
            appendPixelTypeToFileName: false);

    [Theory]
    [WithTestPatternImages(nameof(FlipValues), 53, 37, PixelTypes.Rgba32)]
    [WithTestPatternImages(nameof(FlipValues), 17, 32, PixelTypes.Rgba32)]
    public void Flip_WorksOnWrappedMemoryImage<TPixel>(TestImageProvider<TPixel> provider, FlipMode flipMode)
        where TPixel : unmanaged, IPixel<TPixel>
        => provider.RunValidatingProcessorTestOnWrappedMemoryImage(
            ctx => ctx.Flip(flipMode),
            testOutputDetails: flipMode,
            useReferenceOutputFrom: nameof(this.Flip),
            appendPixelTypeToFileName: false);

    [Theory]
    [WithTestPatternImages(100, 100, PixelTypes.Rgba32)]
    public void FlipVerticalUpdatesSubject<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();

        image.Metadata.ExifProfile = new ExifProfile();
        image.Metadata.ExifProfile.SetValue(ExifTag.SubjectLocation, [5, 15]);
        image.Metadata.ExifProfile.SetValue(ExifTag.SubjectArea, [5, 15, 50, 50]);

        image.Mutate(ctx => ctx.Flip(FlipMode.Vertical));

        // The subject location is a single coordinate, so a vertical flip simply reflects its Y position:
        // newY = imageHeight - originalY - 1
        // This mirrors the point vertically around the image's horizontal axis, preserving its X coordinate.
        Assert.Equal(
            [5, 84],
            image.Metadata.ExifProfile.GetValue(ExifTag.SubjectLocation).Value);

        // The subject area is now inverted because a vertical flip reflects the image across
        // the horizontal axis passing through the image center.
        // The Y-coordinate of the top edge is recalculated as:
        // newY = imageHeight - originalY - height - 1
        Assert.Equal(
            [5, 34, 50, 50],
            image.Metadata.ExifProfile.GetValue(ExifTag.SubjectArea).Value);
    }

    [Theory]
    [WithTestPatternImages(100, 100, PixelTypes.Rgba32)]
    public void FlipHorizontalUpdatesSubject<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();

        image.Metadata.ExifProfile = new ExifProfile();
        image.Metadata.ExifProfile.SetValue(ExifTag.SubjectLocation, [5, 15]);
        image.Metadata.ExifProfile.SetValue(ExifTag.SubjectArea, [5, 15, 50, 50]);

        image.Mutate(ctx => ctx.Flip(FlipMode.Horizontal));

        // The subject location is a single coordinate, so a horizontal flip simply reflects its X position:
        // newX = imageWidth - originalX - 1
        // This mirrors the point horizontally around the image's vertical axis, preserving its Y coordinate.
        Assert.Equal(
            [94, 15],
            image.Metadata.ExifProfile.GetValue(ExifTag.SubjectLocation).Value);

        // The subject area is now inverted because a horizontal flip reflects the image across
        // the vertical axis passing through the image center.
        // The X-coordinate of the left edge is recalculated as:
        // newX = imageWidth - originalX - width - 1
        Assert.Equal(
            [44, 15, 50, 50],
            image.Metadata.ExifProfile.GetValue(ExifTag.SubjectArea).Value);
    }
}
