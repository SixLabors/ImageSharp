using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing
{

    using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

    [GroupOutput("Drawing/GradientBrushes")]
    public class FillRadialGradientBrushTests
    {
        public static ImageComparer TolerantComparer = ImageComparer.TolerantPercentage(0.01f);

        [Theory]
        [WithBlankImages(200, 200, PixelTypes.Rgba32)]
        public void WithEqualColorsReturnsUnicolorImage<TPixel>(
            TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                Color red = Color.Red;

                var unicolorRadialGradientBrush =
                    new RadialGradientBrush(
                        new SixLabors.Primitives.Point(0, 0),
                        100,
                        GradientRepetitionMode.None,
                        new ColorStop(0, red),
                        new ColorStop(1, red));

                image.Mutate(x => x.Fill(unicolorRadialGradientBrush));

                image.DebugSave(provider, appendPixelTypeToFileName: false, appendSourceFileOrDescription: false);

                // no need for reference image in this test:
                image.ComparePixelBufferTo(red);
            }
        }

        [Theory]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 100, 100)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 0, 0)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 100, 0)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 0, 100)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, -40, 100)]
        public void WithDifferentCentersReturnsImage<TPixel>(
            TestImageProvider<TPixel> provider,
            int centerX,
            int centerY)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.VerifyOperation(
                TolerantComparer,
                image =>
                    {
                        var brush = new RadialGradientBrush(
                            new SixLabors.Primitives.Point(centerX, centerY),
                            image.Width / 2f,
                            GradientRepetitionMode.None,
                            new ColorStop(0, Color.Red),
                            new ColorStop(1, Color.Yellow));

                        image.Mutate(x => x.Fill(brush));
                    },
                $"center({centerX},{centerY})",
                false,
                false);
        }
    }
}