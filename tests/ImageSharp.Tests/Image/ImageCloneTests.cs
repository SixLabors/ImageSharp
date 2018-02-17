using System;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public class ImageCloneTests
    {
        [Theory]
        [WithTestPatternImages(9, 9, PixelTypes.Rgba32)]
        public void CloneAs_ToBgra32(TestImageProvider<Rgba32> provider)
        {
            using (Image<Rgba32> image = provider.GetImage())
            using (Image<Bgra32> clone = image.CloneAs<Bgra32>())
            {
                for (int y = 0; y < image.Height; y++)
                {
                    Span<Rgba32> row = image.GetPixelRowSpan(y);
                    Span<Bgra32> rowClone = clone.GetPixelRowSpan(y);

                    for (int x = 0; x < image.Width; x++)
                    {
                        Rgba32 rgba = row[x];
                        Bgra32 bgra = rowClone[x];

                        Assert.Equal(rgba.R, bgra.R);
                        Assert.Equal(rgba.G, bgra.G);
                        Assert.Equal(rgba.B, bgra.B);
                        Assert.Equal(rgba.A, bgra.A);
                    }
                }
            }
        }
    }
}