// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms;

[Trait("Category", "Processors")]
[GroupOutput("Transforms")]
public class SwizzleTests
{
    private readonly struct SwapXAndYSwizzler : ISwizzler
    {
        public SwapXAndYSwizzler(Size sourceSize)
            => this.DestinationSize = new Size(sourceSize.Height, sourceSize.Width);

        public Size DestinationSize { get; }

        public Point Transform(Point point) => new(point.Y, point.X);
    }

    [Theory]
    [WithTestPatternImages(20, 37, PixelTypes.Rgba32)]
    [WithTestPatternImages(53, 37, PixelTypes.Byte4)]
    [WithTestPatternImages(17, 32, PixelTypes.Rgba32)]
    public void InvertXAndYSwizzle<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> expectedImage = provider.GetImage();
        using Image<TPixel> image = provider.GetImage();

        image.Mutate(ctx => ctx.Swizzle(new SwapXAndYSwizzler(new Size(image.Width, image.Height))));

        image.DebugSave(
            provider,
            nameof(SwapXAndYSwizzler),
            appendPixelTypeToFileName: false,
            appendSourceFileOrDescription: true);

        image.Mutate(ctx => ctx.Swizzle(new SwapXAndYSwizzler(new Size(image.Width, image.Height))));

        image.DebugSave(
            provider,
            "Unswizzle",
            appendPixelTypeToFileName: false,
            appendSourceFileOrDescription: true);

        ImageComparer.Exact.VerifySimilarity(expectedImage, image);
    }

    [Theory]
    [WithTestPatternImages(100, 100, PixelTypes.Rgba32)]
    public void SwizzleUpdatesSubject<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();

        image.Metadata.ExifProfile = new ExifProfile();
        image.Metadata.ExifProfile.SetValue(ExifTag.SubjectLocation, [5, 15]);
        image.Metadata.ExifProfile.SetValue(ExifTag.SubjectArea, [5, 15, 20, 20]);

        image.Mutate(ctx => ctx.Swizzle(new SwapXAndYSwizzler(new Size(image.Width, image.Height))));

        Assert.Equal(
            [15, 5],
            image.Metadata.ExifProfile.GetValue(ExifTag.SubjectLocation).Value);

        Assert.Equal(
            [15, 5, 20, 20],
            image.Metadata.ExifProfile.GetValue(ExifTag.SubjectArea).Value);
    }
}
