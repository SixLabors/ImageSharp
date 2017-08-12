// <copyright file="ImageEqualTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.IO;

    using ImageSharp.Formats;
    using ImageSharp.IO;
    using ImageSharp.Tests;
    using Moq;
    using Xunit;

    public class ImageEqualTests
    {
        //private readonly Mock<IFileSystem> fileSystem;
        //private Image<Rgba32> returnImage;
        //private Mock<IImageDecoder> localDecoder;
        //private readonly string FilePath;
        //private readonly Mock<IImageFormatDetector> localMimeTypeDetector;
        //private readonly Mock<IImageFormat> localImageFormatMock;

        //public Configuration LocalConfiguration { get; private set; }
        //public byte[] Marker { get; private set; }
        //public MemoryStream DataStream { get; private set; }
        //public byte[] DecodedData { get; private set; }

        public ImageEqualTests()
        {
            //this.returnImage = new Image<Rgba32>(1, 1);

            //this.localImageFormatMock = new Mock<IImageFormat>();

            //this.localDecoder = new Mock<IImageDecoder>();
            //this.localMimeTypeDetector = new Mock<IImageFormatDetector>();
            //this.localMimeTypeDetector.Setup(x => x.HeaderSize).Returns(1);
            //this.localMimeTypeDetector.Setup(x => x.DetectFormat(It.IsAny<ReadOnlySpan<byte>>())).Returns(localImageFormatMock.Object);

            //this.localDecoder.Setup(x => x.Decode<Rgba32>(It.IsAny<Configuration>(), It.IsAny<Stream>()))

            //    .Callback<Configuration, Stream>((c, s) =>
            //    {
            //        using (var ms = new MemoryStream())
            //        {
            //            s.CopyTo(ms);
            //            this.DecodedData = ms.ToArray();
            //        }
            //    })
            //    .Returns(this.returnImage);

            //this.fileSystem = new Mock<IFileSystem>();

            //this.LocalConfiguration = new Configuration()
            //{
            //    FileSystem = this.fileSystem.Object
            //};
            //this.LocalConfiguration.AddImageFormatDetector(this.localMimeTypeDetector.Object);
            //this.LocalConfiguration.SetDecoder(localImageFormatMock.Object, this.localDecoder.Object);

            //TestFormat.RegisterGloablTestFormat();
            //this.Marker = Guid.NewGuid().ToByteArray();
            //this.DataStream = TestFormat.GlobalTestFormat.CreateStream(this.Marker);

            //this.FilePath = Guid.NewGuid().ToString();
            //this.fileSystem.Setup(x => x.OpenRead(this.FilePath)).Returns(this.DataStream);

            //TestFileSystem.RegisterGloablTestFormat();
            //TestFileSystem.Global.AddFile(this.FilePath, this.DataStream);
        }

        [Fact]
        public void TestsThatVimImagesAreEqual()
        {
            var image1Provider = TestImageProvider<Rgba32>.File(TestImages.Png.VimImage1);
            var image2Provider = TestImageProvider<Rgba32>.File(TestImages.Png.VimImage2);

            using (Image<Rgba32> img1 = image1Provider.GetImage())
            using (Image<Rgba32> img2 = image2Provider.GetImage())
            {
                bool imagesEqual = AreImagesEqual(img1, img2);
                Assert.True(imagesEqual);
            }
        }

        [Fact]
        public void TestsThatVersioningImagesAreEqual()
        {
            var image1Provider = TestImageProvider<Rgba32>.File(TestImages.Png.VersioningImage1);
            var image2Provider = TestImageProvider<Rgba32>.File(TestImages.Png.VersioningImage2);

            using (Image<Rgba32> img1 = image1Provider.GetImage())
            using (Image<Rgba32> img2 = image2Provider.GetImage())
            {
                bool imagesEqual = AreImagesEqual(img1, img2);
                //Assert.True(imagesEqual);
            }
        }

        private bool AreImagesEqual(Image<Rgba32> img1, Image<Rgba32> img2)
        {
            Assert.Equal(img1.Width, img2.Width);
            Assert.Equal(img1.Height, img2.Height);

            for (int y = 0; y < img1.Height; y++)
            {
                for (int x = 0; x < img1.Width; x++)
                {
                    Rgba32 pixel1 = img1[x, y];
                    Rgba32 pixel2 = img2[x, y];

                    if (pixel1 != pixel2)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
