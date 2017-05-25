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
            Image<Rgba32> image = decoder.DecodeImage<Rgba32>(ifd);

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
            Image<Rgba32> image = decoder.DecodeImage<Rgba32>(ifd);

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

            var e = Assert.Throws<ImageFormatException>(() => decoder.DecodeImage<Rgba32>(ifd));

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

            var e = Assert.Throws<ImageFormatException>(() => decoder.DecodeImage<Rgba32>(ifd));

            Assert.Equal("The TIFF IFD does not specify the image dimensions.", e.Message);
        }

        [Theory]
        [InlineData(false, TiffCompression.None, TiffCompressionType.None)]
        [InlineData(true, TiffCompression.None, TiffCompressionType.None)]
        [InlineData(false, TiffCompression.PackBits, TiffCompressionType.PackBits)]
        [InlineData(true, TiffCompression.PackBits, TiffCompressionType.PackBits)]
        [InlineData(false, TiffCompression.Deflate, TiffCompressionType.Deflate)]
        [InlineData(true, TiffCompression.Deflate, TiffCompressionType.Deflate)]
        [InlineData(false, TiffCompression.OldDeflate, TiffCompressionType.Deflate)]
        [InlineData(true, TiffCompression.OldDeflate, TiffCompressionType.Deflate)]
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
        [InlineData(false, TiffCompression.ItuTRecT43)]
        [InlineData(false, TiffCompression.ItuTRecT82)]
        [InlineData(false, TiffCompression.Jpeg)]
        [InlineData(false, TiffCompression.Lzw)]
        [InlineData(false, TiffCompression.OldJpeg)]
        [InlineData(false, 999)]
        [InlineData(true, TiffCompression.Ccitt1D)]
        [InlineData(true, TiffCompression.CcittGroup3Fax)]
        [InlineData(true, TiffCompression.CcittGroup4Fax)]
        [InlineData(true, TiffCompression.ItuTRecT43)]
        [InlineData(true, TiffCompression.ItuTRecT82)]
        [InlineData(true, TiffCompression.Jpeg)]
        [InlineData(true, TiffCompression.Lzw)]
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
        [InlineData(false, TiffPhotometricInterpretation.WhiteIsZero, new[] { 3 }, TiffColorType.WhiteIsZero)]
        [InlineData(true, TiffPhotometricInterpretation.WhiteIsZero, new[] { 3 }, TiffColorType.WhiteIsZero)]
        [InlineData(false, TiffPhotometricInterpretation.WhiteIsZero, new[] { 8 }, TiffColorType.WhiteIsZero8)]
        [InlineData(true, TiffPhotometricInterpretation.WhiteIsZero, new[] { 8 }, TiffColorType.WhiteIsZero8)]
        [InlineData(false, TiffPhotometricInterpretation.WhiteIsZero, new[] { 4 }, TiffColorType.WhiteIsZero4)]
        [InlineData(true, TiffPhotometricInterpretation.WhiteIsZero, new[] { 4 }, TiffColorType.WhiteIsZero4)]
        [InlineData(false, TiffPhotometricInterpretation.WhiteIsZero, new[] { 1 }, TiffColorType.WhiteIsZero1)]
        [InlineData(true, TiffPhotometricInterpretation.WhiteIsZero, new[] { 1 }, TiffColorType.WhiteIsZero1)]
        [InlineData(false, TiffPhotometricInterpretation.BlackIsZero, new[] { 3 }, TiffColorType.BlackIsZero)]
        [InlineData(true, TiffPhotometricInterpretation.BlackIsZero, new[] { 3 }, TiffColorType.BlackIsZero)]
        [InlineData(false, TiffPhotometricInterpretation.BlackIsZero, new[] { 8 }, TiffColorType.BlackIsZero8)]
        [InlineData(true, TiffPhotometricInterpretation.BlackIsZero, new[] { 8 }, TiffColorType.BlackIsZero8)]
        [InlineData(false, TiffPhotometricInterpretation.BlackIsZero, new[] { 4 }, TiffColorType.BlackIsZero4)]
        [InlineData(true, TiffPhotometricInterpretation.BlackIsZero, new[] { 4 }, TiffColorType.BlackIsZero4)]
        [InlineData(false, TiffPhotometricInterpretation.BlackIsZero, new[] { 1 }, TiffColorType.BlackIsZero1)]
        [InlineData(true, TiffPhotometricInterpretation.BlackIsZero, new[] { 1 }, TiffColorType.BlackIsZero1)]
        [InlineData(false, TiffPhotometricInterpretation.PaletteColor, new[] { 3 }, TiffColorType.PaletteColor)]
        [InlineData(true, TiffPhotometricInterpretation.PaletteColor, new[] { 3 }, TiffColorType.PaletteColor)]
        [InlineData(false, TiffPhotometricInterpretation.PaletteColor, new[] { 8 }, TiffColorType.PaletteColor)]
        [InlineData(true, TiffPhotometricInterpretation.PaletteColor, new[] { 8 }, TiffColorType.PaletteColor)]
        [InlineData(false, TiffPhotometricInterpretation.PaletteColor, new[] { 4 }, TiffColorType.PaletteColor)]
        [InlineData(true, TiffPhotometricInterpretation.PaletteColor, new[] { 4 }, TiffColorType.PaletteColor)]
        [InlineData(false, TiffPhotometricInterpretation.PaletteColor, new[] { 1 }, TiffColorType.PaletteColor)]
        [InlineData(true, TiffPhotometricInterpretation.PaletteColor, new[] { 1 }, TiffColorType.PaletteColor)]
        [InlineData(false, TiffPhotometricInterpretation.Rgb, new[] { 4, 4, 4 }, TiffColorType.Rgb)]
        [InlineData(true, TiffPhotometricInterpretation.Rgb, new[] { 4, 4, 4 }, TiffColorType.Rgb)]
        [InlineData(false, TiffPhotometricInterpretation.Rgb, new[] { 8, 8, 8 }, TiffColorType.Rgb888)]
        [InlineData(true, TiffPhotometricInterpretation.Rgb, new[] { 8, 8, 8 }, TiffColorType.Rgb888)]
        public void ReadImageFormat_DeterminesCorrectColorImplementation_Chunky(bool isLittleEndian, ushort photometricInterpretation, int[] bitsPerSample, int colorType)
        {
            Stream stream = CreateTiffGenIfd()
                            .WithEntry(TiffGenEntry.Integer(TiffTags.PhotometricInterpretation, TiffType.Short, photometricInterpretation))
                            .WithEntry(TiffGenEntry.Integer(TiffTags.PlanarConfiguration, TiffType.Short, (int)TiffPlanarConfiguration.Chunky))
                            .WithEntry(TiffGenEntry.Integer(TiffTags.BitsPerSample, TiffType.Short, bitsPerSample))
                            .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
            TiffIfd ifd = decoder.ReadIfd(0);
            decoder.ReadImageFormat(ifd);

            Assert.Equal((TiffColorType)colorType, decoder.ColorType);
        }

        [Theory]
        [InlineData(false, TiffPhotometricInterpretation.Rgb, new[] { 4, 4, 4 }, TiffColorType.RgbPlanar)]
        [InlineData(true, TiffPhotometricInterpretation.Rgb, new[] { 4, 4, 4 }, TiffColorType.RgbPlanar)]
        [InlineData(false, TiffPhotometricInterpretation.Rgb, new[] { 8, 8, 8 }, TiffColorType.RgbPlanar)]
        [InlineData(true, TiffPhotometricInterpretation.Rgb, new[] { 8, 8, 8 }, TiffColorType.RgbPlanar)]
        public void ReadImageFormat_DeterminesCorrectColorImplementation_Planar(bool isLittleEndian, ushort photometricInterpretation, int[] bitsPerSample, int colorType)
        {
            Stream stream = CreateTiffGenIfd()
                            .WithEntry(TiffGenEntry.Integer(TiffTags.PhotometricInterpretation, TiffType.Short, photometricInterpretation))
                            .WithEntry(TiffGenEntry.Integer(TiffTags.PlanarConfiguration, TiffType.Short, (int)TiffPlanarConfiguration.Planar))
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
        [InlineData(false, TiffPhotometricInterpretation.BlackIsZero, TiffColorType.BlackIsZero1)]
        [InlineData(true, TiffPhotometricInterpretation.BlackIsZero, TiffColorType.BlackIsZero1)]
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
        [InlineData(false, TiffPhotometricInterpretation.CieLab)]
        [InlineData(false, TiffPhotometricInterpretation.ColorFilterArray)]
        [InlineData(false, TiffPhotometricInterpretation.IccLab)]
        [InlineData(false, TiffPhotometricInterpretation.ItuLab)]
        [InlineData(false, TiffPhotometricInterpretation.LinearRaw)]
        [InlineData(false, TiffPhotometricInterpretation.Separated)]
        [InlineData(false, TiffPhotometricInterpretation.TransparencyMask)]
        [InlineData(false, TiffPhotometricInterpretation.YCbCr)]
        [InlineData(false, 999)]
        [InlineData(true, TiffPhotometricInterpretation.CieLab)]
        [InlineData(true, TiffPhotometricInterpretation.ColorFilterArray)]
        [InlineData(true, TiffPhotometricInterpretation.IccLab)]
        [InlineData(true, TiffPhotometricInterpretation.ItuLab)]
        [InlineData(true, TiffPhotometricInterpretation.LinearRaw)]
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
        [InlineData(false, new[] { 8u })]
        [InlineData(true, new[] { 8u })]
        [InlineData(false, new[] { 4u })]
        [InlineData(true, new[] { 4u })]
        [InlineData(false, new[] { 1u })]
        [InlineData(true, new[] { 1u })]
        // [InlineData(false, new[] { 1u, 2u, 3u })]
        // [InlineData(true, new[] { 1u, 2u, 3u })]
        // [InlineData(false, new[] { 8u, 8u, 8u })]
        // [InlineData(true, new[] { 8u, 8u, 8u })]
        public void ReadImageFormat_ReadsBitsPerSample(bool isLittleEndian, uint[] bitsPerSample)
        {
            Stream stream = CreateTiffGenIfd()
                            .WithEntry(TiffGenEntry.Integer(TiffTags.BitsPerSample, TiffType.Short, bitsPerSample))
                            .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
            TiffIfd ifd = decoder.ReadIfd(0);
            decoder.ReadImageFormat(ifd);

            Assert.Equal(bitsPerSample, decoder.BitsPerSample);
        }

        [Theory]
        [InlineData(false, TiffPhotometricInterpretation.WhiteIsZero)]
        [InlineData(true, TiffPhotometricInterpretation.WhiteIsZero)]
        [InlineData(false, TiffPhotometricInterpretation.BlackIsZero)]
        [InlineData(true, TiffPhotometricInterpretation.BlackIsZero)]
        public void ReadImageFormat_ReadsBitsPerSample_DefaultsToBilevel(bool isLittleEndian, ushort photometricInterpretation)
        {
            Stream stream = CreateTiffGenIfd()
                            .WithEntry(TiffGenEntry.Integer(TiffTags.PhotometricInterpretation, TiffType.Short, photometricInterpretation))
                            .WithoutEntry(TiffTags.BitsPerSample)
                            .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
            TiffIfd ifd = decoder.ReadIfd(0);
            decoder.ReadImageFormat(ifd);

            Assert.Equal(new[] { 1u }, decoder.BitsPerSample);
        }

        [Theory]
        [MemberData(nameof(IsLittleEndianValues))]
        public void ReadImageFormat_ThrowsExceptionForMissingBitsPerSample(bool isLittleEndian)
        {
            Stream stream = CreateTiffGenIfd()
                            .WithEntry(TiffGenEntry.Integer(TiffTags.PhotometricInterpretation, TiffType.Short, (int)TiffPhotometricInterpretation.PaletteColor))
                            .WithoutEntry(TiffTags.BitsPerSample)
                            .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
            TiffIfd ifd = decoder.ReadIfd(0);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadImageFormat(ifd));

            Assert.Equal("The TIFF BitsPerSample entry is missing.", e.Message);
        }

        [Theory]
        [InlineData(false, TiffPhotometricInterpretation.WhiteIsZero, new int[] { })]
        [InlineData(true, TiffPhotometricInterpretation.WhiteIsZero, new int[] { })]
        [InlineData(false, TiffPhotometricInterpretation.BlackIsZero, new int[] { })]
        [InlineData(true, TiffPhotometricInterpretation.BlackIsZero, new int[] { })]
        [InlineData(false, TiffPhotometricInterpretation.PaletteColor, new int[] { })]
        [InlineData(true, TiffPhotometricInterpretation.PaletteColor, new int[] { })]
        [InlineData(false, TiffPhotometricInterpretation.Rgb, new int[] { })]
        [InlineData(true, TiffPhotometricInterpretation.Rgb, new int[] { })]
        [InlineData(false, TiffPhotometricInterpretation.WhiteIsZero, new[] { 8, 8 })]
        [InlineData(true, TiffPhotometricInterpretation.WhiteIsZero, new[] { 8, 8 })]
        [InlineData(false, TiffPhotometricInterpretation.BlackIsZero, new[] { 8, 8 })]
        [InlineData(true, TiffPhotometricInterpretation.BlackIsZero, new[] { 8, 8 })]
        [InlineData(false, TiffPhotometricInterpretation.PaletteColor, new[] { 8, 8 })]
        [InlineData(true, TiffPhotometricInterpretation.PaletteColor, new[] { 8, 8 })]
        [InlineData(false, TiffPhotometricInterpretation.Rgb, new[] { 8 })]
        [InlineData(true, TiffPhotometricInterpretation.Rgb, new[] { 8 })]
        [InlineData(false, TiffPhotometricInterpretation.Rgb, new[] { 8, 8 })]
        [InlineData(true, TiffPhotometricInterpretation.Rgb, new[] { 8, 8 })]
        public void ReadImageFormat_ThrowsExceptionForUnsupportedNumberOfSamples(bool isLittleEndian, ushort photometricInterpretation, int[] bitsPerSample)
        {
            Stream stream = CreateTiffGenIfd()
                            .WithEntry(TiffGenEntry.Integer(TiffTags.PhotometricInterpretation, TiffType.Short, photometricInterpretation))
                            .WithEntry(TiffGenEntry.Integer(TiffTags.BitsPerSample, TiffType.Short, bitsPerSample))
                            .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
            TiffIfd ifd = decoder.ReadIfd(0);

            var e = Assert.Throws<NotSupportedException>(() => decoder.ReadImageFormat(ifd));

            Assert.Equal("The number of samples in the TIFF BitsPerSample entry is not supported.", e.Message);
        }

        [Theory]
        [MemberData(nameof(IsLittleEndianValues))]
        public void ReadImageFormat_ReadsColorMap(bool isLittleEndian)
        {
            Stream stream = CreateTiffGenIfd()
                            .WithEntry(TiffGenEntry.Integer(TiffTags.PhotometricInterpretation, TiffType.Short, (int)TiffPhotometricInterpretation.PaletteColor))
                            .WithEntry(TiffGenEntry.Integer(TiffTags.ColorMap, TiffType.Short, new int[] { 10, 20, 30, 40, 50, 60 }))
                            .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
            TiffIfd ifd = decoder.ReadIfd(0);
            decoder.ReadImageFormat(ifd);

            Assert.Equal(new uint[] { 10, 20, 30, 40, 50, 60 }, decoder.ColorMap);
        }

        [Theory]
        [MemberData(nameof(IsLittleEndianValues))]
        public void ReadImageFormat_ThrowsExceptionForMissingColorMap(bool isLittleEndian)
        {
            Stream stream = CreateTiffGenIfd()
                            .WithEntry(TiffGenEntry.Integer(TiffTags.PhotometricInterpretation, TiffType.Short, (int)TiffPhotometricInterpretation.PaletteColor))
                            .WithoutEntry(TiffTags.ColorMap)
                            .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
            TiffIfd ifd = decoder.ReadIfd(0);

            var e = Assert.Throws<ImageFormatException>(() => decoder.ReadImageFormat(ifd));

            Assert.Equal("The TIFF ColorMap entry is missing for a pallete color image.", e.Message);
        }

        [Theory]
        [InlineData(false, TiffPlanarConfiguration.Chunky)]
        [InlineData(true, TiffPlanarConfiguration.Chunky)]
        [InlineData(false, TiffPlanarConfiguration.Planar)]
        [InlineData(true, TiffPlanarConfiguration.Planar)]
        public void ReadImageFormat_ReadsPlanarConfiguration(bool isLittleEndian, int planarConfiguration)
        {
            Stream stream = CreateTiffGenIfd()
                            .WithEntry(TiffGenEntry.Integer(TiffTags.PhotometricInterpretation, TiffType.Short, (int)TiffPhotometricInterpretation.Rgb))
                            .WithEntry(TiffGenEntry.Integer(TiffTags.BitsPerSample, TiffType.Short, new int[] { 8, 8, 8 }))
                            .WithEntry(TiffGenEntry.Integer(TiffTags.PlanarConfiguration, TiffType.Short, (int)planarConfiguration))
                            .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
            TiffIfd ifd = decoder.ReadIfd(0);
            decoder.ReadImageFormat(ifd);

            Assert.Equal((TiffPlanarConfiguration)planarConfiguration, decoder.PlanarConfiguration);
        }

        [Theory]
        [MemberData(nameof(IsLittleEndianValues))]
        public void ReadImageFormat_DefaultsPlanarConfigurationToChunky(bool isLittleEndian)
        {
            Stream stream = CreateTiffGenIfd()
                            .WithEntry(TiffGenEntry.Integer(TiffTags.PhotometricInterpretation, TiffType.Short, (int)TiffPhotometricInterpretation.Rgb))
                            .WithEntry(TiffGenEntry.Integer(TiffTags.BitsPerSample, TiffType.Short, new int[] { 8, 8, 8 }))
                            .WithoutEntry(TiffTags.PlanarConfiguration)
                            .ToStream(isLittleEndian);

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
            TiffIfd ifd = decoder.ReadIfd(0);
            decoder.ReadImageFormat(ifd);

            Assert.Equal(TiffPlanarConfiguration.Chunky, decoder.PlanarConfiguration);
        }

        [Theory]
        [InlineData(TiffColorType.WhiteIsZero, new uint[] { 1 }, 160, 80, 20 * 80)]
        [InlineData(TiffColorType.WhiteIsZero, new uint[] { 1 }, 153, 80, 20 * 80)]
        [InlineData(TiffColorType.WhiteIsZero, new uint[] { 3 }, 100, 80, 38 * 80)]
        [InlineData(TiffColorType.WhiteIsZero, new uint[] { 4 }, 100, 80, 50 * 80)]
        [InlineData(TiffColorType.WhiteIsZero, new uint[] { 4 }, 99, 80, 50 * 80)]
        [InlineData(TiffColorType.WhiteIsZero, new uint[] { 8 }, 100, 80, 100 * 80)]
        [InlineData(TiffColorType.PaletteColor, new uint[] { 1 }, 160, 80, 20 * 80)]
        [InlineData(TiffColorType.PaletteColor, new uint[] { 1 }, 153, 80, 20 * 80)]
        [InlineData(TiffColorType.PaletteColor, new uint[] { 3 }, 100, 80, 38 * 80)]
        [InlineData(TiffColorType.PaletteColor, new uint[] { 4 }, 100, 80, 50 * 80)]
        [InlineData(TiffColorType.PaletteColor, new uint[] { 4 }, 99, 80, 50 * 80)]
        [InlineData(TiffColorType.PaletteColor, new uint[] { 8 }, 100, 80, 100 * 80)]
        [InlineData(TiffColorType.Rgb, new uint[] { 8, 8, 8 }, 100, 80, 300 * 80)]
        [InlineData(TiffColorType.Rgb, new uint[] { 4, 4, 4 }, 100, 80, 150 * 80)]
        [InlineData(TiffColorType.Rgb, new uint[] { 4, 8, 4 }, 100, 80, 200 * 80)]
        public void CalculateImageBufferSize_ReturnsCorrectSize_Chunky(ushort colorType, uint[] bitsPerSample, int width, int height, int expectedResult)
        {
            TiffDecoderCore decoder = new TiffDecoderCore(null, null);
            decoder.ColorType = (TiffColorType)colorType;
            decoder.PlanarConfiguration = TiffPlanarConfiguration.Chunky;
            decoder.BitsPerSample = bitsPerSample;

            int bufferSize = decoder.CalculateImageBufferSize(width, height, 0);

            Assert.Equal(expectedResult, bufferSize);
        }

        [Theory]
        [InlineData(TiffColorType.Rgb, new uint[] { 8, 8, 8 }, 100, 80, 0, 100 * 80)]
        [InlineData(TiffColorType.Rgb, new uint[] { 8, 8, 8 }, 100, 80, 1, 100 * 80)]
        [InlineData(TiffColorType.Rgb, new uint[] { 8, 8, 8 }, 100, 80, 2, 100 * 80)]
        [InlineData(TiffColorType.Rgb, new uint[] { 4, 4, 4 }, 100, 80, 0, 50 * 80)]
        [InlineData(TiffColorType.Rgb, new uint[] { 4, 4, 4 }, 100, 80, 1, 50 * 80)]
        [InlineData(TiffColorType.Rgb, new uint[] { 4, 4, 4 }, 100, 80, 2, 50 * 80)]
        [InlineData(TiffColorType.Rgb, new uint[] { 4, 8, 4 }, 100, 80, 0, 50 * 80)]
        [InlineData(TiffColorType.Rgb, new uint[] { 4, 8, 4 }, 100, 80, 1, 100 * 80)]
        [InlineData(TiffColorType.Rgb, new uint[] { 4, 8, 4 }, 100, 80, 2, 50 * 80)]
        [InlineData(TiffColorType.Rgb, new uint[] { 4, 8, 4 }, 99, 80, 0, 50 * 80)]
        [InlineData(TiffColorType.Rgb, new uint[] { 4, 8, 4 }, 99, 80, 1, 99 * 80)]
        [InlineData(TiffColorType.Rgb, new uint[] { 4, 8, 4 }, 99, 80, 2, 50 * 80)]

        public void CalculateImageBufferSize_ReturnsCorrectSize_Planar(ushort colorType, uint[] bitsPerSample, int width, int height, int plane, int expectedResult)
        {
            TiffDecoderCore decoder = new TiffDecoderCore(null, null);
            decoder.ColorType = (TiffColorType)colorType;
            decoder.PlanarConfiguration = TiffPlanarConfiguration.Planar;
            decoder.BitsPerSample = bitsPerSample;

            int bufferSize = decoder.CalculateImageBufferSize(width, height, plane);

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
                            TiffGenEntry.Integer(TiffTags.Compression, TiffType.Short, (int)TiffCompression.None),
                            TiffGenEntry.Integer(TiffTags.ColorMap, TiffType.Short, new int[256])
                        }
            };
        }
    }
}