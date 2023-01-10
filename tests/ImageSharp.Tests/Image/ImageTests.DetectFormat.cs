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

        private byte[] ActualImageBytes => TestFile.Create(TestImages.Bmp.F).Bytes;

        private ReadOnlySpan<byte> ActualImageSpan => this.ActualImageBytes.AsSpan();

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
            bool result = Image.TryDetectFormat(this.ActualImageSpan, out IImageFormat type);

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
        public void FromStream_GlobalConfiguration()
        {
            using (var stream = new MemoryStream(this.ActualImageBytes))
            {
                bool result = Image.TryDetectFormat(stream, out IImageFormat type);

                Assert.True(result);
                Assert.Equal(ExpectedGlobalFormat, type);
            }
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
        public void WhenNoMatchingFormatFound_ReturnsNull()
        {
            DecoderOptions options = new()
            {
                Configuration = new()
            };

            bool result = Image.TryDetectFormat(options, this.DataStream, out IImageFormat type);

            Assert.False(result);
            Assert.Null(type);
        }

        [Fact]
        public async Task FromStreamAsync_GlobalConfiguration()
        {
            using (var stream = new MemoryStream(this.ActualImageBytes))
            {
                IImageFormat type = await Image.DetectFormatAsync(new AsyncStreamWrapper(stream, () => false));
                Assert.Equal(ExpectedGlobalFormat, type);
            }
        }

        [Fact]
        public async Task FromStreamAsync_CustomConfiguration()
        {
            DecoderOptions options = new()
            {
                Configuration = this.LocalConfiguration
            };

            IImageFormat type = await Image.DetectFormatAsync(options, new AsyncStreamWrapper(this.DataStream, () => false));
            Assert.Equal(this.LocalImageFormat, type);
        }

        [Fact]
        public async Task WhenNoMatchingFormatFoundAsync_ReturnsNull()
        {
            DecoderOptions options = new()
            {
                Configuration = new()
            };

            IImageFormat type = await Image.DetectFormatAsync(options, new AsyncStreamWrapper(this.DataStream, () => false));
            Assert.Null(type);
        }
    }
}
