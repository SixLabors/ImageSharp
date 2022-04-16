// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Webp
{
    [Trait("Format", "Webp")]
    public class ImageExtensionsTests
    {
        [Fact]
        public void SaveAsWebp_Path()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTests));
            string file = Path.Combine(dir, "SaveAsWebp_Path.webp");

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsWebp(file);
            }

            using var img = Image.Load(file);
            Assert.Equal("image/webp", img.Metadata.OrigionalImageFormat.DefaultMimeType);
        }

        [Fact]
        public async Task SaveAsWebpAsync_Path()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTests));
            string file = Path.Combine(dir, "SaveAsWebpAsync_Path.webp");

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsWebpAsync(file);
            }

            using var img = Image.Load(file);
            Assert.Equal("image/webp", img.Metadata.OrigionalImageFormat.DefaultMimeType);
        }

        [Fact]
        public void SaveAsWebp_Path_Encoder()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
            string file = Path.Combine(dir, "SaveAsWebp_Path_Encoder.webp");

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsWebp(file, new WebpEncoder());
            }

            using var img = Image.Load(file);
            Assert.Equal("image/webp", img.Metadata.OrigionalImageFormat.DefaultMimeType);
        }

        [Fact]
        public async Task SaveAsWebpAsync_Path_Encoder()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
            string file = Path.Combine(dir, "SaveAsWebpAsync_Path_Encoder.webp");

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsWebpAsync(file, new WebpEncoder());
            }

            using var img = Image.Load(file);
            Assert.Equal("image/webp", img.Metadata.OrigionalImageFormat.DefaultMimeType);
        }

        [Fact]
        public void SaveAsWebp_Stream()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsWebp(memoryStream);
            }

            memoryStream.Position = 0;

            using var img = Image.Load(memoryStream);
            Assert.Equal("image/webp", img.Metadata.OrigionalImageFormat.DefaultMimeType);
        }

        [Fact]
        public async Task SaveAsWebpAsync_StreamAsync()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsWebpAsync(memoryStream);
            }

            memoryStream.Position = 0;

            using var img = Image.Load(memoryStream);
            Assert.Equal("image/webp", img.Metadata.OrigionalImageFormat.DefaultMimeType);
        }

        [Fact]
        public void SaveAsWebp_Stream_Encoder()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsWebp(memoryStream, new WebpEncoder());
            }

            memoryStream.Position = 0;

            using var img = Image.Load(memoryStream);
            Assert.Equal("image/webp", img.Metadata.OrigionalImageFormat.DefaultMimeType);
        }

        [Fact]
        public async Task SaveAsWebpAsync_Stream_Encoder()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsWebpAsync(memoryStream, new WebpEncoder());
            }

            memoryStream.Position = 0;

            using var img = Image.Load(memoryStream);
            Assert.Equal("image/webp", img.Metadata.OrigionalImageFormat.DefaultMimeType);
        }
    }
}
