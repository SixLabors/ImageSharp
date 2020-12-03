// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    [Trait("Format", "Jpg")]
    public class ImageExtensionsTest
    {
        [Fact]
        public void SaveAsJpeg_Path()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
            string file = Path.Combine(dir, "SaveAsJpeg_Path.jpg");

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsJpeg(file);
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/jpeg", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsJpegAsync_Path()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
            string file = Path.Combine(dir, "SaveAsJpegAsync_Path.jpg");

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsJpegAsync(file);
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/jpeg", mime.DefaultMimeType);
            }
        }

        [Fact]
        public void SaveAsJpeg_Path_Encoder()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
            string file = Path.Combine(dir, "SaveAsJpeg_Path_Encoder.jpg");

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsJpeg(file, new JpegEncoder());
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/jpeg", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsJpegAsync_Path_Encoder()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
            string file = Path.Combine(dir, "SaveAsJpegAsync_Path_Encoder.jpg");

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsJpegAsync(file, new JpegEncoder());
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/jpeg", mime.DefaultMimeType);
            }
        }

        [Fact]
        public void SaveAsJpeg_Stream()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsJpeg(memoryStream);
            }

            memoryStream.Position = 0;

            using (Image.Load(memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/jpeg", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsJpegAsync_StreamAsync()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsJpegAsync(memoryStream);
            }

            memoryStream.Position = 0;

            using (Image.Load(memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/jpeg", mime.DefaultMimeType);
            }
        }

        [Fact]
        public void SaveAsJpeg_Stream_Encoder()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsJpeg(memoryStream, new JpegEncoder());
            }

            memoryStream.Position = 0;

            using (Image.Load(memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/jpeg", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsJpegAsync_Stream_Encoder()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsJpegAsync(memoryStream, new JpegEncoder());
            }

            memoryStream.Position = 0;

            using (Image.Load(memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/jpeg", mime.DefaultMimeType);
            }
        }
    }
}
