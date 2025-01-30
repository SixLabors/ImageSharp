// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Gif;

public class ImageExtensionsTest
{
    [Fact]
    public void SaveAsGif_Path()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
        string file = Path.Combine(dir, "SaveAsGif_Path.gif");

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsGif(file);
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is GifFormat);
    }

    [Fact]
    public async Task SaveAsGifAsync_Path()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
        string file = Path.Combine(dir, "SaveAsGifAsync_Path.gif");

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsGifAsync(file);
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is GifFormat);
    }

    [Fact]
    public void SaveAsGif_Path_Encoder()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
        string file = Path.Combine(dir, "SaveAsGif_Path_Encoder.gif");

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsGif(file, new());
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is GifFormat);
    }

    [Fact]
    public async Task SaveAsGifAsync_Path_Encoder()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
        string file = Path.Combine(dir, "SaveAsGifAsync_Path_Encoder.gif");

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsGifAsync(file, new GifEncoder());
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is GifFormat);
    }

    [Fact]
    public void SaveAsGif_Stream()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsGif(memoryStream);
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is GifFormat);
    }

    [Fact]
    public async Task SaveAsGifAsync_StreamAsync()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsGifAsync(memoryStream);
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is GifFormat);
    }

    [Fact]
    public void SaveAsGif_Stream_Encoder()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsGif(memoryStream, new());
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is GifFormat);
    }

    [Fact]
    public async Task SaveAsGifAsync_Stream_Encoder()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsGifAsync(memoryStream, new GifEncoder());
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is GifFormat);
    }
}
