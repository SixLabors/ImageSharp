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

        private IImageFormat LocalImageFormat => this.localImageFormatMock.Object;

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
            Image.TryIdentify(ActualImageBytes, out ImageInfo info);
            Assert.Equal(ExpectedImageSize, info.Size());
            Assert.Equal(ExpectedGlobalFormat, info.Metadata.DecodedImageFormat);
        }

        [Fact]
        public void FromBytes_CustomConfiguration()
        {
            DecoderOptions options = new() { Configuration = this.LocalConfiguration };
            Image.TryIdentify(options, this.ByteArray, out ImageInfo info);

            Assert.Equal(this.LocalImageInfo, info);
        }

        [Fact]
        public void FromFileSystemPath_GlobalConfiguration()
        {
            Image.TryIdentify(ActualImagePath, out ImageInfo info);

            Assert.NotNull(info);
            Assert.Equal(ExpectedGlobalFormat, info.Metadata.DecodedImageFormat);
        }

        [Fact]
        public void FromFileSystemPath_CustomConfiguration()
        {
            DecoderOptions options = new() { Configuration = this.LocalConfiguration };

            Image.TryIdentify(options, this.MockFilePath, out ImageInfo info);

            Assert.Equal(this.LocalImageInfo, info);
        }

        [Fact]
        public void FromStream_GlobalConfiguration()
        {
            using MemoryStream stream = new(ActualImageBytes);
            Image.TryIdentify(stream, out ImageInfo info);

            Assert.NotNull(info);
            Assert.Equal(ExpectedGlobalFormat, info.Metadata.DecodedImageFormat);
        }

        [Fact]
        public void FromStream_GlobalConfiguration_NoFormat()
        {
            using MemoryStream stream = new(ActualImageBytes);
            Image.TryIdentify(stream, out ImageInfo info);

            Assert.NotNull(info);
        }

        [Fact]
        public void FromNonSeekableStream_GlobalConfiguration()
        {
            using MemoryStream stream = new(ActualImageBytes);
            using NonSeekableStream nonSeekableStream = new(stream);

            Image.TryIdentify(nonSeekableStream, out ImageInfo info);

            Assert.NotNull(info);
            Assert.Equal(ExpectedGlobalFormat, info.Metadata.DecodedImageFormat);
        }

        [Fact]
        public void FromNonSeekableStream_GlobalConfiguration_NoFormat()
        {
            using MemoryStream stream = new(ActualImageBytes);
            using NonSeekableStream nonSeekableStream = new(stream);

            Image.TryIdentify(nonSeekableStream, out ImageInfo info);

            Assert.NotNull(info);
        }

        [Fact]
        public void FromStream_CustomConfiguration()
        {
            DecoderOptions options = new() { Configuration = this.LocalConfiguration };

            Image.TryIdentify(options, this.DataStream, out ImageInfo info);

            Assert.Equal(this.LocalImageInfo, info);
        }

        [Fact]
        public void FromStream_CustomConfiguration_NoFormat()
        {
            DecoderOptions options = new() { Configuration = this.LocalConfiguration };

            Image.TryIdentify(options, this.DataStream, out ImageInfo info);

            Assert.Equal(this.LocalImageInfo, info);
        }

        [Fact]
        public void WhenNoMatchingFormatFound_ReturnsNull()
        {
            DecoderOptions options = new() { Configuration = new() };

            Assert.False(Image.TryIdentify(options, this.DataStream, out ImageInfo info));
            Assert.Null(info);
        }

        [Fact]
        public void FromStream_ZeroLength_ReturnsNull()
        {
            // https://github.com/SixLabors/ImageSharp/issues/1903
            using ZipArchive zipFile = new(new MemoryStream(
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

            Assert.False(Image.TryIdentify(stream, out ImageInfo info));
            Assert.Null(info);
        }

        [Fact]
        public async Task FromStreamAsync_GlobalConfiguration_NoFormat()
        {
            using MemoryStream stream = new(ActualImageBytes);
            AsyncStreamWrapper asyncStream = new(stream, () => false);

            Attempt<ImageInfo> attempt = await Image.TryIdentifyAsync(asyncStream);

            Assert.True(attempt.Success);
            Assert.NotNull(attempt.Value);
        }

        [Fact]
        public async Task FromStreamAsync_GlobalConfiguration()
        {
            using MemoryStream stream = new(ActualImageBytes);
            AsyncStreamWrapper asyncStream = new(stream, () => false);
            Attempt<ImageInfo> attempt = await Image.TryIdentifyAsync(asyncStream);

            Assert.True(attempt.Success);
            Assert.Equal(ExpectedImageSize, attempt.Value.Size());
            Assert.Equal(ExpectedGlobalFormat, attempt.Value.Metadata.DecodedImageFormat);
        }

        [Fact]
        public async Task FromNonSeekableStreamAsync_GlobalConfiguration_NoFormat()
        {
            using MemoryStream stream = new(ActualImageBytes);
            using NonSeekableStream nonSeekableStream = new(stream);

            AsyncStreamWrapper asyncStream = new(nonSeekableStream, () => false);
            Attempt<ImageInfo> attempt = await Image.TryIdentifyAsync(asyncStream);

            Assert.True(attempt.Success);
            Assert.NotNull(attempt.Value);
        }

        [Fact]
        public async Task FromNonSeekableStreamAsync_GlobalConfiguration()
        {
            using MemoryStream stream = new(ActualImageBytes);
            using NonSeekableStream nonSeekableStream = new(stream);

            AsyncStreamWrapper asyncStream = new(nonSeekableStream, () => false);
            Attempt<ImageInfo> attempt = await Image.TryIdentifyAsync(asyncStream);

            Assert.True(attempt.Success);
            Assert.Equal(ExpectedImageSize, attempt.Value.Size());
            Assert.Equal(ExpectedGlobalFormat, attempt.Value.Metadata.DecodedImageFormat);
        }

        [Fact]
        public async Task FromStreamAsync_ZeroLength_ReturnsNull()
        {
            // https://github.com/SixLabors/ImageSharp/issues/1903
            using ZipArchive zipFile = new(new MemoryStream(
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

            Attempt<ImageInfo> attempt = await Image.TryIdentifyAsync(stream);

            Assert.False(attempt.Success);
            Assert.Null(attempt.Value);
        }

        [Fact]
        public async Task FromPathAsync_CustomConfiguration()
        {
            DecoderOptions options = new() { Configuration = this.LocalConfiguration };

            Attempt<ImageInfo> attempt = await Image.TryIdentifyAsync(options, this.MockFilePath);

            Assert.True(attempt.Success);
            Assert.Equal(this.LocalImageInfo, attempt.Value);
        }

        [Fact]
        public async Task IdentifyWithFormatAsync_FromPath_CustomConfiguration()
        {
            DecoderOptions options = new() { Configuration = this.LocalConfiguration };

            Attempt<ImageInfo> attempt = await Image.TryIdentifyAsync(options, this.MockFilePath);

            Assert.True(attempt.Success);
            Assert.NotNull(attempt.Value);
        }

        [Fact]
        public async Task IdentifyWithFormatAsync_FromPath_GlobalConfiguration()
        {
            Attempt<ImageInfo> attempt = await Image.TryIdentifyAsync(ActualImagePath);

            Assert.Equal(ExpectedImageSize, attempt.Value.Size());
            Assert.Equal(ExpectedGlobalFormat, attempt.Value.Metadata.DecodedImageFormat);
        }

        [Fact]
        public async Task FromPathAsync_GlobalConfiguration()
        {
            Attempt<ImageInfo> attempt = await Image.TryIdentifyAsync(ActualImagePath);

            Assert.True(attempt.Success);
            Assert.Equal(ExpectedImageSize, attempt.Value.Size());
        }

        [Fact]
        public async Task FromStreamAsync_CustomConfiguration()
        {
            DecoderOptions options = new() { Configuration = this.LocalConfiguration };

            AsyncStreamWrapper asyncStream = new(this.DataStream, () => false);
            Attempt<ImageInfo> attempt = await Image.TryIdentifyAsync(options, asyncStream);

            Assert.True(attempt.Success);
            Assert.Equal(this.LocalImageInfo, attempt.Value);
        }

        [Fact]
        public async Task WhenNoMatchingFormatFoundAsync_ReturnsNull()
        {
            DecoderOptions options = new() { Configuration = new() };

            AsyncStreamWrapper asyncStream = new(this.DataStream, () => false);
            Attempt<ImageInfo> attempt = await Image.TryIdentifyAsync(options, asyncStream);

            Assert.False(attempt.Success);
            Assert.Null(attempt.Value);
        }
    }
}
