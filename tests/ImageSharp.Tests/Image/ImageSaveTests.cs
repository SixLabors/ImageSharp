// <copyright file="PixelAccessorTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using ImageSharp.Formats;
    using ImageSharp.IO;
    using ImageSharp.PixelFormats;

    using Moq;
    using Xunit;

    /// <summary>
    /// Tests the <see cref="Image"/> class.
    /// </summary>
    public class ImageSaveTests : IDisposable
    {
        private readonly Image<Rgba32> Image;
        private readonly Mock<IFileSystem> fileSystem;
        private readonly Mock<IImageEncoder> encoder;
        private readonly Mock<IImageEncoder> encoderNotInFormat;
        private Mock<IMimeTypeDetector> localMimeTypeDetector;

        public ImageSaveTests()
        {
            this.localMimeTypeDetector = new Mock<IMimeTypeDetector>();
            this.localMimeTypeDetector.Setup(x => x.HeaderSize).Returns(1);
            this.localMimeTypeDetector.Setup(x => x.DetectMimeType(It.IsAny<Span<byte>>())).Returns("img/test");

            this.encoder = new Mock<IImageEncoder>();

            this.encoderNotInFormat = new Mock<IImageEncoder>();

            this.fileSystem = new Mock<IFileSystem>();
            var config = new Configuration()
            {
                FileSystem = this.fileSystem.Object
            };
            config.AddMimeTypeDetector(this.localMimeTypeDetector.Object);
            config.SetMimeTypeEncoder("img/test", this.encoder.Object);
            config.SetFileExtensionToMimeTypeMapping("png", "img/test");
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
            var str = this.Image.ToBase64String("img/test");

            this.encoder.Verify(x => x.Encode<Rgba32>(this.Image, It.IsAny<Stream>()));
        }

        [Fact]
        public void SaveStreamWithMime()
        {
            Stream stream = new MemoryStream();
            this.Image.Save(stream, "img/test");

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
