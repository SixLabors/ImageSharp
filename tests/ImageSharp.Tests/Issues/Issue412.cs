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
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(
                    context =>
                    {
                        for (var i = 0; i < 40; ++i)
                        {
                            context.DrawLines(
                                new GraphicsOptions(false),
                                Color.Black,
                                1,
                                new PointF(i, 0.1066f),
                                new PointF(i, 10.1066f));

                            context.DrawLines(
                                new GraphicsOptions(false),
                                Color.Red,
                                1,
                                new PointF(i, 15.1066f),
                                new PointF(i, 25.1066f));
                        }
                    });

                image.DebugSave(provider);
                for (var y = 15; y < 25; y++)
                {
                    for (var x = 0; x < 40; x++)
                    {
                        TPixel red = Color.Red.ToPixel<TPixel>();

                        Assert.True(red.Equals(image[x, y]), $"expected {Color.Red} but found {image[x, y]} at [{x}, {y}]");
                    }
                }
            }
        }
    }
}
