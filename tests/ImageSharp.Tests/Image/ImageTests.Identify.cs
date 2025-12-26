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

        private static IImageFormat ExpectedGlobalFormat
        {
            get
            {
                Configuration.Default.ImageFormatsManager.TryFindFormatByFileExtension("bmp", out IImageFormat format);
                return format!;
            }
        }

        [Fact]
        public void FromBytes_GlobalConfiguration()
        {
            ImageInfo info = Image.Identify(ActualImageBytes);
            Assert.Equal(ExpectedImageSize, info.Size);
            Assert.Equal(ExpectedGlobalFormat, info.Metadata.DecodedImageFormat);
        }

        [Fact]
        public void FromBytes_EmptySpan_Throws()
            => Assert.Throws<UnknownImageFormatException>(() => Image.Identify([]));

        [Fact]
        public void FromBytes_CustomConfiguration()
        {
            DecoderOptions options = new() { Configuration = this.LocalConfiguration };
            ImageInfo info = Image.Identify(options, this.ByteArray);

            Assert.Equal(this.LocalImageInfo, info);
        }

        [Fact]
        public void FromFileSystemPath_GlobalConfiguration()
        {
            ImageInfo info = Image.Identify(ActualImagePath);

            Assert.NotNull(info);
            Assert.Equal(ExpectedGlobalFormat, info.Metadata.DecodedImageFormat);
        }

        [Fact]
        public void FromFileSystemPath_CustomConfiguration()
        {
            DecoderOptions options = new() { Configuration = this.LocalConfiguration };

            ImageInfo info = Image.Identify(options, this.MockFilePath);

            Assert.Equal(this.LocalImageInfo, info);
        }

        [Fact]
        public void FromStream_GlobalConfiguration()
        {
            using MemoryStream stream = new(ActualImageBytes);
            ImageInfo info = Image.Identify(stream);

            Assert.NotNull(info);
            Assert.Equal(ExpectedGlobalFormat, info.Metadata.DecodedImageFormat);
        }

        [Fact]
        public void FromStream_GlobalConfiguration_NoFormat()
        {
            using MemoryStream stream = new(ActualImageBytes);
            ImageInfo info = Image.Identify(stream);

            Assert.NotNull(info);
        }

        [Fact]
        public void FromNonSeekableStream_GlobalConfiguration()
        {
            using MemoryStream stream = new(ActualImageBytes);
            using NonSeekableStream nonSeekableStream = new(stream);

            ImageInfo info = Image.Identify(nonSeekableStream);

            Assert.NotNull(info);
            Assert.Equal(ExpectedGlobalFormat, info.Metadata.DecodedImageFormat);
        }

        [Fact]
        public void FromNonSeekableStream_GlobalConfiguration_NoFormat()
        {
            using MemoryStream stream = new(ActualImageBytes);
            using NonSeekableStream nonSeekableStream = new(stream);

            ImageInfo info = Image.Identify(nonSeekableStream);

            Assert.NotNull(info);
        }

        [Fact]
        public void FromStream_CustomConfiguration()
        {
            DecoderOptions options = new() { Configuration = this.LocalConfiguration };

            ImageInfo info = Image.Identify(options, this.DataStream);

            Assert.Equal(this.LocalImageInfo, info);
        }

        [Fact]
        public void FromStream_CustomConfiguration_NoFormat()
        {
            DecoderOptions options = new() { Configuration = this.LocalConfiguration };

            ImageInfo info = Image.Identify(options, this.DataStream);

            Assert.Equal(this.LocalImageInfo, info);
        }

        [Fact]
        public void WhenNoMatchingFormatFound_Throws_UnknownImageFormatException()
        {
            DecoderOptions options = new() { Configuration = new Configuration() };

            Assert.Throws<UnknownImageFormatException>(() => Image.Identify(options, this.DataStream));
        }

        [Fact]
        public void FromStream_ZeroLength_Throws_UnknownImageFormatException()
        {
            // https://github.com/SixLabors/ImageSharp/issues/1903
            using ZipArchive zipFile = new(new MemoryStream(
            [
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
            ]));
            using Stream stream = zipFile.Entries[0].Open();

            Assert.Throws<UnknownImageFormatException>(() => Image.Identify(stream));
        }

        [Fact]
        public async Task FromStreamAsync_GlobalConfiguration_NoFormat()
        {
            using MemoryStream stream = new(ActualImageBytes);
            AsyncStreamWrapper asyncStream = new(stream, () => false);

            ImageInfo info = await Image.IdentifyAsync(asyncStream);
            Assert.NotNull(info);
        }

        [Fact]
        public async Task FromStreamAsync_GlobalConfiguration()
        {
            using MemoryStream stream = new(ActualImageBytes);
            AsyncStreamWrapper asyncStream = new(stream, () => false);
            ImageInfo info = await Image.IdentifyAsync(asyncStream);

            Assert.Equal(ExpectedImageSize, info.Size);
            Assert.Equal(ExpectedGlobalFormat, info.Metadata.DecodedImageFormat);
        }

        [Fact]
        public async Task FromNonSeekableStreamAsync_GlobalConfiguration_NoFormat()
        {
            using MemoryStream stream = new(ActualImageBytes);
            using NonSeekableStream nonSeekableStream = new(stream);

            AsyncStreamWrapper asyncStream = new(nonSeekableStream, () => false);
            ImageInfo info = await Image.IdentifyAsync(asyncStream);

            Assert.NotNull(info);
        }

        [Fact]
        public async Task FromNonSeekableStreamAsync_GlobalConfiguration()
        {
            using MemoryStream stream = new(ActualImageBytes);
            using NonSeekableStream nonSeekableStream = new(stream);

            AsyncStreamWrapper asyncStream = new(nonSeekableStream, () => false);
            ImageInfo info = await Image.IdentifyAsync(asyncStream);

            Assert.Equal(ExpectedImageSize, info.Size);
            Assert.Equal(ExpectedGlobalFormat, info.Metadata.DecodedImageFormat);
        }

        [Fact]
        public async Task FromStreamAsync_ZeroLength_Throws_UnknownImageFormatException()
        {
            // https://github.com/SixLabors/ImageSharp/issues/1903
            using ZipArchive zipFile = new(new MemoryStream(
            [
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
            ]));
            using Stream stream = zipFile.Entries[0].Open();

            await Assert.ThrowsAsync<UnknownImageFormatException>(async () => await Image.IdentifyAsync(stream));
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

            ImageInfo info = await Image.IdentifyAsync(options, this.MockFilePath);

            Assert.NotNull(info);
        }

        [Fact]
        public async Task IdentifyWithFormatAsync_FromPath_GlobalConfiguration()
        {
            ImageInfo info = await Image.IdentifyAsync(ActualImagePath);

            Assert.Equal(ExpectedImageSize, info.Size);
            Assert.Equal(ExpectedGlobalFormat, info.Metadata.DecodedImageFormat);
        }

        [Fact]
        public async Task FromPathAsync_GlobalConfiguration()
        {
            ImageInfo info = await Image.IdentifyAsync(ActualImagePath);

            Assert.Equal(ExpectedImageSize, info.Size);
        }

        [Fact]
        public async Task FromStreamAsync_CustomConfiguration()
        {
            DecoderOptions options = new() { Configuration = this.LocalConfiguration };

            AsyncStreamWrapper asyncStream = new(this.DataStream, () => false);
            ImageInfo info = await Image.IdentifyAsync(options, asyncStream);

            Assert.Equal(this.LocalImageInfo, info);
        }

        [Fact]
        public Task WhenNoMatchingFormatFoundAsync_Throws_UnknownImageFormatException()
        {
            DecoderOptions options = new() { Configuration = new Configuration() };

            AsyncStreamWrapper asyncStream = new(this.DataStream, () => false);
            return Assert.ThrowsAsync<UnknownImageFormatException>(async () => await Image.IdentifyAsync(options, asyncStream));
        }
    }
}
