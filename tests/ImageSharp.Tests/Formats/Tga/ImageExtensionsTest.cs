// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Tga;

public class ImageExtensionsTest
{
    [Fact]
    public void SaveAsTga_Path()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
        string file = Path.Combine(dir, "SaveAsTga_Path.tga");

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsTga(file);
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is TgaFormat);
    }

    [Fact]
    public async Task SaveAsTgaAsync_Path()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
        string file = Path.Combine(dir, "SaveAsTgaAsync_Path.tga");

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsTgaAsync(file);
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is TgaFormat);
    }

    [Fact]
    public void SaveAsTga_Path_Encoder()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
        string file = Path.Combine(dir, "SaveAsTga_Path_Encoder.tga");

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsTga(file, new TgaEncoder());
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is TgaFormat);
    }

    [Fact]
    public async Task SaveAsTgaAsync_Path_Encoder()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
        string file = Path.Combine(dir, "SaveAsTgaAsync_Path_Encoder.tga");

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsTgaAsync(file, new TgaEncoder());
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is TgaFormat);
    }

    [Fact]
    public void SaveAsTga_Stream()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsTga(memoryStream);
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is TgaFormat);
    }

    [Fact]
    public async Task SaveAsTgaAsync_StreamAsync()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsTgaAsync(memoryStream);
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is TgaFormat);
    }

    [Fact]
    public void SaveAsTga_Stream_Encoder()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsTga(memoryStream, new TgaEncoder());
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is TgaFormat);
    }

    [Fact]
    public async Task SaveAsTgaAsync_Stream_Encoder()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsTgaAsync(memoryStream, new TgaEncoder());
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is TgaFormat);
    }
}
