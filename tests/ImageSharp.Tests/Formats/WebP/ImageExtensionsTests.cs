// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Webp;

[Trait("Format", "Webp")]
public class ImageExtensionsTests
{
    [Fact]
    public void SaveAsWebp_Path()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTests));
        string file = Path.Combine(dir, "SaveAsWebp_Path.webp");

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsWebp(file);
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is WebpFormat);
    }

    [Fact]
    public async Task SaveAsWebpAsync_Path()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTests));
        string file = Path.Combine(dir, "SaveAsWebpAsync_Path.webp");

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsWebpAsync(file);
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is WebpFormat);
    }

    [Fact]
    public void SaveAsWebp_Path_Encoder()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
        string file = Path.Combine(dir, "SaveAsWebp_Path_Encoder.webp");

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsWebp(file, new WebpEncoder());
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is WebpFormat);
    }

    [Fact]
    public async Task SaveAsWebpAsync_Path_Encoder()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
        string file = Path.Combine(dir, "SaveAsWebpAsync_Path_Encoder.webp");

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsWebpAsync(file, new WebpEncoder());
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is WebpFormat);
    }

    [Fact]
    public void SaveAsWebp_Stream()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsWebp(memoryStream);
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is WebpFormat);
    }

    [Fact]
    public async Task SaveAsWebpAsync_StreamAsync()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsWebpAsync(memoryStream);
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is WebpFormat);
    }

    [Fact]
    public void SaveAsWebp_Stream_Encoder()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsWebp(memoryStream, new WebpEncoder());
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is WebpFormat);
    }

    [Fact]
    public async Task SaveAsWebpAsync_Stream_Encoder()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsWebpAsync(memoryStream, new WebpEncoder());
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is WebpFormat);
    }
}
