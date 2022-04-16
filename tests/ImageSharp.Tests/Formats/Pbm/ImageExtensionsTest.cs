// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Pbm;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Pbm
{
    public class ImageExtensionsTest
    {
        [Fact]
        public void SaveAsPbm_Path()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
            string file = Path.Combine(dir, "SaveAsPbm_Path.pbm");

            using (var image = new Image<L8>(10, 10))
            {
                image.SaveAsPbm(file);
            }

            using var img = Image.Load(file);
            Assert.Equal("image/x-portable-pixmap", img.Metadata.OrigionalImageFormat.DefaultMimeType);
        }

        [Fact]
        public async Task SaveAsPbmAsync_Path()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensionsTest));
            string file = Path.Combine(dir, "SaveAsPbmAsync_Path.pbm");

            using (var image = new Image<L8>(10, 10))
            {
                await image.SaveAsPbmAsync(file);
            }

            using var img = Image.Load(file);
            Assert.Equal("image/x-portable-pixmap", img.Metadata.OrigionalImageFormat.DefaultMimeType);
        }

        [Fact]
        public void SaveAsPbm_Path_Encoder()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
            string file = Path.Combine(dir, "SaveAsPbm_Path_Encoder.pbm");

            using (var image = new Image<L8>(10, 10))
            {
                image.SaveAsPbm(file, new PbmEncoder());
            }

            using var img = Image.Load(file);
            Assert.Equal("image/x-portable-pixmap", img.Metadata.OrigionalImageFormat.DefaultMimeType);
        }

        [Fact]
        public async Task SaveAsPbmAsync_Path_Encoder()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageExtensions));
            string file = Path.Combine(dir, "SaveAsPbmAsync_Path_Encoder.pbm");

            using (var image = new Image<L8>(10, 10))
            {
                await image.SaveAsPbmAsync(file, new PbmEncoder());
            }

            using var img = Image.Load(file);
            Assert.Equal("image/x-portable-pixmap", img.Metadata.OrigionalImageFormat.DefaultMimeType);
        }

        [Fact]
        public void SaveAsPbm_Stream()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<L8>(10, 10))
            {
                image.SaveAsPbm(memoryStream);
            }

            memoryStream.Position = 0;

            using var img = Image.Load(memoryStream);
            Assert.Equal("image/x-portable-pixmap", img.Metadata.OrigionalImageFormat.DefaultMimeType);
        }

        [Fact]
        public async Task SaveAsPbmAsync_StreamAsync()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<L8>(10, 10))
            {
                await image.SaveAsPbmAsync(memoryStream);
            }

            memoryStream.Position = 0;

            using var img = Image.Load(memoryStream);
            Assert.Equal("image/x-portable-pixmap", img.Metadata.OrigionalImageFormat.DefaultMimeType);
        }

        [Fact]
        public void SaveAsPbm_Stream_Encoder()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<L8>(10, 10))
            {
                image.SaveAsPbm(memoryStream, new PbmEncoder());
            }

            memoryStream.Position = 0;

            using var img = Image.Load(memoryStream);
            Assert.Equal("image/x-portable-pixmap", img.Metadata.OrigionalImageFormat.DefaultMimeType);
        }

        [Fact]
        public async Task SaveAsPbmAsync_Stream_Encoder()
        {
            using var memoryStream = new MemoryStream();

            using (var image = new Image<L8>(10, 10))
            {
                await image.SaveAsPbmAsync(memoryStream, new PbmEncoder());
            }

            memoryStream.Position = 0;

            using var img = Image.Load(memoryStream);
            Assert.Equal("image/x-portable-pixmap", img.Metadata.OrigionalImageFormat.DefaultMimeType);
        }
    }
}
