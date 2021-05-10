// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
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
        [InlineData(TiffEncodingMode.Default, TiffBitsPerPixel.Bit24)]
        [InlineData(TiffEncodingMode.Rgb, TiffBitsPerPixel.Bit24)]
        [InlineData(TiffEncodingMode.ColorPalette, TiffBitsPerPixel.Bit8)]
        [InlineData(TiffEncodingMode.Gray, TiffBitsPerPixel.Bit8)]
        [InlineData(TiffEncodingMode.BiColor, TiffBitsPerPixel.Bit1)]
        public void EncoderOptions_SetEncodingMode_Works(TiffEncodingMode mode, TiffBitsPerPixel expectedBitsPerPixel)
        {
            // arrange
            var tiffEncoder = new TiffEncoder { Mode = mode };
            using Image input = new Image<Rgb24>(10, 10);
            using var memStream = new MemoryStream();

            // act
            input.Save(memStream, tiffEncoder);

            // assert
            memStream.Position = 0;
            using var output = Image.Load<Rgba32>(Configuration, memStream);
            TiffMetadata meta = output.Metadata.GetTiffMetadata();
            Assert.Equal(expectedBitsPerPixel, meta.BitsPerPixel);
            Assert.Equal(TiffCompression.None, meta.Compression);
        }

        [Theory]
        [InlineData(TiffBitsPerPixel.Bit24)]
        [InlineData(TiffBitsPerPixel.Bit8)]
        [InlineData(TiffBitsPerPixel.Bit4)]
        [InlineData(TiffBitsPerPixel.Bit1)]
        public void EncoderOptions_SetBitPerPixel_Works(TiffBitsPerPixel bitsPerPixel)
        {
            // arrange
            var tiffEncoder = new TiffEncoder { BitsPerPixel = bitsPerPixel };
            using Image input = new Image<Rgb24>(10, 10);
            using var memStream = new MemoryStream();

            // act
            input.Save(memStream, tiffEncoder);

            // assert
            memStream.Position = 0;
            using var output = Image.Load<Rgba32>(Configuration, memStream);
            TiffMetadata meta = output.Metadata.GetTiffMetadata();
            Assert.Equal(bitsPerPixel, meta.BitsPerPixel);
            Assert.Equal(TiffCompression.None, meta.Compression);
        }

        [Theory]
        [InlineData(TiffEncodingMode.Default, TiffCompression.Deflate, TiffBitsPerPixel.Bit24, TiffCompression.Deflate)]
        [InlineData(TiffEncodingMode.Rgb, TiffCompression.Deflate, TiffBitsPerPixel.Bit24, TiffCompression.Deflate)]
        [InlineData(TiffEncodingMode.Gray, TiffCompression.Deflate, TiffBitsPerPixel.Bit8, TiffCompression.Deflate)]
        [InlineData(TiffEncodingMode.BiColor, TiffCompression.Deflate, TiffBitsPerPixel.Bit1, TiffCompression.Deflate)]
        [InlineData(TiffEncodingMode.ColorPalette, TiffCompression.Deflate, TiffBitsPerPixel.Bit8, TiffCompression.Deflate)]
        [InlineData(TiffEncodingMode.Default, TiffCompression.PackBits, TiffBitsPerPixel.Bit24, TiffCompression.PackBits)]
        [InlineData(TiffEncodingMode.Rgb, TiffCompression.PackBits, TiffBitsPerPixel.Bit24, TiffCompression.PackBits)]
        [InlineData(TiffEncodingMode.ColorPalette, TiffCompression.PackBits, TiffBitsPerPixel.Bit8, TiffCompression.PackBits)]
        [InlineData(TiffEncodingMode.Gray, TiffCompression.PackBits, TiffBitsPerPixel.Bit8, TiffCompression.PackBits)]
        [InlineData(TiffEncodingMode.BiColor, TiffCompression.PackBits, TiffBitsPerPixel.Bit1, TiffCompression.PackBits)]
        [InlineData(TiffEncodingMode.Rgb, TiffCompression.Lzw, TiffBitsPerPixel.Bit24, TiffCompression.Lzw)]
        [InlineData(TiffEncodingMode.Gray, TiffCompression.Lzw, TiffBitsPerPixel.Bit8, TiffCompression.Lzw)]
        [InlineData(TiffEncodingMode.ColorPalette, TiffCompression.Lzw, TiffBitsPerPixel.Bit8, TiffCompression.Lzw)]
        [InlineData(TiffEncodingMode.BiColor, TiffCompression.CcittGroup3Fax, TiffBitsPerPixel.Bit1, TiffCompression.CcittGroup3Fax)]
        [InlineData(TiffEncodingMode.BiColor, TiffCompression.Ccitt1D, TiffBitsPerPixel.Bit1, TiffCompression.Ccitt1D)]
        [InlineData(TiffEncodingMode.Rgb, TiffCompression.ItuTRecT43, TiffBitsPerPixel.Bit24, TiffCompression.None)]
        [InlineData(TiffEncodingMode.Rgb, TiffCompression.ItuTRecT82, TiffBitsPerPixel.Bit24, TiffCompression.None)]
        [InlineData(TiffEncodingMode.Rgb, TiffCompression.Jpeg, TiffBitsPerPixel.Bit24, TiffCompression.None)]
        [InlineData(TiffEncodingMode.Rgb, TiffCompression.OldDeflate, TiffBitsPerPixel.Bit24, TiffCompression.None)]
        [InlineData(TiffEncodingMode.Rgb, TiffCompression.OldJpeg, TiffBitsPerPixel.Bit24, TiffCompression.None)]
        public void EncoderOptions_SetEncodingModeAndCompression_Works(TiffEncodingMode mode, TiffCompression compression, TiffBitsPerPixel expectedBitsPerPixel, TiffCompression expectedCompression)
        {
            // arrange
            var tiffEncoder = new TiffEncoder { Mode = mode, Compression = compression };
            using Image input = new Image<Rgb24>(10, 10);
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
        public void TiffEncoder_PreservesBitsPerPixel<TPixel>(TestImageProvider<TPixel> provider, TiffBitsPerPixel expectedBitsPerPixel)
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
        [WithFile(RgbUncompressed, PixelTypes.Rgba32, TiffCompression.CcittGroup3Fax, TiffCompression.CcittGroup3Fax)]
        [WithFile(RgbUncompressed, PixelTypes.Rgba32, TiffCompression.Ccitt1D, TiffCompression.Ccitt1D)]
        [WithFile(GrayscaleUncompressed, PixelTypes.L8, TiffCompression.CcittGroup3Fax, TiffCompression.CcittGroup3Fax)]
        [WithFile(PaletteDeflateMultistrip, PixelTypes.L8, TiffCompression.Ccitt1D, TiffCompression.Ccitt1D)]
        public void TiffEncoder_EncodesWithCorrectBiColorModeCompression<TPixel>(TestImageProvider<TPixel> provider, TiffCompression compression, TiffCompression expectedCompression)
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
        [InlineData(TiffEncodingMode.ColorPalette, TiffCompression.CcittGroup3Fax)]
        [InlineData(TiffEncodingMode.ColorPalette, TiffCompression.Ccitt1D)]
        [InlineData(TiffEncodingMode.Gray, TiffCompression.Ccitt1D)]
        [InlineData(TiffEncodingMode.Rgb, TiffCompression.Ccitt1D)]
        public void TiffEncoder_IncompatibilityOptions(TiffEncodingMode mode, TiffCompression compression)
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
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffEncodingMode.Rgb, TiffCompression.Deflate);

        [Theory]
        [WithFile(Calliphora_RgbUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeRgb_WithDeflateCompressionAndPredictor_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffEncodingMode.Rgb, TiffCompression.Deflate, TiffPredictor.Horizontal);

        [Theory]
        [WithFile(Calliphora_RgbUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeRgb_WithLzwCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffEncodingMode.Rgb, TiffCompression.Lzw);

        [Theory]
        [WithFile(Calliphora_RgbUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeRgb_WithLzwCompressionAndPredictor_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffEncodingMode.Rgb, TiffCompression.Lzw, TiffPredictor.Horizontal);

        [Theory]
        [WithFile(Calliphora_RgbUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeRgb_WithPackBitsCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffEncodingMode.Rgb, TiffCompression.PackBits);

        [Theory]
        [WithFile(Calliphora_GrayscaleUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeGray_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffEncodingMode.Gray);

        [Theory]
        [WithFile(Calliphora_GrayscaleUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeGray_WithDeflateCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffEncodingMode.Gray, TiffCompression.Deflate);

        [Theory]
        [WithFile(Calliphora_GrayscaleUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeGray_WithDeflateCompressionAndPredictor_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffEncodingMode.Gray, TiffCompression.Deflate, TiffPredictor.Horizontal);

        [Theory]
        [WithFile(Calliphora_GrayscaleUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeGray_WithLzwCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffEncodingMode.Gray, TiffCompression.Lzw);

        [Theory]
        [WithFile(Calliphora_GrayscaleUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeGray_WithLzwCompressionAndPredictor_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffEncodingMode.Gray, TiffCompression.Lzw, TiffPredictor.Horizontal);

        [Theory]
        [WithFile(Calliphora_GrayscaleUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeGray_WithPackBitsCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffEncodingMode.Gray, TiffCompression.PackBits);

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
            TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffEncodingMode.ColorPalette, TiffCompression.PackBits, useExactComparer: false, compareTolerance: 0.001f);

        [Theory]
        [WithFile(Calliphora_PaletteUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeColorPalette_WithDeflateCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffEncodingMode.ColorPalette, TiffCompression.Deflate, useExactComparer: false, compareTolerance: 0.001f);

        [Theory]
        [WithFile(Calliphora_PaletteUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeColorPalette_WithDeflateCompressionAndPredictor_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffEncodingMode.ColorPalette, TiffCompression.Deflate, TiffPredictor.Horizontal, useExactComparer: false, compareTolerance: 0.001f);

        [Theory]
        [WithFile(Calliphora_PaletteUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeColorPalette_WithLzwCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffEncodingMode.ColorPalette, TiffCompression.Lzw, useExactComparer: false, compareTolerance: 0.001f);

        [Theory]
        [WithFile(Calliphora_PaletteUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeColorPalette_WithLzwCompressionAndPredictor_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffEncodingMode.ColorPalette, TiffCompression.Lzw, TiffPredictor.Horizontal, useExactComparer: false, compareTolerance: 0.001f);

        [Theory]
        [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeBiColor_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffEncodingMode.BiColor);

        [Theory]
        [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeBiColor_WithDeflateCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffEncodingMode.BiColor, TiffCompression.Deflate);

        [Theory]
        [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeBiColor_WithPackBitsCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffEncodingMode.BiColor, TiffCompression.PackBits);

        [Theory]
        [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeBiColor_WithCcittGroup3FaxCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffEncodingMode.BiColor, TiffCompression.CcittGroup3Fax);

        [Theory]
        [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
        public void TiffEncoder_EncodeBiColor_WithModifiedHuffmanCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffEncodingMode.BiColor, TiffCompression.Ccitt1D);

        [Theory]
        [WithFile(GrayscaleUncompressed, PixelTypes.L8, TiffEncodingMode.Gray, TiffCompression.PackBits)]
        [WithFile(PaletteDeflateMultistrip, PixelTypes.L8, TiffEncodingMode.ColorPalette, TiffCompression.Lzw)]
        [WithFile(RgbUncompressed, PixelTypes.Rgba32, TiffEncodingMode.Rgb, TiffCompression.Deflate)]
        [WithFile(RgbUncompressed, PixelTypes.Rgb24, TiffEncodingMode.Rgb, TiffCompression.None)]
        [WithFile(RgbUncompressed, PixelTypes.Rgba32, TiffEncodingMode.Rgb, TiffCompression.None)]
        [WithFile(RgbUncompressed, PixelTypes.Rgb48, TiffEncodingMode.Rgb, TiffCompression.None)]
        public void TiffEncoder_StripLength<TPixel>(TestImageProvider<TPixel> provider, TiffEncodingMode mode, TiffCompression compression)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TestStripLength(provider, mode, compression);

        [Theory]
        [WithFile(Calliphora_BiColorUncompressed, PixelTypes.L8, TiffEncodingMode.BiColor, TiffCompression.CcittGroup3Fax)]
        public void TiffEncoder_StripLength_OutOfBounds<TPixel>(TestImageProvider<TPixel> provider, TiffEncodingMode mode, TiffCompression compression)
            where TPixel : unmanaged, IPixel<TPixel> =>
            //// CcittGroup3Fax compressed data length can be larger than the original length
            Assert.Throws<Xunit.Sdk.TrueException>(() => TestStripLength(provider, mode, compression));

        private static void TestStripLength<TPixel>(TestImageProvider<TPixel> provider, TiffEncodingMode mode, TiffCompression compression)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // arrange
            var tiffEncoder = new TiffEncoder() { Mode = mode, Compression = compression };
            using Image<TPixel> input = provider.GetImage();
            using var memStream = new MemoryStream();
            TiffFrameMetadata inputMeta = input.Frames.RootFrame.Metadata.GetTiffMetadata();

            // act
            input.Save(memStream, tiffEncoder);

            // assert
            memStream.Position = 0;
            using var output = Image.Load<Rgba32>(Configuration, memStream);
            TiffFrameMetadata meta = output.Frames.RootFrame.Metadata.GetTiffMetadata();
            ImageFrame<Rgba32> rootFrame = output.Frames.RootFrame;

            Assert.True(output.Height > (int)meta.RowsPerStrip);
            Assert.True(meta.StripOffsets.Length > 1);
            Assert.True(meta.StripByteCounts.Length > 1);

            foreach (Number sz in meta.StripByteCounts)
            {
                Assert.True((uint)sz <= TiffConstants.DefaultStripSize);
            }

            // For uncompressed more accurate test.
            if (compression == TiffCompression.None)
            {
                for (int i = 0; i < meta.StripByteCounts.Length - 1; i++)
                {
                    // The difference must be less than one row.
                    int stripBytes = (int)meta.StripByteCounts[i];
                    int widthBytes = (meta.BitsPerPixel + 7) / 8 * rootFrame.Width;

                    Assert.True((TiffConstants.DefaultStripSize - stripBytes) < widthBytes);
                }
            }

            // Compare with reference.
            TestTiffEncoderCore(
                provider,
                (TiffBitsPerPixel)inputMeta.BitsPerPixel,
                mode,
                inputMeta.Compression);
        }

        private static void TestTiffEncoderCore<TPixel>(
            TestImageProvider<TPixel> provider,
            TiffBitsPerPixel bitsPerPixel,
            TiffEncodingMode mode,
            TiffCompression compression = TiffCompression.None,
            TiffPredictor predictor = TiffPredictor.None,
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
                HorizontalPredictor = predictor
            };

            // Does DebugSave & load reference CompareToReferenceInput():
            image.VerifyEncoder(provider, "tiff", bitsPerPixel, encoder, useExactComparer ? ImageComparer.Exact : ImageComparer.Tolerant(compareTolerance), referenceDecoder: ReferenceDecoder);
        }
    }
}
