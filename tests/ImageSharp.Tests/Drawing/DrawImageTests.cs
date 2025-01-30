// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Drawing;

[GroupOutput("Drawing")]
public class DrawImageTests
{
    public static readonly TheoryData<PixelColorBlendingMode> BlendingModes = new()
    {
        PixelColorBlendingMode.Normal,
        PixelColorBlendingMode.Multiply,
        PixelColorBlendingMode.Add,
        PixelColorBlendingMode.Subtract,
        PixelColorBlendingMode.Screen,
        PixelColorBlendingMode.Darken,
        PixelColorBlendingMode.Lighten,
        PixelColorBlendingMode.Overlay,
        PixelColorBlendingMode.HardLight,
    };

    [Theory]
    [WithFile(TestImages.Png.Rainbow, nameof(BlendingModes), PixelTypes.Rgba32)]
    public void ImageBlendingMatchesSvgSpecExamples<TPixel>(TestImageProvider<TPixel> provider, PixelColorBlendingMode mode)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> background = provider.GetImage();
        using Image<TPixel> source = Image.Load<TPixel>(TestFile.Create(TestImages.Png.Ducky).Bytes);
        background.Mutate(x => x.DrawImage(source, mode, 1F));
        background.DebugSave(
            provider,
            new { mode },
            appendPixelTypeToFileName: false,
            appendSourceFileOrDescription: false);

