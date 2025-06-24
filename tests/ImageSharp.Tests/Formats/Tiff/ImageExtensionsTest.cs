// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff;

[Trait("Format", "Tiff")]
public class ImageExtensionsTest
{
    [Fact]
    public void SaveAsTiff_Path()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
        string file = Path.Combine(dir, "SaveAsTiff_Path.tiff");

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsTiff(file);
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is TiffFormat);
    }

    [Fact]
    public async Task SaveAsTiffAsync_Path()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
        string file = Path.Combine(dir, "SaveAsTiffAsync_Path.tiff");

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsTiffAsync(file);
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is TiffFormat);
    }

    [Fact]
    public void SaveAsTiff_Path_Encoder()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
        string file = Path.Combine(dir, "SaveAsTiff_Path_Encoder.tiff");

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsTiff(file, new());
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is TiffFormat);
    }

    [Fact]
    public async Task SaveAsTiffAsync_Path_Encoder()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
        string file = Path.Combine(dir, "SaveAsTiffAsync_Path_Encoder.tiff");

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsTiffAsync(file, new TiffEncoder());
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is TiffFormat);
    }

    [Fact]
    public void SaveAsTiff_Stream()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsTiff(memoryStream);
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is TiffFormat);
    }

    [Fact]
    public async Task SaveAsTiffAsync_StreamAsync()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsTiffAsync(memoryStream);
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is TiffFormat);
    }

    [Fact]
    public void SaveAsTiff_Stream_Encoder()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsTiff(memoryStream, new());
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is TiffFormat);
    }

    [Fact]
    public async Task SaveAsTiffAsync_Stream_Encoder()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsTiffAsync(memoryStream, new TiffEncoder());
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is TiffFormat);
    }
}
