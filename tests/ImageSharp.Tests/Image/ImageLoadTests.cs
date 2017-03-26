// <copyright file="PixelAccessorTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using ImageSharp.Formats;
    using ImageSharp.IO;
    using Moq;
    using Xunit;

    /// <summary>
    /// Tests the <see cref="Image"/> class.
    /// </summary>
    public class ImageLoadTests : IDisposable
    {
        private readonly Mock<IFileSystem> fileSystem;
        private readonly IDecoderOptions decoderOptions;
        private Image<Color> returnImage;
        private Mock<IImageDecoder> localDecoder;
        private Mock<IImageFormat> localFormat;
        private readonly string FilePath;

        public Configuration LocalConfiguration { get; private set; }
        public byte[] Marker { get; private set; }
        public MemoryStream DataStream { get; private set; }
        public byte[] DecodedData { get; private set; }

        public ImageLoadTests()
        {
            this.returnImage = new Image(1, 1);
           
            this.localDecoder = new Mock<IImageDecoder>();
            this.localFormat = new Mock<IImageFormat>();
            this.localFormat.Setup(x => x.Decoder).Returns(this.localDecoder.Object);
            this.localFormat.Setup(x => x.Encoder).Returns(new Mock<IImageEncoder>().Object);
            this.localFormat.Setup(x => x.MimeType).Returns("img/test");
            this.localFormat.Setup(x => x.Extension).Returns("png");
            this.localFormat.Setup(x => x.HeaderSize).Returns(1);
            this.localFormat.Setup(x => x.IsSupportedFileFormat(It.IsAny<byte[]>())).Returns(true);
            this.localFormat.Setup(x => x.SupportedExtensions).Returns(new string[] { "png", "jpg" });

            this.localDecoder.Setup(x => x.Decode<Color>(It.IsAny<Stream>(), It.IsAny<IDecoderOptions>(), It.IsAny<Configuration>()))

                .Callback<Stream, IDecoderOptions, Configuration>((s, o, c) => {
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
            Image img = Image.Load(this.DataStream);

            Assert.NotNull(img);
            Assert.Equal(TestFormat.GlobalTestFormat, img.CurrentImageFormat);


            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, null, Configuration.Default);

        }

        [Fact]
        public void LoadFromStreamWithType()
        {
            Image<Color> img = Image.Load<Color>(this.DataStream);

            Assert.NotNull(img);
            Assert.Equal(TestFormat.GlobalTestFormat.Sample<Color>(), img);
            Assert.Equal(TestFormat.GlobalTestFormat, img.CurrentImageFormat);

            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, null, Configuration.Default);

        }

        [Fact]
        public void LoadFromStreamWithOptions()
        {
            Image img = Image.Load(this.DataStream, this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(TestFormat.GlobalTestFormat, img.CurrentImageFormat);

            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, this.decoderOptions, Configuration.Default);

        }

        [Fact]
        public void LoadFromStreamWithTypeAndOptions()
        {
            Image<Color> img = Image.Load<Color>(this.DataStream, this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(TestFormat.GlobalTestFormat.Sample<Color>(), img);
            Assert.Equal(TestFormat.GlobalTestFormat, img.CurrentImageFormat);

            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, this.decoderOptions, Configuration.Default);

        }

        [Fact]
        public void LoadFromStreamWithConfig()
        {
            Stream stream = new MemoryStream();
            Image img = Image.Load(this.LocalConfiguration, stream);

            Assert.NotNull(img);
            Assert.Equal(this.localFormat.Object, img.CurrentImageFormat);

            this.localDecoder.Verify(x => x.Decode<Color>(stream, null, this.LocalConfiguration));

        }

        [Fact]
        public void LoadFromStreamWithTypeAndConfig()
        {
            Stream stream = new MemoryStream();
            Image<Color> img = Image.Load<Color>(this.LocalConfiguration, stream);

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);
            Assert.Equal(this.localFormat.Object, img.CurrentImageFormat);

            this.localDecoder.Verify(x => x.Decode<Color>(stream, null, this.LocalConfiguration));

        }

        [Fact]
        public void LoadFromStreamWithConfigAndOptions()
        {
            Stream stream = new MemoryStream();
            Image img = Image.Load(this.LocalConfiguration, stream, this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(this.localFormat.Object, img.CurrentImageFormat);

            this.localDecoder.Verify(x => x.Decode<Color>(stream, this.decoderOptions, this.LocalConfiguration));

        }

        [Fact]
        public void LoadFromStreamWithTypeAndConfigAndOptions()
        {
            Stream stream = new MemoryStream();
            Image<Color> img = Image.Load<Color>(this.LocalConfiguration, stream, this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);
            Assert.Equal(this.localFormat.Object, img.CurrentImageFormat);

            this.localDecoder.Verify(x => x.Decode<Color>(stream, this.decoderOptions, this.LocalConfiguration));

        }



        [Fact]
        public void LoadFromStreamWithDecoder()
        {
            Stream stream = new MemoryStream();
            Image img = Image.Load(stream, this.localDecoder.Object);

            Assert.NotNull(img);
            this.localDecoder.Verify(x => x.Decode<Color>(stream, null, Configuration.Default));
        }

        [Fact]
        public void LoadFromStreamWithTypeAndDecoder()
        {
            Stream stream = new MemoryStream();
            Image<Color> img = Image.Load<Color>(stream, this.localDecoder.Object);

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);
            this.localDecoder.Verify(x => x.Decode<Color>(stream, null, Configuration.Default));
        }

        [Fact]
        public void LoadFromStreamWithDecoderAndOptions()
        {
            Stream stream = new MemoryStream();
            Image img = Image.Load(stream, this.localDecoder.Object, this.decoderOptions);

            Assert.NotNull(img);
            this.localDecoder.Verify(x => x.Decode<Color>(stream, this.decoderOptions, Configuration.Default));
        }

        [Fact]
        public void LoadFromStreamWithTypeAndDecoderAndOptions()
        {
            Stream stream = new MemoryStream();
            Image<Color> img = Image.Load<Color>(stream, this.localDecoder.Object, this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);
            this.localDecoder.Verify(x => x.Decode<Color>(stream, this.decoderOptions, Configuration.Default));
        }

        [Fact]
        public void LoadFromBytes()
        {
            Image img = Image.Load(this.DataStream.ToArray());

            Assert.NotNull(img);
            Assert.Equal(TestFormat.GlobalTestFormat, img.CurrentImageFormat);


            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, null, Configuration.Default);

        }

        [Fact]
        public void LoadFromBytesWithType()
        {
            Image<Color> img = Image.Load<Color>(this.DataStream.ToArray());

            Assert.NotNull(img);
            Assert.Equal(TestFormat.GlobalTestFormat.Sample<Color>(), img);
            Assert.Equal(TestFormat.GlobalTestFormat, img.CurrentImageFormat);

            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, null, Configuration.Default);

        }

        [Fact]
        public void LoadFromBytesWithOptions()
        {
            Image img = Image.Load(this.DataStream.ToArray(), this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(TestFormat.GlobalTestFormat, img.CurrentImageFormat);

            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, this.decoderOptions, Configuration.Default);

        }

        [Fact]
        public void LoadFromBytesWithTypeAndOptions()
        {
            Image<Color> img = Image.Load<Color>(this.DataStream.ToArray(), this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(TestFormat.GlobalTestFormat.Sample<Color>(), img);
            Assert.Equal(TestFormat.GlobalTestFormat, img.CurrentImageFormat);

            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, this.decoderOptions, Configuration.Default);

        }

        [Fact]
        public void LoadFromBytesWithConfig()
        {
            Image img = Image.Load(this.LocalConfiguration, this.DataStream.ToArray());

            Assert.NotNull(img);
            Assert.Equal(this.localFormat.Object, img.CurrentImageFormat);

            this.localDecoder.Verify(x => x.Decode<Color>(It.IsAny<Stream>(), null, this.LocalConfiguration));

            Assert.Equal(this.DataStream.ToArray(), this.DecodedData);
        }

        [Fact]
        public void LoadFromBytesWithTypeAndConfig()
        {
            Image<Color> img = Image.Load<Color>(this.LocalConfiguration, this.DataStream.ToArray());

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);
            Assert.Equal(this.localFormat.Object, img.CurrentImageFormat);


            this.localDecoder.Verify(x => x.Decode<Color>(It.IsAny<Stream>(), null, this.LocalConfiguration));

            Assert.Equal(this.DataStream.ToArray(), this.DecodedData);
        }

        [Fact]
        public void LoadFromBytesWithConfigAndOptions()
        {
            Image img = Image.Load(this.LocalConfiguration, this.DataStream.ToArray(), this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(this.localFormat.Object, img.CurrentImageFormat);

            this.localDecoder.Verify(x => x.Decode<Color>(It.IsAny<Stream>(), this.decoderOptions, this.LocalConfiguration));

            Assert.Equal(this.DataStream.ToArray(), this.DecodedData);
        }

        [Fact]
        public void LoadFromBytesWithTypeAndConfigAndOptions()
        {
            Image<Color> img = Image.Load<Color>(this.LocalConfiguration, this.DataStream.ToArray(), this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);
            Assert.Equal(this.localFormat.Object, img.CurrentImageFormat);

            this.localDecoder.Verify(x => x.Decode<Color>(It.IsAny<Stream>(), this.decoderOptions, this.LocalConfiguration));

            Assert.Equal(this.DataStream.ToArray(), this.DecodedData);
        }


        [Fact]
        public void LoadFromBytesWithDecoder()
        {
            Image img = Image.Load(this.DataStream.ToArray(), this.localDecoder.Object);

            Assert.NotNull(img);
            this.localDecoder.Verify(x => x.Decode<Color>(It.IsAny<Stream>(), null, Configuration.Default));
            Assert.Equal(this.DataStream.ToArray(), this.DecodedData);
        }

        [Fact]
        public void LoadFromBytesWithTypeAndDecoder()
        {
            Image<Color> img = Image.Load<Color>(this.DataStream.ToArray(), this.localDecoder.Object);

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);
            this.localDecoder.Verify(x => x.Decode<Color>(It.IsAny<Stream>(), null, Configuration.Default));
            Assert.Equal(this.DataStream.ToArray(), this.DecodedData);
        }

        [Fact]
        public void LoadFromBytesWithDecoderAndOptions()
        {
            Image img = Image.Load(this.DataStream.ToArray(), this.localDecoder.Object, this.decoderOptions);

            Assert.NotNull(img);
            this.localDecoder.Verify(x => x.Decode<Color>(It.IsAny<Stream>(), this.decoderOptions, Configuration.Default));
            Assert.Equal(this.DataStream.ToArray(), this.DecodedData);
        }

        [Fact]
        public void LoadFromBytesWithTypeAndDecoderAndOptions()
        {
            Image<Color> img = Image.Load<Color>(this.DataStream.ToArray(), this.localDecoder.Object, this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);
            this.localDecoder.Verify(x => x.Decode<Color>(It.IsAny<Stream>(), this.decoderOptions, Configuration.Default));
            Assert.Equal(this.DataStream.ToArray(), this.DecodedData);
        }

        [Fact]
        public void LoadFromFile()
        {
            Image img = Image.Load(this.DataStream);

            Assert.NotNull(img);
            Assert.Equal(TestFormat.GlobalTestFormat, img.CurrentImageFormat);


            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, null, Configuration.Default);

        }

        [Fact]
        public void LoadFromFileWithType()
        {
            Image<Color> img = Image.Load<Color>(this.DataStream);

            Assert.NotNull(img);
            Assert.Equal(TestFormat.GlobalTestFormat.Sample<Color>(), img);
            Assert.Equal(TestFormat.GlobalTestFormat, img.CurrentImageFormat);

            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, null, Configuration.Default);

        }

        [Fact]
        public void LoadFromFileWithOptions()
        {
            Image img = Image.Load(this.DataStream, this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(TestFormat.GlobalTestFormat, img.CurrentImageFormat);

            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, this.decoderOptions, Configuration.Default);

        }

        [Fact]
        public void LoadFromFileWithTypeAndOptions()
        {
            Image<Color> img = Image.Load<Color>(this.DataStream, this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(TestFormat.GlobalTestFormat.Sample<Color>(), img);
            Assert.Equal(TestFormat.GlobalTestFormat, img.CurrentImageFormat);

            TestFormat.GlobalTestFormat.VerifyDecodeCall(this.Marker, this.decoderOptions, Configuration.Default);

        }

        [Fact]
        public void LoadFromFileWithConfig()
        {
            Image img = Image.Load(this.LocalConfiguration, this.FilePath);

            Assert.NotNull(img);
            Assert.Equal(this.localFormat.Object, img.CurrentImageFormat);

            this.localDecoder.Verify(x => x.Decode<Color>(this.DataStream, null, this.LocalConfiguration));

        }

        [Fact]
        public void LoadFromFileWithTypeAndConfig()
        {
            Image<Color> img = Image.Load<Color>(this.LocalConfiguration, this.FilePath);

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);
            Assert.Equal(this.localFormat.Object, img.CurrentImageFormat);

            this.localDecoder.Verify(x => x.Decode<Color>(this.DataStream, null, this.LocalConfiguration));

        }

        [Fact]
        public void LoadFromFileWithConfigAndOptions()
        {
            Image img = Image.Load(this.LocalConfiguration, this.FilePath, this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(this.localFormat.Object, img.CurrentImageFormat);

            this.localDecoder.Verify(x => x.Decode<Color>(this.DataStream, this.decoderOptions, this.LocalConfiguration));

        }

        [Fact]
        public void LoadFromFileWithTypeAndConfigAndOptions()
        {
            Image<Color> img = Image.Load<Color>(this.LocalConfiguration, this.FilePath, this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);
            Assert.Equal(this.localFormat.Object, img.CurrentImageFormat);

            this.localDecoder.Verify(x => x.Decode<Color>(this.DataStream, this.decoderOptions, this.LocalConfiguration));

        }


        [Fact]
        public void LoadFromFileWithDecoder()
        {
            Image img = Image.Load(this.FilePath, this.localDecoder.Object);

            Assert.NotNull(img);
            this.localDecoder.Verify(x => x.Decode<Color>(this.DataStream, null, Configuration.Default));
        }

        [Fact]
        public void LoadFromFileWithTypeAndDecoder()
        {
            Image<Color> img = Image.Load<Color>(this.FilePath, this.localDecoder.Object);

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);
            this.localDecoder.Verify(x => x.Decode<Color>(this.DataStream, null, Configuration.Default));
        }

        [Fact]
        public void LoadFromFileWithDecoderAndOptions()
        {
            Image img = Image.Load(this.FilePath, this.localDecoder.Object, this.decoderOptions);

            Assert.NotNull(img);
            this.localDecoder.Verify(x => x.Decode<Color>(this.DataStream, this.decoderOptions, Configuration.Default));
        }

        [Fact]
        public void LoadFromFileWithTypeAndDecoderAndOptions()
        {
            Image<Color> img = Image.Load<Color>(this.FilePath, this.localDecoder.Object, this.decoderOptions);

            Assert.NotNull(img);
            Assert.Equal(this.returnImage, img);
            this.localDecoder.Verify(x => x.Decode<Color>(this.DataStream, this.decoderOptions, Configuration.Default));
        }

        public void Dispose()
        {
            // clean up the global object;
            this.returnImage?.Dispose();
        }
    }
}
