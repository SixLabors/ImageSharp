// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.PixelFormats;
using Moq;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Tests the <see cref="Image"/> class.
    /// </summary>
    public class ImageSaveTests : IDisposable
    {
        private readonly Image<Rgba32> Image;
        private readonly Mock<IFileSystem> fileSystem;
        private readonly Mock<IImageEncoder> encoder;
        private readonly Mock<IImageEncoder> encoderNotInFormat;
        private Mock<IImageFormatDetector> localMimeTypeDetector;
        private Mock<IImageFormat> localImageFormat;

        public ImageSaveTests()
        {
            this.localImageFormat = new Mock<IImageFormat>();
            this.localImageFormat.Setup(x => x.FileExtensions).Returns(new[] { "png" });

            this.localMimeTypeDetector = new Mock<IImageFormatDetector>();
            this.localMimeTypeDetector.Setup(x => x.HeaderSize).Returns(1);
            this.localMimeTypeDetector.Setup(x => x.DetectFormat(It.IsAny<Span<byte>>())).Returns(localImageFormat.Object);

            this.encoder = new Mock<IImageEncoder>();

            this.encoderNotInFormat = new Mock<IImageEncoder>();

            this.fileSystem = new Mock<IFileSystem>();
            var config = new Configuration()
            {
                FileSystem = this.fileSystem.Object
            };
            config.AddImageFormatDetector(this.localMimeTypeDetector.Object);
            config.SetEncoder(localImageFormat.Object, this.encoder.Object);
            this.Image = new Image<Rgba32>(config, 1, 1);
        }

        [Fact]
        public void SavePixelData_Rgba32()
        {
            using (var img = new Image<Rgba32>(2, 2))
            {
                img[0, 0] = Rgba32.White;
                img[1, 0] = Rgba32.Black;

                img[0, 1] = Rgba32.Red;
                img[1, 1] = Rgba32.Blue;
                var buffer = new byte[2 * 2 * 4]; // width * height * bytes per pixel
                img.SavePixelData(buffer);

                Assert.Equal(255, buffer[0]); // 0, 0, R
                Assert.Equal(255, buffer[1]); // 0, 0, G
                Assert.Equal(255, buffer[2]); // 0, 0, B
                Assert.Equal(255, buffer[3]); // 0, 0, A

                Assert.Equal(0, buffer[4]); // 1, 0, R
                Assert.Equal(0, buffer[5]); // 1, 0, G
                Assert.Equal(0, buffer[6]); // 1, 0, B
                Assert.Equal(255, buffer[7]); // 1, 0, A

                Assert.Equal(255, buffer[8]); // 0, 1, R
                Assert.Equal(0, buffer[9]); // 0, 1, G
                Assert.Equal(0, buffer[10]); // 0, 1, B
                Assert.Equal(255, buffer[11]); // 0, 1, A

                Assert.Equal(0, buffer[12]); // 1, 1, R
                Assert.Equal(0, buffer[13]); // 1, 1, G
                Assert.Equal(255, buffer[14]); // 1, 1, B
                Assert.Equal(255, buffer[15]); // 1, 1, A
            }
        }


        [Fact]
        public void SavePixelData_Bgr24()
        {
            using (var img = new Image<Bgr24>(2, 2))
            {
                img[0, 0] = NamedColors<Bgr24>.White;
                img[1, 0] = NamedColors<Bgr24>.Black;

                img[0, 1] = NamedColors<Bgr24>.Red;
                img[1, 1] = NamedColors<Bgr24>.Blue;

                var buffer = new byte[2 * 2 * 3]; // width * height * bytes per pixel
                img.SavePixelData(buffer);

                Assert.Equal(255, buffer[0]); // 0, 0, B
                Assert.Equal(255, buffer[1]); // 0, 0, G
                Assert.Equal(255, buffer[2]); // 0, 0, R

                Assert.Equal(0, buffer[3]); // 1, 0, B
                Assert.Equal(0, buffer[4]); // 1, 0, G
                Assert.Equal(0, buffer[5]); // 1, 0, R

                Assert.Equal(0, buffer[6]); // 0, 1, B
                Assert.Equal(0, buffer[7]); // 0, 1,   G
                Assert.Equal(255, buffer[8]); // 0, 1,  R

                Assert.Equal(255, buffer[9]); // 1, 1,  B
                Assert.Equal(0, buffer[10]); // 1, 1,  G
                Assert.Equal(0, buffer[11]); // 1, 1, R
            }
        }

        [Fact]
        public void SavePixelData_Rgba32_Buffer_must_be_bigger()
        {
            using (var img = new Image<Rgba32>(2, 2))
            {
                img[0, 0] = Rgba32.White;
                img[1, 0] = Rgba32.Black;

                img[0, 1] = Rgba32.Red;
                img[1, 1] = Rgba32.Blue;
                var buffer = new byte[2 * 2]; // width * height * bytes per pixel

                Assert.Throws<ArgumentOutOfRangeException>(() =>
                {
                    img.SavePixelData(buffer);
                });
            }
        }

        [Fact]
        public void SavePath()
        {
            Stream stream = new MemoryStream();
            this.fileSystem.Setup(x => x.Create("path.png")).Returns(stream);
            this.Image.Save("path.png");

            this.encoder.Verify(x => x.Encode<Rgba32>(this.Image, stream));
        }


        [Fact]
        public void SavePathWithEncoder()
        {
            Stream stream = new MemoryStream();
            this.fileSystem.Setup(x => x.Create("path.jpg")).Returns(stream);

            this.Image.Save("path.jpg", this.encoderNotInFormat.Object);

            this.encoderNotInFormat.Verify(x => x.Encode<Rgba32>(this.Image, stream));
        }

        [Fact]
        public void ToBase64String()
        {
            var str = this.Image.ToBase64String(localImageFormat.Object);

            this.encoder.Verify(x => x.Encode<Rgba32>(this.Image, It.IsAny<Stream>()));
        }

        [Fact]
        public void SaveStreamWithMime()
        {
            Stream stream = new MemoryStream();
            this.Image.Save(stream, localImageFormat.Object);

            this.encoder.Verify(x => x.Encode<Rgba32>(this.Image, stream));
        }

        [Fact]
        public void SaveStreamWithEncoder()
        {
            Stream stream = new MemoryStream();

            this.Image.Save(stream, this.encoderNotInFormat.Object);

            this.encoderNotInFormat.Verify(x => x.Encode<Rgba32>(this.Image, stream));
        }

        public void Dispose()
        {
            this.Image.Dispose();
        }
    }
}
