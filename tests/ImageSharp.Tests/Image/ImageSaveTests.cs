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
// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests
{
    using System.Runtime.CompilerServices;

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

        [Theory]
        [WithTestPatternImages(13, 19, PixelTypes.Rgba32 | PixelTypes.Bgr24)]
        public void SavePixelData_ToPixelStructArray<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                TPixel[] buffer = new TPixel[image.Width*image.Height];
                image.SavePixelData(buffer);

                image.ComparePixelBufferTo(buffer);

                // TODO: We need a separate test-case somewhere ensuring that image pixels are stored in row-major order!
            }
        }

        [Theory]
        [WithTestPatternImages(19, 13, PixelTypes.Rgba32 | PixelTypes.Bgr24)]
        public void SavePixelData_ToByteArray<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                byte[] buffer = new byte[image.Width*image.Height*Unsafe.SizeOf<TPixel>()];

                image.SavePixelData(buffer);

                image.ComparePixelBufferTo(buffer.AsSpan().NonPortableCast<byte, TPixel>());
            }
        }
        
        [Fact]
        public void SavePixelData_Rgba32_WhenBufferIsTooSmall_Throws()
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
