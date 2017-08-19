// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.PixelFormats;
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
        private readonly Mock<IImageFormatDetector> localMimeTypeDetector;
        private readonly Mock<IImageFormat> localImageFormatMock;

        public IImageFormat localImageFormat => localImageFormatMock.Object;
        public Configuration LocalConfiguration { get; private set; }
        public byte[] Marker { get; private set; }
        public MemoryStream DataStream { get; private set; }
        public byte[] DecodedData { get; private set; }
        private const string localMimeType = "image/local";

        public DiscoverImageFormatTests()
        {
            this.localImageFormatMock = new Mock<IImageFormat>();

            this.localMimeTypeDetector = new Mock<IImageFormatDetector>();
            this.localMimeTypeDetector.Setup(x => x.HeaderSize).Returns(1);
            this.localMimeTypeDetector.Setup(x => x.DetectFormat(It.IsAny<ReadOnlySpan<byte>>())).Returns(localImageFormatMock.Object);

            this.fileSystem = new Mock<IFileSystem>();

            this.LocalConfiguration = new Configuration()
            {
                FileSystem = this.fileSystem.Object
            };
            this.LocalConfiguration.AddImageFormatDetector(this.localMimeTypeDetector.Object);

            TestFormat.RegisterGloablTestFormat();
            this.Marker = Guid.NewGuid().ToByteArray();
            this.DataStream = TestFormat.GlobalTestFormat.CreateStream(this.Marker);

            this.FilePath = Guid.NewGuid().ToString();
            this.fileSystem.Setup(x => x.OpenRead(this.FilePath)).Returns(this.DataStream);

            TestFileSystem.RegisterGloablTestFormat();
            TestFileSystem.Global.AddFile(this.FilePath, this.DataStream);
        }

        [Fact]
        public void DiscoverImageFormatByteArray()
        {
            var type = Image.DetectFormat(DataStream.ToArray());
            Assert.Equal(TestFormat.GlobalTestFormat, type);
        }

        [Fact]
        public void DiscoverImageFormatByteArray_WithConfig()
        {
            var type = Image.DetectFormat(this.LocalConfiguration, DataStream.ToArray());
            Assert.Equal(localImageFormat, type);
        }

        [Fact]
        public void DiscoverImageFormatFile()
        {
            var type = Image.DetectFormat(this.FilePath);
            Assert.Equal(TestFormat.GlobalTestFormat, type);
        }

        [Fact]
        public void DiscoverImageFormatFilePath_WithConfig()
        {
            var type = Image.DetectFormat(this.LocalConfiguration, FilePath);
            Assert.Equal(localImageFormat, type);
        }


        [Fact]
        public void DiscoverImageFormatStream()
        {
            var type = Image.DetectFormat(this.DataStream);
            Assert.Equal(TestFormat.GlobalTestFormat, type);
        }

        [Fact]
        public void DiscoverImageFormatFileStream_WithConfig()
        {
            var type = Image.DetectFormat(this.LocalConfiguration, DataStream);
            Assert.Equal(localImageFormat, type);
        }

        [Fact]
        public void DiscoverImageFormatNoDetectorsRegisterdShouldReturnNull()
        {
            var type = Image.DetectFormat(new Configuration(), DataStream);
            Assert.Null(type);
        }
    }
}
