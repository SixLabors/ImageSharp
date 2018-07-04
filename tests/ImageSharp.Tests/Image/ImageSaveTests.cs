// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.PixelFormats;
using Moq;
using Xunit;
// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests
{
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Tests the <see cref="Image"/> class.
    /// </summary>
    public class ImageSaveTests : IDisposable
    {
        private readonly Image<Rgba32> Image;
        private readonly Mock<IFileSystem> fileSystem;
        private readonly Mock<IImageEncoder> encoder;
        private readonly Mock<IImageEncoder> encoderNotInFormat;
        private IImageFormatDetector localMimeTypeDetector;
        private Mock<IImageFormat> localImageFormat;

        public ImageSaveTests()
        {
            this.localImageFormat = new Mock<IImageFormat>();
            this.localImageFormat.Setup(x => x.FileExtensions).Returns(new[] { "png" });
            this.localMimeTypeDetector = new MockImageFormatDetector(this.localImageFormat.Object);

            this.encoder = new Mock<IImageEncoder>();

            this.encoderNotInFormat = new Mock<IImageEncoder>();

            this.fileSystem = new Mock<IFileSystem>();
            var config = new Configuration()
            {
                FileSystem = this.fileSystem.Object
            };
            config.ImageFormatsManager.AddImageFormatDetector(this.localMimeTypeDetector);
            config.ImageFormatsManager.SetEncoder(this.localImageFormat.Object, this.encoder.Object);
            this.Image = new Image<Rgba32>(config, 1, 1);
        }

        [Fact]
        public void SavePath()
        {
            Stream stream = new MemoryStream();
            this.fileSystem.Setup(x => x.Create("path.png")).Returns(stream);
            this.Image.Save("path.png");

            this.encoder.Verify(x => x.Encode<Rgba32>(this.Image, stream));
        }


        [Fact]
        public void SavePathWithEncoder()
        {
            Stream stream = new MemoryStream();
            this.fileSystem.Setup(x => x.Create("path.jpg")).Returns(stream);

            this.Image.Save("path.jpg", this.encoderNotInFormat.Object);

            this.encoderNotInFormat.Verify(x => x.Encode<Rgba32>(this.Image, stream));
        }

        [Fact]
        public void ToBase64String()
        {
            string str = this.Image.ToBase64String(this.localImageFormat.Object);

            this.encoder.Verify(x => x.Encode<Rgba32>(this.Image, It.IsAny<Stream>()));
        }

        [Fact]
        public void SaveStreamWithMime()
        {
            Stream stream = new MemoryStream();
            this.Image.Save(stream, this.localImageFormat.Object);

            this.encoder.Verify(x => x.Encode<Rgba32>(this.Image, stream));
        }

        [Fact]
        public void SaveStreamWithEncoder()
        {
            Stream stream = new MemoryStream();

            this.Image.Save(stream, this.encoderNotInFormat.Object);

            this.encoderNotInFormat.Verify(x => x.Encode<Rgba32>(this.Image, stream));
        }

        public void Dispose()
        {
            this.Image.Dispose();
        }
    }
}
