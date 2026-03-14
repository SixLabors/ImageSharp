// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.OpenExr;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Exr;

[Trait("Format", "Exr")]
public class ImageExtensionsTest
{
    [Fact]
    public void SaveAsExr_Path()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
        string file = Path.Combine(dir, "SaveAsExr_Path.exr");

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsExr(file);
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is ExrFormat);
    }

    [Fact]
    public async Task SaveAsExrAsync_Path()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
        string file = Path.Combine(dir, "SaveAsExrAsync_Path.exr");

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsExrAsync(file);
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is ExrFormat);
    }

    [Fact]
    public void SaveAsExr_Path_Encoder()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
        string file = Path.Combine(dir, "SaveAsExr_Path_Encoder.exr");

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsExr(file, new ExrEncoder());
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is ExrFormat);
    }

    [Fact]
    public async Task SaveAsExrAsync_Path_Encoder()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
        string file = Path.Combine(dir, "SaveAsExrAsync_Path_Encoder.tiff");

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsExrAsync(file, new ExrEncoder());
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is ExrFormat);
    }

    [Fact]
    public void SaveAsExr_Stream()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsExr(memoryStream);
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is ExrFormat);
    }

    [Fact]
    public async Task SaveAsExrAsync_StreamAsync()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsExrAsync(memoryStream);
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is ExrFormat);
    }

    [Fact]
    public void SaveAsExr_Stream_Encoder()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            image.SaveAsTiff(memoryStream, new ExrEncoder());
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is ExrFormat);
    }

    [Fact]
    public async Task SaveAsExrAsync_Stream_Encoder()
    {
        using MemoryStream memoryStream = new();

        using (Image<Rgba32> image = new(10, 10))
        {
            await image.SaveAsExrAsync(memoryStream, new ExrEncoder());
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is ExrFormat);
    }
}
