// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;

using Moq;

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests
{
    public partial class ImageTests
    {
        public abstract class ImageLoadTestBase : IDisposable
        {
            protected Image<Rgba32> returnImage;

            protected Mock<IImageDecoder> localDecoder;

            protected IImageFormatDetector localMimeTypeDetector;

            protected Mock<IImageFormat> localImageFormatMock;

            protected readonly string MockFilePath = Guid.NewGuid().ToString();

            internal readonly Mock<IFileSystem> localFileSystemMock = new Mock<IFileSystem>();

            protected readonly TestFileSystem topLevelFileSystem = new TestFileSystem();

            public Configuration LocalConfiguration { get; }

            public TestFormat TestFormat { get; } = new TestFormat();

            /// <summary>
            /// Gets the top-level configuration in the context of this test case.
            /// It has <see cref="TestFormat"/> registered.
            /// </summary>
            public Configuration TopLevelConfiguration { get; }

            public byte[] Marker { get; }

            public MemoryStream DataStream { get; }

            public byte[] DecodedData { get; private set; }

            protected ImageLoadTestBase()
            {
                this.returnImage = new Image<Rgba32>(1, 1);

                this.localImageFormatMock = new Mock<IImageFormat>();

                this.localDecoder = new Mock<IImageDecoder>();
                this.localMimeTypeDetector = new MockImageFormatDetector(this.localImageFormatMock.Object);
                this.localDecoder.Setup(x => x.Decode<Rgba32>(It.IsAny<Configuration>(), It.IsAny<Stream>()))
                    .Callback<Configuration, Stream>((c, s) =>
                        {
                            using (var ms = new MemoryStream())
                            {
                                s.CopyTo(ms);
                                this.DecodedData = ms.ToArray();
                            }
                        })
                    .Returns(this.returnImage);

                this.LocalConfiguration = new Configuration
                                              {
                                              };
                this.LocalConfiguration.ImageFormatsManager.AddImageFormatDetector(this.localMimeTypeDetector);
                this.LocalConfiguration.ImageFormatsManager.SetDecoder(this.localImageFormatMock.Object, this.localDecoder.Object);

                this.TopLevelConfiguration = new Configuration(this.TestFormat);

                this.Marker = Guid.NewGuid().ToByteArray();
                this.DataStream = this.TestFormat.CreateStream(this.Marker);

                this.localFileSystemMock.Setup(x => x.OpenRead(this.MockFilePath)).Returns(this.DataStream);
                this.topLevelFileSystem.AddFile(this.MockFilePath, this.DataStream);
                this.LocalConfiguration.FileSystem = this.localFileSystemMock.Object;
                this.TopLevelConfiguration.FileSystem = this.topLevelFileSystem;
            }

            public void Dispose()
            {
                // clean up the global object;
                this.returnImage?.Dispose();
            }
        }
    }
}