// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    [Trait("Category", "Tiff")]
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

            TiffDecoderCore decoder = new TiffDecoderCore(stream, isLittleEndian, null, null);
            TiffIfd ifd = decoder.ReadIfd(0);
            Image<Rgba32> image = decoder.DecodeImage<Rgba32>(ifd);

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
        [InlineData(false, (ushort)TiffCompression.None, (int)TiffCompressionType.None)]
        [InlineData(true, (ushort)TiffCompression.None, (int)TiffCompressionType.None)]
        [InlineData(false, (ushort)TiffCompression.PackBits, (int)TiffCompressionType.PackBits)]
        [InlineData(true, (ushort)TiffCompression.PackBits, (int)TiffCompressionType.PackBits)]
        [InlineData(false, (ushort)TiffCompression.Deflate, (int)TiffCompressionType.Deflate)]
        [InlineData(true, (ushort)TiffCompression.Deflate, (int)TiffCompressionType.Deflate)]
        [InlineData(false, (ushort)TiffCompression.OldDeflate, (int)TiffCompressionType.Deflate)]
        [InlineData(true, (ushort)TiffCompression.OldDeflate, (int)TiffCompressionType.Deflate)]
        [InlineData(false, (ushort)TiffCompression.Lzw, (int)TiffCompressionType.Lzw)]
        [InlineData(true, (ushort)TiffCompression.Lzw, (int)TiffCompressionType.Lzw)]
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
        [InlineData(false, (ushort)TiffCompression.Ccitt1D)]
        [InlineData(false, (ushort)TiffCompression.CcittGroup3Fax)]
        [InlineData(false, (ushort)TiffCompression.CcittGroup4Fax)]
        [InlineData(false, (ushort)TiffCompression.ItuTRecT43)]
        [InlineData(false, (ushort)TiffCompression.ItuTRecT82)]
        [InlineData(false, (ushort)TiffCompression.Jpeg)]
        [InlineData(false, (ushort)TiffCompression.OldJpeg)]
        [InlineData(false, 999)]
        [InlineData(true, (ushort)TiffCompression.Ccitt1D)]
        [InlineData(true, (ushort)TiffCompression.CcittGroup3Fax)]
        [InlineData(true, (ushort)TiffCompression.CcittGroup4Fax)]
        [InlineData(true, (ushort)TiffCompression.ItuTRecT43)]
        [InlineData(true, (ushort)TiffCompression.ItuTRecT82)]
        [InlineData(true, (ushort)TiffCompression.Jpeg)]
        [InlineData(true, (ushort)TiffCompression.OldJpeg)]
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
        [InlineData(false, (ushort)TiffPhotometricInterpretation.WhiteIsZero, new[] { 3 }, (int)TiffColorType.WhiteIsZero)]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.WhiteIsZero, new[] { 3 }, (int)TiffColorType.WhiteIsZero)]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.WhiteIsZero, new[] { 8 }, (int)TiffColorType.WhiteIsZero8)]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.WhiteIsZero, new[] { 8 }, (int)TiffColorType.WhiteIsZero8)]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.WhiteIsZero, new[] { 4 }, (int)TiffColorType.WhiteIsZero4)]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.WhiteIsZero, new[] { 4 }, (int)TiffColorType.WhiteIsZero4)]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.WhiteIsZero, new[] { 1 }, (int)TiffColorType.WhiteIsZero1)]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.WhiteIsZero, new[] { 1 }, (int)TiffColorType.WhiteIsZero1)]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.BlackIsZero, new[] { 3 }, (int)TiffColorType.BlackIsZero)]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.BlackIsZero, new[] { 3 }, (int)TiffColorType.BlackIsZero)]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.BlackIsZero, new[] { 8 }, (int)TiffColorType.BlackIsZero8)]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.BlackIsZero, new[] { 8 }, (int)TiffColorType.BlackIsZero8)]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.BlackIsZero, new[] { 4 }, (int)TiffColorType.BlackIsZero4)]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.BlackIsZero, new[] { 4 }, (int)TiffColorType.BlackIsZero4)]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.BlackIsZero, new[] { 1 }, (int)TiffColorType.BlackIsZero1)]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.BlackIsZero, new[] { 1 }, (int)TiffColorType.BlackIsZero1)]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.PaletteColor, new[] { 3 }, (int)TiffColorType.PaletteColor)]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.PaletteColor, new[] { 3 }, (int)TiffColorType.PaletteColor)]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.PaletteColor, new[] { 8 }, (int)TiffColorType.PaletteColor)]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.PaletteColor, new[] { 8 }, (int)TiffColorType.PaletteColor)]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.PaletteColor, new[] { 4 }, (int)TiffColorType.PaletteColor)]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.PaletteColor, new[] { 4 }, (int)TiffColorType.PaletteColor)]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.PaletteColor, new[] { 1 }, (int)TiffColorType.PaletteColor)]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.PaletteColor, new[] { 1 }, (int)TiffColorType.PaletteColor)]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.Rgb, new[] { 4, 4, 4 }, (int)TiffColorType.Rgb)]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.Rgb, new[] { 4, 4, 4 }, (int)TiffColorType.Rgb)]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.Rgb, new[] { 8, 8, 8 }, (int)TiffColorType.Rgb888)]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.Rgb, new[] { 8, 8, 8 }, (int)TiffColorType.Rgb888)]
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
        [InlineData(false, (ushort)TiffPhotometricInterpretation.Rgb, new[] { 4, 4, 4 }, (int)TiffColorType.RgbPlanar)]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.Rgb, new[] { 4, 4, 4 }, (int)TiffColorType.RgbPlanar)]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.Rgb, new[] { 8, 8, 8 }, (int)TiffColorType.RgbPlanar)]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.Rgb, new[] { 8, 8, 8 }, (int)TiffColorType.RgbPlanar)]
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
        [InlineData(false, (ushort)TiffPhotometricInterpretation.WhiteIsZero, (int)TiffColorType.WhiteIsZero1)]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.WhiteIsZero, (int)TiffColorType.WhiteIsZero1)]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.BlackIsZero, (int)TiffColorType.BlackIsZero1)]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.BlackIsZero, (int)TiffColorType.BlackIsZero1)]
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
        // [InlineData(false, new[] { 8 }, (int)TiffColorType.WhiteIsZero8)]
        // [InlineData(true, new[] { 8 }, (int)TiffColorType.WhiteIsZero8)]
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
        [InlineData(false, (ushort)TiffPhotometricInterpretation.CieLab)]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.ColorFilterArray)]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.IccLab)]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.ItuLab)]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.LinearRaw)]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.Separated)]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.TransparencyMask)]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.YCbCr)]
        [InlineData(false, 999)]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.CieLab)]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.ColorFilterArray)]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.IccLab)]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.ItuLab)]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.LinearRaw)]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.Separated)]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.TransparencyMask)]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.YCbCr)]
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
        [InlineData(false, (ushort)TiffPhotometricInterpretation.WhiteIsZero)]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.WhiteIsZero)]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.BlackIsZero)]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.BlackIsZero)]
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
        [InlineData(false, (ushort)TiffPhotometricInterpretation.WhiteIsZero, new int[] { })]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.WhiteIsZero, new int[] { })]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.BlackIsZero, new int[] { })]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.BlackIsZero, new int[] { })]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.PaletteColor, new int[] { })]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.PaletteColor, new int[] { })]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.Rgb, new int[] { })]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.Rgb, new int[] { })]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.WhiteIsZero, new[] { 8, 8 })]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.WhiteIsZero, new[] { 8, 8 })]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.BlackIsZero, new[] { 8, 8 })]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.BlackIsZero, new[] { 8, 8 })]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.PaletteColor, new[] { 8, 8 })]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.PaletteColor, new[] { 8, 8 })]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.Rgb, new[] { 8 })]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.Rgb, new[] { 8 })]
        [InlineData(false, (ushort)TiffPhotometricInterpretation.Rgb, new[] { 8, 8 })]
        [InlineData(true, (ushort)TiffPhotometricInterpretation.Rgb, new[] { 8, 8 })]
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
        [InlineData(false, (ushort)TiffPlanarConfiguration.Chunky)]
        [InlineData(true, (ushort)TiffPlanarConfiguration.Chunky)]
        [InlineData(false, (ushort)TiffPlanarConfiguration.Planar)]
        [InlineData(true, (ushort)TiffPlanarConfiguration.Planar)]
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
        [InlineData((ushort)TiffColorType.WhiteIsZero, new uint[] { 1 }, 160, 80, 20 * 80)]
        [InlineData((ushort)TiffColorType.WhiteIsZero, new uint[] { 1 }, 153, 80, 20 * 80)]
        [InlineData((ushort)TiffColorType.WhiteIsZero, new uint[] { 3 }, 100, 80, 38 * 80)]
        [InlineData((ushort)TiffColorType.WhiteIsZero, new uint[] { 4 }, 100, 80, 50 * 80)]
        [InlineData((ushort)TiffColorType.WhiteIsZero, new uint[] { 4 }, 99, 80, 50 * 80)]
        [InlineData((ushort)TiffColorType.WhiteIsZero, new uint[] { 8 }, 100, 80, 100 * 80)]
        [InlineData((ushort)TiffColorType.PaletteColor, new uint[] { 1 }, 160, 80, 20 * 80)]
        [InlineData((ushort)TiffColorType.PaletteColor, new uint[] { 1 }, 153, 80, 20 * 80)]
        [InlineData((ushort)TiffColorType.PaletteColor, new uint[] { 3 }, 100, 80, 38 * 80)]
        [InlineData((ushort)TiffColorType.PaletteColor, new uint[] { 4 }, 100, 80, 50 * 80)]
        [InlineData((ushort)TiffColorType.PaletteColor, new uint[] { 4 }, 99, 80, 50 * 80)]
        [InlineData((ushort)TiffColorType.PaletteColor, new uint[] { 8 }, 100, 80, 100 * 80)]
        [InlineData((ushort)TiffColorType.Rgb, new uint[] { 8, 8, 8 }, 100, 80, 300 * 80)]
        [InlineData((ushort)TiffColorType.Rgb, new uint[] { 4, 4, 4 }, 100, 80, 150 * 80)]
        [InlineData((ushort)TiffColorType.Rgb, new uint[] { 4, 8, 4 }, 100, 80, 200 * 80)]
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
        [InlineData((ushort)TiffColorType.Rgb, new uint[] { 8, 8, 8 }, 100, 80, 0, 100 * 80)]
        [InlineData((ushort)TiffColorType.Rgb, new uint[] { 8, 8, 8 }, 100, 80, 1, 100 * 80)]
        [InlineData((ushort)TiffColorType.Rgb, new uint[] { 8, 8, 8 }, 100, 80, 2, 100 * 80)]
        [InlineData((ushort)TiffColorType.Rgb, new uint[] { 4, 4, 4 }, 100, 80, 0, 50 * 80)]
        [InlineData((ushort)TiffColorType.Rgb, new uint[] { 4, 4, 4 }, 100, 80, 1, 50 * 80)]
        [InlineData((ushort)TiffColorType.Rgb, new uint[] { 4, 4, 4 }, 100, 80, 2, 50 * 80)]
        [InlineData((ushort)TiffColorType.Rgb, new uint[] { 4, 8, 4 }, 100, 80, 0, 50 * 80)]
        [InlineData((ushort)TiffColorType.Rgb, new uint[] { 4, 8, 4 }, 100, 80, 1, 100 * 80)]
        [InlineData((ushort)TiffColorType.Rgb, new uint[] { 4, 8, 4 }, 100, 80, 2, 50 * 80)]
        [InlineData((ushort)TiffColorType.Rgb, new uint[] { 4, 8, 4 }, 99, 80, 0, 50 * 80)]
        [InlineData((ushort)TiffColorType.Rgb, new uint[] { 4, 8, 4 }, 99, 80, 1, 99 * 80)]
        [InlineData((ushort)TiffColorType.Rgb, new uint[] { 4, 8, 4 }, 99, 80, 2, 50 * 80)]

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
                            TiffGenEntry.Rational(TiffTags.XResolution, 100, 1),
                            TiffGenEntry.Rational(TiffTags.YResolution, 200, 1),
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