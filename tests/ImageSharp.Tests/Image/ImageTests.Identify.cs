// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
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

            private byte[] ActualImageBytes => TestFile.Create(TestImages.Bmp.F).Bytes;

            private IImageInfo LocalImageInfo => this.localImageInfoMock.Object;

            private IImageFormat LocalImageFormat => this.localImageFormatMock.Object;

            private static readonly IImageFormat ExpectedGlobalFormat =
                Configuration.Default.ImageFormatsManager.FindFormatByFileExtension("bmp");

            [Fact]
            public void FromBytes_GlobalConfiguration()
            {
                IImageInfo info = Image.Identify(this.ActualImageBytes, out IImageFormat type);

                Assert.Equal(ExpectedImageSize, info.Size());
                Assert.Equal(ExpectedGlobalFormat, type);
            }

            [Fact]
            public void FromBytes_CustomConfiguration()
            {
                IImageInfo info = Image.Identify(this.LocalConfiguration, this.ByteArray, out IImageFormat type);

                Assert.Equal(this.LocalImageInfo, info);
                Assert.Equal(this.LocalImageFormat, type);
            }

            [Fact]
            public void FromFileSystemPath_GlobalConfiguration()
            {
                IImageInfo info = Image.Identify(ActualImagePath, out IImageFormat type);

                Assert.NotNull(info);
                Assert.Equal(ExpectedGlobalFormat, type);
            }

            [Fact]
            public void FromFileSystemPath_CustomConfiguration()
            {
                IImageInfo info = Image.Identify(this.LocalConfiguration, this.MockFilePath, out IImageFormat type);

                Assert.Equal(this.LocalImageInfo, info);
                Assert.Equal(this.LocalImageFormat, type);
            }

            [Fact]
            public void FromStream_GlobalConfiguration()
            {
                using (var stream = new MemoryStream(this.ActualImageBytes))
                {
                    IImageInfo info = Image.Identify(stream, out IImageFormat type);

                    Assert.NotNull(info);
                    Assert.Equal(ExpectedGlobalFormat, type);
                }
            }

            [Fact]
            public void FromStream_GlobalConfiguration_NoFormat()
            {
                using (var stream = new MemoryStream(this.ActualImageBytes))
                {
                    IImageInfo info = Image.Identify(stream);

                    Assert.NotNull(info);
                }
            }

            [Fact]
            public void FromNonSeekableStream_GlobalConfiguration()
            {
                using var stream = new MemoryStream(this.ActualImageBytes);
                using var nonSeekableStream = new NonSeekableStream(stream);

                IImageInfo info = Image.Identify(nonSeekableStream, out IImageFormat type);

                Assert.NotNull(info);
                Assert.Equal(ExpectedGlobalFormat, type);
            }

            [Fact]
            public void FromNonSeekableStream_GlobalConfiguration_NoFormat()
            {
                using var stream = new MemoryStream(this.ActualImageBytes);
                using var nonSeekableStream = new NonSeekableStream(stream);

                IImageInfo info = Image.Identify(nonSeekableStream);

                Assert.NotNull(info);
            }

            [Fact]
            public void FromStream_CustomConfiguration()
            {
                IImageInfo info = Image.Identify(this.LocalConfiguration, this.DataStream, out IImageFormat type);

                Assert.Equal(this.LocalImageInfo, info);
                Assert.Equal(this.LocalImageFormat, type);
            }

            [Fact]
            public void FromStream_CustomConfiguration_NoFormat()
            {
                IImageInfo info = Image.Identify(this.LocalConfiguration, this.DataStream);

                Assert.Equal(this.LocalImageInfo, info);
            }

            [Fact]
            public void WhenNoMatchingFormatFound_ReturnsNull()
            {
                IImageInfo info = Image.Identify(new Configuration(), this.DataStream, out IImageFormat type);

                Assert.Null(info);
                Assert.Null(type);
            }

            [Fact]
            public async Task FromStreamAsync_GlobalConfiguration_NoFormat()
            {
                using (var stream = new MemoryStream(this.ActualImageBytes))
                {
                    var asyncStream = new AsyncStreamWrapper(stream, () => false);
                    IImageInfo info = await Image.IdentifyAsync(asyncStream);

                    Assert.NotNull(info);
                }
            }

            [Fact]
            public async Task FromStreamAsync_GlobalConfiguration()
            {
                using (var stream = new MemoryStream(this.ActualImageBytes))
                {
                    var asyncStream = new AsyncStreamWrapper(stream, () => false);
                    (IImageInfo ImageInfo, IImageFormat Format) res = await Image.IdentifyWithFormatAsync(asyncStream);

                    Assert.Equal(ExpectedImageSize, res.ImageInfo.Size());
                    Assert.Equal(ExpectedGlobalFormat, res.Format);
                }
            }

            [Fact]
            public async Task FromNonSeekableStreamAsync_GlobalConfiguration_NoFormat()
            {
                using var stream = new MemoryStream(this.ActualImageBytes);
                using var nonSeekableStream = new NonSeekableStream(stream);

                var asyncStream = new AsyncStreamWrapper(nonSeekableStream, () => false);
                IImageInfo info = await Image.IdentifyAsync(asyncStream);

                Assert.NotNull(info);
            }

            [Fact]
            public async Task FromNonSeekableStreamAsync_GlobalConfiguration()
            {
                using var stream = new MemoryStream(this.ActualImageBytes);
                using var nonSeekableStream = new NonSeekableStream(stream);

                var asyncStream = new AsyncStreamWrapper(nonSeekableStream, () => false);
                (IImageInfo ImageInfo, IImageFormat Format) res = await Image.IdentifyWithFormatAsync(asyncStream);

                Assert.Equal(ExpectedImageSize, res.ImageInfo.Size());
                Assert.Equal(ExpectedGlobalFormat, res.Format);
            }

            [Fact]
            public async Task FromPathAsync_CustomConfiguration()
            {
                IImageInfo info = await Image.IdentifyAsync(this.LocalConfiguration, this.MockFilePath);
                Assert.Equal(this.LocalImageInfo, info);
            }

            [Fact]
            public async Task IdentifyWithFormatAsync_FromPath_CustomConfiguration()
            {
                (IImageInfo ImageInfo, IImageFormat Format) info = await Image.IdentifyWithFormatAsync(this.LocalConfiguration, this.MockFilePath);
                Assert.NotNull(info.ImageInfo);
                Assert.Equal(this.LocalImageFormat, info.Format);
            }

            [Fact]
            public async Task IdentifyWithFormatAsync_FromPath_GlobalConfiguration()
            {
                (IImageInfo ImageInfo, IImageFormat Format) res = await Image.IdentifyWithFormatAsync(ActualImagePath);

                Assert.Equal(ExpectedImageSize, res.ImageInfo.Size());
                Assert.Equal(ExpectedGlobalFormat, res.Format);
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
                (IImageInfo ImageInfo, IImageFormat Format) info = await Image.IdentifyWithFormatAsync(this.LocalConfiguration, asyncStream);

                Assert.Equal(this.LocalImageInfo, info.ImageInfo);
                Assert.Equal(this.LocalImageFormat, info.Format);
            }

            [Fact]
            public async Task WhenNoMatchingFormatFoundAsync_ReturnsNull()
            {
                var asyncStream = new AsyncStreamWrapper(this.DataStream, () => false);
                (IImageInfo ImageInfo, IImageFormat Format) info = await Image.IdentifyWithFormatAsync(new Configuration(), asyncStream);

                Assert.Null(info.ImageInfo);
            }
        }
    }
}
