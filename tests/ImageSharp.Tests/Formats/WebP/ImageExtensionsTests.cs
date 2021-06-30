// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Webp
{
    [Trait("Format", "Webp")]
    public class ImageExtensionsTests
    {
        private readonly Configuration configuration;

        public ImageExtensionsTests()
        {
            this.configuration = new Configuration();
            this.configuration.AddWebp();
        }

        [Fact]
        public void SaveAsWebp_Path()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTests));
            string file = Path.Combine(dir, "SaveAsWebp_Path.webp");

            using (var image = new Image<Rgba32>(this.configuration, 10, 10))
            {
                image.SaveAsWebp(file);
            }

            using (Image.Load(this.configuration, file, out IImageFormat mime))
            {
                Assert.Equal("image/webp", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsWebpAsync_Path()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTests));
            string file = Path.Combine(dir, "SaveAsWebpAsync_Path.webp");

            using (var image = new Image<Rgba32>(this.configuration, 10, 10))
            {
                await image.SaveAsWebpAsync(file);
            }

            using (Image.Load(this.configuration, file, out IImageFormat mime))
            {
                Assert.Equal("image/webp", mime.DefaultMimeType);
            }
        }

        [Fact]
        public void SaveAsWebp_Path_Encoder()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
            string file = Path.Combine(dir, "SaveAsWebp_Path_Encoder.webp");

            using (var image = new Image<Rgba32>(this.configuration, 10, 10))
            {
                image.SaveAsWebp(file, new WebpEncoder());
            }

            using (Image.Load(this.configuration, file, out IImageFormat mime))
            {
                Assert.Equal("image/webp", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsWebpAsync_Path_Encoder()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
            string file = Path.Combine(dir, "SaveAsWebpAsync_Path_Encoder.webp");

            using (var image = new Image<Rgba32>(this.configuration, 10, 10))
            {
                await image.SaveAsWebpAsync(file, new WebpEncoder());
            }

            using (Image.Load(this.configuration, file, out IImageFormat mime))
            {
                Assert.Equal("image/webp", mime.DefaultMimeType);
            }
        }

        [Fact]
        public void SaveAsWebp_Stream()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(this.configuration, 10, 10))
            {
                image.SaveAsWebp(memoryStream);
            }

            memoryStream.Position = 0;

            using (Image.Load(this.configuration, memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/webp", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsWebpAsync_StreamAsync()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(this.configuration, 10, 10))
            {
                await image.SaveAsWebpAsync(memoryStream);
            }

            memoryStream.Position = 0;

            using (Image.Load(this.configuration, memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/webp", mime.DefaultMimeType);
            }
        }

        [Fact]
        public void SaveAsWebp_Stream_Encoder()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(this.configuration, 10, 10))
            {
                image.SaveAsWebp(memoryStream, new WebpEncoder());
            }

            memoryStream.Position = 0;

            using (Image.Load(this.configuration, memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/webp", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsWebpAsync_Stream_Encoder()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(this.configuration, 10, 10))
            {
                await image.SaveAsWebpAsync(memoryStream, new WebpEncoder());
            }

            memoryStream.Position = 0;

            using (Image.Load(this.configuration, memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/webp", mime.DefaultMimeType);
            }
        }
    }
}
