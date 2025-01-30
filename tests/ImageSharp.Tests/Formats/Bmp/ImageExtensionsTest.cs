// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Bmp;

public class ImageExtensionsTest
{
    [Fact]
    public void SaveAsBmp_Path()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
        string file = Path.Combine(dir, "SaveAsBmp_Path.bmp");

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsBmp(file);
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is BmpFormat);
    }

    [Fact]
    public async Task SaveAsBmpAsync_Path()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
        string file = Path.Combine(dir, "SaveAsBmpAsync_Path.bmp");

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsBmpAsync(file);
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is BmpFormat);
    }

    [Fact]
    public void SaveAsBmp_Path_Encoder()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
        string file = Path.Combine(dir, "SaveAsBmp_Path_Encoder.bmp");

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsBmp(file, new());
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is BmpFormat);
    }

    [Fact]
    public async Task SaveAsBmpAsync_Path_Encoder()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
        string file = Path.Combine(dir, "SaveAsBmpAsync_Path_Encoder.bmp");

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsBmpAsync(file, new BmpEncoder());
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is BmpFormat);
    }

    [Fact]
    public void SaveAsBmp_Stream()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsBmp(memoryStream);
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is BmpFormat);
    }

    [Fact]
    public async Task SaveAsBmpAsync_StreamAsync()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsBmpAsync(memoryStream);
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is BmpFormat);
    }

    [Fact]
    public void SaveAsBmp_Stream_Encoder()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsBmp(memoryStream, new());
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is BmpFormat);
    }

    [Fact]
    public async Task SaveAsBmpAsync_Stream_Encoder()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsBmpAsync(memoryStream, new BmpEncoder());
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is BmpFormat);
    }
}
