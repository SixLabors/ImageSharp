using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Drawing;
using SixLabors.ImageSharp.Processing.Drawing.Brushes.GradientBrushes;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing
{
    public class FillRadialGradientBrushTests : FileTestBase
    {
        [Theory]
        [WithBlankImages(200, 200, PixelTypes.Rgba32)]
        public void RadialGradientBrushWithEqualColorsReturnsUnicolorImage<TPixel>(
            TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (var image = provider.GetImage())
            {
                TPixel red = NamedColors<TPixel>.Red;

                RadialGradientBrush<TPixel> unicolorRadialGradientBrush =
                    new RadialGradientBrush<TPixel>(
                        new SixLabors.Primitives.Point(0, 0),
                        100,
                        GradientRepetitionMode.None,
                        new ColorStop<TPixel>(0, red),
                        new ColorStop<TPixel>(1, red));

                image.Mutate(x => x.Fill(unicolorRadialGradientBrush));
                image.DebugSave(provider);

                image.CompareToReferenceOutput(provider);
            }
        }

        [Theory]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 100, 100)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 0, 0)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 100, 0)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 0, 100)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, -40, 100)]
        public void RadialGradientBrushWithDifferentCentersReturnsImage<TPixel>(
            TestImageProvider<TPixel> provider,
            int centerX,
            int centerY)
            where TPixel : struct, IPixel<TPixel>
        {
            using (var image = provider.GetImage())
            {
                RadialGradientBrush<TPixel> brush =
                    new RadialGradientBrush<TPixel>(
                        new SixLabors.Primitives.Point(centerX, centerY),
                        image.Width / 2f,
                        GradientRepetitionMode.None,
                        new ColorStop<TPixel>(0, NamedColors<TPixel>.Red),
                        new ColorStop<TPixel>(1, NamedColors<TPixel>.Yellow));

                image.Mutate(x => x.Fill(brush));
                image.DebugSave(provider);
                image.CompareToReferenceOutput(provider);
            }
        }
    }
}