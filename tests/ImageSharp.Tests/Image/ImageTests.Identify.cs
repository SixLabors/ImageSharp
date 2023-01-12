// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.IO.Compression;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Tests.TestUtilities;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests;

public partial class ImageTests
{
    /// <summary>
    /// Tests the <see cref="Image"/> class.
    /// </summary>
    public class Identify : ImageLoadTestBase
    {
        private static readonly string ActualImagePath = TestFile.GetInputFileFullPath(TestImages.Bmp.F);

        private static readonly Size ExpectedImageSize = new(108, 202);

        private static byte[] ActualImageBytes => TestFile.Create(TestImages.Bmp.F).Bytes;

        private ImageInfo LocalImageInfo => new(
            this.localImageInfoMock.Object.PixelType,
            this.localImageInfoMock.Object.Width,
            this.localImageInfoMock.Object.Height,
            this.localImageInfoMock.Object.Metadata);

        private IImageFormat LocalImageFormat => this.localImageFormatMock.Object;

        private static IImageFormat ExpectedGlobalFormat
        {
            get
            {
                Configuration.Default.ImageFormatsManager.TryFindFormatByFileExtension("bmp", out var format);
                return format!;
            }
        }

        [Fact]
        public void FromBytes_GlobalConfiguration()
        {
            ImageInfo info = Image.Identify(ActualImageBytes, out IImageFormat type);

            Assert.Equal(ExpectedImageSize, info.Size());
            Assert.Equal(ExpectedGlobalFormat, type);
        }

        [Fact]
        public void FromBytes_CustomConfiguration()
        {
            DecoderOptions options = new() { Configuration = this.LocalConfiguration };

            ImageInfo info = Image.Identify(options, this.ByteArray, out IImageFormat type);

            Assert.Equal(this.LocalImageInfo, info);
            Assert.Equal(this.LocalImageFormat, type);
        }

        [Fact]
        public void FromFileSystemPath_GlobalConfiguration()
        {
            ImageInfo info = Image.Identify(ActualImagePath, out IImageFormat type);

            Assert.NotNull(info);
            Assert.Equal(ExpectedGlobalFormat, type);
        }

        [Fact]
        public void FromFileSystemPath_CustomConfiguration()
        {
            DecoderOptions options = new() { Configuration = this.LocalConfiguration };

            ImageInfo info = Image.Identify(options, this.MockFilePath, out IImageFormat type);

            Assert.Equal(this.LocalImageInfo, info);
            Assert.Equal(this.LocalImageFormat, type);
        }

        [Fact]
        public void FromStream_GlobalConfiguration()
        {
            using var stream = new MemoryStream(ActualImageBytes);
            ImageInfo info = Image.Identify(stream, out IImageFormat type);

            Assert.NotNull(info);
            Assert.Equal(ExpectedGlobalFormat, type);
        }

        [Fact]
        public void FromStream_GlobalConfiguration_NoFormat()
        {
            using var stream = new MemoryStream(ActualImageBytes);
            ImageInfo info = Image.Identify(stream);

            Assert.NotNull(info);
        }

        [Fact]
        public void FromNonSeekableStream_GlobalConfiguration()
        {
            using var stream = new MemoryStream(ActualImageBytes);
            using var nonSeekableStream = new NonSeekableStream(stream);

            ImageInfo info = Image.Identify(nonSeekableStream, out IImageFormat type);

            Assert.NotNull(info);
            Assert.Equal(ExpectedGlobalFormat, type);
        }

        [Fact]
        public void FromNonSeekableStream_GlobalConfiguration_NoFormat()
        {
            using var stream = new MemoryStream(ActualImageBytes);
            using var nonSeekableStream = new NonSeekableStream(stream);

            ImageInfo info = Image.Identify(nonSeekableStream);

            Assert.NotNull(info);
        }

        [Fact]
        public void FromStream_CustomConfiguration()
        {
            DecoderOptions options = new() { Configuration = this.LocalConfiguration };

            ImageInfo info = Image.Identify(options, this.DataStream, out IImageFormat type);

            Assert.Equal(this.LocalImageInfo, info);
            Assert.Equal(this.LocalImageFormat, type);
        }

