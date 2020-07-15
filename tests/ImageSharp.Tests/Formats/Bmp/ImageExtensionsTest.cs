// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Bmp
{
    public class ImageExtensionsTest
    {
        [Fact]
        public void SaveAsBmp_Path()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
            string file = Path.Combine(dir, "SaveAsBmp_Path.bmp");

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsBmp(file);
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/bmp", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsBmpAsync_Path()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
            string file = Path.Combine(dir, "SaveAsBmpAsync_Path.bmp");

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsBmpAsync(file);
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/bmp", mime.DefaultMimeType);
            }
        }

        [Fact]
        public void SaveAsBmp_Path_Encoder()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
            string file = Path.Combine(dir, "SaveAsBmp_Path_Encoder.bmp");

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsBmp(file, new BmpEncoder());
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/bmp", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsBmpAsync_Path_Encoder()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
            string file = Path.Combine(dir, "SaveAsBmpAsync_Path_Encoder.bmp");

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsBmpAsync(file, new BmpEncoder());
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/bmp", mime.DefaultMimeType);
            }
        }

        [Fact]
        public void SaveAsBmp_Stream()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsBmp(memoryStream);
            }

            memoryStream.Position = 0;

            using (Image.Load(memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/bmp", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsBmpAsync_StreamAsync()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsBmpAsync(memoryStream);
            }

            memoryStream.Position = 0;

            using (Image.Load(memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/bmp", mime.DefaultMimeType);
            }
        }

        [Fact]
        public void SaveAsBmp_Stream_Encoder()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsBmp(memoryStream, new BmpEncoder());
            }

            memoryStream.Position = 0;

            using (Image.Load(memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/bmp", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsBmpAsync_Stream_Encoder()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsBmpAsync(memoryStream, new BmpEncoder());
            }

            memoryStream.Position = 0;

            using (Image.Load(memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/bmp", mime.DefaultMimeType);
            }
        }
    }
}
