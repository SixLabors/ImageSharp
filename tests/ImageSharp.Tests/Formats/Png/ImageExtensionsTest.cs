// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Png;

[Trait("Format", "Png")]
public class ImageExtensionsTest
{
    [Fact]
    public void SaveAsPng_Path()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
        string file = Path.Combine(dir, "SaveAsPng_Path.png");

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsPng(file);
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is PngFormat);
    }

    [Fact]
    public async Task SaveAsPngAsync_Path()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
        string file = Path.Combine(dir, "SaveAsPngAsync_Path.png");

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsPngAsync(file);
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is PngFormat);
    }

    [Fact]
    public void SaveAsPng_Path_Encoder()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
        string file = Path.Combine(dir, "SaveAsPng_Path_Encoder.png");

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsPng(file, new PngEncoder());
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is PngFormat);
    }

    [Fact]
    public async Task SaveAsPngAsync_Path_Encoder()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
        string file = Path.Combine(dir, "SaveAsPngAsync_Path_Encoder.png");

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsPngAsync(file, new PngEncoder());
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is PngFormat);
    }

    [Fact]
    public void SaveAsPng_Stream()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsPng(memoryStream);
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is PngFormat);
    }

    [Fact]
    public async Task SaveAsPngAsync_StreamAsync()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsPngAsync(memoryStream);
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is PngFormat);
    }

    [Fact]
    public void SaveAsPng_Stream_Encoder()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsPng(memoryStream, new PngEncoder());
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is PngFormat);
    }

    [Fact]
    public async Task SaveAsPngAsync_Stream_Encoder()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsPngAsync(memoryStream, new PngEncoder());
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is PngFormat);
    }
}
