// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Experimental.WebP;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Webp
{
    [Trait("Format", "Webp")]
    public class ImageExtensionsTest
    {
        public ImageExtensionsTest()
        {
            Configuration.Default.ImageFormatsManager.AddImageFormat(WebPFormat.Instance);
            Configuration.Default.ImageFormatsManager.AddImageFormatDetector(new WebPImageFormatDetector());
            Configuration.Default.ImageFormatsManager.SetDecoder(WebPFormat.Instance, new WebPDecoder());
            Configuration.Default.ImageFormatsManager.SetEncoder(WebPFormat.Instance, new WebPEncoder());
        }

        [Fact]
        public void SaveAsWebp_Path()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
            string file = Path.Combine(dir, "SaveAsWebp_Path.webp");

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsWebP(file);
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/webp", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsWebpAsync_Path()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
            string file = Path.Combine(dir, "SaveAsWebpAsync_Path.webp");

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsWebPAsync(file);
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/webp", mime.DefaultMimeType);
            }
        }

        [Fact]
        public void SaveAsWebp_Path_Encoder()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
            string file = Path.Combine(dir, "SaveAsWebp_Path_Encoder.webp");

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsWebP(file, new WebPEncoder());
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/webp", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsWebpAsync_Path_Encoder()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
            string file = Path.Combine(dir, "SaveAsWebpAsync_Path_Encoder.webp");

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsWebPAsync(file, new WebPEncoder());
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/webp", mime.DefaultMimeType);
            }
        }

        [Fact]
        public void SaveAsWebp_Stream()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsWebP(memoryStream);
            }

            memoryStream.Position = 0;

            using (Image.Load(memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/webp", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsWebpAsync_StreamAsync()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsWebPAsync(memoryStream);
            }

            memoryStream.Position = 0;

            using (Image.Load(memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/webp", mime.DefaultMimeType);
            }
        }

        [Fact]
        public void SaveAsWebp_Stream_Encoder()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsWebp(memoryStream, new WebPEncoder());
            }

            memoryStream.Position = 0;

            using (Image.Load(memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/webp", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsWebpAsync_Stream_Encoder()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsWebPAsync(memoryStream, new WebPEncoder());
            }

            memoryStream.Position = 0;

            using (Image.Load(memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/webp", mime.DefaultMimeType);
            }
        }
    }
}
