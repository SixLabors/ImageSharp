// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.IO;
using Moq;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Tests the <see cref="Image"/> class.
    /// </summary>
    public class DiscoverImageFormatTests
    {
        private readonly Mock<IFileSystem> fileSystem;
        private readonly string FilePath;
        private readonly IImageFormatDetector localMimeTypeDetector;
        private readonly Mock<IImageFormat> localImageFormatMock;

        public IImageFormat localImageFormat => this.localImageFormatMock.Object;
        public Configuration LocalConfiguration { get; private set; }
        public byte[] Marker { get; private set; }
        public MemoryStream DataStream { get; private set; }
        public byte[] DecodedData { get; private set; }
        private const string localMimeType = "image/local";

        public DiscoverImageFormatTests()
        {
            this.localImageFormatMock = new Mock<IImageFormat>();

            this.localMimeTypeDetector = new MockImageFormatDetector(this.localImageFormatMock.Object);

            this.fileSystem = new Mock<IFileSystem>();

            this.LocalConfiguration = new Configuration
            {
                FileSystem = this.fileSystem.Object
            };

            this.LocalConfiguration.AddImageFormatDetector(this.localMimeTypeDetector);

            TestFormat.RegisterGlobalTestFormat();
            this.Marker = Guid.NewGuid().ToByteArray();
            this.DataStream = TestFormat.GlobalTestFormat.CreateStream(this.Marker);

            this.FilePath = Guid.NewGuid().ToString();
            this.fileSystem.Setup(x => x.OpenRead(this.FilePath)).Returns(this.DataStream);

            TestFileSystem.RegisterGlobalTestFormat();
            TestFileSystem.Global.AddFile(this.FilePath, this.DataStream);
        }

        [Fact]
        public void DiscoverImageFormatByteArray()
        {
            IImageFormat type = Image.DetectFormat(this.DataStream.ToArray());
            Assert.Equal(TestFormat.GlobalTestFormat, type);
        }

        [Fact]
        public void DiscoverImageFormatByteArray_WithConfig()
        {
            IImageFormat type = Image.DetectFormat(this.LocalConfiguration, this.DataStream.ToArray());
            Assert.Equal(this.localImageFormat, type);
        }

        [Fact]
        public void DiscoverImageFormatFile()
        {
            IImageFormat type = Image.DetectFormat(this.FilePath);
            Assert.Equal(TestFormat.GlobalTestFormat, type);
        }

        [Fact]
        public void DiscoverImageFormatFilePath_WithConfig()
        {
            IImageFormat type = Image.DetectFormat(this.LocalConfiguration, this.FilePath);
            Assert.Equal(this.localImageFormat, type);
        }

        [Fact]
        public void DiscoverImageFormatStream()
        {
            IImageFormat type = Image.DetectFormat(this.DataStream);
            Assert.Equal(TestFormat.GlobalTestFormat, type);
        }

        [Fact]
        public void DiscoverImageFormatFileStream_WithConfig()
        {
            IImageFormat type = Image.DetectFormat(this.LocalConfiguration, this.DataStream);
            Assert.Equal(this.localImageFormat, type);
        }

        [Fact]
        public void DiscoverImageFormatNoDetectorsRegisterdShouldReturnNull()
        {
            IImageFormat type = Image.DetectFormat(new Configuration(), this.DataStream);
            Assert.Null(type);
        }
    }
}
