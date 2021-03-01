// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Experimental.Tiff;
using SixLabors.ImageSharp.Formats.Experimental.Tiff.Constants;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

using Xunit;

using static SixLabors.ImageSharp.Tests.TestImages.Tiff;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff
{
    [Trait("Format", "Tiff")]
    public class TiffEncoderTests
    {
        private static readonly IImageDecoder ReferenceDecoder = new MagickReferenceDecoder();

        private static readonly Configuration Configuration;

        static TiffEncoderTests()
        {
            Configuration = new Configuration();
            Configuration.AddTiff();
        }

        [Theory]
        [InlineData(TiffEncodingMode.Default, TiffEncoderCompression.None, TiffBitsPerPixel.Bit24, TiffCompression.None)]
        [InlineData(TiffEncodingMode.Rgb, TiffEncoderCompression.None, TiffBitsPerPixel.Bit24, TiffCompression.None)]
        [InlineData(TiffEncodingMode.ColorPalette, TiffEncoderCompression.None, TiffBitsPerPixel.Bit8, TiffCompression.None)]
        [InlineData(TiffEncodingMode.Gray, TiffEncoderCompression.None, TiffBitsPerPixel.Bit8, TiffCompression.None)]
        [InlineData(TiffEncodingMode.BiColor, TiffEncoderCompression.None, TiffBitsPerPixel.Bit1, TiffCompression.None)]
        [InlineData(TiffEncodingMode.Default, TiffEncoderCompression.Deflate, TiffBitsPerPixel.Bit24, TiffCompression.Deflate)]
        [InlineData(TiffEncodingMode.Rgb, TiffEncoderCompression.Deflate, TiffBitsPerPixel.Bit24, TiffCompression.Deflate)]
        [InlineData(TiffEncodingMode.Gray, TiffEncoderCompression.Deflate, TiffBitsPerPixel.Bit8, TiffCompression.Deflate)]
        [InlineData(TiffEncodingMode.BiColor, TiffEncoderCompression.Deflate, TiffBitsPerPixel.Bit1, TiffCompression.Deflate)]
        [InlineData(TiffEncodingMode.ColorPalette, TiffEncoderCompression.Deflate, TiffBitsPerPixel.Bit8, TiffCompression.Deflate)]
        [InlineData(TiffEncodingMode.Default, TiffEncoderCompression.PackBits, TiffBitsPerPixel.Bit24, TiffCompression.PackBits)]
        [InlineData(TiffEncodingMode.Rgb, TiffEncoderCompression.PackBits, TiffBitsPerPixel.Bit24, TiffCompression.PackBits)]
        [InlineData(TiffEncodingMode.ColorPalette, TiffEncoderCompression.PackBits, TiffBitsPerPixel.Bit8, TiffCompression.PackBits)]
        [InlineData(TiffEncodingMode.Gray, TiffEncoderCompression.PackBits, TiffBitsPerPixel.Bit8, TiffCompression.PackBits)]
        [InlineData(TiffEncodingMode.BiColor, TiffEncoderCompression.PackBits, TiffBitsPerPixel.Bit1, TiffCompression.PackBits)]
        [InlineData(TiffEncodingMode.Rgb, TiffEncoderCompression.Lzw, TiffBitsPerPixel.Bit24, TiffCompression.Lzw)]
        [InlineData(TiffEncodingMode.Gray, TiffEncoderCompression.Lzw, TiffBitsPerPixel.Bit8, TiffCompression.Lzw)]
        [InlineData(TiffEncodingMode.ColorPalette, TiffEncoderCompression.Lzw, TiffBitsPerPixel.Bit8, TiffCompression.Lzw)]
        [InlineData(TiffEncodingMode.BiColor, TiffEncoderCompression.CcittGroup3Fax, TiffBitsPerPixel.Bit1, TiffCompression.CcittGroup3Fax)]
        [InlineData(TiffEncodingMode.BiColor, TiffEncoderCompression.ModifiedHuffman, TiffBitsPerPixel.Bit1, TiffCompression.Ccitt1D)]
        public void EncoderOptions_Work(TiffEncodingMode mode, TiffEncoderCompression compression, TiffBitsPerPixel expectedBitsPerPixel, TiffCompression expectedCompression)
        {
            // arrange
            var tiffEncoder = new TiffEncoder { Mode = mode, Compression = compression };
            Image input = new Image<Rgb24>(10, 10);
            using var memStream = new MemoryStream();

            // act
            input.Save(memStream, tiffEncoder);

            // assert
            memStream.Position = 0;
            using var output = Image.Load<Rgba32>(Configuration, memStream);
            TiffMetadata meta = output.Metadata.GetTiffMetadata();
            Assert.Equal(expectedBitsPerPixel, meta.BitsPerPixel);
            Assert.Equal(expectedCompression, meta.Compression);
        }

        [Theory]
        [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32, TiffBitsPerPixel.Bit1)]
        [WithFile(GrayscaleUncompressed, PixelTypes.Rgba32, TiffBitsPerPixel.Bit8)]
        [WithFile(RgbUncompressed, PixelTypes.Rgba32, TiffBitsPerPixel.Bit24)]
        [WithFile(Rgb4BitPalette, PixelTypes.Rgba32, TiffBitsPerPixel.Bit4)]
        [WithFile(RgbPalette, PixelTypes.Rgba32, TiffBitsPerPixel.Bit8)]
        [WithFile(Calliphora_PaletteUncompressed, PixelTypes.Rgba32, TiffBitsPerPixel.Bit8)]
        public void TiffEncoder_PreserveBitsPerPixel<TPixel>(TestImageProvider<TPixel> provider, TiffBitsPerPixel expectedBitsPerPixel)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // arrange
            var tiffEncoder = new TiffEncoder();
            using Image<TPixel> input = provider.GetImage();
            using var memStream = new MemoryStream();

            // act
            input.Save(memStream, tiffEncoder);

            // assert
            memStream.Position = 0;
            using var output = Image.Load<Rgba32>(Configuration, memStream);
            TiffMetadata meta = output.Metadata.GetTiffMetadata();
            Assert.Equal(expectedBitsPerPixel, meta.BitsPerPixel);
        }


        [Theory]
        [WithFile(RgbUncompressed, PixelTypes.Rgba32, TiffEncoderCompression.CcittGroup3Fax, TiffCompression.CcittGroup3Fax)]
        [WithFile(RgbUncompressed, PixelTypes.Rgba32, TiffEncoderCompression.ModifiedHuffman, TiffCompression.Ccitt1D)]
        [WithFile(GrayscaleUncompressed, PixelTypes.L8, TiffEncoderCompression.CcittGroup3Fax, TiffCompression.CcittGroup3Fax)]
        [WithFile(PaletteDeflateMultistrip, PixelTypes.L8, TiffEncoderCompression.ModifiedHuffman, TiffCompression.Ccitt1D)]
        public void TiffEncoder_EncodesWithCorrectBiColorModeCompression<TPixel>(TestImageProvider<TPixel> provider, TiffEncoderCompression compression, TiffCompression expectedCompression)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // arrange
            var encoder = new TiffEncoder() { Compression = compression };
            using Image<TPixel> input = provider.GetImage();
            using var memStream = new MemoryStream();

            // act
            input.Save(memStream, encoder);

            // assert
            memStream.Position = 0;
            using var output = Image.Load<Rgba32>(Configuration, memStream);
            TiffMetadata meta = output.Metadata.GetTiffMetadata();

            Assert.Equal(TiffBitsPerPixel.Bit1, meta.BitsPerPixel);
            Assert.Equal(expectedCompression, meta.Compression);
        }

        [Theory]
        [InlineData(TiffEncodingMode.ColorPalette, TiffEncoderCompression.CcittGroup3Fax)]
        [InlineData(TiffEncodingMode.ColorPalette, TiffEncoderCompression.ModifiedHuffman)]
        [InlineData(TiffEncodingMode.Gray, TiffEncoderCompression.ModifiedHuffman)]
        [InlineData(TiffEncodingMode.Rgb, TiffEncoderCompression.ModifiedHuffman)]
        public void TiffEncoder_IncompatibilityOptions(TiffEncodingMode mode, TiffEncoderCompression compression)
        {
            // arrange
            using var input = new Image<Rgb24>(10, 10);
            var encoder = new TiffEncoder() { Mode = mode, Compression = compression };
            using var memStream = new MemoryStream();

            // act
            Assert.Throws<ImageFormatException>(() => input.Save(memStream, encoder));
        }

        [Theory]
        [WithFile(Calliphora_RgbUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeRgb_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffEncodingMode.Rgb);

        [Theory]
        [WithFile(Calliphora_RgbUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeRgb_WithDeflateCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffEncodingMode.Rgb, TiffEncoderCompression.Deflate);

        [Theory]
        [WithFile(Calliphora_RgbUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeRgb_WithDeflateCompressionAndPredictor_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffEncodingMode.Rgb, TiffEncoderCompression.Deflate, usePredictor: true);

        [Theory]
        [WithFile(Calliphora_RgbUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeRgb_WithLzwCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffEncodingMode.Rgb, TiffEncoderCompression.Lzw);

        [Theory]
        [WithFile(Calliphora_RgbUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeRgb_WithLzwCompressionAndPredictor_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffEncodingMode.Rgb, TiffEncoderCompression.Lzw, usePredictor: true);

        [Theory]
        [WithFile(Calliphora_RgbUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeRgb_WithPackBitsCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffEncodingMode.Rgb, TiffEncoderCompression.PackBits);

        [Theory]
        [WithFile(Calliphora_GrayscaleUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeGray_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffEncodingMode.Gray);

        [Theory]
        [WithFile(Calliphora_GrayscaleUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeGray_WithDeflateCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffEncodingMode.Gray, TiffEncoderCompression.Deflate);

        [Theory]
        [WithFile(Calliphora_GrayscaleUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeGray_WithDeflateCompressionAndPredictor_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffEncodingMode.Gray, TiffEncoderCompression.Deflate, usePredictor: true);

        [Theory]
        [WithFile(Calliphora_GrayscaleUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeGray_WithLzwCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffEncodingMode.Gray, TiffEncoderCompression.Lzw);

        [Theory]
        [WithFile(Calliphora_GrayscaleUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeGray_WithLzwCompressionAndPredictor_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffEncodingMode.Gray, TiffEncoderCompression.Lzw, usePredictor: true);

        [Theory]
        [WithFile(Calliphora_GrayscaleUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeGray_WithPackBitsCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffEncodingMode.Gray, TiffEncoderCompression.PackBits);

        [Theory]
        [WithFile(Calliphora_PaletteUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeColorPalette_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffEncodingMode.ColorPalette, useExactComparer: false, compareTolerance: 0.001f);

        [Theory]
        [WithFile(Rgb4BitPalette, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeColorPalette_With4Bit_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffEncodingMode.ColorPalette, useExactComparer: false, compareTolerance: 0.001f);

        [Theory]
        [WithFile(Calliphora_PaletteUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeColorPalette_WithPackBitsCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffEncodingMode.ColorPalette, TiffEncoderCompression.PackBits, useExactComparer: false, compareTolerance: 0.001f);

        [Theory]
        [WithFile(Calliphora_PaletteUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeColorPalette_WithDeflateCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffEncodingMode.ColorPalette, TiffEncoderCompression.Deflate, useExactComparer: false, compareTolerance: 0.001f);

        [Theory]
        [WithFile(Calliphora_PaletteUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeColorPalette_WithDeflateCompressionAndPredictor_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffEncodingMode.ColorPalette, TiffEncoderCompression.Deflate, usePredictor: true, useExactComparer: false, compareTolerance: 0.001f);

        [Theory]
        [WithFile(Calliphora_PaletteUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeColorPalette_WithLzwCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffEncodingMode.ColorPalette, TiffEncoderCompression.Lzw, useExactComparer: false, compareTolerance: 0.001f);

        [Theory]
        [WithFile(Calliphora_PaletteUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeColorPalette_WithLzwCompressionAndPredictor_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffEncodingMode.ColorPalette, TiffEncoderCompression.Lzw, usePredictor: true, useExactComparer: false, compareTolerance: 0.001f);

        [Theory]
        [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeBiColor_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffEncodingMode.BiColor);

        [Theory]
        [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeBiColor_WithDeflateCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffEncodingMode.BiColor, TiffEncoderCompression.Deflate);

        [Theory]
        [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeBiColor_WithPackBitsCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffEncodingMode.BiColor, TiffEncoderCompression.PackBits);

        [Theory]
        [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeBiColor_WithCcittGroup3FaxCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffEncodingMode.BiColor, TiffEncoderCompression.CcittGroup3Fax);

        [Theory]
        [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeBiColor_WithModifiedHuffmanCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffEncodingMode.BiColor, TiffEncoderCompression.ModifiedHuffman);

        [Theory]
        [WithFile(GrayscaleUncompressed, PixelTypes.L8, TiffEncodingMode.Gray, TiffEncoderCompression.PackBits, 16 * 1024)]
        [WithFile(PaletteDeflateMultistrip, PixelTypes.L8, TiffEncodingMode.ColorPalette, TiffEncoderCompression.Lzw, 32 * 1024)]
        [WithFile(RgbUncompressed, PixelTypes.Rgba32, TiffEncodingMode.Rgb, TiffEncoderCompression.Deflate, 64 * 1024)]
        [WithFile(RgbUncompressed, PixelTypes.Rgb24, TiffEncodingMode.Rgb, TiffEncoderCompression.None, 10 * 1024)]
        [WithFile(RgbUncompressed, PixelTypes.Rgba32, TiffEncodingMode.Rgb, TiffEncoderCompression.None, 30 * 1024)]
        [WithFile(RgbUncompressed, PixelTypes.Rgb48, TiffEncodingMode.Rgb, TiffEncoderCompression.None, 70 * 1024)]
        public void TiffEncoder_StripLength<TPixel>(TestImageProvider<TPixel> provider, TiffEncodingMode mode, TiffEncoderCompression compression, int maxSize)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TestStripLength(provider, mode, compression, maxSize);

        [Theory]
        [WithFile(Calliphora_BiColorUncompressed, PixelTypes.L8, TiffEncodingMode.BiColor, TiffEncoderCompression.CcittGroup3Fax, 9 * 1024)]
        public void TiffEncoder_StripLength_OutOfBounds<TPixel>(TestImageProvider<TPixel> provider, TiffEncodingMode mode, TiffEncoderCompression compression, int maxSize)
            where TPixel : unmanaged, IPixel<TPixel> =>
            //// CcittGroup3Fax compressed data length can be larger than the original length
            Assert.Throws<Xunit.Sdk.TrueException>(() => TestStripLength(provider, mode, compression, maxSize));

        private static void TestStripLength<TPixel>(TestImageProvider<TPixel> provider, TiffEncodingMode mode, TiffEncoderCompression compression, int maxSize)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // arrange
            var tiffEncoder = new TiffEncoder() { Mode = mode, Compression = compression, MaxStripBytes = maxSize };
            Image<TPixel> input = provider.GetImage();
            using var memStream = new MemoryStream();
            TiffFrameMetadata inputMeta = input.Frames.RootFrame.Metadata.GetTiffMetadata();

            // act
            input.Save(memStream, tiffEncoder);

            // assert
            memStream.Position = 0;
            var output = Image.Load<Rgba32>(Configuration, memStream);
            TiffFrameMetadata meta = output.Frames.RootFrame.Metadata.GetTiffMetadata();

            Assert.True(output.Height > (int)meta.RowsPerStrip);
            Assert.True(meta.StripOffsets.Length > 1);
            Assert.True(meta.StripByteCounts.Length > 1);

            foreach (Number sz in meta.StripByteCounts)
            {
                Assert.True((uint)sz <= maxSize);
            }

            // for uncompressed more accurate test
            if (compression == TiffEncoderCompression.None)
            {
                for (int i = 0; i < meta.StripByteCounts.Length - 1; i++)
                {
                    // the difference must be less than one row
                    int stripBytes = (int)meta.StripByteCounts[i];
                    var widthBytes = (meta.BitsPerPixel + 7) / 8 * (int)meta.Width;

                    Assert.True((maxSize - stripBytes) < widthBytes);
                }
            }

            // compare with reference
            TestTiffEncoderCore(
                provider,
                (TiffBitsPerPixel)inputMeta.BitsPerPixel,
                mode,
                Convert(inputMeta.Compression),
                maxStripSize: maxSize);
        }

        private static void TestTiffEncoderCore<TPixel>(
            TestImageProvider<TPixel> provider,
            TiffBitsPerPixel bitsPerPixel,
            TiffEncodingMode mode,
            TiffEncoderCompression compression = TiffEncoderCompression.None,
            bool usePredictor = false,
            bool useExactComparer = true,
            int maxStripSize = 0,
            float compareTolerance = 0.01f)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage();
            var encoder = new TiffEncoder
            {
                Mode = mode,
                BitsPerPixel = bitsPerPixel,
                Compression = compression,
                UseHorizontalPredictor = usePredictor,
                MaxStripBytes = maxStripSize
            };

            // Does DebugSave & load reference CompareToReferenceInput():
            image.VerifyEncoder(provider, "tiff", bitsPerPixel, encoder, useExactComparer ? ImageComparer.Exact : ImageComparer.Tolerant(compareTolerance), referenceDecoder: ReferenceDecoder);
        }

        private static TiffEncoderCompression Convert(TiffCompression compression)
        {
            switch (compression)
            {
                default:
                case TiffCompression.None:
                    return TiffEncoderCompression.None;
                case TiffCompression.Deflate:
                    return TiffEncoderCompression.Deflate;
                case TiffCompression.Lzw:
                    return TiffEncoderCompression.Lzw;
                case TiffCompression.PackBits:
                    return TiffEncoderCompression.PackBits;
                case TiffCompression.Ccitt1D:
                    return TiffEncoderCompression.ModifiedHuffman;
                case TiffCompression.CcittGroup3Fax:
                    return TiffEncoderCompression.CcittGroup3Fax;
            }
        }
    }
}
