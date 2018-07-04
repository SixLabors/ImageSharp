using SixLabors.Primitives;

using Xunit;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Tests.Issues
{
    public class Issue412
    {
        [Theory]
        [WithBlankImages(40, 30, PixelTypes.Rgba32)]
        public void AllPixelsExpectedToBeRedWhenAntialiasedDisabled<TPixel>(TestImageProvider<TPixel> provider) where TPixel : struct, IPixel<TPixel>
        {
            using (var image = provider.GetImage())
            {
                image.Mutate(
                    context =>
                    {
                        for (var i = 0; i < 40; ++i)
                        {
                            context.DrawLines(
                                new GraphicsOptions(false),
                                NamedColors<TPixel>.Black,
                                1,
                                new[]
                                {
                                new PointF(i, 0.1066f),
                                new PointF(i, 10.1066f)
                                });

                            context.DrawLines(
                                new GraphicsOptions(false),
                                NamedColors<TPixel>.Red,
                                1,
                                new[]
                                {
                                new PointF(i, 15.1066f),
                                new PointF(i, 25.1066f)
                                });
                        }
                    });

                image.DebugSave(provider);
                for (var y = 15; y < 25; y++)
                {
                    for (var x = 0; x < 40; x++)
                    {

                        Assert.True(NamedColors<TPixel>.Red.Equals(image[x, y]), $"expected {NamedColors<TPixel>.Red} but found {image[x, y]} at [{x}, {y}]");
                    }
                }
            }
        }
    }
}
