// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

// ReSharper disable InconsistentNaming
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using static SixLabors.ImageSharp.Tests.TestImages.Tiff;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff;

// Several of the tests  in this class comparing images with associated alpha encoding use a high tolerance for comparison.
// This is due to an issue in the reference decoder where it is not correctly checking for a zero alpha component value
// before unpremultying the encoded values. This can lead to incorrect values when the rgb channels contain non-zero values.
// The tests should be manually verified following any changes to the decoder.
[Trait("Format", "Tiff")]
[ValidateDisposedMemoryAllocations]
public class TiffDecoderTests : TiffDecoderBaseTester
{
    public static readonly string[] MultiframeTestImages = Multiframes;

    [Theory]
    // [WithFile(MultiframeDifferentVariants, PixelTypes.Rgba32)]
    [WithFile(Cmyk64BitDeflate, PixelTypes.Rgba32)]
    public void ThrowsNotSupported<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => Assert.Throws<NotSupportedException>(() => provider.GetImage(TiffDecoder.Instance));

    [Theory]
    [InlineData(RgbUncompressed, 24, 256, 256, 300, 300, PixelResolutionUnit.PixelsPerInch)]
    [InlineData(SmallRgbDeflate, 24, 32, 32, 96, 96, PixelResolutionUnit.PixelsPerInch)]
    [InlineData(Calliphora_GrayscaleUncompressed, 8, 200, 298, 96, 96, PixelResolutionUnit.PixelsPerInch)]
    [InlineData(Calliphora_GrayscaleUncompressed16Bit, 16, 200, 298, 96, 96, PixelResolutionUnit.PixelsPerInch)]
    [InlineData(Flower4BitPalette, 4, 73, 43, 72, 72, PixelResolutionUnit.PixelsPerInch)]
    public void Identify(string imagePath, int expectedPixelSize, int expectedWidth, int expectedHeight, double expectedHResolution, double expectedVResolution, PixelResolutionUnit expectedResolutionUnit)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo info = Image.Identify(stream);

