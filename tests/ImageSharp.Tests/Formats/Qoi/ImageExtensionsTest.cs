// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Qoi;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Qoi;

public class ImageExtensionsTest
{
    [Fact]
    public void SaveAsQoi_Path()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
        string file = Path.Combine(dir, "SaveAsQoi_Path.qoi");

        using (Image<L8> image = new(10, 10))
        {
            image.SaveAsQoi(file);
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is QoiFormat);
    }

    [Fact]
    public async Task SaveAsQoiAsync_Path()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
        string file = Path.Combine(dir, "SaveAsQoiAsync_Path.qoi");

        using (Image<L8> image = new(10, 10))
        {
            await image.SaveAsQoiAsync(file);
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is QoiFormat);
    }

    [Fact]
    public void SaveAsQoi_Path_Encoder()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
        string file = Path.Combine(dir, "SaveAsQoi_Path_Encoder.qoi");

        using (Image<L8> image = new(10, 10))
        {
            image.SaveAsQoi(file, new QoiEncoder());
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is QoiFormat);
    }

    [Fact]
    public async Task SaveAsQoiAsync_Path_Encoder()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
        string file = Path.Combine(dir, "SaveAsQoiAsync_Path_Encoder.qoi");

        using (Image<L8> image = new(10, 10))
        {
            await image.SaveAsQoiAsync(file, new QoiEncoder());
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is QoiFormat);
    }

    [Fact]
    public void SaveAsQoi_Stream()
    {
        using MemoryStream memoryStream = new();

        using (Image<L8> image = new(10, 10))
        {
            image.SaveAsQoi(memoryStream);
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is QoiFormat);
    }

    [Fact]
    public async Task SaveAsQoiAsync_StreamAsync()
    {
        using MemoryStream memoryStream = new();

        using (Image<L8> image = new(10, 10))
        {
            await image.SaveAsQoiAsync(memoryStream);
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is QoiFormat);
    }

    [Fact]
    public void SaveAsQoi_Stream_Encoder()
    {
        using MemoryStream memoryStream = new();

        using (Image<L8> image = new(10, 10))
        {
            image.SaveAsQoi(memoryStream, new QoiEncoder());
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is QoiFormat);
    }

    [Fact]
    public async Task SaveAsQoiAsync_Stream_Encoder()
    {
        using MemoryStream memoryStream = new();

        using (Image<L8> image = new(10, 10))
        {
            await image.SaveAsQoiAsync(memoryStream, new QoiEncoder());
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is QoiFormat);
    }
}
