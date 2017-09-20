// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.IO;
using Moq;
using Xunit;
using SixLabors.ImageSharp.Advanced;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Tests the <see cref="Image"/> class.
    /// </summary>
    public class ImageLoadTests : IDisposable
    {
        private readonly Mock<IFileSystem> fileSystem;
        private Image<Rgba32> returnImage;
        private Mock<IImageDecoder> localDecoder;
        private readonly string FilePath;
        private readonly Mock<IImageFormatDetector> localMimeTypeDetector;
        private readonly Mock<IImageFormat> localImageFormatMock;

        public Configuration LocalConfiguration { get; private set; }
        public byte[] Marker { get; private set; }
        public MemoryStream DataStream { get; private set; }
        public byte[] DecodedData { get; private set; }

        public ImageLoadTests()
        {
            this.returnImage = new Image<Rgba32>(1, 1);

            this.localImageFormatMock = new Mock<IImageFormat>();

            this.localDecoder = new Mock<IImageDecoder>();
            this.localMimeTypeDetector = new Mock<IImageFormatDetector>();
            this.localMimeTypeDetector.Setup(x => x.HeaderSize).Returns(1);
            this.localMimeTypeDetector.Setup(x => x.DetectFormat(It.IsAny<ReadOnlySpan<byte>>())).Returns(localImageFormatMock.Object);

            this.localDecoder.Setup(x => x.Decode<Rgba32>(It.IsAny<Configuration>(), It.IsAny<Stream>()))

                .Callback<Configuration, Stream>((c, s) =>
                {
                    using (var ms = new MemoryStream())
                    {
                        s.CopyTo(ms);
                        this.DecodedData = ms.ToArray();
                    }
                })
                .Returns(this.returnImage);

            this.fileSystem = new Mock<IFileSystem>();

            this.LocalConfiguration = new Configuration()
            {
                FileSystem = this.fileSystem.Object
            };
            this.LocalConfiguration.AddImageFormatDetector(this.localMimeTypeDetector.Object);
            this.LocalConfiguration.SetDecoder(localImageFormatMock.Object, this.localDecoder.Object);

            TestFormat.RegisterGloablTestFormat();
            this.Marker = Guid.NewGuid().ToByteArray();
            this.DataStream = TestFormat.GlobalTestFormat.CreateStream(this.Marker);

            this.FilePath = Guid.NewGuid().ToString();
            this.fileSystem.Setup(x => x.OpenRead(this.FilePath)).Returns(this.DataStream);

            TestFileSystem.RegisterGloablTestFormat();
            TestFileSystem.Global.AddFile(this.FilePath, this.DataStream);
        }

        [Fact]
        public void LoadFromStream()
        {
            var img = Image.Load<Rgba32>(this.DataStream);

            Assert.NotNull(img);

            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, Configuration.Default);
        }

        [Fact]
        public void LoadFromNoneSeekableStream()
        {
            var stream = new NoneSeekableStream(this.DataStream);
            var img = Image.Load<Rgba32>(stream);

            Assert.NotNull(img);

            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, Configuration.Default);
        }

        [Fact]
        public void LoadFromStreamWithType()
        {
            var img = Image.Load<Rgba32>(this.DataStream);

            Assert.NotNull(img);
            Assert.Equal(TestFormat.GlobalTestFormat.Sample<Rgba32>(), img);

            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, Configuration.Default);
        }


        [Fact]
        public void LoadFromStreamWithConfig()
        {
            Stream stream = new MemoryStream();
            var img = Image.Load<Rgba32>(this.LocalConfiguration, stream);

            Assert.NotNull(img);

            this.localDecoder.Verify(x => x.Decode<Rgba32>(this.LocalConfiguration, stream));
        }

        [Fact]
        public void LoadFromStreamWithTypeAndConfig()
        {
            Stream stream = new MemoryStream();
            var img = Image.Load<Rgba32>(this.LocalConfiguration, stream);

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);

            this.localDecoder.Verify(x => x.Decode<Rgba32>(this.LocalConfiguration, stream));
        }


        [Fact]
        public void LoadFromStreamWithDecoder()
        {
            Stream stream = new MemoryStream();
            var img = Image.Load<Rgba32>(stream, this.localDecoder.Object);

            Assert.NotNull(img);
            this.localDecoder.Verify(x => x.Decode<Rgba32>(Configuration.Default, stream));
        }

        [Fact]
        public void LoadFromStreamWithTypeAndDecoder()
        {
            Stream stream = new MemoryStream();
            var img = Image.Load<Rgba32>(stream, this.localDecoder.Object);

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);
            this.localDecoder.Verify(x => x.Decode<Rgba32>(Configuration.Default, stream));
        }

        [Fact]
        public void LoadFromBytes()
        {
            var img = Image.Load<Rgba32>(this.DataStream.ToArray());

            Assert.NotNull(img);

            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, Configuration.Default);
        }

        [Fact]
        public void LoadFromBytesWithType()
        {
            var img = Image.Load<Rgba32>(this.DataStream.ToArray());

            Assert.NotNull(img);
            Assert.Equal(TestFormat.GlobalTestFormat.Sample<Rgba32>(), img);

            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, Configuration.Default);

        }

        [Fact]
        public void LoadFromBytesWithConfig()
        {
            var img = Image.Load<Rgba32>(this.LocalConfiguration, this.DataStream.ToArray());

            Assert.NotNull(img);

            this.localDecoder.Verify(x => x.Decode<Rgba32>(this.LocalConfiguration, It.IsAny<Stream>()));

            Assert.Equal(this.DataStream.ToArray(), this.DecodedData);
        }

        [Fact]
        public void LoadFromBytesWithTypeAndConfig()
        {
            var img = Image.Load<Rgba32>(this.LocalConfiguration, this.DataStream.ToArray());

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);

            this.localDecoder.Verify(x => x.Decode<Rgba32>(this.LocalConfiguration, It.IsAny<Stream>()));

            Assert.Equal(this.DataStream.ToArray(), this.DecodedData);
        }

        [Fact]
        public void LoadFromBytesWithDecoder()
        {
            var img = Image.Load<Rgba32>(this.DataStream.ToArray(), this.localDecoder.Object);

            Assert.NotNull(img);
            this.localDecoder.Verify(x => x.Decode<Rgba32>(Configuration.Default, It.IsAny<Stream>()));
            Assert.Equal(this.DataStream.ToArray(), this.DecodedData);
        }

        [Fact]
        public void LoadFromBytesWithTypeAndDecoder()
        {
            var img = Image.Load<Rgba32>(this.DataStream.ToArray(), this.localDecoder.Object);

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);
            this.localDecoder.Verify(x => x.Decode<Rgba32>(Configuration.Default, It.IsAny<Stream>()));
            Assert.Equal(this.DataStream.ToArray(), this.DecodedData);
        }

        [Fact]
        public void LoadFromFile()
        {
            var img = Image.Load<Rgba32>(this.DataStream);

            Assert.NotNull(img);

            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, Configuration.Default);
        }

        [Fact]
        public void LoadFromFileWithType()
        {
            var img = Image.Load<Rgba32>(this.DataStream);

            Assert.NotNull(img);
            Assert.Equal(TestFormat.GlobalTestFormat.Sample<Rgba32>(), img);

            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, Configuration.Default);
        }

        [Fact]
        public void LoadFromFileWithConfig()
        {
            var img = Image.Load<Rgba32>(this.LocalConfiguration, this.FilePath);

            Assert.NotNull(img);

            this.localDecoder.Verify(x => x.Decode<Rgba32>(this.LocalConfiguration, this.DataStream));
        }

        [Fact]
        public void LoadFromFileWithTypeAndConfig()
        {
            var img = Image.Load<Rgba32>(this.LocalConfiguration, this.FilePath);

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);

            this.localDecoder.Verify(x => x.Decode<Rgba32>(this.LocalConfiguration, this.DataStream));
        }

        [Fact]
        public void LoadFromFileWithDecoder()
        {
            var img = Image.Load<Rgba32>(this.FilePath, this.localDecoder.Object);

            Assert.NotNull(img);
            this.localDecoder.Verify(x => x.Decode<Rgba32>(Configuration.Default, this.DataStream));
        }

        [Fact]
        public void LoadFromFileWithTypeAndDecoder()
        {
            var img = Image.Load<Rgba32>(this.FilePath, this.localDecoder.Object);

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);
            this.localDecoder.Verify(x => x.Decode<Rgba32>(Configuration.Default, this.DataStream));
        }

        [Fact]
        public void LoadFromPixelData_Pixels()
        {
            var img = Image.LoadPixelData<Rgba32>(new Rgba32[] {
                Rgba32.Black, Rgba32.White,
                Rgba32.White, Rgba32.Black,
            }, 2, 2);

            Assert.NotNull(img);
            using (var px = img.Lock())
            {
                Assert.Equal(Rgba32.Black, px[0, 0]);
                Assert.Equal(Rgba32.White, px[0, 1]);

                Assert.Equal(Rgba32.White, px[1, 0]);
                Assert.Equal(Rgba32.Black, px[1, 1]);
            }
        }

        [Fact]
        public void LoadFromPixelData_Bytes()
        {
            var img = Image.LoadPixelData<Rgba32>(new byte[] {
                0,0,0,255, // 0,0
                255,255,255,255, // 0,1
                255,255,255,255, // 1,0
                0,0,0,255, // 1,1
            }, 2, 2);

            Assert.NotNull(img);
            using (var px = img.Lock())
            {
                Assert.Equal(Rgba32.Black, px[0, 0]);
                Assert.Equal(Rgba32.White, px[0, 1]);

                Assert.Equal(Rgba32.White, px[1, 0]);
                Assert.Equal(Rgba32.Black, px[1, 1]);
            }
        }


        [Fact]
        public void LoadsImageWithoutThrowingCrcException()
        {
            var image1Provider = TestImageProvider<Rgba32>.File(TestImages.Png.VersioningImage1);

            using (Image<Rgba32> img = image1Provider.GetImage())
            {
                Assert.Equal(166036, img.Frames.RootFrame.GetPixelSpan().Length);
            }
        }

        public void Dispose()
        {
            // clean up the global object;
            this.returnImage?.Dispose();
        }
    }
}
