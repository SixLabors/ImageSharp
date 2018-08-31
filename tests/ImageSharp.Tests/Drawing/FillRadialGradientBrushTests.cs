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
                TPixel red = NamedColors<TPixel>.Red;

                var unicolorRadialGradientBrush =
                    new RadialGradientBrush<TPixel>(
                        new SixLabors.Primitives.Point(0, 0),
                        100,
                        GradientRepetitionMode.None,
                        new ColorStop<TPixel>(0, red),
                        new ColorStop<TPixel>(1, red));

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
                        var brush = new RadialGradientBrush<TPixel>(
                            new SixLabors.Primitives.Point(centerX, centerY),
                            image.Width / 2f,
                            GradientRepetitionMode.None,
                            new ColorStop<TPixel>(0, NamedColors<TPixel>.Red),
                            new ColorStop<TPixel>(1, NamedColors<TPixel>.Yellow));

                        image.Mutate(x => x.Fill(brush));
                    },
                $"center({centerX},{centerY})",
                false,
                false);
        }
    }
}