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
        private readonly Mock<IImageFormat> format;
        private readonly Mock<IImageFormat> formatNotRegistered;
        private readonly Mock<IImageEncoder> encoder;
        private readonly Mock<IImageEncoder> encoderNotInFormat;
        private readonly IEncoderOptions encoderOptions;

        public ImageSaveTests()
        {
            this.encoder = new Mock<IImageEncoder>();
            this.format = new Mock<IImageFormat>();
            this.format.Setup(x => x.Encoder).Returns(this.encoder.Object);
            this.format.Setup(x => x.Decoder).Returns(new Mock<IImageDecoder>().Object);
            this.format.Setup(x => x.MimeType).Returns("img/test");
            this.format.Setup(x => x.Extension).Returns("png");
            this.format.Setup(x => x.SupportedExtensions).Returns(new string[] { "png", "jpg" });


            this.encoderNotInFormat = new Mock<IImageEncoder>();
            this.formatNotRegistered = new Mock<IImageFormat>();
            this.formatNotRegistered.Setup(x => x.Encoder).Returns(this.encoderNotInFormat.Object);
            this.formatNotRegistered.Setup(x => x.Decoder).Returns(new Mock<IImageDecoder>().Object);
            this.formatNotRegistered.Setup(x => x.MimeType).Returns("img/test");
            this.formatNotRegistered.Setup(x => x.Extension).Returns("png");
            this.formatNotRegistered.Setup(x => x.SupportedExtensions).Returns(new string[] { "png", "jpg" });

            this.fileSystem = new Mock<IFileSystem>();
            this.encoderOptions = new Mock<IEncoderOptions>().Object;
            this.Image = new Image<Rgba32>(new Configuration(this.format.Object)
            {
                FileSystem = this.fileSystem.Object
            }, 1, 1);
        }

        [Fact]
        public void SavePath()
        {
            Stream stream = new MemoryStream();
            this.fileSystem.Setup(x => x.Create("path.png")).Returns(stream);
            this.Image.Save("path.png");

            this.encoder.Verify(x => x.Encode<Rgba32>(this.Image, stream, null));
        }

        [Fact]
        public void SavePathWithOptions()
        {
            Stream stream = new MemoryStream();
            this.fileSystem.Setup(x => x.Create("path.jpg")).Returns(stream);

            this.Image.Save("path.jpg", this.encoderOptions);

            this.encoder.Verify(x => x.Encode<Rgba32>(this.Image, stream, this.encoderOptions));
        }

        [Fact]
        public void SavePathWithEncoder()
        {
            Stream stream = new MemoryStream();
            this.fileSystem.Setup(x => x.Create("path.jpg")).Returns(stream);

            this.Image.Save("path.jpg", this.encoderNotInFormat.Object);

            this.encoderNotInFormat.Verify(x => x.Encode<Rgba32>(this.Image, stream, null));
        }

        [Fact]
        public void SavePathWithEncoderAndOptions()
        {
            Stream stream = new MemoryStream();
            this.fileSystem.Setup(x => x.Create("path.jpg")).Returns(stream);

            this.Image.Save("path.jpg", this.encoderNotInFormat.Object, this.encoderOptions);

            this.encoderNotInFormat.Verify(x => x.Encode<Rgba32>(this.Image, stream, this.encoderOptions));
        }



        [Fact]
        public void SavePathWithFormat()
        {
            Stream stream = new MemoryStream();
            this.fileSystem.Setup(x => x.Create("path.jpg")).Returns(stream);

            this.Image.Save("path.jpg", this.encoderNotInFormat.Object);

            this.encoderNotInFormat.Verify(x => x.Encode<Rgba32>(this.Image, stream, null));
        }

        [Fact]
        public void SavePathWithFormatAndOptions()
        {
            Stream stream = new MemoryStream();
            this.fileSystem.Setup(x => x.Create("path.jpg")).Returns(stream);

            this.Image.Save("path.jpg", this.encoderNotInFormat.Object, this.encoderOptions);

            this.encoderNotInFormat.Verify(x => x.Encode<Rgba32>(this.Image, stream, this.encoderOptions));
        }

        [Fact]
        public void SaveStream()
        {
            Stream stream = new MemoryStream();
            this.Image.Save(stream);

            this.encoder.Verify(x => x.Encode<Rgba32>(this.Image, stream, null));
        }

        [Fact]
        public void SaveStreamWithOptions()
        {
            Stream stream = new MemoryStream();

            this.Image.Save(stream, this.encoderOptions);

            this.encoder.Verify(x => x.Encode<Rgba32>(this.Image, stream, this.encoderOptions));
        }

        [Fact]
        public void SaveStreamWithEncoder()
        {
            Stream stream = new MemoryStream();

            this.Image.Save(stream, this.encoderNotInFormat.Object);

            this.encoderNotInFormat.Verify(x => x.Encode<Rgba32>(this.Image, stream, null));
        }

        [Fact]
        public void SaveStreamWithEncoderAndOptions()
        {
            Stream stream = new MemoryStream();

            this.Image.Save(stream, this.encoderNotInFormat.Object, this.encoderOptions);

            this.encoderNotInFormat.Verify(x => x.Encode<Rgba32>(this.Image, stream, this.encoderOptions));
        }

        [Fact]
        public void SaveStreamWithFormat()
        {
            Stream stream = new MemoryStream();

            this.Image.Save(stream, this.formatNotRegistered.Object);

            this.encoderNotInFormat.Verify(x => x.Encode<Rgba32>(this.Image, stream, null));
        }

        [Fact]
        public void SaveStreamWithFormatAndOptions()
        {
            Stream stream = new MemoryStream();

            this.Image.Save(stream, this.formatNotRegistered.Object, this.encoderOptions);

            this.encoderNotInFormat.Verify(x => x.Encode<Rgba32>(this.Image, stream, this.encoderOptions));
        }

        public void Dispose()
        {
            this.Image.Dispose();
        }
    }
}