        [Fact]
        public void FromStream_CustomConfiguration_NoFormat()
        {
            DecoderOptions options = new() { Configuration = this.LocalConfiguration };

            ImageInfo info = Image.Identify(options, this.DataStream);

            Assert.Equal(this.LocalImageInfo, info);
        }

        [Fact]
        public void WhenNoMatchingFormatFound_ReturnsNull()
        {
            DecoderOptions options = new() { Configuration = new() };

            ImageInfo info = Image.Identify(options, this.DataStream, out IImageFormat type);

            Assert.Null(info);
            Assert.Null(type);
        }

        [Fact]
        public void FromStream_ZeroLength_ReturnsNull()
        {
            // https://github.com/SixLabors/ImageSharp/issues/1903
            using var zipFile = new ZipArchive(new MemoryStream(
                new byte[]
                {
                    0x50, 0x4B, 0x03, 0x04, 0x0A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x77, 0xAF, 0x94, 0x53, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x07, 0x00, 0x00, 0x00, 0x6D, 0x79,
                    0x73, 0x74, 0x65, 0x72, 0x79, 0x50, 0x4B, 0x01, 0x02, 0x3F, 0x00, 0x0A, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x77, 0xAF, 0x94, 0x53, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x07, 0x00, 0x24, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x6D, 0x79, 0x73, 0x74, 0x65, 0x72, 0x79, 0x0A, 0x00, 0x20, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x01, 0x00, 0x18, 0x00, 0x46, 0x82, 0xFF, 0x91, 0x27, 0xF6, 0xD7, 0x01, 0x55, 0xA1,
                    0xF9, 0x91, 0x27, 0xF6, 0xD7, 0x01, 0x55, 0xA1, 0xF9, 0x91, 0x27, 0xF6, 0xD7, 0x01, 0x50, 0x4B,
                    0x05, 0x06, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x59, 0x00, 0x00, 0x00, 0x25, 0x00,
                    0x00, 0x00, 0x00, 0x00
                }));
            using Stream stream = zipFile.Entries[0].Open();
            ImageInfo info = Image.Identify(stream);
            Assert.Null(info);
        }

        [Fact]
        public async Task FromStreamAsync_GlobalConfiguration_NoFormat()
        {
            using var stream = new MemoryStream(ActualImageBytes);
            var asyncStream = new AsyncStreamWrapper(stream, () => false);
            ImageInfo info = await Image.IdentifyAsync(asyncStream);

            Assert.NotNull(info);
        }

        [Fact]
        public async Task FromStreamAsync_GlobalConfiguration()
        {
            using var stream = new MemoryStream(ActualImageBytes);
            var asyncStream = new AsyncStreamWrapper(stream, () => false);
            (ImageInfo ImageInfo, IImageFormat Format) res = await Image.IdentifyWithFormatAsync(asyncStream);

            Assert.Equal(ExpectedImageSize, res.ImageInfo.Size());
            Assert.Equal(ExpectedGlobalFormat, res.Format);
        }

        [Fact]
        public async Task FromNonSeekableStreamAsync_GlobalConfiguration_NoFormat()
        {
            using var stream = new MemoryStream(ActualImageBytes);
            using var nonSeekableStream = new NonSeekableStream(stream);

            var asyncStream = new AsyncStreamWrapper(nonSeekableStream, () => false);
            ImageInfo info = await Image.IdentifyAsync(asyncStream);

            Assert.NotNull(info);
        }

        [Fact]
        public async Task FromNonSeekableStreamAsync_GlobalConfiguration()
        {
            using var stream = new MemoryStream(ActualImageBytes);
            using var nonSeekableStream = new NonSeekableStream(stream);

            var asyncStream = new AsyncStreamWrapper(nonSeekableStream, () => false);
            (ImageInfo ImageInfo, IImageFormat Format) res = await Image.IdentifyWithFormatAsync(asyncStream);

            Assert.Equal(ExpectedImageSize, res.ImageInfo.Size());
            Assert.Equal(ExpectedGlobalFormat, res.Format);
        }

