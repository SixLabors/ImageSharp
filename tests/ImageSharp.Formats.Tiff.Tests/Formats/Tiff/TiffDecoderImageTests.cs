// <copyright file="TiffDecoderImageTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;
    using Xunit;

    using ImageSharp.Formats;

    public class TiffDecoderImageTests
    {
        public const int ImageWidth = 200;
        public const int ImageHeight = 150;

        public static object[][] IsLittleEndianValues = new[] { new object[] { false },
                                                                new object[] { true } };

        [Theory]
        [MemberData(nameof(IsLittleEndianValues))]
        public void DecodeImage_SetsImageDimensions(bool isLittleEndian)
        {
            Stream stream = CreateTiffGenIfd()
                            .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null);
            TiffIfd ifd = decoder.ReadIfd(0);
            Image<Color> image = new Image<Color>(1,1);

            decoder.DecodeImage(ifd, image);
            
            Assert.Equal(ImageWidth, image.Width);
            Assert.Equal(ImageHeight, image.Height);
        }

        [Theory]
        [MemberData(nameof(IsLittleEndianValues))]
        public void DecodeImage_ThrowsException_WithMissingImageWidth(bool isLittleEndian)
        {
            Stream stream = CreateTiffGenIfd()
                            .WithoutEntry(TiffTags.ImageWidth)
                            .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null);
            TiffIfd ifd = decoder.ReadIfd(0);
            Image<Color> image = new Image<Color>(1,1);
            
            var e = Assert.Throws<ImageFormatException>(() => decoder.DecodeImage(ifd, image));

            Assert.Equal("The TIFF IFD does not specify the image dimensions.", e.Message);
        }

        [Theory]
        [MemberData(nameof(IsLittleEndianValues))]
        public void DecodeImage_ThrowsException_WithMissingImageLength(bool isLittleEndian)
        {
            Stream stream = CreateTiffGenIfd()
                            .WithoutEntry(TiffTags.ImageLength)
                            .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null);
            TiffIfd ifd = decoder.ReadIfd(0);
            Image<Color> image = new Image<Color>(1,1);
            
            var e = Assert.Throws<ImageFormatException>(() => decoder.DecodeImage(ifd, image));

            Assert.Equal("The TIFF IFD does not specify the image dimensions.", e.Message);
        }

        private TiffGenIfd CreateTiffGenIfd()
        {
            return new TiffGenIfd()
                    {
                        Entries =
                        {
                            TiffGenEntry.Integer(TiffTags.ImageWidth, TiffType.Long, ImageWidth),
                            TiffGenEntry.Integer(TiffTags.ImageLength, TiffType.Long, ImageHeight),
                        }
                    };
        }
    }
}