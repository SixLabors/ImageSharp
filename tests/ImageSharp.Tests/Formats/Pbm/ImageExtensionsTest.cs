// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Pbm;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Pbm;

public class ImageExtensionsTest
{
    [Fact]
    public void SaveAsPbm_Path()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
        string file = Path.Combine(dir, "SaveAsPbm_Path.pbm");

        using (Image<L8> image = new(10, 10))
        {
            image.SaveAsPbm(file);
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is PbmFormat);
    }

    [Fact]
    public async Task SaveAsPbmAsync_Path()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
        string file = Path.Combine(dir, "SaveAsPbmAsync_Path.pbm");

        using (Image<L8> image = new(10, 10))
        {
            await image.SaveAsPbmAsync(file);
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is PbmFormat);
    }

    [Fact]
    public void SaveAsPbm_Path_Encoder()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
        string file = Path.Combine(dir, "SaveAsPbm_Path_Encoder.pbm");

        using (Image<L8> image = new(10, 10))
        {
            image.SaveAsPbm(file, new PbmEncoder());
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is PbmFormat);
    }

    [Fact]
    public async Task SaveAsPbmAsync_Path_Encoder()
    {
        string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
        string file = Path.Combine(dir, "SaveAsPbmAsync_Path_Encoder.pbm");

        using (Image<L8> image = new(10, 10))
        {
            await image.SaveAsPbmAsync(file, new PbmEncoder());
        }

        IImageFormat format = Image.DetectFormat(file);
        Assert.True(format is PbmFormat);
    }

    [Fact]
    public void SaveAsPbm_Stream()
    {
        using MemoryStream memoryStream = new();

        using (Image<L8> image = new(10, 10))
        {
            image.SaveAsPbm(memoryStream);
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is PbmFormat);
    }

    [Fact]
    public async Task SaveAsPbmAsync_StreamAsync()
    {
        using MemoryStream memoryStream = new();

        using (Image<L8> image = new(10, 10))
        {
            await image.SaveAsPbmAsync(memoryStream);
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is PbmFormat);
    }

    [Fact]
    public void SaveAsPbm_Stream_Encoder()
    {
        using MemoryStream memoryStream = new();

        using (Image<L8> image = new(10, 10))
        {
            image.SaveAsPbm(memoryStream, new PbmEncoder());
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is PbmFormat);
    }

    [Fact]
    public async Task SaveAsPbmAsync_Stream_Encoder()
    {
        using MemoryStream memoryStream = new();

        using (Image<L8> image = new(10, 10))
        {
            await image.SaveAsPbmAsync(memoryStream, new PbmEncoder());
        }

        memoryStream.Position = 0;

        IImageFormat format = Image.DetectFormat(memoryStream);
        Assert.True(format is PbmFormat);
    }
}
