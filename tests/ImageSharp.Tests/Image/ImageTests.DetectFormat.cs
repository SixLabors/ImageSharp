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
            IImageFormat format = Image.DetectFormat(ActualImageSpan);
            Assert.Equal(ExpectedGlobalFormat, format);
        }

        [Fact]
        public void FromBytes_CustomConfiguration()
        {
            DecoderOptions options = new()
            {
                Configuration = this.LocalConfiguration
            };

            IImageFormat format = Image.DetectFormat(options, this.ByteArray);
            Assert.Equal(this.LocalImageFormat, format);
        }

        [Fact]
        public void FromFileSystemPath_GlobalConfiguration()
        {
            IImageFormat format = Image.DetectFormat(ActualImagePath);
            Assert.Equal(ExpectedGlobalFormat, format);
        }

        [Fact]
        public async Task FromFileSystemPathAsync_GlobalConfiguration()
        {
            IImageFormat format = await Image.DetectFormatAsync(ActualImagePath);
            Assert.Equal(ExpectedGlobalFormat, format);
        }

        [Fact]
        public void FromFileSystemPath_CustomConfiguration()
        {
            DecoderOptions options = new()
            {
                Configuration = this.LocalConfiguration
            };

            IImageFormat format = Image.DetectFormat(options, this.MockFilePath);
            Assert.Equal(this.LocalImageFormat, format);
        }

        [Fact]
        public async Task FromFileSystemPathAsync_CustomConfiguration()
        {
            DecoderOptions options = new()
            {
                Configuration = this.LocalConfiguration
            };

            IImageFormat format = await Image.DetectFormatAsync(options, this.MockFilePath);
            Assert.Equal(this.LocalImageFormat, format);
        }

        [Fact]
        public void FromStream_GlobalConfiguration()
        {
            using MemoryStream stream = new(ActualImageBytes);
            IImageFormat format = Image.DetectFormat(stream);

            Assert.Equal(ExpectedGlobalFormat, format);
        }

        [Fact]
        public void FromStream_CustomConfiguration()
        {
            DecoderOptions options = new()
            {
                Configuration = this.LocalConfiguration
            };

            IImageFormat format = Image.DetectFormat(options, this.DataStream);
            Assert.Equal(this.LocalImageFormat, format);
        }

        [Fact]
        public void WhenNoMatchingFormatFound_Throws_UnknownImageFormatException()
        {
            DecoderOptions options = new()
            {
                Configuration = new Configuration()
            };

            Assert.Throws<UnknownImageFormatException>(() => Image.DetectFormat(options, this.DataStream));
        }

        [Fact]
        public async Task FromStreamAsync_GlobalConfiguration()
        {
            using MemoryStream stream = new(ActualImageBytes);
            IImageFormat format = await Image.DetectFormatAsync(new AsyncStreamWrapper(stream, () => false));
            Assert.Equal(ExpectedGlobalFormat, format);
        }

        [Fact]
        public async Task FromStreamAsync_CustomConfiguration()
        {
            DecoderOptions options = new()
            {
                Configuration = this.LocalConfiguration
            };

            IImageFormat format = await Image.DetectFormatAsync(options, new AsyncStreamWrapper(this.DataStream, () => false));
            Assert.Equal(this.LocalImageFormat, format);
        }

        [Fact]
        public Task WhenNoMatchingFormatFoundAsync_Throws_UnknownImageFormatException()
        {
            DecoderOptions options = new()
            {
                Configuration = new Configuration()
            };

            return Assert.ThrowsAsync<UnknownImageFormatException>(async () => await Image.DetectFormatAsync(options, new AsyncStreamWrapper(this.DataStream, () => false)));
        }
    }
}
