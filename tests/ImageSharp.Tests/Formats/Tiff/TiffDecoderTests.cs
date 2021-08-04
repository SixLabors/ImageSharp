// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InconsistentNaming
using System;
using System.IO;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

using Xunit;

using static SixLabors.ImageSharp.Tests.TestImages.Tiff;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff
{
    [Collection("RunSerial")]
    [Trait("Format", "Tiff")]
    public class TiffDecoderTests
    {
        public static readonly string[] MultiframeTestImages = Multiframes;

        public static readonly string[] NotSupportedImages = NotSupported;

        private static TiffDecoder TiffDecoder => new TiffDecoder();

        private static MagickReferenceDecoder ReferenceDecoder => new MagickReferenceDecoder();

        [Theory]
        [WithFileCollection(nameof(NotSupportedImages), PixelTypes.Rgba32)]
        public void ThrowsNotSupported<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => Assert.Throws<NotSupportedException>(() => provider.GetImage(TiffDecoder));

        [Theory]
        [InlineData(RgbUncompressed, 24, 256, 256, 300, 300, PixelResolutionUnit.PixelsPerInch)]
        [InlineData(SmallRgbDeflate, 24, 32, 32, 96, 96, PixelResolutionUnit.PixelsPerInch)]
        [InlineData(Calliphora_GrayscaleUncompressed, 8, 804, 1198, 96, 96, PixelResolutionUnit.PixelsPerInch)]
        [InlineData(Flower4BitPalette, 4, 73, 43, 72, 72, PixelResolutionUnit.PixelsPerInch)]
        public void Identify(string imagePath, int expectedPixelSize, int expectedWidth, int expectedHeight, double expectedHResolution, double expectedVResolution, PixelResolutionUnit expectedResolutionUnit)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                IImageInfo info = Image.Identify(stream);

                Assert.Equal(expectedPixelSize, info.PixelType?.BitsPerPixel);
                Assert.Equal(expectedWidth, info.Width);
                Assert.Equal(expectedHeight, info.Height);
                Assert.NotNull(info.Metadata);
                Assert.Equal(expectedHResolution, info.Metadata.HorizontalResolution);
                Assert.Equal(expectedVResolution, info.Metadata.VerticalResolution);
                Assert.Equal(expectedResolutionUnit, info.Metadata.ResolutionUnits);
            }
        }

        [Theory]
        [InlineData(RgbLzwNoPredictorMultistrip, ImageSharp.ByteOrder.LittleEndian)]
        [InlineData(RgbLzwNoPredictorMultistripMotorola, ImageSharp.ByteOrder.BigEndian)]
        public void ByteOrder(string imagePath, ByteOrder expectedByteOrder)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                IImageInfo info = Image.Identify(stream);

                Assert.NotNull(info.Metadata);
                Assert.Equal(expectedByteOrder, info.Metadata.GetTiffMetadata().ByteOrder);

                stream.Seek(0, SeekOrigin.Begin);

                using var img = Image.Load(stream);
                Assert.Equal(expectedByteOrder, img.Metadata.GetTiffMetadata().ByteOrder);
            }
        }

        [Theory]
        [WithFile(RgbUncompressed, PixelTypes.Rgba32)]
        [WithFile(Calliphora_GrayscaleUncompressed, PixelTypes.Rgba32)]
        [WithFile(Calliphora_RgbUncompressed, PixelTypes.Rgba32)]
        [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
        public void TiffDecoder_CanDecode_Uncompressed<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

        [Theory]
        [WithFile(Calliphora_PaletteUncompressed, PixelTypes.Rgba32)]
        [WithFile(PaletteDeflateMultistrip, PixelTypes.Rgba32)]
        [WithFile(RgbPalette, PixelTypes.Rgba32)]
        [WithFile(RgbPaletteDeflate, PixelTypes.Rgba32)]
        [WithFile(PaletteUncompressed, PixelTypes.Rgba32)]
        public void TiffDecoder_CanDecode_WithPalette<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

        [Theory]
        [WithFile(Rgb4BitPalette, PixelTypes.Rgba32)]
        [WithFile(Flower4BitPalette, PixelTypes.Rgba32)]
        [WithFile(Flower4BitPaletteGray, PixelTypes.Rgba32)]
        public void TiffDecoder_CanDecode_4Bit_WithPalette<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider, ReferenceDecoder, useExactComparer: false, 0.01f);

        [Theory]
        [WithFile(Flower2BitPalette, PixelTypes.Rgba32)]
        public void TiffDecoder_CanDecode_2Bit_WithPalette<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider, ReferenceDecoder, useExactComparer: false, 0.01f);

        [Theory]
        [WithFile(Flower2BitGray, PixelTypes.Rgba32)]
        public void TiffDecoder_CanDecode_2Bit_Gray<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

        [Theory]
        [WithFile(FlowerRgb222Contiguous, PixelTypes.Rgba32)]
        [WithFile(FlowerRgb222Planar, PixelTypes.Rgba32)]
        public void TiffDecoder_CanDecode_6Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

        [Theory]
        [WithFile(Flower6BitGray, PixelTypes.Rgba32)]
        public void TiffDecoder_CanDecode_6Bit_Gray<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

        [Theory]
        [WithFile(Flower8BitGray, PixelTypes.Rgba32)]
        public void TiffDecoder_CanDecode_8Bit_Gray<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

        [Theory]
        [WithFile(Flower10BitGray, PixelTypes.Rgba32)]
        public void TiffDecoder_CanDecode_10Bit_Gray<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

        [Theory]
        [WithFile(FlowerRgb444Contiguous, PixelTypes.Rgba32)]
        [WithFile(FlowerRgb444Planar, PixelTypes.Rgba32)]
        public void TiffDecoder_CanDecode_12Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

        [Theory]
        [WithFile(Flower12BitGray, PixelTypes.Rgba32)]
        public void TiffDecoder_CanDecode_12Bit_Gray<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

        [Theory]
        [WithFile(Flower14BitGray, PixelTypes.Rgba32)]
        public void TiffDecoder_CanDecode_14Bit_Gray<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

        [Theory]
        [WithFile(Flower16BitGrayLittleEndian, PixelTypes.Rgba32)]
        [WithFile(Flower16BitGray, PixelTypes.Rgba32)]
        [WithFile(Flower16BitGrayMinIsWhiteLittleEndian, PixelTypes.Rgba32)]
        [WithFile(Flower16BitGrayMinIsWhiteBigEndian, PixelTypes.Rgba32)]
        public void TiffDecoder_CanDecode_16Bit_Gray<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

        [Theory]
        [WithFile(Flower24BitGray, PixelTypes.Rgba32)]
        public void TiffDecoder_CanDecode_24Bit_Gray<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

        [Theory]
        [WithFile(FlowerRgb101010Contiguous, PixelTypes.Rgba32)]
        [WithFile(FlowerRgb101010Planar, PixelTypes.Rgba32)]
        public void TiffDecoder_CanDecode_30Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

        [Theory]
        [WithFile(Flower32BitGray, PixelTypes.Rgba32)]
        public void TiffDecoder_CanDecode_32Bit_Gray<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

        [Theory]
        [WithFile(FlowerRgb121212Contiguous, PixelTypes.Rgba32)]
        public void TiffDecoder_CanDecode_36Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

        [Theory]
        [WithFile(FlowerRgb141414Contiguous, PixelTypes.Rgba32)]
        [WithFile(FlowerRgb141414Planar, PixelTypes.Rgba32)]
        public void TiffDecoder_CanDecode_42Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

        [Theory]
        [WithFile(FlowerRgb161616Contiguous, PixelTypes.Rgba32)]
        [WithFile(FlowerRgb161616ContiguousLittleEndian, PixelTypes.Rgba32)]
        [WithFile(FlowerRgb161616Planar, PixelTypes.Rgba32)]
        [WithFile(FlowerRgb161616PlanarLittleEndian, PixelTypes.Rgba32)]
        [WithFile(Issues1716Rgb161616BitLittleEndian, PixelTypes.Rgba32)]
        public void TiffDecoder_CanDecode_48Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

        [Theory]
        [WithFile(FlowerRgb242424Contiguous, PixelTypes.Rgba32)]
        [WithFile(FlowerRgb242424ContiguousLittleEndian, PixelTypes.Rgba32)]
        [WithFile(FlowerRgb242424Planar, PixelTypes.Rgba32)]
        [WithFile(FlowerRgb242424PlanarLittleEndian, PixelTypes.Rgba32)]
        public void TiffDecoder_CanDecode_72Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

        [Theory]
        [WithFile(FlowerRgb323232Contiguous, PixelTypes.Rgba32)]
        public void TiffDecoder_CanDecode_96Bit<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

        [Theory]
        [WithFile(GrayscaleDeflateMultistrip, PixelTypes.Rgba32)]
        [WithFile(RgbDeflateMultistrip, PixelTypes.Rgba32)]
        [WithFile(Calliphora_GrayscaleDeflate, PixelTypes.Rgba32)]
        [WithFile(Calliphora_GrayscaleDeflate_Predictor, PixelTypes.Rgba32)]
        [WithFile(Calliphora_RgbDeflate_Predictor, PixelTypes.Rgba32)]
        [WithFile(RgbDeflate, PixelTypes.Rgba32)]
        [WithFile(RgbDeflatePredictor, PixelTypes.Rgba32)]
        [WithFile(SmallRgbDeflate, PixelTypes.Rgba32)]
        public void TiffDecoder_CanDecode_DeflateCompressed<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

        [Theory]
        [WithFile(RgbLzwPredictor, PixelTypes.Rgba32)]
        [WithFile(RgbLzwNoPredictor, PixelTypes.Rgba32)]
        [WithFile(RgbLzwNoPredictorSinglestripMotorola, PixelTypes.Rgba32)]
        [WithFile(RgbLzwNoPredictorMultistripMotorola, PixelTypes.Rgba32)]
        [WithFile(RgbLzwMultistripPredictor, PixelTypes.Rgba32)]
        [WithFile(Calliphora_RgbPaletteLzw_Predictor, PixelTypes.Rgba32)]
        [WithFile(Calliphora_RgbLzwPredictor, PixelTypes.Rgba32)]
        [WithFile(Calliphora_GrayscaleLzw_Predictor, PixelTypes.Rgba32)]
        [WithFile(SmallRgbLzw, PixelTypes.Rgba32)]
        public void TiffDecoder_CanDecode_LzwCompressed<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

        [Theory]
        [WithFile(HuffmanRleAllTermCodes, PixelTypes.Rgba32)]
        [WithFile(HuffmanRleAllMakeupCodes, PixelTypes.Rgba32)]
        [WithFile(HuffmanRle_basi3p02, PixelTypes.Rgba32)]
        [WithFile(Calliphora_HuffmanCompressed, PixelTypes.Rgba32)]
        public void TiffDecoder_CanDecode_HuffmanCompressed<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

        [Theory]
        [WithFile(CcittFax3AllTermCodes, PixelTypes.Rgba32)]
        [WithFile(CcittFax3AllMakeupCodes, PixelTypes.Rgba32)]
        [WithFile(Calliphora_Fax3Compressed, PixelTypes.Rgba32)]
        [WithFile(Calliphora_Fax3Compressed_WithEolPadding, PixelTypes.Rgba32)]
        [WithFile(Fax3Uncompressed, PixelTypes.Rgba32)]
        public void TiffDecoder_CanDecode_Fax3Compressed<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

        [Theory]
        [WithFile(Calliphora_RgbPackbits, PixelTypes.Rgba32)]
        [WithFile(RgbPackbits, PixelTypes.Rgba32)]
        [WithFile(RgbPackbitsMultistrip, PixelTypes.Rgba32)]
        public void TiffDecoder_CanDecode_PackBitsCompressed<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

        [Theory]
        [WithFileCollection(nameof(MultiframeTestImages), PixelTypes.Rgba32)]
        public void DecodeMultiframe<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage(TiffDecoder);
            Assert.True(image.Frames.Count > 1);

            image.DebugSave(provider);
            image.CompareToOriginal(provider, ImageComparer.Exact, ReferenceDecoder);

            image.DebugSaveMultiFrame(provider);
            image.CompareToOriginalMultiFrame(provider, ImageComparer.Exact, ReferenceDecoder);
        }

        private static void TestTiffDecoder<TPixel>(TestImageProvider<TPixel> provider, IImageDecoder referenceDecoder = null, bool useExactComparer = true, float compareTolerance = 0.001f)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage(TiffDecoder);
            image.DebugSave(provider);
            image.CompareToOriginal(
                provider,
                useExactComparer ? ImageComparer.Exact : ImageComparer.Tolerant(compareTolerance),
                referenceDecoder ?? ReferenceDecoder);
        }
    }
}