        Assert.Equal(expectedPixelSize, info.PixelType.BitsPerPixel);
        Assert.Equal(expectedWidth, info.Width);
        Assert.Equal(expectedHeight, info.Height);
        Assert.NotNull(info.Metadata);
        Assert.Equal(expectedHResolution, info.Metadata.HorizontalResolution);
        Assert.Equal(expectedVResolution, info.Metadata.VerticalResolution);
        Assert.Equal(expectedResolutionUnit, info.Metadata.ResolutionUnits);
    }

    [Theory]
    [InlineData(RgbLzwNoPredictorMultistrip, ImageSharp.ByteOrder.LittleEndian)]
    [InlineData(RgbLzwNoPredictorMultistripMotorola, ImageSharp.ByteOrder.BigEndian)]
    public void ByteOrder(string imagePath, ByteOrder expectedByteOrder)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo info = Image.Identify(stream);

        Assert.NotNull(info.Metadata);
        Assert.Equal(expectedByteOrder, info.Metadata.GetTiffMetadata().ByteOrder);

        stream.Seek(0, SeekOrigin.Begin);

        using Image img = Image.Load(stream);
        Assert.Equal(expectedByteOrder, img.Metadata.GetTiffMetadata().ByteOrder);
    }

    [Theory]
    [WithFile(RgbUncompressed, PixelTypes.Rgba32)]
    [WithFile(Calliphora_GrayscaleUncompressed, PixelTypes.Rgba32)]
    [WithFile(Calliphora_GrayscaleUncompressed16Bit, PixelTypes.Rgba32)]
    [WithFile(Calliphora_RgbUncompressed, PixelTypes.Rgba32)]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_Uncompressed<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(FlowerRgb888Planar6Strips, PixelTypes.Rgba32)]
    [WithFile(FlowerRgb888Planar15Strips, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_Planar<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(Tiled, PixelTypes.Rgba32)]
    [WithFile(QuadTile, PixelTypes.Rgba32)]
    [WithFile(TiledChunky, PixelTypes.Rgba32)]
    [WithFile(TiledPlanar, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_Tiled<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(TiledRgbaDeflateCompressedWithPredictor, PixelTypes.Rgba32)]
    [WithFile(TiledRgbDeflateCompressedWithPredictor, PixelTypes.Rgba32)]
    [WithFile(TiledGrayDeflateCompressedWithPredictor, PixelTypes.Rgba32)]
    [WithFile(TiledGray16BitLittleEndianDeflateCompressedWithPredictor, PixelTypes.Rgba32)]
    [WithFile(TiledGray16BitBigEndianDeflateCompressedWithPredictor, PixelTypes.Rgba32)]
    [WithFile(TiledGray32BitLittleEndianDeflateCompressedWithPredictor, PixelTypes.Rgba32)]
    [WithFile(TiledGray32BitBigEndianDeflateCompressedWithPredictor, PixelTypes.Rgba32)]
    [WithFile(TiledRgb48BitLittleEndianDeflateCompressedWithPredictor, PixelTypes.Rgba32)]
    [WithFile(TiledRgb48BitBigEndianDeflateCompressedWithPredictor, PixelTypes.Rgba32)]
    [WithFile(TiledRgba64BitLittleEndianDeflateCompressedWithPredictor, PixelTypes.Rgba32)]
    [WithFile(TiledRgba64BitBigEndianDeflateCompressedWithPredictor, PixelTypes.Rgba32)]
    [WithFile(TiledRgb96BitLittleEndianDeflateCompressedWithPredictor, PixelTypes.Rgba32)]
    [WithFile(TiledRgb96BitBigEndianDeflateCompressedWithPredictor, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_Tiled_Deflate_Compressed<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(TiledRgbaLzwCompressedWithPredictor, PixelTypes.Rgba32)]
    [WithFile(TiledRgbLzwCompressedWithPredictor, PixelTypes.Rgba32)]
    [WithFile(TiledGrayLzwCompressedWithPredictor, PixelTypes.Rgba32)]
    [WithFile(TiledGray16BitLittleEndianLzwCompressedWithPredictor, PixelTypes.Rgba32)]
    [WithFile(TiledGray16BitBigEndianLzwCompressedWithPredictor, PixelTypes.Rgba32)]
    [WithFile(TiledGray32BitLittleEndianLzwCompressedWithPredictor, PixelTypes.Rgba32)]
    [WithFile(TiledGray32BitBigEndianLzwCompressedWithPredictor, PixelTypes.Rgba32)]
    [WithFile(TiledRgb48BitLittleEndianLzwCompressedWithPredictor, PixelTypes.Rgba32)]
    [WithFile(TiledRgb48BitBigEndianLzwCompressedWithPredictor, PixelTypes.Rgba32)]
    [WithFile(TiledRgba64BitLittleEndianLzwCompressedWithPredictor, PixelTypes.Rgba32)]
    [WithFile(TiledRgba64BitBigEndianLzwCompressedWithPredictor, PixelTypes.Rgba32)]
    [WithFile(TiledRgb96BitLittleEndianLzwCompressedWithPredictor, PixelTypes.Rgba32)]
    [WithFile(TiledRgb96BitBigEndianDeflateCompressedWithPredictor, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_Tiled_Lzw_Compressed<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(Rgba8BitPlanarUnassociatedAlpha, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_Planar_32Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(Rgba16BitPlanarUnassociatedAlphaLittleEndian, PixelTypes.Rgba32)]
    [WithFile(Rgba16BitPlanarUnassociatedAlphaBigEndian, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_Planar_64Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(Rgba24BitPlanarUnassociatedAlphaLittleEndian, PixelTypes.Rgba32)]
    [WithFile(Rgba24BitPlanarUnassociatedAlphaBigEndian, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_Planar_96Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Note: because the MagickReferenceDecoder fails to load the image, we only debug save them.
        using Image<TPixel> image = provider.GetImage();
        image.DebugSave(provider);
    }

    [Theory]
    [WithFile(Rgba32BitPlanarUnassociatedAlphaLittleEndian, PixelTypes.Rgba32)]
    [WithFile(Rgba32BitPlanarUnassociatedAlphaBigEndian, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_Planar_128Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Note: because the MagickReferenceDecoder fails to load the image, we only debug save them.
        using Image<TPixel> image = provider.GetImage();
        image.DebugSave(provider);
    }

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
    [WithFile(Rgba2BitUnassociatedAlpha, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_8Bit_WithUnassociatedAlpha<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(FLowerRgb3Bit, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_9Bit_WithUnassociatedAlpha<TPixel>(TestImageProvider<TPixel> provider)
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
    [WithFile(Rgba3BitUnassociatedAlpha, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_12Bit_WithUnassociatedAlpha<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(Rgba3BitAssociatedAlpha, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_12Bit_WithAssociatedAlpha<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider, useExactComparer: false, compareTolerance: 0.264F);

    [Theory]
    [WithFile(Flower14BitGray, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_14Bit_Gray<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(FLowerRgb5Bit, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_15Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(Flower16BitGrayLittleEndian, PixelTypes.Rgba32)]
    [WithFile(Flower16BitGray, PixelTypes.Rgba32)]
    [WithFile(Flower16BitGrayMinIsWhiteLittleEndian, PixelTypes.Rgba32)]
    [WithFile(Flower16BitGrayMinIsWhiteBigEndian, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_16Bit_Gray<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(Flower16BitGrayPredictorBigEndian, PixelTypes.Rgba32)]
    [WithFile(Flower16BitGrayPredictorLittleEndian, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_16Bit_Gray_WithPredictor<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(Rgba4BitUnassociatedAlpha, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_16Bit_WithUnassociatedAlpha<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(FLowerRgb6Bit, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_18Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(Rgba5BitUnassociatedAlpha, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_20Bit_WithUnassociatedAlpha<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(Rgba5BitAssociatedAlpha, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_20Bit_WithAssociatedAlpha<TPixel>(TestImageProvider<TPixel> provider)

        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider, useExactComparer: false, compareTolerance: 0.376F);

    [Theory]
    [WithFile(FlowerRgb888Contiguous, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_24Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(Rgba6BitUnassociatedAlpha, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_24Bit_WithUnassociatedAlpha<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(Rgba6BitAssociatedAlpha, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_24Bit_WithAssociatedAlpha<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider, useExactComparer: false, compareTolerance: 0.405F);

    [Theory]
    [WithFile(Flower24BitGray, PixelTypes.Rgba32)]
    [WithFile(Flower24BitGrayLittleEndian, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_24Bit_Gray<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Note: because the MagickReferenceDecoder fails to load the image, we only debug save them.
        using Image<TPixel> image = provider.GetImage();
        image.DebugSave(provider);
    }

    [Theory]
    [WithFile(FlowerYCbCr888Contiguous, PixelTypes.Rgba32)]
    [WithFile(FlowerYCbCr888Planar, PixelTypes.Rgba32)]
    [WithFile(RgbYCbCr888Contiguoush2v2, PixelTypes.Rgba32)]
    [WithFile(RgbYCbCr888Contiguoush4v4, PixelTypes.Rgba32)]
    [WithFile(FlowerYCbCr888Contiguoush2v1, PixelTypes.Rgba32)]
    [WithFile(FlowerYCbCr888Contiguoush2v2, PixelTypes.Rgba32)]
    [WithFile(FlowerYCbCr888Contiguoush4v4, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_YCbCr_24Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Note: The image from MagickReferenceDecoder does not look right, maybe we are doing something wrong
        // converting the pixel data from Magick.NET to our format with YCbCr?
        using Image<TPixel> image = provider.GetImage();
        image.DebugSave(provider);
        image.CompareToReferenceOutput(ImageComparer.Exact, provider);
    }

    [Theory]
    [WithFile(CieLab, PixelTypes.Rgba32)]
    [WithFile(CieLabPlanar, PixelTypes.Rgba32)]
    [WithFile(CieLabLzwPredictor, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_CieLab<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Note: The image from MagickReferenceDecoder does not look right, maybe we are doing something wrong
        // converting the pixel data from Magick.NET to our format with CieLab?
        using Image<TPixel> image = provider.GetImage();
        image.DebugSave(provider);
        image.CompareToReferenceOutput(ImageComparer.Exact, provider);
    }

    [Theory]
    [WithFile(Cmyk, PixelTypes.Rgba32)]
    [WithFile(CmykLzwPredictor, PixelTypes.Rgba32)]
    [WithFile(CmykJpeg, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_Cmyk<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Note: The image from MagickReferenceDecoder does not look right, maybe we are doing something wrong
        // converting the pixel data from Magick.NET to our format with CMYK?
        using Image<TPixel> image = provider.GetImage(TiffDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToReferenceOutput(ImageComparer.Exact, provider);
    }

    [Theory]
    [WithFile(Icc.PerceptualCmyk, PixelTypes.Rgba32)]
    [WithFile(Icc.PerceptualCieLab, PixelTypes.Rgba32)]
    public void Decode_WhenColorProfileHandlingIsConvert_ApplyIccProfile<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(TiffDecoder.Instance, new DecoderOptions { ColorProfileHandling = ColorProfileHandling.Convert });

        image.DebugSave(provider);
        image.CompareToReferenceOutput(provider);
        Assert.Null(image.Metadata.IccProfile);
    }

    [Theory]
    [WithFile(Issues2454_A, PixelTypes.Rgba32)]
    [WithFile(Issues2454_B, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_YccK<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(TiffDecoder.Instance);
        image.DebugSave(provider);

        // ARM reports a 0.0000% difference, so we use a tolerant comparer here.
        image.CompareToReferenceOutput(ImageComparer.TolerantPercentage(0.0001F), provider);
    }

    [Theory]
    [WithFile(Issues3031, PixelTypes.Rgba64)]
    [WithFile(Rgba16BitAssociatedAlphaBigEndian, PixelTypes.Rgba64)]
    [WithFile(Rgba16BitAssociatedAlphaLittleEndian, PixelTypes.Rgba64)]
    public void TiffDecoder_CanDecode_64Bit_WithAssociatedAlpha<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(TiffDecoder.Instance);
        image.DebugSave(provider);

        image.CompareToReferenceOutput(ImageComparer.Exact, provider);
    }

    [Theory]
    [WithFile(Issues2454_A, PixelTypes.Rgba32)]
    [WithFile(Issues2454_B, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_YccK_ICC<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new()
        {
            ColorProfileHandling = ColorProfileHandling.Convert,
        };

        using Image<TPixel> image = provider.GetImage(TiffDecoder.Instance, options);
        image.DebugSave(provider);

        // Linux reports a 0.0000% difference, so we use a tolerant comparer here.
        image.CompareToReferenceOutput(ImageComparer.TolerantPercentage(0.0001F), provider);
    }

    [Theory]
    [WithFile(FlowerRgb101010Contiguous, PixelTypes.Rgba32)]
    [WithFile(FlowerRgb101010Planar, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_30Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(Flower32BitGray, PixelTypes.Rgba32)]
    [WithFile(Flower32BitGrayLittleEndian, PixelTypes.Rgba32)]
    [WithFile(Flower32BitGrayMinIsWhite, PixelTypes.Rgba32)]
    [WithFile(Flower32BitGrayMinIsWhiteLittleEndian, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_32Bit_Gray<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Note: because the MagickReferenceDecoder fails to load the image, we only debug save them.
        using Image<TPixel> image = provider.GetImage();
        image.DebugSave(provider);
    }

    [Theory]
    [WithFile(Rgba8BitUnassociatedAlpha, PixelTypes.Rgba32)]
    [WithFile(Rgba8BitUnassociatedAlphaWithPredictor, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_32Bit_WithUnassociatedAlpha<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Note: because the MagickReferenceDecoder fails to load the image, we only debug save them.
        using Image<TPixel> image = provider.GetImage();
        image.DebugSave(provider);
    }

    [Theory]
    [WithFile(Rgba8BitAssociatedAlpha, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_32Bit_WithAssociatedAlpha<TPixel>(TestImageProvider<TPixel> provider)
        // Note: Using tolerant comparer here, because there is a small difference to the reference decoder probably due to floating point rounding issues.
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider, useExactComparer: false, compareTolerance: 0.004F);

    [Theory]
    [WithFile(Flower32BitGrayPredictorBigEndian, PixelTypes.Rgba32)]
    [WithFile(Flower32BitGrayPredictorLittleEndian, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_32Bit_Gray_WithPredictor<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Note: because the MagickReferenceDecoder fails to load the image, we only debug save them.
        using Image<TPixel> image = provider.GetImage();
        image.DebugSave(provider);
    }

    [Theory]
    [WithFile(FlowerRgb121212Contiguous, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_36Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(Rgba10BitUnassociatedAlphaBigEndian, PixelTypes.Rgba32)]
    [WithFile(Rgba10BitUnassociatedAlphaLittleEndian, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_40Bit_WithUnassociatedAlpha<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(Rgba10BitAssociatedAlphaBigEndian, PixelTypes.Rgba32)]
    [WithFile(Rgba10BitAssociatedAlphaLittleEndian, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_40Bit_WithAssociatedAlpha<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider, useExactComparer: false, compareTolerance: 0.247F);

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
    [WithFile(Rgba12BitUnassociatedAlphaBigEndian, PixelTypes.Rgba32)]
    [WithFile(Rgba12BitUnassociatedAlphaLittleEndian, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_48Bit_WithUnassociatedAlpha<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(Rgba12BitAssociatedAlphaBigEndian, PixelTypes.Rgba32)]
    [WithFile(Rgba12BitAssociatedAlphaLittleEndian, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_48Bit_WithAssociatedAlpha<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider, useExactComparer: false, compareTolerance: 0.118F);

    [Theory]
    [WithFile(FlowerRgb161616PredictorBigEndian, PixelTypes.Rgba32)]
    [WithFile(FlowerRgb161616PredictorLittleEndian, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_48Bit_WithPredictor<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(Rgba14BitUnassociatedAlphaBigEndian, PixelTypes.Rgba32)]
    [WithFile(Rgba14BitUnassociatedAlphaLittleEndian, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_56Bit_WithUnassociatedAlpha<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(Rgba14BitAssociatedAlphaBigEndian, PixelTypes.Rgba32)]
    [WithFile(Rgba14BitAssociatedAlphaLittleEndian, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_56Bit_WithAssociatedAlpha<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider, useExactComparer: false, compareTolerance: 0.075F);

    [Theory]
    [WithFile(FlowerRgb242424Contiguous, PixelTypes.Rgba32)]
    [WithFile(FlowerRgb242424ContiguousLittleEndian, PixelTypes.Rgba32)]
    [WithFile(FlowerRgb242424Planar, PixelTypes.Rgba32)]
    [WithFile(FlowerRgb242424PlanarLittleEndian, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_72Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Note: because the MagickReferenceDecoder fails to load the image, we only debug save them.
        using Image<TPixel> image = provider.GetImage();
        image.DebugSave(provider);
    }

    [Theory]
    [WithFile(FlowerRgb323232Contiguous, PixelTypes.Rgba32)]
    [WithFile(FlowerRgb323232ContiguousLittleEndian, PixelTypes.Rgba32)]
    [WithFile(FlowerRgb323232Planar, PixelTypes.Rgba32)]
    [WithFile(FlowerRgb323232PlanarLittleEndian, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_96Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Note: because the MagickReferenceDecoder fails to load the image, we only debug save them.
        using Image<TPixel> image = provider.GetImage();
        image.DebugSave(provider);
    }

    [Theory]
    [WithFile(Rgba24BitUnassociatedAlphaBigEndian, PixelTypes.Rgba32)]
    [WithFile(Rgba24BitUnassociatedAlphaLittleEndian, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_96Bit_WithUnassociatedAlpha<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Note: because the MagickReferenceDecoder fails to load the image, we only debug save them.
        using Image<TPixel> image = provider.GetImage();
        image.DebugSave(provider);
    }

    [Theory]
    [WithFile(FlowerRgbFloat323232, PixelTypes.Rgba32)]
    [WithFile(FlowerRgbFloat323232LittleEndian, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_Float_96Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Note: because the MagickReferenceDecoder fails to load the image, we only debug save them.
        using Image<TPixel> image = provider.GetImage();
        image.DebugSave(provider);
    }

    [Theory]
    [WithFile(FlowerRgb323232PredictorBigEndian, PixelTypes.Rgba32)]
    [WithFile(FlowerRgb323232PredictorLittleEndian, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_96Bit_WithPredictor<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Note: because the MagickReferenceDecoder fails to load the image, we only debug save them.
        using Image<TPixel> image = provider.GetImage();
        image.DebugSave(provider);
    }

    [Theory]
    [WithFile(Flower32BitFloatGray, PixelTypes.Rgba32)]
    [WithFile(Flower32BitFloatGrayLittleEndian, PixelTypes.Rgba32)]
    [WithFile(Flower32BitFloatGrayMinIsWhite, PixelTypes.Rgba32)]
    [WithFile(Flower32BitFloatGrayMinIsWhiteLittleEndian, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_Float_96Bit_Gray<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Note: because the MagickReferenceDecoder fails to load the image, we only debug save them.
        using Image<TPixel> image = provider.GetImage();
        image.DebugSave(provider);
    }

    [Theory]
    [WithFile(Rgba16BitUnassociatedAlphaBigEndian, PixelTypes.Rgba32)]
    [WithFile(Rgba16BitUnassociatedAlphaLittleEndian, PixelTypes.Rgba32)]
    [WithFile(Rgba16BitUnassociatedAlphaBigEndianWithPredictor, PixelTypes.Rgba32)]
    [WithFile(Rgba16BitUnassociatedAlphaLittleEndianWithPredictor, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_128Bit_UnassociatedAlpha<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(Rgba32BitUnassociatedAlphaBigEndian, PixelTypes.Rgba32)]
    [WithFile(Rgba32BitUnassociatedAlphaLittleEndian, PixelTypes.Rgba32)]
    [WithFile(Rgba32BitUnassociatedAlphaBigEndianWithPredictor, PixelTypes.Rgba32)]
    [WithFile(Rgba32BitUnassociatedAlphaLittleEndianWithPredictor, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_128Bit_WithUnassociatedAlpha<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Note: because the MagickReferenceDecoder fails to load the image, we only debug save them.
        using Image<TPixel> image = provider.GetImage();
        image.DebugSave(provider);
    }

    [Theory]
    [WithFile(GrayscaleDeflateMultistrip, PixelTypes.Rgba32)]
    [WithFile(RgbDeflateMultistrip, PixelTypes.Rgba32)]
    [WithFile(Calliphora_GrayscaleDeflate, PixelTypes.Rgba32)]
    [WithFile(Calliphora_GrayscaleDeflate_Predictor, PixelTypes.Rgba32)]
    [WithFile(Calliphora_GrayscaleDeflate16Bit, PixelTypes.Rgba32)]
    [WithFile(Calliphora_GrayscaleDeflate_Predictor16Bit, PixelTypes.Rgba32)]
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
    [WithFile(Calliphora_GrayscaleLzw_Predictor16Bit, PixelTypes.Rgba32)]
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
    [WithFile(Fax4Compressed, PixelTypes.Rgba32)]
    [WithFile(Fax4Compressed2, PixelTypes.Rgba32)]
    [WithFile(Fax4CompressedMinIsBlack, PixelTypes.Rgba32)]
    [WithFile(Fax4CompressedLowerOrderBitsFirst, PixelTypes.Rgba32)]
    [WithFile(Calliphora_Fax4Compressed, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_Fax4Compressed<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(CcittFax3LowerOrderBitsFirst, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_Compressed_LowerOrderBitsFirst<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(Calliphora_RgbPackbits, PixelTypes.Rgba32)]
    [WithFile(RgbPackbits, PixelTypes.Rgba32)]
    [WithFile(RgbPackbitsMultistrip, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_PackBitsCompressed<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(MultiFrameMipMap, PixelTypes.Rgba32)]
    public void CanDecodeJustOneFrame<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new() { MaxFrames = 1 };
        using Image<TPixel> image = provider.GetImage(TiffDecoder.Instance, options);
        Assert.Equal(1, image.Frames.Count);
    }

    [Theory]
    [WithFile(MultiFrameMipMap, PixelTypes.Rgba32)]
    public void CanDecode_MultiFrameMipMap<TPixel>(TestImageProvider<TPixel> provider)
    where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(TiffDecoder.Instance);
        Assert.Equal(7, image.Frames.Count);
        image.DebugSaveMultiFrame(provider);
    }

    [Theory]
    [WithFile(RgbJpegCompressed, PixelTypes.Rgba32)]
    [WithFile(RgbJpegCompressed2, PixelTypes.Rgba32)]
    [WithFile(RgbWithStripsJpegCompressed, PixelTypes.Rgba32)]
    [WithFile(YCbCrJpegCompressed, PixelTypes.Rgba32)]
    [WithFile(YCbCrJpegCompressed2, PixelTypes.Rgba32)]
    [WithFile(RgbJpegCompressedNoJpegTable, PixelTypes.Rgba32)]
    [WithFile(GrayscaleJpegCompressed, PixelTypes.Rgba32)]
    [WithFile(Issues2123, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_JpegCompressed<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider, useExactComparer: false);

    [Theory]
    [WithFile(RgbOldJpegCompressed, PixelTypes.Rgba32)]
    [WithFile(RgbOldJpegCompressed2, PixelTypes.Rgba32)]
    [WithFile(RgbOldJpegCompressed3, PixelTypes.Rgba32)]
    [WithFile(RgbOldJpegCompressedGray, PixelTypes.Rgba32)]
    [WithFile(YCbCrOldJpegCompressed, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_OldJpegCompressed<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions decoderOptions = new()
        {
            MaxFrames = 1
        };
        using Image<TPixel> image = provider.GetImage(TiffDecoder.Instance, decoderOptions);
        image.DebugSave(provider);
        image.CompareToOriginal(
            provider,
            ImageComparer.Tolerant(0.001f),
            ReferenceDecoder,
            decoderOptions);
    }

    [Theory]
    [WithFile(WebpCompressed, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_WebpCompressed<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (TestEnvironment.IsWindows)
        {
            TestTiffDecoder(provider, useExactComparer: false);
        }
    }

    // https://github.com/SixLabors/ImageSharp/issues/1891
    [Theory]
    [WithFile(Issues1891, PixelTypes.Rgba32)]
    public void TiffDecoder_ThrowsException_WithTooManyDirectories<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => Assert.Throws<ImageFormatException>(
            () =>
            {
                using (provider.GetImage(TiffDecoder.Instance))
                {
                }
            });

    // https://github.com/SixLabors/ImageSharp/issues/2149
    [Theory]
    [WithFile(Issues2149, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_Fax4CompressedWithStrips<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    // https://github.com/SixLabors/ImageSharp/issues/2435
    [Theory]
    [WithFile(Issues2435, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_TiledWithNonEqualWidthAndHeight<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    // https://github.com/SixLabors/ImageSharp/issues/2587
    [Theory]
    [WithFile(Issues2587, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_BiColorWithMissingBitsPerSample<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    // https://github.com/SixLabors/ImageSharp/issues/2679
    [Theory]
    [WithFile(Issues2679, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_JpegCompressedWithIssue2679<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(TiffDecoder.Instance);

        // The image is handcrafted to simulate issue 2679. ImageMagick will throw an expection here and wont decode,
        // so we compare to rererence output instead.
        image.DebugSave(provider);
        image.CompareToReferenceOutput(
            ImageComparer.Exact,
            provider,
            appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithFile(JpegCompressedGray0000539558, PixelTypes.Rgba32)]
    public void TiffDecoder_ThrowsException_WithCircular_IFD_Offsets<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
        => Assert.Throws<ImageFormatException>(
            () =>
            {
                using (provider.GetImage(TiffDecoder.Instance))
                {
                }
            });

    [Theory]
    [WithFile(Tiled0000023664, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_TiledWithBadZlib<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(TiffDecoder.Instance);

        // ImageMagick cannot decode this image.
        image.DebugSave(provider);
        image.CompareToReferenceOutput(
            ImageComparer.TolerantPercentage(0.0034F), // NET 10 Uses zlib-ng to decompress, which manages to decode 3 extra pixels.
            provider,
            appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithFileCollection(nameof(MultiframeTestImages), PixelTypes.Rgba32)]
    public void DecodeMultiframe<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(TiffDecoder.Instance);
        Assert.True(image.Frames.Count > 1);

        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact, ReferenceDecoder);

        image.DebugSaveMultiFrame(provider);
        image.CompareToOriginalMultiFrame(provider, ImageComparer.Exact, ReferenceDecoder);
    }

    [Theory]
    [WithFile(Rgba3BitUnassociatedAlpha, PixelTypes.Rgba32)]
    public void TiffDecoder_Decode_Resize<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new()
        {
            TargetSize = new Size { Width = 150, Height = 150 }
        };

        using Image<TPixel> image = provider.GetImage(TiffDecoder.Instance, options);

        FormattableString details = $"{options.TargetSize.Value.Width}_{options.TargetSize.Value.Height}";

        image.DebugSave(provider, testOutputDetails: details, appendPixelTypeToFileName: false);

        // Floating point differences in FMA used in the ResizeKernel result in minor pixel differences.
        // Output have been manually verified.
        // For more details see discussion: https://github.com/SixLabors/ImageSharp/pull/1513#issuecomment-763643594
        image.CompareToReferenceOutput(
            Fma.IsSupported ? ImageComparer.Exact : ImageComparer.TolerantPercentage(0.0006F),
            provider,
            testOutputDetails: details,
            appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithFile(ExtraSamplesUnspecified, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_ExtraSamplesUnspecified<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);

    [Theory]
    [WithFile(Issue2983, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_Issue2983<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider);
}
