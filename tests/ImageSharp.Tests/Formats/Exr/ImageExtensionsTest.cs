// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.OpenExr;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Exr
{
    [Trait("Format", "Exr")]
    public class ImageExtensionsTest
    {
        [Fact]
        public void SaveAsExr_Path()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
            string file = Path.Combine(dir, "SaveAsExr_Path.exr");

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsOpenExr(file);
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/x-exr", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsExrAsync_Path()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
            string file = Path.Combine(dir, "SaveAsExrAsync_Path.exr");

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsOpenExrAsync(file);
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/x-exr", mime.DefaultMimeType);
            }
        }

        [Fact]
        public void SaveAsExr_Path_Encoder()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
            string file = Path.Combine(dir, "SaveAsExr_Path_Encoder.exr");

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsOpenExr(file, new ExrEncoder());
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/x-exr", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsExrAsync_Path_Encoder()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
            string file = Path.Combine(dir, "SaveAsExrAsync_Path_Encoder.tiff");

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsOpenExrAsync(file, new ExrEncoder());
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/x-exr", mime.DefaultMimeType);
            }
        }

        [Fact]
        public void SaveAsExr_Stream()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsOpenExr(memoryStream);
            }

            memoryStream.Position = 0;

            using (Image.Load(memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/x-exr", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsExrAsync_StreamAsync()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsOpenExrAsync(memoryStream);
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
}
