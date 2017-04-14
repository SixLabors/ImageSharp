// <copyright file="TiffDecoderImageTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.IO;
    using Xunit;

    using ImageSharp.Formats;
    using ImageSharp.Formats.Tiff;

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

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
            TiffIfd ifd = decoder.ReadIfd(0);
            Image<Color> image = decoder.DecodeImage<Color>(ifd);

            Assert.Equal(ImageWidth, image.Width);
            Assert.Equal(ImageHeight, image.Height);
        }

        [Theory]
        [InlineData(false, 150u, 1u, 200u, 1u, 2u /* Inch */, 150.0, 200.0)]
        [InlineData(false, 150u, 1u, 200u, 1u, 3u /* Cm */, 150.0 * 2.54, 200.0 * 2.54)]
        [InlineData(false, 150u, 1u, 200u, 1u, 1u /* None */, 96.0, 96.0)]
        [InlineData(false, 150u, 1u, 200u, 1u, null /* Inch */, 150.0, 200.0)]
        [InlineData(false, 5u, 2u, 9u, 4u, 2u /* Inch */, 2.5, 2.25)]
        [InlineData(false, null, null, null, null, null /* Inch */, 96.0, 96.0)]
        [InlineData(false, 150u, 1u, null, null, 2u /* Inch */, 150.0, 96.0)]
        [InlineData(false, null, null, 200u, 1u, 2u /* Inch */, 96.0, 200.0)]
        [InlineData(true, 150u, 1u, 200u, 1u, 2u /* Inch */, 150.0, 200.0)]
        [InlineData(true, 150u, 1u, 200u, 1u, 3u /* Cm */, 150.0 * 2.54, 200.0 * 2.54)]
        [InlineData(true, 150u, 1u, 200u, 1u, 1u /* None */, 96.0, 96.0)]
        [InlineData(true, 5u, 2u, 9u, 4u, 2u /* Inch */, 2.5, 2.25)]
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

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
            TiffIfd ifd = decoder.ReadIfd(0);
            Image<Color> image = decoder.DecodeImage<Color>(ifd);

            Assert.Equal(expectedHorizonalResolution, image.MetaData.HorizontalResolution, 10);
            Assert.Equal(expectedVerticalResolution, image.MetaData.VerticalResolution, 10);
        }

        [Theory]
        [MemberData(nameof(IsLittleEndianValues))]
        public void DecodeImage_ThrowsException_WithMissingImageWidth(bool isLittleEndian)
        {
            Stream stream = CreateTiffGenIfd()
                            .WithoutEntry(TiffTags.ImageWidth)
                            .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
            TiffIfd ifd = decoder.ReadIfd(0);

            var e = Assert.Throws<ImageFormatException>(() => decoder.DecodeImage<Color>(ifd));

            Assert.Equal("The TIFF IFD does not specify the image dimensions.", e.Message);
        }

        [Theory]
        [MemberData(nameof(IsLittleEndianValues))]
        public void DecodeImage_ThrowsException_WithMissingImageLength(bool isLittleEndian)
        {
            Stream stream = CreateTiffGenIfd()
                            .WithoutEntry(TiffTags.ImageLength)
                            .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
            TiffIfd ifd = decoder.ReadIfd(0);

            var e = Assert.Throws<ImageFormatException>(() => decoder.DecodeImage<Color>(ifd));

            Assert.Equal("The TIFF IFD does not specify the image dimensions.", e.Message);
        }

        [Theory]
        [InlineData(false, TiffCompression.None, TiffCompressionType.None)]
        [InlineData(true, TiffCompression.None, TiffCompressionType.None)]
        [InlineData(false, TiffCompression.PackBits, TiffCompressionType.PackBits)]
        [InlineData(true, TiffCompression.PackBits, TiffCompressionType.PackBits)]
        public void ReadImageFormat_DeterminesCorrectCompressionImplementation(bool isLittleEndian, ushort compression, int compressionType)
        {
            Stream stream = CreateTiffGenIfd()
                            .WithEntry(TiffGenEntry.Integer(TiffTags.Compression, TiffType.Short, compression))
                            .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
            TiffIfd ifd = decoder.ReadIfd(0);
            decoder.ReadImageFormat(ifd);

            Assert.Equal((TiffCompressionType)compressionType, decoder.CompressionType);
        }

        [Theory]
        [InlineData(false, TiffCompression.Ccitt1D)]
        [InlineData(false, TiffCompression.CcittGroup3Fax)]
        [InlineData(false, TiffCompression.CcittGroup4Fax)]
        [InlineData(false, TiffCompression.Deflate)]
        [InlineData(false, TiffCompression.ItuTRecT43)]
        [InlineData(false, TiffCompression.ItuTRecT82)]
        [InlineData(false, TiffCompression.Jpeg)]
        [InlineData(false, TiffCompression.Lzw)]
        [InlineData(false, TiffCompression.OldDeflate)]
        [InlineData(false, TiffCompression.OldJpeg)]
        [InlineData(false, 999)]
        [InlineData(true, TiffCompression.Ccitt1D)]
        [InlineData(true, TiffCompression.CcittGroup3Fax)]
        [InlineData(true, TiffCompression.CcittGroup4Fax)]
        [InlineData(true, TiffCompression.Deflate)]
        [InlineData(true, TiffCompression.ItuTRecT43)]
        [InlineData(true, TiffCompression.ItuTRecT82)]
        [InlineData(true, TiffCompression.Jpeg)]
        [InlineData(true, TiffCompression.Lzw)]
        [InlineData(true, TiffCompression.OldDeflate)]
        [InlineData(true, TiffCompression.OldJpeg)]
        [InlineData(true, 999)]
        public void ReadImageFormat_ThrowsExceptionForUnsupportedCompression(bool isLittleEndian, ushort compression)
        {
            Stream stream = CreateTiffGenIfd()
                            .WithEntry(TiffGenEntry.Integer(TiffTags.Compression, TiffType.Short, compression))
                            .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
            TiffIfd ifd = decoder.ReadIfd(0);

            var e = Assert.Throws<NotSupportedException>(() => decoder.ReadImageFormat(ifd));

            Assert.Equal("The specified TIFF compression format is not supported.", e.Message);
        }

        [Theory]
        [InlineData(false, TiffPhotometricInterpretation.WhiteIsZero, new[] { 8 }, TiffColorType.WhiteIsZero8)]
        [InlineData(true, TiffPhotometricInterpretation.WhiteIsZero, new[] { 8 }, TiffColorType.WhiteIsZero8)]
        [InlineData(false, TiffPhotometricInterpretation.WhiteIsZero, new[] { 4 }, TiffColorType.WhiteIsZero4)]
        [InlineData(true, TiffPhotometricInterpretation.WhiteIsZero, new[] { 4 }, TiffColorType.WhiteIsZero4)]
        [InlineData(false, TiffPhotometricInterpretation.WhiteIsZero, new[] { 1 }, TiffColorType.WhiteIsZero1)]
        [InlineData(true, TiffPhotometricInterpretation.WhiteIsZero, new[] { 1 }, TiffColorType.WhiteIsZero1)]
        public void ReadImageFormat_DeterminesCorrectColorImplementation(bool isLittleEndian, ushort photometricInterpretation, int[] bitsPerSample, int colorType)
        {
            Stream stream = CreateTiffGenIfd()
                            .WithEntry(TiffGenEntry.Integer(TiffTags.PhotometricInterpretation, TiffType.Short, photometricInterpretation))
                            .WithEntry(TiffGenEntry.Integer(TiffTags.BitsPerSample, TiffType.Short, bitsPerSample))
                            .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
            TiffIfd ifd = decoder.ReadIfd(0);
            decoder.ReadImageFormat(ifd);

            Assert.Equal((TiffColorType)colorType, decoder.ColorType);
        }

        [Theory]
        [InlineData(false, TiffPhotometricInterpretation.WhiteIsZero, TiffColorType.WhiteIsZero1)]
        [InlineData(true, TiffPhotometricInterpretation.WhiteIsZero, TiffColorType.WhiteIsZero1)]
        public void ReadImageFormat_DeterminesCorrectColorImplementation_DefaultsToBilevel(bool isLittleEndian, ushort photometricInterpretation, int colorType)
        {
            Stream stream = CreateTiffGenIfd()
                            .WithEntry(TiffGenEntry.Integer(TiffTags.PhotometricInterpretation, TiffType.Short, photometricInterpretation))
                            .WithoutEntry(TiffTags.BitsPerSample)
                            .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
            TiffIfd ifd = decoder.ReadIfd(0);
            decoder.ReadImageFormat(ifd);

            Assert.Equal((TiffColorType)colorType, decoder.ColorType);
        }

        // [Theory]
        // [InlineData(false, new[] { 8 }, TiffColorType.WhiteIsZero8)]
        // [InlineData(true, new[] { 8 }, TiffColorType.WhiteIsZero8)]
        // public void ReadImageFormat_UsesDefaultColorImplementationForCcitt1D(bool isLittleEndian, int[] bitsPerSample, int colorType)
        // {
        //     Stream stream = CreateTiffGenIfd()
        //                     .WithEntry(TiffGenEntry.Integer(TiffTags.Compression, TiffType.Short, (int)TiffCompression.Ccitt1D))
        //                     .WithEntry(TiffGenEntry.Integer(TiffTags.BitsPerSample, TiffType.Short, bitsPerSample))
        //                     .WithoutEntry(TiffTags.PhotometricInterpretation)
        //                     .ToStream(isLittleEndian);

        //     TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
        //     TiffIfd ifd = decoder.ReadIfd(0);
        //     decoder.ReadImageFormat(ifd);

        //     Assert.Equal((TiffColorType)colorType, decoder.ColorType);
        // }

        [Theory]
        [MemberData(nameof(IsLittleEndianValues))]
        public void ReadImageFormat_ThrowsExceptionForMissingPhotometricInterpretation(bool isLittleEndian)
        {
            Stream stream = CreateTiffGenIfd()
                            .WithoutEntry(TiffTags.PhotometricInterpretation)
                            .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
            TiffIfd ifd = decoder.ReadIfd(0);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadImageFormat(ifd));

            Assert.Equal("The TIFF photometric interpretation entry is missing.", e.Message);
        }

        [Theory]
        [InlineData(false, TiffPhotometricInterpretation.BlackIsZero)]
        [InlineData(false, TiffPhotometricInterpretation.CieLab)]
        [InlineData(false, TiffPhotometricInterpretation.ColorFilterArray)]
        [InlineData(false, TiffPhotometricInterpretation.IccLab)]
        [InlineData(false, TiffPhotometricInterpretation.ItuLab)]
        [InlineData(false, TiffPhotometricInterpretation.LinearRaw)]
        [InlineData(false, TiffPhotometricInterpretation.PaletteColor)]
        [InlineData(false, TiffPhotometricInterpretation.Rgb)]
        [InlineData(false, TiffPhotometricInterpretation.Separated)]
        [InlineData(false, TiffPhotometricInterpretation.TransparencyMask)]
        [InlineData(false, TiffPhotometricInterpretation.YCbCr)]
        [InlineData(false, 999)]
        [InlineData(true, TiffPhotometricInterpretation.BlackIsZero)]
        [InlineData(true, TiffPhotometricInterpretation.CieLab)]
        [InlineData(true, TiffPhotometricInterpretation.ColorFilterArray)]
        [InlineData(true, TiffPhotometricInterpretation.IccLab)]
        [InlineData(true, TiffPhotometricInterpretation.ItuLab)]
        [InlineData(true, TiffPhotometricInterpretation.LinearRaw)]
        [InlineData(true, TiffPhotometricInterpretation.PaletteColor)]
        [InlineData(true, TiffPhotometricInterpretation.Rgb)]
        [InlineData(true, TiffPhotometricInterpretation.Separated)]
        [InlineData(true, TiffPhotometricInterpretation.TransparencyMask)]
        [InlineData(true, TiffPhotometricInterpretation.YCbCr)]
        [InlineData(true, 999)]
        public void ReadImageFormat_ThrowsExceptionForUnsupportedPhotometricInterpretation(bool isLittleEndian, ushort photometricInterpretation)
        {
            Stream stream = CreateTiffGenIfd()
                            .WithEntry(TiffGenEntry.Integer(TiffTags.PhotometricInterpretation, TiffType.Short, photometricInterpretation))
                            .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
            TiffIfd ifd = decoder.ReadIfd(0);

            var e = Assert.Throws<NotSupportedException>(() => decoder.ReadImageFormat(ifd));

            Assert.Equal("The specified TIFF photometric interpretation is not supported.", e.Message);
        }

        [Theory]
        [InlineData(false, TiffPhotometricInterpretation.WhiteIsZero, new[] { 3 })]
        [InlineData(true, TiffPhotometricInterpretation.WhiteIsZero, new[] { 3 })]
        public void ReadImageFormat_ThrowsExceptionForUnsupportedBitDepth(bool isLittleEndian, ushort photometricInterpretation, int[] bitsPerSample)
        {
            Stream stream = CreateTiffGenIfd()
                            .WithEntry(TiffGenEntry.Integer(TiffTags.PhotometricInterpretation, TiffType.Short, photometricInterpretation))
                            .WithEntry(TiffGenEntry.Integer(TiffTags.BitsPerSample, TiffType.Short, bitsPerSample))
                            .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
            TiffIfd ifd = decoder.ReadIfd(0);

            var e = Assert.Throws<NotSupportedException>(() => decoder.ReadImageFormat(ifd));

            Assert.Equal("The specified TIFF bit-depth is not supported.", e.Message);
        }

        [Theory]
        [InlineData(TiffColorType.WhiteIsZero8, 100, 80, 100 * 80)]
        [InlineData(TiffColorType.WhiteIsZero4, 100, 80, 50 * 80)]
        [InlineData(TiffColorType.WhiteIsZero4, 99, 80, 50 * 80)]
        [InlineData(TiffColorType.WhiteIsZero1, 160, 80, 20 * 80)]
        [InlineData(TiffColorType.WhiteIsZero1, 153, 80, 20 * 80)]
        public void CalculateImageBufferSize_ReturnsCorrectSize(ushort colorType, int width, int height, int expectedResult)
        {
            TiffDecoderCore decoder = new TiffDecoderCore(null, null);
            decoder.ColorType = (TiffColorType)colorType;

            int bufferSize = decoder.CalculateImageBufferSize(width, height);

            Assert.Equal(expectedResult, bufferSize);
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
                            TiffGenEntry.Integer(TiffTags.ResolutionUnit, TiffType.Short, 2),
                            TiffGenEntry.Integer(TiffTags.PhotometricInterpretation, TiffType.Short, (int)TiffPhotometricInterpretation.WhiteIsZero),
                            TiffGenEntry.Integer(TiffTags.BitsPerSample, TiffType.Short, new int[] { 8 }),
                            TiffGenEntry.Integer(TiffTags.Compression, TiffType.Short, (int)TiffCompression.None)
                        }
            };
        }
    }
}