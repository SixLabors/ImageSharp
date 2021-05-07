// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff
{
    [Trait("Format", "Tiff")]
    public class ImageExtensionsTest
    {
        private readonly Configuration configuration;

        public ImageExtensionsTest()
        {
            this.configuration = new Configuration();
            this.configuration.AddTiff();
        }

        [Fact]
        public void SaveAsTiff_Path()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
            string file = Path.Combine(dir, "SaveAsTiff_Path.tiff");

            using (var image = new Image<Rgba32>(this.configuration, 10, 10))
            {
                image.SaveAsTiff(file);
            }

            using (Image.Load(this.configuration, file, out IImageFormat mime))
            {
                Assert.Equal("image/tiff", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsTiffAsync_Path()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
            string file = Path.Combine(dir, "SaveAsTiffAsync_Path.tiff");

            using (var image = new Image<Rgba32>(this.configuration, 10, 10))
            {
                await image.SaveAsTiffAsync(file);
            }

            using (Image.Load(this.configuration, file, out IImageFormat mime))
            {
                Assert.Equal("image/tiff", mime.DefaultMimeType);
            }
        }

        [Fact]
        public void SaveAsTiff_Path_Encoder()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
            string file = Path.Combine(dir, "SaveAsTiff_Path_Encoder.tiff");

            using (var image = new Image<Rgba32>(this.configuration, 10, 10))
            {
                image.SaveAsTiff(file, new TiffEncoder());
            }

            using (Image.Load(this.configuration, file, out IImageFormat mime))
            {
                Assert.Equal("image/tiff", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsTiffAsync_Path_Encoder()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
            string file = Path.Combine(dir, "SaveAsTiffAsync_Path_Encoder.tiff");

            using (var image = new Image<Rgba32>(this.configuration, 10, 10))
            {
                await image.SaveAsTiffAsync(file, new TiffEncoder());
            }

            using (Image.Load(this.configuration, file, out IImageFormat mime))
            {
                Assert.Equal("image/tiff", mime.DefaultMimeType);
            }
        }

        [Fact]
        public void SaveAsTiff_Stream()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(this.configuration, 10, 10))
            {
                image.SaveAsTiff(memoryStream);
            }

            memoryStream.Position = 0;

            using (Image.Load(this.configuration, memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/tiff", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsTiffAsync_StreamAsync()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(this.configuration, 10, 10))
            {
                await image.SaveAsTiffAsync(memoryStream);
            }

            memoryStream.Position = 0;

            using (Image.Load(this.configuration, memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/tiff", mime.DefaultMimeType);
            }
        }

        [Fact]
        public void SaveAsTiff_Stream_Encoder()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(this.configuration, 10, 10))
            {
                image.SaveAsTiff(memoryStream, new TiffEncoder());
            }

            memoryStream.Position = 0;

            using (Image.Load(this.configuration, memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/tiff", mime.DefaultMimeType);
            }
        }

        [Fact]
        public async Task SaveAsTiffAsync_Stream_Encoder()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<Rgba32>(this.configuration, 10, 10))
            {
                await image.SaveAsTiffAsync(memoryStream, new TiffEncoder());
            }

            memoryStream.Position = 0;

            using (Image.Load(this.configuration, memoryStream, out IImageFormat mime))
            {
                Assert.Equal("image/tiff", mime.DefaultMimeType);
            }
        }
    }
}
