// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests
{
    public partial class ImageTests
    {
        /// <summary>
        /// Tests the <see cref="Image"/> class.
        /// </summary>
        public class Identify : ImageLoadTestBase
        {
            private static readonly string ActualImagePath = TestFile.GetInputFileFullPath(TestImages.Bmp.F);

            private static readonly Size ExpectedImageSize = new Size(108, 202);

            private static byte[] ActualImageBytes => TestFile.Create(TestImages.Bmp.F).Bytes;

            private IImageFormat LocalImageFormat => this.localImageFormatMock.Object;

            private static readonly IImageFormat ExpectedGlobalFormat =
                Configuration.Default.ImageFormatsManager.FindFormatByFileExtension("bmp");

            [Fact]
            public void FromBytes_GlobalConfiguration()
            {
                IImageInfo info = Image.Identify(ActualImageBytes);

                Assert.Equal(ExpectedImageSize, info.Size());
                Assert.Equal(ExpectedGlobalFormat, info.Metadata.OrigionalImageFormat);
            }

            [Fact]
            public void FromBytes_CustomConfiguration()
            {
                IImageInfo info = Image.Identify(this.LocalConfiguration, this.ByteArray);

                Assert.Equal(this.localImageInfo, info);
                Assert.Equal(this.LocalImageFormat, info.Metadata.OrigionalImageFormat);
            }

            [Fact]
            public void FromFileSystemPath_GlobalConfiguration()
            {
                IImageInfo info = Image.Identify(ActualImagePath);

                Assert.NotNull(info);
                Assert.Equal(ExpectedGlobalFormat, info.Metadata.OrigionalImageFormat);
            }

            [Fact]
            public void FromFileSystemPath_CustomConfiguration()
            {
                IImageInfo info = Image.Identify(this.LocalConfiguration, this.MockFilePath);

                Assert.Equal(this.localImageInfo, info);
                Assert.Equal(this.LocalImageFormat, info.Metadata.OrigionalImageFormat);
            }

            [Fact]
            public void FromStream_GlobalConfiguration()
            {
                using (var stream = new MemoryStream(ActualImageBytes))
                {
                    IImageInfo info = Image.Identify(stream);

                    Assert.NotNull(info);
                    Assert.Equal(ExpectedGlobalFormat, info.Metadata.OrigionalImageFormat);
                }
            }

            [Fact]
            public void FromStream_GlobalConfiguration_NoFormat()
            {
                using (var stream = new MemoryStream(ActualImageBytes))
                {
                    IImageInfo info = Image.Identify(stream);

                    Assert.NotNull(info);
                }
            }

            [Fact]
            public void FromNonSeekableStream_GlobalConfiguration()
            {
                using var stream = new MemoryStream(ActualImageBytes);
                using var nonSeekableStream = new NonSeekableStream(stream);

                IImageInfo info = Image.Identify(nonSeekableStream);

                Assert.NotNull(info);
                Assert.Equal(ExpectedGlobalFormat, info.Metadata.OrigionalImageFormat);
            }

            [Fact]
            public void FromNonSeekableStream_GlobalConfiguration_NoFormat()
            {
                using var stream = new MemoryStream(ActualImageBytes);
                using var nonSeekableStream = new NonSeekableStream(stream);

                IImageInfo info = Image.Identify(nonSeekableStream);

                Assert.NotNull(info);
            }

            [Fact]
            [Obsolete]
            public void FromStream_CustomConfiguration()
            {
                IImageInfo info = Image.Identify(this.LocalConfiguration, this.DataStream);

                Assert.Equal(this.localImageInfo, info);
                Assert.Equal(this.LocalImageFormat, info.Metadata.OrigionalImageFormat);
            }

            [Fact]
            public void FromStream_CustomConfiguration_NoFormat()
            {
                IImageInfo info = Image.Identify(this.LocalConfiguration, this.DataStream);

                Assert.Equal(this.localImageInfo, info);
            }

            [Fact]
            [Obsolete]
            public void WhenNoMatchingFormatFound_ReturnsNull()
            {
                IImageInfo info = Image.Identify(new Configuration(), this.DataStream);

                Assert.Null(info);
            }

            [Fact]
            public void FromStream_ZeroLength_ReturnsNull()
            {
                // https://github.com/SixLabors/ImageSharp/issues/1903
                using var zipFile = new ZipArchive(new MemoryStream(
                    new byte[]
                    {
                        0x50, 0x4B, 0x03, 0x04, 0x0A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x77, 0xAF,
                        0x94, 0x53, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x07, 0x00, 0x00, 0x00, 0x6D, 0x79, 0x73, 0x74, 0x65, 0x72,
                        0x79, 0x50, 0x4B, 0x01, 0x02, 0x3F, 0x00, 0x0A, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x77, 0xAF, 0x94, 0x53, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x07, 0x00, 0x24, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x6D,
                        0x79, 0x73, 0x74, 0x65, 0x72, 0x79, 0x0A, 0x00, 0x20, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x01, 0x00, 0x18, 0x00, 0x46, 0x82, 0xFF, 0x91, 0x27, 0xF6,
                        0xD7, 0x01, 0x55, 0xA1, 0xF9, 0x91, 0x27, 0xF6, 0xD7, 0x01, 0x55, 0xA1,
                        0xF9, 0x91, 0x27, 0xF6, 0xD7, 0x01, 0x50, 0x4B, 0x05, 0x06, 0x00, 0x00,
                        0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x59, 0x00, 0x00, 0x00, 0x25, 0x00,
                        0x00, 0x00, 0x00, 0x00
                    }));
                using Stream stream = zipFile.Entries[0].Open();
                IImageInfo info = Image.Identify(stream);
                Assert.Null(info);
            }

            [Fact]
            public async Task FromStreamAsync_GlobalConfiguration_NoFormat()
            {
                using (var stream = new MemoryStream(ActualImageBytes))
                {
                    var asyncStream = new AsyncStreamWrapper(stream, () => false);
                    IImageInfo info = await Image.IdentifyAsync(asyncStream);

                    Assert.NotNull(info);
                }
            }

            [Fact]
            [Obsolete]
            public async Task FromStreamAsync_GlobalConfiguration()
            {
                using (var stream = new MemoryStream(ActualImageBytes))
                {
                    var asyncStream = new AsyncStreamWrapper(stream, () => false);
                    IImageInfo info = await Image.IdentifyAsync(asyncStream);

                    Assert.Equal(ExpectedImageSize, info.Size());
                    Assert.Equal(ExpectedGlobalFormat, info.Metadata.OrigionalImageFormat);
                }
            }

            [Fact]
            public async Task FromNonSeekableStreamAsync_GlobalConfiguration_NoFormat()
            {
                using var stream = new MemoryStream(ActualImageBytes);
                using var nonSeekableStream = new NonSeekableStream(stream);

                var asyncStream = new AsyncStreamWrapper(nonSeekableStream, () => false);
                IImageInfo info = await Image.IdentifyAsync(asyncStream);

                Assert.NotNull(info);
            }

            [Fact]
            public async Task FromNonSeekableStreamAsync_GlobalConfiguration()
            {
                using var stream = new MemoryStream(ActualImageBytes);
                using var nonSeekableStream = new NonSeekableStream(stream);

                var asyncStream = new AsyncStreamWrapper(nonSeekableStream, () => false);
                IImageInfo info = await Image.IdentifyAsync(asyncStream);

                Assert.Equal(ExpectedImageSize, info.Size());
                Assert.Equal(ExpectedGlobalFormat, info.Metadata.OrigionalImageFormat);
            }

            [Fact]
            public async Task FromStreamAsync_ZeroLength_ReturnsNull()
            {
                // https://github.com/SixLabors/ImageSharp/issues/1903
                using var zipFile = new ZipArchive(new MemoryStream(
                    new byte[]
                    {
                        0x50, 0x4B, 0x03, 0x04, 0x0A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x77, 0xAF,
                        0x94, 0x53, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x07, 0x00, 0x00, 0x00, 0x6D, 0x79, 0x73, 0x74, 0x65, 0x72,
                        0x79, 0x50, 0x4B, 0x01, 0x02, 0x3F, 0x00, 0x0A, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x77, 0xAF, 0x94, 0x53, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x07, 0x00, 0x24, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x6D,
                        0x79, 0x73, 0x74, 0x65, 0x72, 0x79, 0x0A, 0x00, 0x20, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x01, 0x00, 0x18, 0x00, 0x46, 0x82, 0xFF, 0x91, 0x27, 0xF6,
                        0xD7, 0x01, 0x55, 0xA1, 0xF9, 0x91, 0x27, 0xF6, 0xD7, 0x01, 0x55, 0xA1,
                        0xF9, 0x91, 0x27, 0xF6, 0xD7, 0x01, 0x50, 0x4B, 0x05, 0x06, 0x00, 0x00,
                        0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x59, 0x00, 0x00, 0x00, 0x25, 0x00,
                        0x00, 0x00, 0x00, 0x00
                    }));
                using Stream stream = zipFile.Entries[0].Open();
                IImageInfo info = await Image.IdentifyAsync(stream);
                Assert.Null(info);
            }

            [Fact]
            public async Task FromPathAsync_CustomConfiguration()
            {
                IImageInfo info = await Image.IdentifyAsync(this.LocalConfiguration, this.MockFilePath);
                Assert.Equal(this.localImageInfo, info);
            }

            [Fact]
            public async Task IdentifyWithFormatAsync_FromPath_CustomConfiguration()
            {
                IImageInfo info = await Image.IdentifyAsync(this.LocalConfiguration, this.MockFilePath);
                Assert.NotNull(info);
                Assert.Equal(this.LocalImageFormat, info.Metadata.OrigionalImageFormat);
            }

            [Fact]
            public async Task IdentifyWithFormatAsync_FromPath_GlobalConfiguration()
            {
                IImageInfo info = await Image.IdentifyAsync(ActualImagePath);

                Assert.Equal(ExpectedImageSize, info.Size());
                Assert.Equal(ExpectedGlobalFormat, info.Metadata.OrigionalImageFormat);
            }

            [Fact]
            public async Task FromPathAsync_GlobalConfiguration()
            {
                IImageInfo info = await Image.IdentifyAsync(ActualImagePath);

                Assert.Equal(ExpectedImageSize, info.Size());
            }

            [Fact]
            public async Task FromStreamAsync_CustomConfiguration()
            {
                var asyncStream = new AsyncStreamWrapper(this.DataStream, () => false);
                IImageInfo info = await Image.IdentifyAsync(this.LocalConfiguration, asyncStream);

                Assert.Equal(this.localImageInfo, info);
                Assert.Equal(this.LocalImageFormat, info.Metadata.OrigionalImageFormat);
            }

            [Fact]
            public async Task WhenNoMatchingFormatFoundAsync_ReturnsNull()
            {
                var asyncStream = new AsyncStreamWrapper(this.DataStream, () => false);
                IImageInfo info = await Image.IdentifyAsync(new Configuration(), asyncStream);

                Assert.Null(info);
            }
        }
    }
}
