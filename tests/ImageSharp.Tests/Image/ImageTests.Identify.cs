// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.Formats;

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

            private byte[] ActualImageBytes => TestFile.Create(TestImages.Bmp.F).Bytes;

            private byte[] ByteArray => this.DataStream.ToArray();

            private IImageInfo LocalImageInfo => this.localImageInfoMock.Object;

            private IImageFormat LocalImageFormat => this.localImageFormatMock.Object;

            private static readonly IImageFormat ExpectedGlobalFormat =
                Configuration.Default.ImageFormatsManager.FindFormatByFileExtension("bmp");

            [Fact]
            public void FromBytes_GlobalConfiguration()
            {
                IImageInfo info = Image.Identify(this.ActualImageBytes, out IImageFormat type);

                Assert.NotNull(info);
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
            public void FromStream_CustomConfiguration()
            {
                IImageInfo info = Image.Identify(this.LocalConfiguration, this.DataStream, out IImageFormat type);

                Assert.Equal(this.LocalImageInfo, info);
                Assert.Equal(this.LocalImageFormat, type);
            }

            [Fact]
            public void WhenNoMatchingFormatFound_ReturnsNull()
            {
                IImageInfo info = Image.Identify(new Configuration(), this.DataStream, out IImageFormat type);

                Assert.Null(info);
                Assert.Null(type);
            }
        }
    }
}
