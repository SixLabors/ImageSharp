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

        public ImageSaveTests()
        {
            this.fileSystem = new Mock<IFileSystem>();
            this.Image = new SaveWatchingImage(1, 1, this.fileSystem.Object);
        }

        [Fact]
        public void SavePath()
        {
            Stream stream = new MemoryStream();
            this.fileSystem.Setup(x => x.OpenWrite("path")).Returns(stream);
            this.Image.Save("path");

            SaveWatchingImage.OperationDetails operation = this.Image.Saves.Single();
            Assert.Equal(stream, operation.stream);
            Assert.Equal(this.Image.CurrentImageFormat.Encoder, operation.encoder);
            Assert.Null(operation.options);
        }

        public void Dispose()
        {
            this.Image.Dispose();
        }
    }
}
