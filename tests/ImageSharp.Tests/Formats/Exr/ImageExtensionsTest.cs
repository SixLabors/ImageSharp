// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.OpenExr;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Exr;

[Trait("Format", "Exr")]
public class ImageExtensionsTest
{
    [Fact]
    public void SaveAsExr_Stream()
    {
        using var memoryStream = new MemoryStream();

        using (var image = new Image<Rgba32>(10, 10))
        {
            image.SaveAsOpenExr(memoryStream, new ExrEncoder());
        }

        memoryStream.Position = 0;

        using (Image.Load(memoryStream, out IImageFormat mime))
        {
            Assert.Equal("image/x-exr", mime.DefaultMimeType);
        }
    }

    [Fact]
    public void SaveAsExr_Stream_Encoder()
    {
        using var memoryStream = new MemoryStream();

        using (var image = new Image<Rgba32>(10, 10))
        {
            image.SaveAsOpenExr(memoryStream, new ExrEncoder());
        }

        memoryStream.Position = 0;

        using (Image.Load(memoryStream, out IImageFormat mime))
        {
            Assert.Equal("image/x-exr", mime.DefaultMimeType);
        }
    }

    [Fact]
    public async Task SaveAsExrAsync_Stream_Encoder()
    {
        using var memoryStream = new MemoryStream();

        using (var image = new Image<Rgba32>(10, 10))
        {
            await image.SaveAsOpenExrAsync(memoryStream, new ExrEncoder());
        }

        memoryStream.Position = 0;

        using (Image.Load(memoryStream, out IImageFormat mime))
        {
            Assert.Equal("image/x-exr", mime.DefaultMimeType);
        }
    }
}