        ImageComparer comparer = ImageComparer.TolerantPercentage(0.01F);
        background.CompareToReferenceOutput(
            comparer,
            provider,
            new { mode },
            appendPixelTypeToFileName: false,
            appendSourceFileOrDescription: false);
    }

    [Theory]
    [WithFile(TestImages.Png.CalliphoraPartial, PixelTypes.Rgba32, TestImages.Png.Splash, PixelColorBlendingMode.Normal, 1f)]
    [WithFile(TestImages.Png.CalliphoraPartial, PixelTypes.Bgr24, TestImages.Png.Bike, PixelColorBlendingMode.Normal, 1f)]
    [WithFile(TestImages.Png.CalliphoraPartial, PixelTypes.Rgba32, TestImages.Png.Splash, PixelColorBlendingMode.Normal, 0.75f)]
    [WithFile(TestImages.Png.CalliphoraPartial, PixelTypes.Rgba32, TestImages.Png.Splash, PixelColorBlendingMode.Normal, 0.25f)]

    [WithTestPatternImages(400, 400, PixelTypes.Rgba32, TestImages.Png.Splash, PixelColorBlendingMode.Multiply, 0.5f)]
    [WithTestPatternImages(400, 400, PixelTypes.Rgba32, TestImages.Png.Splash, PixelColorBlendingMode.Add, 0.5f)]
    [WithTestPatternImages(400, 400, PixelTypes.Rgba32, TestImages.Png.Splash, PixelColorBlendingMode.Subtract, 0.5f)]

    [WithFile(TestImages.Png.Rgb48Bpp, PixelTypes.Rgba64, TestImages.Png.Splash, PixelColorBlendingMode.Normal, 1f)]
    [WithFile(TestImages.Png.Rgb48Bpp, PixelTypes.Rgba64, TestImages.Png.Splash, PixelColorBlendingMode.Normal, 0.25f)]
    public void WorksWithDifferentConfigurations<TPixel>(
        TestImageProvider<TPixel> provider,
        string brushImage,
        PixelColorBlendingMode mode,
        float opacity)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        using Image<TPixel> blend = Image.Load<TPixel>(TestFile.Create(brushImage).Bytes);
        Size size = new(image.Width * 3 / 4, image.Height * 3 / 4);
        Point position = new(image.Width / 8, image.Height / 8);
        blend.Mutate(x => x.Resize(size.Width, size.Height, KnownResamplers.Bicubic));
        image.Mutate(x => x.DrawImage(blend, position, mode, opacity));
        FormattableString testInfo = $"{Path.GetFileNameWithoutExtension(brushImage)}-{mode}-{opacity}";

        PngEncoder encoder;
        if (provider.PixelType == PixelTypes.Rgba64)
        {
            encoder = new() { BitDepth = PngBitDepth.Bit16 };
        }
        else
        {
            encoder = new();
        }

        image.DebugSave(provider, testInfo, encoder: encoder);
        image.CompareToReferenceOutput(
            ImageComparer.TolerantPercentage(0.01f),
            provider,
            testInfo);
    }

    [Theory]
    [WithTestPatternImages(200, 200, PixelTypes.Rgba32 | PixelTypes.Bgra32)]
    public void DrawImageOfDifferentPixelType<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        byte[] brushData = TestFile.Create(TestImages.Png.Ducky).Bytes;

        using Image<TPixel> image = provider.GetImage();
        using Image brushImage = provider.PixelType == PixelTypes.Rgba32
                                      ? Image.Load<Bgra32>(brushData)
                                      : Image.Load<Rgba32>(brushData);
        image.Mutate(c => c.DrawImage(brushImage, 0.5f));

        image.DebugSave(provider, appendSourceFileOrDescription: false);
        image.CompareToReferenceOutput(
            ImageComparer.TolerantPercentage(0.01f),
            provider,
            appendSourceFileOrDescription: false);
    }

    [Theory]
    [WithSolidFilledImages(100, 100, "White", PixelTypes.Rgba32, 0, 0)]
    [WithSolidFilledImages(100, 100, "White", PixelTypes.Rgba32, 25, 25)]
    [WithSolidFilledImages(100, 100, "White", PixelTypes.Rgba32, 75, 50)]
    [WithSolidFilledImages(100, 100, "White", PixelTypes.Rgba32, -25, -30)]
    public void WorksWithDifferentLocations(TestImageProvider<Rgba32> provider, int x, int y)
    {
        using Image<Rgba32> background = provider.GetImage();
        using Image<Rgba32> overlay = new(50, 50, Color.Black.ToPixel<Rgba32>());

        background.Mutate(c => c.DrawImage(overlay, new Point(x, y), PixelColorBlendingMode.Normal, 1F));

        background.DebugSave(
            provider,
            testOutputDetails: $"{x}_{y}",
            appendPixelTypeToFileName: false,
            appendSourceFileOrDescription: false);

        background.CompareToReferenceOutput(
            provider,
            testOutputDetails: $"{x}_{y}",
            appendPixelTypeToFileName: false,
            appendSourceFileOrDescription: false);
    }

    [Theory]
    [WithSolidFilledImages(100, 100, "White", PixelTypes.Rgba32, 10, 10)]
    [WithSolidFilledImages(100, 100, "White", PixelTypes.Rgba32, 50, 25)]
    [WithSolidFilledImages(100, 100, "White", PixelTypes.Rgba32, 25, 50)]
    [WithSolidFilledImages(100, 100, "White", PixelTypes.Rgba32, 50, 50)]
    public void WorksWithDifferentBounds(TestImageProvider<Rgba32> provider, int width, int height)
    {
        using Image<Rgba32> background = provider.GetImage();
        using Image<Rgba32> overlay = new(50, 50, Color.Black.ToPixel<Rgba32>());

        background.Mutate(c => c.DrawImage(overlay, new Rectangle(0, 0, width, height), PixelColorBlendingMode.Normal, 1F));

        background.DebugSave(
            provider,
            testOutputDetails: $"{width}_{height}",
            appendPixelTypeToFileName: false,
            appendSourceFileOrDescription: false);

        background.CompareToReferenceOutput(
            provider,
            testOutputDetails: $"{width}_{height}",
            appendPixelTypeToFileName: false,
            appendSourceFileOrDescription: false);
    }

    [Theory]
    [WithFile(TestImages.Png.Splash, PixelTypes.Rgba32)]
    public void DrawTransformed<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        using Image<TPixel> blend = Image.Load<TPixel>(TestFile.Create(TestImages.Bmp.Car).Bytes);
        AffineTransformBuilder builder = new AffineTransformBuilder()
            .AppendRotationDegrees(45F)
            .AppendScale(new SizeF(.25F, .25F))
            .AppendTranslation(new PointF(10, 10));

        // Apply a background color so we can see the translation.
        blend.Mutate(x => x.Transform(builder));
        blend.Mutate(x => x.BackgroundColor(Color.HotPink));

        // Lets center the matrix so we can tell whether any cut-off issues we may have belong to the drawing processor
        Point position = new((image.Width - blend.Width) / 2, (image.Height - blend.Height) / 2);
        image.Mutate(x => x.DrawImage(blend, position, .75F));

        image.DebugSave(provider, appendPixelTypeToFileName: false, appendSourceFileOrDescription: false);
        image.CompareToReferenceOutput(
            ImageComparer.TolerantPercentage(0.002f),
            provider,
            appendPixelTypeToFileName: false,
            appendSourceFileOrDescription: false);
    }

    [Theory]
    [WithFile(TestImages.Png.Issue2447, PixelTypes.Rgba32)]
    public void Issue2447_A<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> foreground = provider.GetImage();
        using Image<Rgba32> background = new(100, 100, new(0, 255, 255));

        background.Mutate(c => c.DrawImage(foreground, new(64, 10), new Rectangle(32, 32, 32, 32), 1F));

        background.DebugSave(
            provider,
            appendPixelTypeToFileName: false,
            appendSourceFileOrDescription: false);

        background.CompareToReferenceOutput(
            provider,
            appendPixelTypeToFileName: false,
            appendSourceFileOrDescription: false);
    }

    [Theory]
    [WithFile(TestImages.Png.Issue2447, PixelTypes.Rgba32)]
    public void Issue2447_B<TPixel>(TestImageProvider<TPixel> provider)
    where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> foreground = provider.GetImage();
        using Image<Rgba32> background = new(100, 100, new(0, 255, 255));

        background.Mutate(c => c.DrawImage(foreground, new(10, 10), new Rectangle(320, 128, 32, 32), 1F));

        background.DebugSave(
            provider,
            appendPixelTypeToFileName: false,
            appendSourceFileOrDescription: false);

        background.CompareToReferenceOutput(
            provider,
            appendPixelTypeToFileName: false,
            appendSourceFileOrDescription: false);
    }

    [Theory]
    [WithFile(TestImages.Png.Issue2447, PixelTypes.Rgba32)]
    public void Issue2447_C<TPixel>(TestImageProvider<TPixel> provider)
    where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> foreground = provider.GetImage();
        using Image<Rgba32> background = new(100, 100, new(0, 255, 255));

        background.Mutate(c => c.DrawImage(foreground, new(10, 10), new Rectangle(32, 32, 32, 32), 1F));

        background.DebugSave(
            provider,
            appendPixelTypeToFileName: false,
            appendSourceFileOrDescription: false);

        background.CompareToReferenceOutput(
            provider,
            appendPixelTypeToFileName: false,
            appendSourceFileOrDescription: false);
    }

    [Theory]
    [WithFile(TestImages.Png.Issue2447, PixelTypes.Rgba32)]
    public void Issue2608_NegOffset<TPixel>(TestImageProvider<TPixel> provider)
    where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> foreground = provider.GetImage();
        using Image<Rgba32> background = new(100, 100, new(0, 255, 255));

        background.Mutate(c => c.DrawImage(foreground, new(-10, -10), new Rectangle(32, 32, 32, 32), 1F));

        background.DebugSave(
            provider,
            appendPixelTypeToFileName: false,
            appendSourceFileOrDescription: false);

        background.CompareToReferenceOutput(
            provider,
            appendPixelTypeToFileName: false,
            appendSourceFileOrDescription: false);
    }

    [Theory]
    [WithFile(TestImages.Png.Issue2447, PixelTypes.Rgba32)]
    public void Issue2603<TPixel>(TestImageProvider<TPixel> provider)
    where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> foreground = provider.GetImage();
        using Image<Rgba32> background = new(100, 100, new(0, 255, 255));

        background.Mutate(c => c.DrawImage(foreground, new(80, 80), new Rectangle(32, 32, 32, 32), 1F));

        background.DebugSave(
            provider,
            appendPixelTypeToFileName: false,
            appendSourceFileOrDescription: false);

        background.CompareToReferenceOutput(
            provider,
            appendPixelTypeToFileName: false,
            appendSourceFileOrDescription: false);
    }
}
