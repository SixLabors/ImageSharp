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

        private static readonly IImageFormat ExpectedGlobalFormat =
            Configuration.Default.ImageFormatsManager.FindFormatByFileExtension("bmp");

        [Fact]
        public void FromBytes_GlobalConfiguration()
        {
            IImageFormat type = Image.DetectFormat(this.ActualImageSpan);

            Assert.Equal(ExpectedGlobalFormat, type);
        }

        [Fact]
        public void FromBytes_CustomConfiguration()
        {
            DecoderOptions options = new()
            {
                Configuration = this.LocalConfiguration
            };

            IImageFormat type = Image.DetectFormat(options, this.ByteArray);

            Assert.Equal(this.LocalImageFormat, type);
        }

        [Fact]
        public void FromFileSystemPath_GlobalConfiguration()
        {
            IImageFormat type = Image.DetectFormat(ActualImagePath);
            Assert.Equal(ExpectedGlobalFormat, type);
        }

        [Fact]
        public void FromFileSystemPath_CustomConfiguration()
        {
            DecoderOptions options = new()
            {
                Configuration = this.LocalConfiguration
            };

            IImageFormat type = Image.DetectFormat(options, this.MockFilePath);
            Assert.Equal(this.LocalImageFormat, type);
        }

        [Fact]
        public void FromStream_GlobalConfiguration()
        {
            using (var stream = new MemoryStream(this.ActualImageBytes))
            {
                IImageFormat type = Image.DetectFormat(stream);
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

            IImageFormat type = Image.DetectFormat(options, this.DataStream);
            Assert.Equal(this.LocalImageFormat, type);
        }

        [Fact]
        public void WhenNoMatchingFormatFound_ReturnsNull()
        {
            DecoderOptions options = new()
            {
                Configuration = new()
            };

            IImageFormat type = Image.DetectFormat(options, this.DataStream);
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
