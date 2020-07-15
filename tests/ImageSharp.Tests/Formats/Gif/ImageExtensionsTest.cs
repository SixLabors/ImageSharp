// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Gif
{
    public class ImageExtensionsTest
    {
        [Fact]
        public void SaveAsGif_Path()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
            string file = Path.Combine(dir, "SaveAsGif_Path.gif");

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsGif(file);
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/gif", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsGifAsync_Path()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
            string file = Path.Combine(dir, "SaveAsGifAsync_Path.gif");

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsGifAsync(file);
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/gif", mime.DefaultMimeType);
            }
        }

        [Fact]
        public void SaveAsGif_Path_Encoder()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
            string file = Path.Combine(dir, "SaveAsGif_Path_Encoder.gif");

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsGif(file, new GifEncoder());
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/gif", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsGifAsync_Path_Encoder()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
            string file = Path.Combine(dir, "SaveAsGifAsync_Path_Encoder.gif");

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsGifAsync(file, new GifEncoder());
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/gif", mime.DefaultMimeType);
            }
        }

        [Fact]
        public void SaveAsGif_Stream()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsGif(memoryStream);
            }

            memoryStream.Position = 0;

            using (Image.Load(memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/gif", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsGifAsync_StreamAsync()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsGifAsync(memoryStream);
            }

            memoryStream.Position = 0;

            using (Image.Load(memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/gif", mime.DefaultMimeType);
            }
        }

        [Fact]
        public void SaveAsGif_Stream_Encoder()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsGif(memoryStream, new GifEncoder());
            }

            memoryStream.Position = 0;

            using (Image.Load(memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/gif", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsGifAsync_Stream_Encoder()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsGifAsync(memoryStream, new GifEncoder());
            }

            memoryStream.Position = 0;

            using (Image.Load(memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/gif", mime.DefaultMimeType);
            }
        }
    }
}
