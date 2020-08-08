// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
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
        public class DetectFormat : ImageLoadTestBase
        {
            private static readonly string ActualImagePath = TestFile.GetInputFileFullPath(TestImages.Bmp.F);

            private byte[] ActualImageBytes => TestFile.Create(TestImages.Bmp.F).Bytes;

            private ReadOnlySpan<byte> ActualImageSpan => this.ActualImageBytes.AsSpan();

            private IImageFormat LocalImageFormat => this.localImageFormatMock.Object;

            private static readonly IImageFormat ExpectedGlobalFormat =
                Configuration.Default.ImageFormatsManager.FindFormatByFileExtension("bmp");

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void FromBytes_GlobalConfiguration(bool useSpan)
            {
                IImageFormat type = useSpan
                                        ? Image.DetectFormat(this.ActualImageSpan)
                                        : Image.DetectFormat(this.ActualImageBytes);

                Assert.Equal(ExpectedGlobalFormat, type);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void FromBytes_CustomConfiguration(bool useSpan)
            {
                IImageFormat type = useSpan
                                        ? Image.DetectFormat(this.LocalConfiguration, this.ByteArray.AsSpan())
                                        : Image.DetectFormat(this.LocalConfiguration, this.ByteArray);

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
                IImageFormat type = Image.DetectFormat(this.LocalConfiguration, this.MockFilePath);
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
                IImageFormat type = Image.DetectFormat(this.LocalConfiguration, this.DataStream);
                Assert.Equal(this.LocalImageFormat, type);
            }

            [Fact]
            public void WhenNoMatchingFormatFound_ReturnsNull()
            {
                IImageFormat type = Image.DetectFormat(new Configuration(), this.DataStream);
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
                IImageFormat type = await Image.DetectFormatAsync(this.LocalConfiguration, new AsyncStreamWrapper(this.DataStream, () => false));
                Assert.Equal(this.LocalImageFormat, type);
            }

            [Fact]
            public async Task WhenNoMatchingFormatFoundAsync_ReturnsNull()
            {
                IImageFormat type = await Image.DetectFormatAsync(new Configuration(), new AsyncStreamWrapper(this.DataStream, () => false));
                Assert.Null(type);
            }
        }
    }
}
