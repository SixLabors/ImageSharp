// <copyright file="PixelAccessorTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.IO;

    using ImageSharp.Formats;
    using ImageSharp.IO;
    using ImageSharp.PixelFormats;
    using Moq;
    using Xunit;

    /// <summary>
    /// Tests the <see cref="Image"/> class.
    /// </summary>
    public class DiscoverMimeTypeTests
    {
        private readonly Mock<IFileSystem> fileSystem;
        private readonly string FilePath;
        private readonly Mock<IMimeTypeDetector> localMimeTypeDetector;

        public Configuration LocalConfiguration { get; private set; }
        public byte[] Marker { get; private set; }
        public MemoryStream DataStream { get; private set; }
        public byte[] DecodedData { get; private set; }
        private const string localMimeType = "image/local";

        public DiscoverMimeTypeTests()
        {
            this.localMimeTypeDetector = new Mock<IMimeTypeDetector>();
            this.localMimeTypeDetector.Setup(x => x.HeaderSize).Returns(1);
            this.localMimeTypeDetector.Setup(x => x.DetectMimeType(It.IsAny<Span<byte>>())).Returns(localMimeType);

            this.fileSystem = new Mock<IFileSystem>();

            this.LocalConfiguration = new Configuration()
            {
                FileSystem = this.fileSystem.Object
            };
            this.LocalConfiguration.AddMimeTypeDetector(this.localMimeTypeDetector.Object);

            TestFormat.RegisterGloablTestFormat();
            this.Marker = Guid.NewGuid().ToByteArray();
            this.DataStream = TestFormat.GlobalTestFormat.CreateStream(this.Marker);

            this.FilePath = Guid.NewGuid().ToString();
            this.fileSystem.Setup(x => x.OpenRead(this.FilePath)).Returns(this.DataStream);

            TestFileSystem.RegisterGloablTestFormat();
            TestFileSystem.Global.AddFile(this.FilePath, this.DataStream);
        }

        [Fact]
        public void DiscoverMimeTypeByteArray()
        {
            var type = Image.DiscoverMimeType(DataStream.ToArray());
            Assert.Equal(TestFormat.GlobalTestFormat.MimeType, type);
        }

        [Fact]
        public void DiscoverMimeTypeByteArray_WithConfig()
        {
            var type = Image.DiscoverMimeType(this.LocalConfiguration, DataStream.ToArray());
            Assert.Equal(localMimeType, type);
        }

        [Fact]
        public void DiscoverMimeTypeFile()
        {
            var type = Image.DiscoverMimeType(this.FilePath);
            Assert.Equal(TestFormat.GlobalTestFormat.MimeType, type);
        }

        [Fact]
        public void DiscoverMimeTypeFilePath_WithConfig()
        {
            var type = Image.DiscoverMimeType(this.LocalConfiguration, FilePath);
            Assert.Equal(localMimeType, type);
        }


        [Fact]
        public void DiscoverMimeTypeStream()
        {
            var type = Image.DiscoverMimeType(this.DataStream);
            Assert.Equal(TestFormat.GlobalTestFormat.MimeType, type);
        }

        [Fact]
        public void DiscoverMimeTypeFileStream_WithConfig()
        {
            var type = Image.DiscoverMimeType(this.LocalConfiguration, DataStream);
            Assert.Equal(localMimeType, type);
        }

        [Fact]
        public void DiscoverMimeTypeNoDetectorsRegisterdShouldReturnNull()
        {
            var type = Image.DiscoverMimeType(new Configuration(), DataStream);
            Assert.Null(type);
        }
    }
}
