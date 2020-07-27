// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Tga
{
    public class ImageExtensionsTest
    {
        [Fact]
        public void SaveAsTga_Path()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
            string file = Path.Combine(dir, "SaveAsTga_Path.tga");

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsTga(file);
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/tga", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsTgaAsync_Path()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
            string file = Path.Combine(dir, "SaveAsTgaAsync_Path.tga");

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsTgaAsync(file);
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/tga", mime.DefaultMimeType);
            }
        }

        [Fact]
        public void SaveAsTga_Path_Encoder()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
            string file = Path.Combine(dir, "SaveAsTga_Path_Encoder.tga");

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsTga(file, new TgaEncoder());
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/tga", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsTgaAsync_Path_Encoder()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
            string file = Path.Combine(dir, "SaveAsTgaAsync_Path_Encoder.tga");

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsTgaAsync(file, new TgaEncoder());
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/tga", mime.DefaultMimeType);
            }
        }

        [Fact]
        public void SaveAsTga_Stream()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsTga(memoryStream);
            }

            memoryStream.Position = 0;

            using (Image.Load(memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/tga", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsTgaAsync_StreamAsync()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsTgaAsync(memoryStream);
            }

            memoryStream.Position = 0;

            using (Image.Load(memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/tga", mime.DefaultMimeType);
            }
        }

        [Fact]
        public void SaveAsTga_Stream_Encoder()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsTga(memoryStream, new TgaEncoder());
            }

            memoryStream.Position = 0;

            using (Image.Load(memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/tga", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsTgaAsync_Stream_Encoder()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsTgaAsync(memoryStream, new TgaEncoder());
            }

            memoryStream.Position = 0;

            using (Image.Load(memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/tga", mime.DefaultMimeType);
            }
        }
    }
}