        [Fact]
        public async Task FromStreamAsync_ZeroLength_ReturnsNull()
        {
            // https://github.com/SixLabors/ImageSharp/issues/1903
            using var zipFile = new ZipArchive(new MemoryStream(
                new byte[]
                {
                    0x50, 0x4B, 0x03, 0x04, 0x0A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x77, 0xAF, 0x94, 0x53, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x07, 0x00, 0x00, 0x00, 0x6D, 0x79,
                    0x73, 0x74, 0x65, 0x72, 0x79, 0x50, 0x4B, 0x01, 0x02, 0x3F, 0x00, 0x0A, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x77, 0xAF, 0x94, 0x53, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x07, 0x00, 0x24, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x6D, 0x79, 0x73, 0x74, 0x65, 0x72, 0x79, 0x0A, 0x00, 0x20, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x01, 0x00, 0x18, 0x00, 0x46, 0x82, 0xFF, 0x91, 0x27, 0xF6, 0xD7, 0x01, 0x55, 0xA1,
                    0xF9, 0x91, 0x27, 0xF6, 0xD7, 0x01, 0x55, 0xA1, 0xF9, 0x91, 0x27, 0xF6, 0xD7, 0x01, 0x50, 0x4B,
                    0x05, 0x06, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x59, 0x00, 0x00, 0x00, 0x25, 0x00,
                    0x00, 0x00, 0x00, 0x00
                }));
            using Stream stream = zipFile.Entries[0].Open();
            ImageInfo info = await Image.IdentifyAsync(stream);
            Assert.Null(info);
        }

        [Fact]
        public async Task FromPathAsync_CustomConfiguration()
        {
            DecoderOptions options = new() { Configuration = this.LocalConfiguration };

            ImageInfo info = await Image.IdentifyAsync(options, this.MockFilePath);
            Assert.Equal(this.LocalImageInfo, info);
        }

        [Fact]
        public async Task IdentifyWithFormatAsync_FromPath_CustomConfiguration()
        {
            DecoderOptions options = new() { Configuration = this.LocalConfiguration };

            (ImageInfo ImageInfo, IImageFormat Format) info =
                await Image.IdentifyWithFormatAsync(options, this.MockFilePath);
            Assert.NotNull(info.ImageInfo);
            Assert.Equal(this.LocalImageFormat, info.Format);
        }

        [Fact]
        public async Task IdentifyWithFormatAsync_FromPath_GlobalConfiguration()
        {
            (ImageInfo ImageInfo, IImageFormat Format) res = await Image.IdentifyWithFormatAsync(ActualImagePath);

            Assert.Equal(ExpectedImageSize, res.ImageInfo.Size());
            Assert.Equal(ExpectedGlobalFormat, res.Format);
        }

        [Fact]
        public async Task FromPathAsync_GlobalConfiguration()
        {
            ImageInfo info = await Image.IdentifyAsync(ActualImagePath);

            Assert.Equal(ExpectedImageSize, info.Size());
        }

        [Fact]
        public async Task FromStreamAsync_CustomConfiguration()
        {
            DecoderOptions options = new() { Configuration = this.LocalConfiguration };

            var asyncStream = new AsyncStreamWrapper(this.DataStream, () => false);
            (ImageInfo ImageInfo, IImageFormat Format)
                info = await Image.IdentifyWithFormatAsync(options, asyncStream);

            Assert.Equal(this.LocalImageInfo, info.ImageInfo);
            Assert.Equal(this.LocalImageFormat, info.Format);
        }

        [Fact]
        public async Task WhenNoMatchingFormatFoundAsync_ReturnsNull()
        {
            DecoderOptions options = new() { Configuration = new() };

            var asyncStream = new AsyncStreamWrapper(this.DataStream, () => false);
            (ImageInfo ImageInfo, IImageFormat Format)
                info = await Image.IdentifyWithFormatAsync(options, asyncStream);

            Assert.Null(info.ImageInfo);
        }
    }
}
