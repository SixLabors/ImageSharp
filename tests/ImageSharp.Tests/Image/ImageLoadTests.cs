// <copyright file="PixelAccessorTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.IO;

    using ImageSharp.Formats;
    using ImageSharp.IO;
    using ImageSharp.PixelFormats;
    using Moq;
    using Xunit;

    /// <summary>
    /// Tests the <see cref="Image"/> class.
    /// </summary>
    public class ImageLoadTests : IDisposable
    {
        private readonly Mock<IFileSystem> fileSystem;
        private readonly IDecoderOptions decoderOptions;
        private Image<Rgba32> returnImage;
        private Mock<IImageDecoder> localDecoder;
        private Mock<IImageFormat> localFormat;
        private readonly string FilePath;

        public Configuration LocalConfiguration { get; private set; }
        public byte[] Marker { get; private set; }
        public MemoryStream DataStream { get; private set; }
        public byte[] DecodedData { get; private set; }

        public ImageLoadTests()
        {
            this.returnImage = new Image<Rgba32>(1, 1);

            this.localDecoder = new Mock<IImageDecoder>();
            this.localFormat = new Mock<IImageFormat>();
            this.localFormat.Setup(x => x.Decoder).Returns(this.localDecoder.Object);
            this.localFormat.Setup(x => x.Encoder).Returns(new Mock<IImageEncoder>().Object);
            this.localFormat.Setup(x => x.MimeType).Returns("img/test");
            this.localFormat.Setup(x => x.Extension).Returns("png");
            this.localFormat.Setup(x => x.HeaderSize).Returns(1);
            this.localFormat.Setup(x => x.IsSupportedFileFormat(It.IsAny<byte[]>())).Returns(true);
            this.localFormat.Setup(x => x.SupportedExtensions).Returns(new string[] { "png", "jpg" });

            this.localDecoder.Setup(x => x.Decode<Rgba32>(It.IsAny<Configuration>(), It.IsAny<Stream>(), It.IsAny<IDecoderOptions>()))

                .Callback<Configuration, Stream, IDecoderOptions>((c, s, o) => {
                    using (var ms = new MemoryStream())
                    {
                        s.CopyTo(ms);
                        this.DecodedData = ms.ToArray();
                    }
                })
                .Returns(this.returnImage);

            this.fileSystem = new Mock<IFileSystem>();

            this.LocalConfiguration = new Configuration(this.localFormat.Object)
            {
                FileSystem = this.fileSystem.Object
            };
            TestFormat.RegisterGloablTestFormat();
            this.Marker = Guid.NewGuid().ToByteArray();
            this.DataStream = TestFormat.GlobalTestFormat.CreateStream(this.Marker);
            this.decoderOptions = new Mock<IDecoderOptions>().Object;

            this.FilePath = Guid.NewGuid().ToString();
            this.fileSystem.Setup(x => x.OpenRead(this.FilePath)).Returns(this.DataStream);

            TestFileSystem.RegisterGloablTestFormat();
            TestFileSystem.Global.AddFile(this.FilePath, this.DataStream);
        }

        [Fact]
        public void LoadFromStream()
        {
             Image<Rgba32> img = Image.Load<Rgba32>(this.DataStream);

            Assert.NotNull(img);
            Assert.Equal(TestFormat.GlobalTestFormat, img.CurrentImageFormat);


            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, null, Configuration.Default);

        }

        [Fact]
        public void LoadFromNoneSeekableStream()
        {
            NoneSeekableStream stream = new NoneSeekableStream(this.DataStream);
             Image<Rgba32> img = Image.Load<Rgba32>(stream);

            Assert.NotNull(img);
            Assert.Equal(TestFormat.GlobalTestFormat, img.CurrentImageFormat);


            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, null, Configuration.Default);

        }

        [Fact]
        public void LoadFromStreamWithType()
        {
            Image<Rgba32> img = Image.Load<Rgba32>(this.DataStream);

            Assert.NotNull(img);
            Assert.Equal(TestFormat.GlobalTestFormat.Sample<Rgba32>(), img);
            Assert.Equal(TestFormat.GlobalTestFormat, img.CurrentImageFormat);

            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, null, Configuration.Default);

        }

        [Fact]
        public void LoadFromStreamWithOptions()
        {
             Image<Rgba32> img = Image.Load<Rgba32>(this.DataStream, this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(TestFormat.GlobalTestFormat, img.CurrentImageFormat);

            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, this.decoderOptions, Configuration.Default);

        }

        [Fact]
        public void LoadFromStreamWithTypeAndOptions()
        {
            Image<Rgba32> img = Image.Load<Rgba32>(this.DataStream, this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(TestFormat.GlobalTestFormat.Sample<Rgba32>(), img);
            Assert.Equal(TestFormat.GlobalTestFormat, img.CurrentImageFormat);

            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, this.decoderOptions, Configuration.Default);

        }

        [Fact]
        public void LoadFromStreamWithConfig()
        {
            Stream stream = new MemoryStream();
             Image<Rgba32> img = Image.Load<Rgba32>(this.LocalConfiguration, stream);

            Assert.NotNull(img);
            Assert.Equal(this.localFormat.Object, img.CurrentImageFormat);

            this.localDecoder.Verify(x => x.Decode<Rgba32>(this.LocalConfiguration, stream, null));

        }

        [Fact]
        public void LoadFromStreamWithTypeAndConfig()
        {
            Stream stream = new MemoryStream();
            Image<Rgba32> img = Image.Load<Rgba32>(this.LocalConfiguration, stream);

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);
            Assert.Equal(this.localFormat.Object, img.CurrentImageFormat);

            this.localDecoder.Verify(x => x.Decode<Rgba32>(this.LocalConfiguration, stream, null));

        }

        [Fact]
        public void LoadFromStreamWithConfigAndOptions()
        {
            Stream stream = new MemoryStream();
             Image<Rgba32> img = Image.Load<Rgba32>(this.LocalConfiguration, stream, this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(this.localFormat.Object, img.CurrentImageFormat);

            this.localDecoder.Verify(x => x.Decode<Rgba32>(this.LocalConfiguration, stream, this.decoderOptions));

        }

        [Fact]
        public void LoadFromStreamWithTypeAndConfigAndOptions()
        {
            Stream stream = new MemoryStream();
            Image<Rgba32> img = Image.Load<Rgba32>(this.LocalConfiguration, stream, this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);
            Assert.Equal(this.localFormat.Object, img.CurrentImageFormat);

            this.localDecoder.Verify(x => x.Decode<Rgba32>(this.LocalConfiguration, stream, this.decoderOptions));

        }



        [Fact]
        public void LoadFromStreamWithDecoder()
        {
            Stream stream = new MemoryStream();
             Image<Rgba32> img = Image.Load<Rgba32>(stream, this.localDecoder.Object);

            Assert.NotNull(img);
            this.localDecoder.Verify(x => x.Decode<Rgba32>(Configuration.Default, stream, null));
        }

        [Fact]
        public void LoadFromStreamWithTypeAndDecoder()
        {
            Stream stream = new MemoryStream();
            Image<Rgba32> img = Image.Load<Rgba32>(stream, this.localDecoder.Object);

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);
            this.localDecoder.Verify(x => x.Decode<Rgba32>(Configuration.Default, stream, null));
        }

        [Fact]
        public void LoadFromStreamWithDecoderAndOptions()
        {
            Stream stream = new MemoryStream();
             Image<Rgba32> img = Image.Load<Rgba32>(stream, this.localDecoder.Object, this.decoderOptions);

            Assert.NotNull(img);
            this.localDecoder.Verify(x => x.Decode<Rgba32>(Configuration.Default, stream, this.decoderOptions));
        }

        [Fact]
        public void LoadFromStreamWithTypeAndDecoderAndOptions()
        {
            Stream stream = new MemoryStream();
            Image<Rgba32> img = Image.Load<Rgba32>(stream, this.localDecoder.Object, this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);
            this.localDecoder.Verify(x => x.Decode<Rgba32>(Configuration.Default, stream, this.decoderOptions));
        }

        [Fact]
        public void LoadFromBytes()
        {
             Image<Rgba32> img = Image.Load<Rgba32>(this.DataStream.ToArray());

            Assert.NotNull(img);
            Assert.Equal(TestFormat.GlobalTestFormat, img.CurrentImageFormat);


            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, null, Configuration.Default);

        }

        [Fact]
        public void LoadFromBytesWithType()
        {
            Image<Rgba32> img = Image.Load<Rgba32>(this.DataStream.ToArray());

            Assert.NotNull(img);
            Assert.Equal(TestFormat.GlobalTestFormat.Sample<Rgba32>(), img);
            Assert.Equal(TestFormat.GlobalTestFormat, img.CurrentImageFormat);

            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, null, Configuration.Default);

        }

        [Fact]
        public void LoadFromBytesWithOptions()
        {
             Image<Rgba32> img = Image.Load<Rgba32>(this.DataStream.ToArray(), this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(TestFormat.GlobalTestFormat, img.CurrentImageFormat);

            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, this.decoderOptions, Configuration.Default);

        }

        [Fact]
        public void LoadFromBytesWithTypeAndOptions()
        {
            Image<Rgba32> img = Image.Load<Rgba32>(this.DataStream.ToArray(), this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(TestFormat.GlobalTestFormat.Sample<Rgba32>(), img);
            Assert.Equal(TestFormat.GlobalTestFormat, img.CurrentImageFormat);

            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, this.decoderOptions, Configuration.Default);

        }

        [Fact]
        public void LoadFromBytesWithConfig()
        {
             Image<Rgba32> img = Image.Load<Rgba32>(this.LocalConfiguration, this.DataStream.ToArray());

            Assert.NotNull(img);
            Assert.Equal(this.localFormat.Object, img.CurrentImageFormat);

            this.localDecoder.Verify(x => x.Decode<Rgba32>(this.LocalConfiguration, It.IsAny<Stream>(), null));

            Assert.Equal(this.DataStream.ToArray(), this.DecodedData);
        }

        [Fact]
        public void LoadFromBytesWithTypeAndConfig()
        {
            Image<Rgba32> img = Image.Load<Rgba32>(this.LocalConfiguration, this.DataStream.ToArray());

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);
            Assert.Equal(this.localFormat.Object, img.CurrentImageFormat);


            this.localDecoder.Verify(x => x.Decode<Rgba32>(this.LocalConfiguration, It.IsAny<Stream>(), null));

            Assert.Equal(this.DataStream.ToArray(), this.DecodedData);
        }

        [Fact]
        public void LoadFromBytesWithConfigAndOptions()
        {
             Image<Rgba32> img = Image.Load<Rgba32>(this.LocalConfiguration, this.DataStream.ToArray(), this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(this.localFormat.Object, img.CurrentImageFormat);

            this.localDecoder.Verify(x => x.Decode<Rgba32>(this.LocalConfiguration, It.IsAny<Stream>(), this.decoderOptions));

            Assert.Equal(this.DataStream.ToArray(), this.DecodedData);
        }

        [Fact]
        public void LoadFromBytesWithTypeAndConfigAndOptions()
        {
            Image<Rgba32> img = Image.Load<Rgba32>(this.LocalConfiguration, this.DataStream.ToArray(), this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);
            Assert.Equal(this.localFormat.Object, img.CurrentImageFormat);

            this.localDecoder.Verify(x => x.Decode<Rgba32>(this.LocalConfiguration, It.IsAny<Stream>(), this.decoderOptions));

            Assert.Equal(this.DataStream.ToArray(), this.DecodedData);
        }


        [Fact]
        public void LoadFromBytesWithDecoder()
        {
             Image<Rgba32> img = Image.Load<Rgba32>(this.DataStream.ToArray(), this.localDecoder.Object);

            Assert.NotNull(img);
            this.localDecoder.Verify(x => x.Decode<Rgba32>(Configuration.Default, It.IsAny<Stream>(), null));
            Assert.Equal(this.DataStream.ToArray(), this.DecodedData);
        }

        [Fact]
        public void LoadFromBytesWithTypeAndDecoder()
        {
            Image<Rgba32> img = Image.Load<Rgba32>(this.DataStream.ToArray(), this.localDecoder.Object);

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);
            this.localDecoder.Verify(x => x.Decode<Rgba32>(Configuration.Default, It.IsAny<Stream>(), null));
            Assert.Equal(this.DataStream.ToArray(), this.DecodedData);
        }

        [Fact]
        public void LoadFromBytesWithDecoderAndOptions()
        {
             Image<Rgba32> img = Image.Load<Rgba32>(this.DataStream.ToArray(), this.localDecoder.Object, this.decoderOptions);

            Assert.NotNull(img);
            this.localDecoder.Verify(x => x.Decode<Rgba32>(Configuration.Default, It.IsAny<Stream>(), this.decoderOptions));
            Assert.Equal(this.DataStream.ToArray(), this.DecodedData);
        }

        [Fact]
        public void LoadFromBytesWithTypeAndDecoderAndOptions()
        {
            Image<Rgba32> img = Image.Load<Rgba32>(this.DataStream.ToArray(), this.localDecoder.Object, this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);
            this.localDecoder.Verify(x => x.Decode<Rgba32>(Configuration.Default, It.IsAny<Stream>(), this.decoderOptions));
            Assert.Equal(this.DataStream.ToArray(), this.DecodedData);
        }

        [Fact]
        public void LoadFromFile()
        {
             Image<Rgba32> img = Image.Load<Rgba32>(this.DataStream);

            Assert.NotNull(img);
            Assert.Equal(TestFormat.GlobalTestFormat, img.CurrentImageFormat);


            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, null, Configuration.Default);

        }

        [Fact]
        public void LoadFromFileWithType()
        {
            Image<Rgba32> img = Image.Load<Rgba32>(this.DataStream);

            Assert.NotNull(img);
            Assert.Equal(TestFormat.GlobalTestFormat.Sample<Rgba32>(), img);
            Assert.Equal(TestFormat.GlobalTestFormat, img.CurrentImageFormat);

            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, null, Configuration.Default);

        }

        [Fact]
        public void LoadFromFileWithOptions()
        {
             Image<Rgba32> img = Image.Load<Rgba32>(this.DataStream, this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(TestFormat.GlobalTestFormat, img.CurrentImageFormat);

            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, this.decoderOptions, Configuration.Default);

        }

        [Fact]
        public void LoadFromFileWithTypeAndOptions()
        {
            Image<Rgba32> img = Image.Load<Rgba32>(this.DataStream, this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(TestFormat.GlobalTestFormat.Sample<Rgba32>(), img);
            Assert.Equal(TestFormat.GlobalTestFormat, img.CurrentImageFormat);

            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, this.decoderOptions, Configuration.Default);

        }

        [Fact]
        public void LoadFromFileWithConfig()
        {
             Image<Rgba32> img = Image.Load<Rgba32>(this.LocalConfiguration, this.FilePath);

            Assert.NotNull(img);
            Assert.Equal(this.localFormat.Object, img.CurrentImageFormat);

            this.localDecoder.Verify(x => x.Decode<Rgba32>(this.LocalConfiguration, this.DataStream, null));

        }

        [Fact]
        public void LoadFromFileWithTypeAndConfig()
        {
            Image<Rgba32> img = Image.Load<Rgba32>(this.LocalConfiguration, this.FilePath);

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);
            Assert.Equal(this.localFormat.Object, img.CurrentImageFormat);

            this.localDecoder.Verify(x => x.Decode<Rgba32>(this.LocalConfiguration, this.DataStream, null));

        }

        [Fact]
        public void LoadFromFileWithConfigAndOptions()
        {
             Image<Rgba32> img = Image.Load<Rgba32>(this.LocalConfiguration, this.FilePath, this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(this.localFormat.Object, img.CurrentImageFormat);

            this.localDecoder.Verify(x => x.Decode<Rgba32>(this.LocalConfiguration, this.DataStream, this.decoderOptions));

        }

        [Fact]
        public void LoadFromFileWithTypeAndConfigAndOptions()
        {
            Image<Rgba32> img = Image.Load<Rgba32>(this.LocalConfiguration, this.FilePath, this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);
            Assert.Equal(this.localFormat.Object, img.CurrentImageFormat);

            this.localDecoder.Verify(x => x.Decode<Rgba32>(this.LocalConfiguration, this.DataStream, this.decoderOptions));

        }


        [Fact]
        public void LoadFromFileWithDecoder()
        {
             Image<Rgba32> img = Image.Load<Rgba32>(this.FilePath, this.localDecoder.Object);

            Assert.NotNull(img);
            this.localDecoder.Verify(x => x.Decode<Rgba32>(Configuration.Default, this.DataStream, null));
        }

        [Fact]
        public void LoadFromFileWithTypeAndDecoder()
        {
            Image<Rgba32> img = Image.Load<Rgba32>(this.FilePath, this.localDecoder.Object);

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);
            this.localDecoder.Verify(x => x.Decode<Rgba32>(Configuration.Default, this.DataStream, null));
        }

        [Fact]
        public void LoadFromFileWithDecoderAndOptions()
        {
             Image<Rgba32> img = Image.Load<Rgba32>(this.FilePath, this.localDecoder.Object, this.decoderOptions);

            Assert.NotNull(img);
            this.localDecoder.Verify(x => x.Decode<Rgba32>(Configuration.Default, this.DataStream, this.decoderOptions));
        }

        [Fact]
        public void LoadFromFileWithTypeAndDecoderAndOptions()
        {
            Image<Rgba32> img = Image.Load<Rgba32>(this.FilePath, this.localDecoder.Object, this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);
            this.localDecoder.Verify(x => x.Decode<Rgba32>(Configuration.Default, this.DataStream, this.decoderOptions));
        }

        public void Dispose()
        {
            // clean up the global object;
            this.returnImage?.Dispose();
        }
    }
}
