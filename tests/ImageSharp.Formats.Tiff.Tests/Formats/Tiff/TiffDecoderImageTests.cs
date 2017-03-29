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
        public const int XResolution = 100;
        public const int YResolution = 200;

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
            Image<Color> image = new Image<Color>(1, 1);

            decoder.DecodeImage(ifd, image);

            Assert.Equal(ImageWidth, image.Width);
            Assert.Equal(ImageHeight, image.Height);
        }

        [Theory]
        [InlineData(false, 150u, 1u, 200u, 1u, 2u /* Inch */, 150.0, 200.0)]
        [InlineData(false, 150u, 1u, 200u, 1u, 3u /* Cm */, 150.0 / 2.54, 200.0 / 2.54)]
        [InlineData(false, 150u, 1u, 200u, 1u, 1u /* None */, 96.0, 96.0)]
        [InlineData(false, 150u, 1u, 200u, 1u, null /* Inch */, 150.0, 200.0)]
        [InlineData(false, 5u, 2u, 9u, 4u, 2u /* Inch */, 2.5, 2.25)]
        [InlineData(false, null, null, null, null, null /* Inch */, 96.0, 96.0)]
        [InlineData(false, 150u, 1u, null, null, 2u /* Inch */, 150.0, 96.0)]
        [InlineData(false, null, null, 200u, 1u, 2u /* Inch */, 96.0, 200.0)]
        [InlineData(true, 150u, 1u, 200u, 1u, 2u /* Inch */, 150.0, 200.0)]
        [InlineData(true, 150u, 1u, 200u, 1u, 3u /* Cm */, 150.0 / 2.54, 200.0 / 2.54)]
        [InlineData(true, 150u, 1u, 200u, 1u, 1u /* None */, 96.0, 96.0)]
        [InlineData(false, 5u, 2u, 9u, 4u, 2u /* Inch */, 2.5, 2.25)]
        [InlineData(true, 150u, 1u, 200u, 1u, null /* Inch */, 150.0, 200.0)]
        [InlineData(true, null, null, null, null, null /* Inch */, 96.0, 96.0)]
        [InlineData(true, 150u, 1u, null, null, 2u /* Inch */, 150.0, 96.0)]
        [InlineData(true, null, null, 200u, 1u, 2u /* Inch */, 96.0, 200.0)]
        public void DecodeImage_SetsImageResolution(bool isLittleEndian, uint? xResolutionNumerator, uint? xResolutionDenominator,
            uint? yResolutionNumerator, uint? yResolutionDenominator, uint? resolutionUnit,
            double expectedHorizonalResolution, double expectedVerticalResolution)
        {
            TiffGenIfd ifdGen = CreateTiffGenIfd()
                                .WithoutEntry(TiffTags.XResolution)
                                .WithoutEntry(TiffTags.YResolution)
                                .WithoutEntry(TiffTags.ResolutionUnit);

            if (xResolutionNumerator != null)
            {
                ifdGen.WithEntry(TiffGenEntry.Rational(TiffTags.XResolution, xResolutionNumerator.Value, xResolutionDenominator.Value));
            }

            if (yResolutionNumerator != null)
            {
                ifdGen.WithEntry(TiffGenEntry.Rational(TiffTags.YResolution, yResolutionNumerator.Value, yResolutionDenominator.Value));
            }

            if (resolutionUnit != null)
            {
                ifdGen.WithEntry(TiffGenEntry.Integer(TiffTags.ResolutionUnit, TiffType.Short, resolutionUnit.Value));
            }

            Stream stream = ifdGen.ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null);
            TiffIfd ifd = decoder.ReadIfd(0);
            Image<Color> image = new Image<Color>(1, 1);

            decoder.DecodeImage(ifd, image);

            Assert.Equal(expectedHorizonalResolution, image.MetaData.HorizontalResolution);
            Assert.Equal(expectedVerticalResolution, image.MetaData.VerticalResolution);
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
            Image<Color> image = new Image<Color>(1, 1);

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
            Image<Color> image = new Image<Color>(1, 1);

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
                            TiffGenEntry.Rational(TiffTags.XResolution, XResolution, 1),
                            TiffGenEntry.Rational(TiffTags.YResolution, YResolution, 1),
                            TiffGenEntry.Integer(TiffTags.ResolutionUnit, TiffType.Short, 2)
                        }
            };
        }
    }
}