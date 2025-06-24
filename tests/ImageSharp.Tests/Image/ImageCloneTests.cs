// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests;

public class ImageCloneTests
{
    [Fact]
    public void CloneAs_WhenDisposed_Throws()
    {
        Image<Rgba32> image = new Image<Rgba32>(5, 5);
        image.Dispose();

        Assert.Throws<ObjectDisposedException>(() => image.CloneAs<Bgra32>());
    }

    [Fact]
    public void Clone_WhenDisposed_Throws()
    {
        Image<Rgba32> image = new Image<Rgba32>(5, 5);
        image.Dispose();

        Assert.Throws<ObjectDisposedException>(() => image.Clone());
    }

    [Theory]
    [WithTestPatternImages(9, 9, PixelTypes.Rgba32)]
    public void CloneAs_ToBgra32(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage();
        using Image<Bgra32> clone = image.CloneAs<Bgra32>();

        image.ProcessPixelRows(clone, static (imageAccessor, cloneAccessor) =>
        {
            for (int y = 0; y < imageAccessor.Height; y++)
            {
                Span<Rgba32> row = imageAccessor.GetRowSpan(y);
                Span<Bgra32> rowClone = cloneAccessor.GetRowSpan(y);

                for (int x = 0; x < imageAccessor.Width; x++)
                {
                    Rgba32 expected = row[x];
                    Bgra32 actual = rowClone[x];

                    Assert.Equal(expected.R, actual.R);
                    Assert.Equal(expected.G, actual.G);
                    Assert.Equal(expected.B, actual.B);
                    Assert.Equal(expected.A, actual.A);
                }
            }
        });
    }

    [Theory]
    [WithTestPatternImages(9, 9, PixelTypes.Rgba32)]
    public void CloneAs_ToAbgr32(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage();
        using Image<Abgr32> clone = image.CloneAs<Abgr32>();

        image.ProcessPixelRows(clone, static (imageAccessor, cloneAccessor) =>
        {
            for (int y = 0; y < imageAccessor.Height; y++)
            {
                Span<Rgba32> row = imageAccessor.GetRowSpan(y);
                Span<Abgr32> rowClone = cloneAccessor.GetRowSpan(y);

                for (int x = 0; x < cloneAccessor.Width; x++)
                {
                    Rgba32 expected = row[x];
                    Abgr32 actual = rowClone[x];

                    Assert.Equal(expected.R, actual.R);
                    Assert.Equal(expected.G, actual.G);
                    Assert.Equal(expected.B, actual.B);
                }
            }
        });
    }

    [Theory]
    [WithTestPatternImages(9, 9, PixelTypes.Rgba32)]
    public void CloneAs_ToBgr24(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage();
        using Image<Bgr24> clone = image.CloneAs<Bgr24>();

        image.ProcessPixelRows(clone, static (imageAccessor, cloneAccessor) =>
        {
            for (int y = 0; y < imageAccessor.Height; y++)
            {
                Span<Rgba32> row = imageAccessor.GetRowSpan(y);
                Span<Bgr24> rowClone = cloneAccessor.GetRowSpan(y);

                for (int x = 0; x < cloneAccessor.Width; x++)
                {
                    Rgba32 expected = row[x];
                    Bgr24 actual = rowClone[x];

                    Assert.Equal(expected.R, actual.R);
                    Assert.Equal(expected.G, actual.G);
                    Assert.Equal(expected.B, actual.B);
                }
            }
        });
    }

    [Theory]
    [WithTestPatternImages(9, 9, PixelTypes.Rgba32)]
    public void CloneAs_ToArgb32(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage();
        using Image<Argb32> clone = image.CloneAs<Argb32>();
        image.ProcessPixelRows(clone, static (imageAccessor, cloneAccessor) =>
        {
            for (int y = 0; y < imageAccessor.Height; y++)
            {
                Span<Rgba32> row = imageAccessor.GetRowSpan(y);
                Span<Argb32> rowClone = cloneAccessor.GetRowSpan(y);

                for (int x = 0; x < cloneAccessor.Width; x++)
                {
                    Rgba32 expected = row[x];
                    Argb32 actual = rowClone[x];

                    Assert.Equal(expected.R, actual.R);
                    Assert.Equal(expected.G, actual.G);
                    Assert.Equal(expected.B, actual.B);
                    Assert.Equal(expected.A, actual.A);
                }
            }
        });
    }

    [Theory]
    [WithTestPatternImages(9, 9, PixelTypes.Rgba32)]
    public void CloneAs_ToRgb24(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage();
        using Image<Rgb24> clone = image.CloneAs<Rgb24>();
        image.ProcessPixelRows(clone, static (imageAccessor, cloneAccessor) =>
        {
            for (int y = 0; y < imageAccessor.Height; y++)
            {
                Span<Rgba32> row = imageAccessor.GetRowSpan(y);
                Span<Rgb24> rowClone = cloneAccessor.GetRowSpan(y);

                for (int x = 0; x < imageAccessor.Width; x++)
                {
                    Rgba32 expected = row[x];
                    Rgb24 actual = rowClone[x];

                    Assert.Equal(expected.R, actual.R);
                    Assert.Equal(expected.G, actual.G);
                    Assert.Equal(expected.B, actual.B);
                }
            }
        });
    }
}
