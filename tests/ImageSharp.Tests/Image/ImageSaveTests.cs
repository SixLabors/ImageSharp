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
    using Moq;
    using Xunit;

    /// <summary>
    /// Tests the <see cref="Image"/> class.
    /// </summary>
    public class ImageSaveTests : IDisposable
    {
        private readonly SaveWatchingImage Image;
        private readonly Mock<IFileSystem> fileSystem;
        private readonly IEncoderOptions encoderOptions;

        public ImageSaveTests()
        {
            this.fileSystem = new Mock<IFileSystem>();
            this.encoderOptions = new Mock<IEncoderOptions>().Object;
            this.Image = new SaveWatchingImage(1, 1, this.fileSystem.Object);
        }

        [Fact]
        public void SavePath()
        {
            Stream stream = new MemoryStream();
            this.fileSystem.Setup(x => x.Create("path.png")).Returns(stream);
            this.Image.Save("path.png");

            SaveWatchingImage.OperationDetails operation = this.Image.Saves.Single();
            Assert.Equal(stream, operation.stream);
            Assert.IsType<PngEncoder>(operation.encoder);
            Assert.Null(operation.options);
        }

        [Fact]
        public void SavePathWithOptions()
        {
            Stream stream = new MemoryStream();
            this.fileSystem.Setup(x => x.Create("path.jpg")).Returns(stream);

            this.Image.Save("path.jpg", this.encoderOptions);

            SaveWatchingImage.OperationDetails operation = this.Image.Saves.Single();
            Assert.Equal(stream, operation.stream);
            Assert.IsType<JpegEncoder>(operation.encoder);
            Assert.Equal(this.encoderOptions, operation.options);
        }

        [Fact]
        public void SavePathWithEncoder()
        {
            Stream stream = new MemoryStream();
            this.fileSystem.Setup(x => x.Create("path.jpg")).Returns(stream);

            this.Image.Save("path.jpg", new BmpEncoder());

            SaveWatchingImage.OperationDetails operation = this.Image.Saves.Single();
            Assert.Equal(stream, operation.stream);
            Assert.IsType<BmpEncoder>(operation.encoder);
            Assert.Null(operation.options);
        }

        [Fact]
        public void SavePathWithEncoderAndOptions()
        {
            Stream stream = new MemoryStream();
            this.fileSystem.Setup(x => x.Create("path.jpg")).Returns(stream);

            this.Image.Save("path.jpg", new BmpEncoder(), this.encoderOptions);

            SaveWatchingImage.OperationDetails operation = this.Image.Saves.Single();
            Assert.Equal(stream, operation.stream);
            Assert.IsType<BmpEncoder>(operation.encoder);
            Assert.Equal(this.encoderOptions, operation.options);
        }



        [Fact]
        public void SavePathWithFormat()
        {
            Stream stream = new MemoryStream();
            this.fileSystem.Setup(x => x.Create("path.jpg")).Returns(stream);

            this.Image.Save("path.jpg", new GifFormat());

            SaveWatchingImage.OperationDetails operation = this.Image.Saves.Single();
            Assert.Equal(stream, operation.stream);
            Assert.IsType<GifEncoder>(operation.encoder);
            Assert.Null(operation.options);
        }

        [Fact]
        public void SavePathWithFormatAndOptions()
        {
            Stream stream = new MemoryStream();
            this.fileSystem.Setup(x => x.Create("path.jpg")).Returns(stream);

            this.Image.Save("path.jpg", new BmpFormat(), this.encoderOptions);

            SaveWatchingImage.OperationDetails operation = this.Image.Saves.Single();
            Assert.Equal(stream, operation.stream);
            Assert.IsType<BmpEncoder>(operation.encoder);
            Assert.Equal(this.encoderOptions, operation.options);
        }

        /// <summary>
        /// /////////////////////////////////////////////////////////////
        /// </summary>
        /// 

        [Fact]
        public void SaveStream()
        {
            Stream stream = new MemoryStream();
            this.Image.Save(stream);

            SaveWatchingImage.OperationDetails operation = this.Image.Saves.Single();
            Assert.Equal(stream, operation.stream);
            Assert.IsType(this.Image.CurrentImageFormat.Encoder.GetType(), operation.encoder);
            Assert.Null(operation.options);
        }

        [Fact]
        public void SaveStreamWithOptions()
        {
            Stream stream = new MemoryStream();

            this.Image.Save(stream, this.encoderOptions);

            SaveWatchingImage.OperationDetails operation = this.Image.Saves.Single();
            Assert.Equal(stream, operation.stream);
            Assert.IsType(this.Image.CurrentImageFormat.Encoder.GetType(), operation.encoder);

            Assert.Equal(this.encoderOptions, operation.options);
        }

        [Fact]
        public void SaveStreamWithEncoder()
        {
            Stream stream = new MemoryStream();

            this.Image.Save(stream, new BmpEncoder());

            SaveWatchingImage.OperationDetails operation = this.Image.Saves.Single();
            Assert.Equal(stream, operation.stream);
            Assert.IsType<BmpEncoder>(operation.encoder);
            Assert.Null(operation.options);
        }

        [Fact]
        public void SaveStreamWithEncoderAndOptions()
        {
            Stream stream = new MemoryStream();

            this.Image.Save(stream, new BmpEncoder(), this.encoderOptions);

            SaveWatchingImage.OperationDetails operation = this.Image.Saves.Single();
            Assert.Equal(stream, operation.stream);
            Assert.IsType<BmpEncoder>(operation.encoder);
            Assert.Equal(this.encoderOptions, operation.options);
        }

        [Fact]
        public void SaveStreamWithFormat()
        {
            Stream stream = new MemoryStream();

            this.Image.Save(stream, new GifFormat());

            SaveWatchingImage.OperationDetails operation = this.Image.Saves.Single();
            Assert.Equal(stream, operation.stream);
            Assert.IsType<GifEncoder>(operation.encoder);
            Assert.Null(operation.options);
        }

        [Fact]
        public void SaveStreamWithFormatAndOptions()
        {
            Stream stream = new MemoryStream();

            this.Image.Save(stream, new BmpFormat(), this.encoderOptions);

            SaveWatchingImage.OperationDetails operation = this.Image.Saves.Single();
            Assert.Equal(stream, operation.stream);
            Assert.IsType<BmpEncoder>(operation.encoder);
            Assert.Equal(this.encoderOptions, operation.options);
        }

        public void Dispose()
        {
            this.Image.Dispose();
        }
    }
}
