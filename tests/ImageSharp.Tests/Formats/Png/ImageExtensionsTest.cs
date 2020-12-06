// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Png
{
    [Trait("Format", "Png")]
    public class ImageExtensionsTest
    {
        [Fact]
        public void SaveAsPng_Path()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
            string file = Path.Combine(dir, "SaveAsPng_Path.png");

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsPng(file);
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/png", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsPngAsync_Path()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
            string file = Path.Combine(dir, "SaveAsPngAsync_Path.png");

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsPngAsync(file);
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/png", mime.DefaultMimeType);
            }
        }

        [Fact]
        public void SaveAsPng_Path_Encoder()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
            string file = Path.Combine(dir, "SaveAsPng_Path_Encoder.png");

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsPng(file, new PngEncoder());
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/png", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsPngAsync_Path_Encoder()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
            string file = Path.Combine(dir, "SaveAsPngAsync_Path_Encoder.png");

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsPngAsync(file, new PngEncoder());
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/png", mime.DefaultMimeType);
            }
        }

        [Fact]
        public void SaveAsPng_Stream()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsPng(memoryStream);
            }

            memoryStream.Position = 0;

            using (Image.Load(memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/png", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsPngAsync_StreamAsync()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsPngAsync(memoryStream);
            }

            memoryStream.Position = 0;

            using (Image.Load(memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/png", mime.DefaultMimeType);
            }
        }

        [Fact]
        public void SaveAsPng_Stream_Encoder()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsPng(memoryStream, new PngEncoder());
            }

            memoryStream.Position = 0;

            using (Image.Load(memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/png", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsPngAsync_Stream_Encoder()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsPngAsync(memoryStream, new PngEncoder());
            }

            memoryStream.Position = 0;

            using (Image.Load(memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/png", mime.DefaultMimeType);
            }
        }
    }
}
