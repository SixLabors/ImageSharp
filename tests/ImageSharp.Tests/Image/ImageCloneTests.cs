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
                        Rgba32 expected = row[x];
                        Bgra32 actual = rowClone[x];

                        Assert.Equal(expected.R, actual.R);
                        Assert.Equal(expected.G, actual.G);
                        Assert.Equal(expected.B, actual.B);
                        Assert.Equal(expected.A, actual.A);
                    }
                }
            }
        }

        [Theory]
        [WithTestPatternImages(9, 9, PixelTypes.Rgba32)]
        public void CloneAs_ToBgr24(TestImageProvider<Rgba32> provider)
        {
            using (Image<Rgba32> image = provider.GetImage())
            using (Image<Bgr24> clone = image.CloneAs<Bgr24>())
            {
                for (int y = 0; y < image.Height; y++)
                {
                    Span<Rgba32> row = image.GetPixelRowSpan(y);
                    Span<Bgr24> rowClone = clone.GetPixelRowSpan(y);

                    for (int x = 0; x < image.Width; x++)
                    {
                        Rgba32 expected = row[x];
                        Bgr24 actual = rowClone[x];

                        Assert.Equal(expected.R, actual.R);
                        Assert.Equal(expected.G, actual.G);
                        Assert.Equal(expected.B, actual.B);
                    }
                }
            }
        }

        [Theory]
        [WithTestPatternImages(9, 9, PixelTypes.Rgba32)]
        public void CloneAs_ToArgb32(TestImageProvider<Rgba32> provider)
        {
            using (Image<Rgba32> image = provider.GetImage())
            using (Image<Argb32> clone = image.CloneAs<Argb32>())
            {
                for (int y = 0; y < image.Height; y++)
                {
                    Span<Rgba32> row = image.GetPixelRowSpan(y);
                    Span<Argb32> rowClone = clone.GetPixelRowSpan(y);

                    for (int x = 0; x < image.Width; x++)
                    {
                        Rgba32 expected = row[x];
                        Argb32 actual = rowClone[x];

                        Assert.Equal(expected.R, actual.R);
                        Assert.Equal(expected.G, actual.G);
                        Assert.Equal(expected.B, actual.B);
                        Assert.Equal(expected.A, actual.A);
                    }
                }
            }
        }

        [Theory]
        [WithTestPatternImages(9, 9, PixelTypes.Rgba32)]
        public void CloneAs_ToRgb24(TestImageProvider<Rgba32> provider)
        {
            using (Image<Rgba32> image = provider.GetImage())
            using (Image<Rgb24> clone = image.CloneAs<Rgb24>())
            {
                for (int y = 0; y < image.Height; y++)
                {
                    Span<Rgba32> row = image.GetPixelRowSpan(y);
                    Span<Rgb24> rowClone = clone.GetPixelRowSpan(y);

                    for (int x = 0; x < image.Width; x++)
                    {
                        Rgba32 expected = row[x];
                        Rgb24 actual = rowClone[x];

                        Assert.Equal(expected.R, actual.R);
                        Assert.Equal(expected.G, actual.G);
                        Assert.Equal(expected.B, actual.B);
                    }
                }
            }
        }
    }
}