using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Drawing;
using SixLabors.ImageSharp.Processing.Drawing.Brushes.GradientBrushes;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing
{
    public class FillRadialGradientBrushTests : FileTestBase
    {
        [Fact]
        public void RadialGradientBrushWithEqualColorsReturnsUnicolorImage()
        {
            string path = TestEnvironment.CreateOutputDirectory("Fill", "RadialGradientBrush");
            using (var image = new Image<Rgba32>(200, 200))
            {
                RadialGradientBrush<Rgba32> unicolorRadialGradientBrush =
                    new RadialGradientBrush<Rgba32>(
                        new SixLabors.Primitives.Point(0, 0),
                        100,
                        GradientRepetitionMode.None,
                        new ColorStop<Rgba32>(0, Rgba32.Red),
                        new ColorStop<Rgba32>(1, Rgba32.Red));

                image.Mutate(x => x.Fill(unicolorRadialGradientBrush));
                image.Save($"{path}/UnicolorGradient.png");

                using (PixelAccessor<Rgba32> sourcePixels = image.Lock())
                {
                    Assert.Equal(Rgba32.Red, sourcePixels[0, 0]);
                    Assert.Equal(Rgba32.Red, sourcePixels[9, 9]);
                    Assert.Equal(Rgba32.Red, sourcePixels[5, 5]);
                    Assert.Equal(Rgba32.Red, sourcePixels[3, 8]);
                }
            }
        }

        [Theory]
        [InlineData(250, 250)]
        [InlineData(0, 0)]
        [InlineData(250, 0)]
        [InlineData(0, 250)]
        [InlineData(-100, 250)]
        public void RadialGradientBrushWithDifferentCentersReturnsImage(
            int centerX,
            int centerY)
        {
            int width = 500;

            string path = TestEnvironment.CreateOutputDirectory("Fill", "RadialGradientBrush");
            using (var image = new Image<Rgba32>(width, width))
            {
                RadialGradientBrush<Rgba32> brush =
                    new RadialGradientBrush<Rgba32>(
                        new SixLabors.Primitives.Point(centerX, centerY),
                        width / 2f,
                        GradientRepetitionMode.None,
                        new ColorStop<Rgba32>(0, Rgba32.Red),
                        new ColorStop<Rgba32>(1, Rgba32.Yellow));

                image.Mutate(x => x.Fill(brush));
                image.Save($"{path}/CenterAt{centerX}_{centerY}.png");

                // using (PixelAccessor<Rgba32> sourcePixels = image.Lock())
                // {
                //     Rgba32 columnColor0 = sourcePixels[0, 0];
                //     Rgba32 columnColor23 = sourcePixels[23, 0];
                //     Rgba32 columnColor42 = sourcePixels[42, 0];
                //     Rgba32 columnColor333 = sourcePixels[333, 0];
                //
                //     Rgba32 lastColumnColor = sourcePixels[lastColumnIndex, 0];
                //
                //     for (int i = 0; i < width; i++)
                //     {
                //         // check first and last column:
                //         Assert.Equal(columnColor0, sourcePixels[0, i]);
                //         Assert.Equal(lastColumnColor, sourcePixels[lastColumnIndex, i]);
                //
                //         // check the random colors:
                //         Assert.True(columnColor23 == sourcePixels[23, i], $"at {i}");
                //         Assert.Equal(columnColor42, sourcePixels[42, i]);
                //         Assert.Equal(columnColor333, sourcePixels[333, i]);
                //     }
                // }
            }
        }
    }
}