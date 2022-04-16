// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff
{
    [Trait("Format", "Tiff")]
    public class ImageExtensionsTest
    {
        [Fact]
        public void SaveAsTiff_Path()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
            string file = Path.Combine(dir, "SaveAsTiff_Path.tiff");

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsTiff(file);
            }

            using var img = Image.Load(file);
            Assert.Equal("image/tiff", img.Metadata.OrigionalImageFormat.DefaultMimeType);
        }

        [Fact]
        public async Task SaveAsTiffAsync_Path()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
            string file = Path.Combine(dir, "SaveAsTiffAsync_Path.tiff");

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsTiffAsync(file);
            }

            using var img = Image.Load(file);
            Assert.Equal("image/tiff", img.Metadata.OrigionalImageFormat.DefaultMimeType);
        }

        [Fact]
        public void SaveAsTiff_Path_Encoder()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
            string file = Path.Combine(dir, "SaveAsTiff_Path_Encoder.tiff");

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsTiff(file, new TiffEncoder());
            }

            using var img = Image.Load(file);
            Assert.Equal("image/tiff", img.Metadata.OrigionalImageFormat.DefaultMimeType);
        }

        [Fact]
        public async Task SaveAsTiffAsync_Path_Encoder()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
            string file = Path.Combine(dir, "SaveAsTiffAsync_Path_Encoder.tiff");

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsTiffAsync(file, new TiffEncoder());
            }

            using var img = Image.Load(file);
            Assert.Equal("image/tiff", img.Metadata.OrigionalImageFormat.DefaultMimeType);
        }

        [Fact]
        public void SaveAsTiff_Stream()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsTiff(memoryStream);
            }

            memoryStream.Position = 0;

            using var img = Image.Load(memoryStream);
            Assert.Equal("image/tiff", img.Metadata.OrigionalImageFormat.DefaultMimeType);
        }

        [Fact]
        public async Task SaveAsTiffAsync_StreamAsync()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsTiffAsync(memoryStream);
            }

            memoryStream.Position = 0;

            using var img = Image.Load(memoryStream);
            Assert.Equal("image/tiff", img.Metadata.OrigionalImageFormat.DefaultMimeType);
        }

        [Fact]
        public void SaveAsTiff_Stream_Encoder()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.SaveAsTiff(memoryStream, new TiffEncoder());
            }

            memoryStream.Position = 0;

            using var img = Image.Load(memoryStream);
            Assert.Equal("image/tiff", img.Metadata.OrigionalImageFormat.DefaultMimeType);
        }

        [Fact]
        public async Task SaveAsTiffAsync_Stream_Encoder()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(10, 10))
            {
                await image.SaveAsTiffAsync(memoryStream, new TiffEncoder());
            }

            memoryStream.Position = 0;

            using var img = Image.Load(memoryStream);
            Assert.Equal("image/tiff", img.Metadata.OrigionalImageFormat.DefaultMimeType);
        }
    }
}
