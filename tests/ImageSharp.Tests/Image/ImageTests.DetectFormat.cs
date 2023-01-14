// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Tests.TestUtilities;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests;

public partial class ImageTests
{
    /// <summary>
    /// Tests the <see cref="Image"/> class.
    /// </summary>
    public class DetectFormat : ImageLoadTestBase
    {
        private static readonly string ActualImagePath = TestFile.GetInputFileFullPath(TestImages.Bmp.F);

        private static byte[] ActualImageBytes => TestFile.Create(TestImages.Bmp.F).Bytes;

        private static ReadOnlySpan<byte> ActualImageSpan => ActualImageBytes.AsSpan();

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
            bool result = Image.TryDetectFormat(ActualImageSpan, out IImageFormat type);

            Assert.True(result);
            Assert.Equal(ExpectedGlobalFormat, type);
        }

        [Fact]
        public void FromBytes_CustomConfiguration()
        {
            DecoderOptions options = new()
            {
                Configuration = this.LocalConfiguration
            };

            bool result = Image.TryDetectFormat(options, this.ByteArray, out IImageFormat type);

            Assert.True(result);
            Assert.Equal(this.LocalImageFormat, type);
        }

        [Fact]
        public void FromFileSystemPath_GlobalConfiguration()
        {
            bool result = Image.TryDetectFormat(ActualImagePath, out IImageFormat type);

            Assert.True(result);
            Assert.Equal(ExpectedGlobalFormat, type);
        }

        [Fact]
        public async Task FromFileSystemPathAsync_GlobalConfiguration()
        {
            Attempt<IImageFormat> attempt = await Image.TryDetectFormatAsync(ActualImagePath);

            Assert.True(attempt.Success);
            Assert.Equal(ExpectedGlobalFormat, attempt.Value);
        }

        [Fact]
        public void FromFileSystemPath_CustomConfiguration()
        {
            DecoderOptions options = new()
            {
                Configuration = this.LocalConfiguration
            };

            bool result = Image.TryDetectFormat(options, this.MockFilePath, out IImageFormat type);

            Assert.True(result);
            Assert.Equal(this.LocalImageFormat, type);
        }

        [Fact]
        public async Task FromFileSystemPathAsync_CustomConfiguration()
        {
            DecoderOptions options = new()
            {
                Configuration = this.LocalConfiguration
            };

            Attempt<IImageFormat> attempt = await Image.TryDetectFormatAsync(options, this.MockFilePath);

            Assert.True(attempt.Success);
            Assert.Equal(this.LocalImageFormat, attempt.Value);
        }

        [Fact]
        public void FromStream_GlobalConfiguration()
        {
            using MemoryStream stream = new(ActualImageBytes);
            bool result = Image.TryDetectFormat(stream, out IImageFormat type);

            Assert.True(result);
            Assert.Equal(ExpectedGlobalFormat, type);
        }

        [Fact]
        public void FromStream_CustomConfiguration()
        {
            DecoderOptions options = new()
            {
                Configuration = this.LocalConfiguration
            };

            bool result = Image.TryDetectFormat(options, this.DataStream, out IImageFormat type);

            Assert.True(result);
            Assert.Equal(this.LocalImageFormat, type);
        }

        [Fact]
        public void WhenNoMatchingFormatFound_Throws_UnknownImageFormatException()
        {
            DecoderOptions options = new()
            {
                Configuration = new()
            };

            Assert.Throws<UnknownImageFormatException>(() => Image.TryDetectFormat(options, this.DataStream, out IImageFormat type));
        }

        [Fact]
        public async Task FromStreamAsync_GlobalConfiguration()
        {
            using MemoryStream stream = new(ActualImageBytes);
            Attempt<IImageFormat> attempt = await Image.TryDetectFormatAsync(new AsyncStreamWrapper(stream, () => false));
            Assert.Equal(ExpectedGlobalFormat, attempt.Value);
        }

        [Fact]
        public async Task FromStreamAsync_CustomConfiguration()
        {
            DecoderOptions options = new()
            {
                Configuration = this.LocalConfiguration
            };

            Attempt<IImageFormat> attempt = await Image.TryDetectFormatAsync(options, new AsyncStreamWrapper(this.DataStream, () => false));
            Assert.Equal(this.LocalImageFormat, attempt.Value);
        }

        [Fact]
        public Task WhenNoMatchingFormatFoundAsync_Throws_UnknownImageFormatException()
        {
            DecoderOptions options = new()
            {
                Configuration = new()
            };

            return Assert.ThrowsAsync<UnknownImageFormatException>(async () => await Image.TryDetectFormatAsync(options, new AsyncStreamWrapper(this.DataStream, () => false)));
        }
    }
}
