// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg;

[Trait("Format", "Jpg")]
public class ImageExtensionsTest
{
    [Fact]
    public void SaveAsJpeg_Path()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
        string file = Path.Combine(dir, "SaveAsJpeg_Path.jpg");

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsJpeg(file);
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is JpegFormat);
    }

    [Fact]
    public async Task SaveAsJpegAsync_Path()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
        string file = Path.Combine(dir, "SaveAsJpegAsync_Path.jpg");

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsJpegAsync(file);
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is JpegFormat);
    }

    [Fact]
    public void SaveAsJpeg_Path_Encoder()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
        string file = Path.Combine(dir, "SaveAsJpeg_Path_Encoder.jpg");

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsJpeg(file, new());
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is JpegFormat);
    }

    [Fact]
    public async Task SaveAsJpegAsync_Path_Encoder()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
        string file = Path.Combine(dir, "SaveAsJpegAsync_Path_Encoder.jpg");

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsJpegAsync(file, new JpegEncoder());
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is JpegFormat);
    }

    [Fact]
    public void SaveAsJpeg_Stream()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsJpeg(memoryStream);
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is JpegFormat);
    }

    [Fact]
    public async Task SaveAsJpegAsync_StreamAsync()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsJpegAsync(memoryStream);
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is JpegFormat);
    }

    [Fact]
    public void SaveAsJpeg_Stream_Encoder()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsJpeg(memoryStream, new());
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is JpegFormat);
    }

    [Fact]
    public async Task SaveAsJpegAsync_Stream_Encoder()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsJpegAsync(memoryStream, new JpegEncoder());
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is JpegFormat);
    }
}
