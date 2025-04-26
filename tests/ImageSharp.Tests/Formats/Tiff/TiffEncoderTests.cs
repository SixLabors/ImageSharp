// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.PixelFormats;
using static SixLabors.ImageSharp.Tests.TestImages.Tiff;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff;

[Trait("Format", "Tiff")]
public class TiffEncoderTests : TiffEncoderBaseTester
{
    [Fact]
    public void TiffEncoderDefaultInstanceHasQuantizer() => Assert.NotNull(new TiffEncoder().Quantizer);

    [Theory]
    [InlineData(null, TiffBitsPerPixel.Bit24)]
    [InlineData(TiffPhotometricInterpretation.Rgb, TiffBitsPerPixel.Bit24)]
    [InlineData(TiffPhotometricInterpretation.PaletteColor, TiffBitsPerPixel.Bit8)]
    [InlineData(TiffPhotometricInterpretation.BlackIsZero, TiffBitsPerPixel.Bit8)]
    [InlineData(TiffPhotometricInterpretation.WhiteIsZero, TiffBitsPerPixel.Bit8)]
    [InlineData(TiffPhotometricInterpretation.BlackIsZero, TiffBitsPerPixel.Bit16)]
    //// Unsupported TiffPhotometricInterpretation should default to 24 bits
    [InlineData(TiffPhotometricInterpretation.CieLab, TiffBitsPerPixel.Bit24)]
    [InlineData(TiffPhotometricInterpretation.ColorFilterArray, TiffBitsPerPixel.Bit24)]
    [InlineData(TiffPhotometricInterpretation.ItuLab, TiffBitsPerPixel.Bit24)]
    [InlineData(TiffPhotometricInterpretation.LinearRaw, TiffBitsPerPixel.Bit24)]
    [InlineData(TiffPhotometricInterpretation.Separated, TiffBitsPerPixel.Bit24)]
    [InlineData(TiffPhotometricInterpretation.TransparencyMask, TiffBitsPerPixel.Bit24)]
    public void EncoderOptions_SetPhotometricInterpretation_Works(TiffPhotometricInterpretation? photometricInterpretation, TiffBitsPerPixel expectedBitsPerPixel)
    {
        // arrange
        TiffEncoder tiffEncoder = new() { PhotometricInterpretation = photometricInterpretation };
        using Image input = expectedBitsPerPixel is TiffBitsPerPixel.Bit16
            ? new Image<L16>(10, 10)
            : new Image<Rgb24>(10, 10);
        using MemoryStream memStream = new();

        // act
        input.Save(memStream, tiffEncoder);

        // assert
        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        TiffFrameMetadata frameMetaData = output.Frames.RootFrame.Metadata.GetTiffMetadata();
        Assert.Equal(expectedBitsPerPixel, frameMetaData.BitsPerPixel);
        Assert.Equal(TiffCompression.None, frameMetaData.Compression);
    }

    [Theory]
    [InlineData(TiffBitsPerPixel.Bit24)]
    [InlineData(TiffBitsPerPixel.Bit16)]
    [InlineData(TiffBitsPerPixel.Bit8)]
    [InlineData(TiffBitsPerPixel.Bit4)]
    [InlineData(TiffBitsPerPixel.Bit1)]
    public void EncoderOptions_SetBitPerPixel_Works(TiffBitsPerPixel bitsPerPixel)
    {
        // arrange
        TiffEncoder tiffEncoder = new()
        { BitsPerPixel = bitsPerPixel };
        using Image input = new Image<Rgb24>(10, 10);
        using MemoryStream memStream = new();

        // act
        input.Save(memStream, tiffEncoder);

        // assert
        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);

        TiffFrameMetadata frameMetaData = output.Frames.RootFrame.Metadata.GetTiffMetadata();
        Assert.Equal(bitsPerPixel, frameMetaData.BitsPerPixel);
        Assert.Equal(TiffCompression.None, frameMetaData.Compression);
    }

    [Theory]
    [InlineData(TiffBitsPerPixel.Bit48)]
    [InlineData(TiffBitsPerPixel.Bit42)]
    [InlineData(TiffBitsPerPixel.Bit36)]
    [InlineData(TiffBitsPerPixel.Bit30)]
    [InlineData(TiffBitsPerPixel.Bit12)]
    [InlineData(TiffBitsPerPixel.Bit10)]
    [InlineData(TiffBitsPerPixel.Bit6)]
    public void EncoderOptions_UnsupportedBitPerPixel_DefaultTo24Bits(TiffBitsPerPixel bitsPerPixel)
    {
        // arrange
        TiffEncoder tiffEncoder = new()
        { BitsPerPixel = bitsPerPixel };
        using Image input = new Image<Rgb24>(10, 10);
        using MemoryStream memStream = new();

        // act
        input.Save(memStream, tiffEncoder);

        // assert
        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);

        TiffFrameMetadata frameMetaData = output.Frames.RootFrame.Metadata.GetTiffMetadata();
        Assert.Equal(TiffBitsPerPixel.Bit24, frameMetaData.BitsPerPixel);
    }

    [Theory]
    [InlineData(TiffPhotometricInterpretation.Rgb, TiffCompression.Ccitt1D)]
    [InlineData(TiffPhotometricInterpretation.Rgb, TiffCompression.CcittGroup3Fax)]
    [InlineData(TiffPhotometricInterpretation.Rgb, TiffCompression.CcittGroup4Fax)]
    public void EncoderOptions_WithInvalidCompressionAndPixelTypeCombination_DefaultsToRgb(TiffPhotometricInterpretation photometricInterpretation, TiffCompression compression)
    {
        // arrange
        TiffEncoder tiffEncoder = new()
        { PhotometricInterpretation = photometricInterpretation, Compression = compression };
        using Image input = new Image<Rgb24>(10, 10);
        using MemoryStream memStream = new();

        // act
        input.Save(memStream, tiffEncoder);

        // assert
        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);

        TiffFrameMetadata frameMetaData = output.Frames.RootFrame.Metadata.GetTiffMetadata();
        Assert.Equal(TiffBitsPerPixel.Bit24, frameMetaData.BitsPerPixel);
    }

    [Theory]
    [InlineData(null, TiffCompression.Deflate, TiffBitsPerPixel.Bit24, TiffCompression.Deflate)]
    [InlineData(TiffPhotometricInterpretation.Rgb, TiffCompression.Deflate, TiffBitsPerPixel.Bit24, TiffCompression.Deflate)]
    [InlineData(TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Deflate, TiffBitsPerPixel.Bit16, TiffCompression.Deflate)]
    [InlineData(TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Deflate, TiffBitsPerPixel.Bit8, TiffCompression.Deflate)]
    [InlineData(TiffPhotometricInterpretation.PaletteColor, TiffCompression.Deflate, TiffBitsPerPixel.Bit8, TiffCompression.Deflate)]
    [InlineData(null, TiffCompression.PackBits, TiffBitsPerPixel.Bit24, TiffCompression.PackBits)]
    [InlineData(TiffPhotometricInterpretation.Rgb, TiffCompression.PackBits, TiffBitsPerPixel.Bit24, TiffCompression.PackBits)]
    [InlineData(TiffPhotometricInterpretation.BlackIsZero, TiffCompression.PackBits, TiffBitsPerPixel.Bit16, TiffCompression.PackBits)]
    [InlineData(TiffPhotometricInterpretation.BlackIsZero, TiffCompression.PackBits, TiffBitsPerPixel.Bit8, TiffCompression.PackBits)]
    [InlineData(TiffPhotometricInterpretation.PaletteColor, TiffCompression.PackBits, TiffBitsPerPixel.Bit8, TiffCompression.PackBits)]
    [InlineData(null, TiffCompression.Lzw, TiffBitsPerPixel.Bit24, TiffCompression.Lzw)]
    [InlineData(TiffPhotometricInterpretation.Rgb, TiffCompression.Lzw, TiffBitsPerPixel.Bit24, TiffCompression.Lzw)]
    [InlineData(TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Lzw, TiffBitsPerPixel.Bit16, TiffCompression.Lzw)]
    [InlineData(TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Lzw, TiffBitsPerPixel.Bit8, TiffCompression.Lzw)]
    [InlineData(TiffPhotometricInterpretation.PaletteColor, TiffCompression.Lzw, TiffBitsPerPixel.Bit8, TiffCompression.Lzw)]
    [InlineData(TiffPhotometricInterpretation.BlackIsZero, TiffCompression.CcittGroup3Fax, TiffBitsPerPixel.Bit1, TiffCompression.CcittGroup3Fax)]
    [InlineData(TiffPhotometricInterpretation.BlackIsZero, TiffCompression.CcittGroup4Fax, TiffBitsPerPixel.Bit1, TiffCompression.CcittGroup4Fax)]
    [InlineData(TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Ccitt1D, TiffBitsPerPixel.Bit1, TiffCompression.Ccitt1D)]
    [InlineData(TiffPhotometricInterpretation.Rgb, TiffCompression.ItuTRecT43, TiffBitsPerPixel.Bit24, TiffCompression.None)]
    [InlineData(TiffPhotometricInterpretation.Rgb, TiffCompression.ItuTRecT82, TiffBitsPerPixel.Bit24, TiffCompression.None)]
    [InlineData(TiffPhotometricInterpretation.Rgb, TiffCompression.Jpeg, TiffBitsPerPixel.Bit24, TiffCompression.Jpeg)]
    [InlineData(TiffPhotometricInterpretation.Rgb, TiffCompression.OldDeflate, TiffBitsPerPixel.Bit24, TiffCompression.None)]
    [InlineData(TiffPhotometricInterpretation.Rgb, TiffCompression.OldJpeg, TiffBitsPerPixel.Bit24, TiffCompression.None)]
    public void EncoderOptions_SetPhotometricInterpretationAndCompression_Works(
        TiffPhotometricInterpretation? photometricInterpretation,
        TiffCompression compression,
        TiffBitsPerPixel expectedBitsPerPixel,
        TiffCompression expectedCompression)
    {
        // arrange
        TiffEncoder tiffEncoder = new()
        { PhotometricInterpretation = photometricInterpretation, Compression = compression };
        using Image input = expectedBitsPerPixel is TiffBitsPerPixel.Bit16
            ? new Image<L16>(10, 10)
            : new Image<Rgb24>(10, 10);
        using MemoryStream memStream = new();

        // act
        input.Save(memStream, tiffEncoder);

        // assert
        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        TiffFrameMetadata rootFrameMetaData = output.Frames.RootFrame.Metadata.GetTiffMetadata();
        Assert.Equal(expectedBitsPerPixel, rootFrameMetaData.BitsPerPixel);
        Assert.Equal(expectedCompression, rootFrameMetaData.Compression);
    }

    [Theory]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32, TiffBitsPerPixel.Bit1)]
    [WithFile(GrayscaleUncompressed, PixelTypes.Rgba32, TiffBitsPerPixel.Bit8)]
    [WithFile(GrayscaleUncompressed16Bit, PixelTypes.L16, TiffBitsPerPixel.Bit16)]
    [WithFile(RgbUncompressed, PixelTypes.Rgba32, TiffBitsPerPixel.Bit24)]
    [WithFile(Rgb4BitPalette, PixelTypes.Rgba32, TiffBitsPerPixel.Bit4)]
    [WithFile(RgbPalette, PixelTypes.Rgba32, TiffBitsPerPixel.Bit8)]
    [WithFile(Calliphora_PaletteUncompressed, PixelTypes.Rgba32, TiffBitsPerPixel.Bit8)]
    public void TiffEncoder_PreservesBitsPerPixel<TPixel>(TestImageProvider<TPixel> provider, TiffBitsPerPixel expectedBitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // arrange
        TiffEncoder tiffEncoder = new();
        using Image<TPixel> input = provider.GetImage();
        using MemoryStream memStream = new();

        // act
        input.Save(memStream, tiffEncoder);

        // assert
        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        TiffFrameMetadata frameMetaData = output.Frames.RootFrame.Metadata.GetTiffMetadata();
        Assert.Equal(expectedBitsPerPixel, frameMetaData.BitsPerPixel);
    }

    [Fact]
    public void TiffEncoder_PreservesBitsPerPixel_WhenInputIsL8()
    {
        // arrange
        TiffEncoder tiffEncoder = new();
        using Image input = new Image<L8>(10, 10);
        using MemoryStream memStream = new();
        const TiffBitsPerPixel expectedBitsPerPixel = TiffBitsPerPixel.Bit8;

        // act
        input.Save(memStream, tiffEncoder);

        // assert
        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        TiffFrameMetadata frameMetaData = output.Frames.RootFrame.Metadata.GetTiffMetadata();
        Assert.Equal(expectedBitsPerPixel, frameMetaData.BitsPerPixel);
    }

    [Theory]
    [WithFile(RgbUncompressed, PixelTypes.Rgba32, TiffCompression.None)]
    [WithFile(RgbLzwNoPredictor, PixelTypes.Rgba32, TiffCompression.Lzw)]
    [WithFile(RgbDeflate, PixelTypes.Rgba32, TiffCompression.Deflate)]
    [WithFile(RgbPackbits, PixelTypes.Rgba32, TiffCompression.PackBits)]
    public void TiffEncoder_PreservesCompression<TPixel>(TestImageProvider<TPixel> provider, TiffCompression expectedCompression)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // arrange
        TiffEncoder tiffEncoder = new();
        using Image<TPixel> input = provider.GetImage();
        using MemoryStream memStream = new();

        // act
        input.Save(memStream, tiffEncoder);

        // assert
        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        Assert.Equal(expectedCompression, output.Frames.RootFrame.Metadata.GetTiffMetadata().Compression);
    }

    [Theory]
    [WithFile(RgbLzwNoPredictor, PixelTypes.Rgba32, null)]
    [WithFile(RgbLzwPredictor, PixelTypes.Rgba32, TiffPredictor.Horizontal)]
    [WithFile(RgbDeflate, PixelTypes.Rgba32, null)]
    [WithFile(RgbDeflatePredictor, PixelTypes.Rgba32, TiffPredictor.Horizontal)]
    public void TiffEncoder_PreservesPredictor<TPixel>(TestImageProvider<TPixel> provider, TiffPredictor? expectedPredictor)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // arrange
        TiffEncoder tiffEncoder = new();
        using Image<TPixel> input = provider.GetImage();
        using MemoryStream memStream = new();

        // act
        input.Save(memStream, tiffEncoder);

        // assert
        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        TiffFrameMetadata frameMetadata = output.Frames.RootFrame.Metadata.GetTiffMetadata();
        Assert.Equal(expectedPredictor, frameMetadata.Predictor);
    }

    // https://github.com/SixLabors/ImageSharp/issues/2297
    [Fact]
    public void TiffEncoder_WritesIfdOffsetAtWordBoundary()
    {
        // arrange
        TiffEncoder tiffEncoder = new();
        using MemoryStream memStream = new();
        using Image<Rgba32> image = new(1, 1);
        byte[] expectedIfdOffsetBytes = { 12, 0 };

        // act
        image.Save(memStream, tiffEncoder);

        // assert
        byte[] imageBytes = memStream.ToArray();
        Assert.Equal(imageBytes[4], expectedIfdOffsetBytes[0]);
        Assert.Equal(imageBytes[5], expectedIfdOffsetBytes[1]);
    }

    [Theory]
    [WithFile(RgbUncompressed, PixelTypes.Rgba32, TiffCompression.CcittGroup3Fax, TiffCompression.CcittGroup3Fax)]
    [WithFile(RgbUncompressed, PixelTypes.Rgba32, TiffCompression.CcittGroup4Fax, TiffCompression.CcittGroup4Fax)]
    [WithFile(RgbUncompressed, PixelTypes.Rgba32, TiffCompression.Ccitt1D, TiffCompression.Ccitt1D)]
    [WithFile(GrayscaleUncompressed, PixelTypes.L8, TiffCompression.CcittGroup3Fax, TiffCompression.CcittGroup3Fax)]
    [WithFile(GrayscaleUncompressed, PixelTypes.L8, TiffCompression.CcittGroup4Fax, TiffCompression.CcittGroup4Fax)]
    [WithFile(PaletteDeflateMultistrip, PixelTypes.L8, TiffCompression.Ccitt1D, TiffCompression.Ccitt1D)]
    public void TiffEncoder_EncodesWithCorrectBiColorModeCompression<TPixel>(TestImageProvider<TPixel> provider, TiffCompression compression, TiffCompression expectedCompression)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // arrange
        TiffEncoder encoder = new() { Compression = compression, BitsPerPixel = TiffBitsPerPixel.Bit1 };
        using Image<TPixel> input = provider.GetImage();
        using MemoryStream memStream = new();

        // act
        input.Save(memStream, encoder);

        // assert
        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        TiffFrameMetadata frameMetaData = output.Frames.RootFrame.Metadata.GetTiffMetadata();
        Assert.Equal(TiffBitsPerPixel.Bit1, frameMetaData.BitsPerPixel);
        Assert.Equal(expectedCompression, frameMetaData.Compression);
    }

    // This makes sure, that when decoding a planar tiff, the planar configuration is not carried over to the encoded image.
    [Theory]
    [WithFile(FlowerRgb444Planar, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodePlanar_AndReload_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb, imageDecoder: TiffDecoder.Instance);

    [Theory]
    [WithFile(Calliphora_RgbUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeRgb_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb);

    [Theory]
    [WithFile(Calliphora_RgbUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeRgb_WithDeflateCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb, TiffCompression.Deflate);

    [Theory]
    [WithFile(Calliphora_RgbUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeRgb_WithDeflateCompressionAndPredictor_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb, TiffCompression.Deflate, TiffPredictor.Horizontal);

    [Theory]
    [WithFile(Calliphora_RgbUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeRgb_WithLzwCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb, TiffCompression.Lzw);

    [Theory]
    [WithFile(Calliphora_RgbUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeRgb_WithLzwCompressionAndPredictor_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb, TiffCompression.Lzw, TiffPredictor.Horizontal);

    [Theory]
    [WithFile(Calliphora_RgbUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeRgb_WithPackBitsCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb, TiffCompression.PackBits);

    [Theory]
    [WithFile(Calliphora_RgbUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeRgb_WithJpegCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb, TiffCompression.Jpeg, useExactComparer: false, compareTolerance: 0.012f);

    [Theory]
    [WithFile(Calliphora_GrayscaleUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeGray_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffPhotometricInterpretation.BlackIsZero);

    [Theory]
    [WithFile(Calliphora_GrayscaleUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeGray_WithDeflateCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Deflate);

    [Theory]
    [WithFile(Calliphora_GrayscaleUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeGray_WithDeflateCompressionAndPredictor_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Deflate, TiffPredictor.Horizontal);

    [Theory]
    [WithFile(Calliphora_GrayscaleUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeGray_WithLzwCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Lzw);

    [Theory]
    [WithFile(Calliphora_GrayscaleUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeGray_WithLzwCompressionAndPredictor_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Lzw, TiffPredictor.Horizontal);

    [Theory]
    [WithFile(Calliphora_GrayscaleUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeGray_WithPackBitsCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.PackBits);

    [Theory]
    [WithFile(Calliphora_PaletteUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeColorPalette_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> =>
        TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffPhotometricInterpretation.PaletteColor, useExactComparer: false, compareTolerance: 0.001f);

    [Theory]
    [WithFile(Rgb4BitPalette, PixelTypes.Rgba32)]
    [WithFile(Flower4BitPalette, PixelTypes.Rgba32)]
    [WithFile(Flower4BitPaletteGray, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeColorPalette_With4Bit_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> =>
        TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit4, TiffPhotometricInterpretation.PaletteColor, useExactComparer: false, compareTolerance: 0.003f);

    [Theory]
    [WithFile(Calliphora_PaletteUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeColorPalette_WithPackBitsCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> =>
        TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffPhotometricInterpretation.PaletteColor, TiffCompression.PackBits, useExactComparer: false, compareTolerance: 0.001f);

    [Theory]
    [WithFile(Calliphora_PaletteUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeColorPalette_WithDeflateCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> =>
        TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffPhotometricInterpretation.PaletteColor, TiffCompression.Deflate, useExactComparer: false, compareTolerance: 0.001f);

    [Theory]
    [WithFile(Calliphora_PaletteUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeColorPalette_WithDeflateCompressionAndPredictor_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> =>
        TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffPhotometricInterpretation.PaletteColor, TiffCompression.Deflate, TiffPredictor.Horizontal, useExactComparer: false, compareTolerance: 0.001f);

    [Theory]
    [WithFile(Calliphora_PaletteUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeColorPalette_WithLzwCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> =>
        TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffPhotometricInterpretation.PaletteColor, TiffCompression.Lzw, useExactComparer: false, compareTolerance: 0.001f);

    [Theory]
    [WithFile(Calliphora_PaletteUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeColorPalette_WithLzwCompressionAndPredictor_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> =>
        TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit8, TiffPhotometricInterpretation.PaletteColor, TiffCompression.Lzw, TiffPredictor.Horizontal, useExactComparer: false, compareTolerance: 0.001f);

    [Theory]
    [WithFile(Calliphora_GrayscaleUncompressed16Bit, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeGray16_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit16, TiffPhotometricInterpretation.BlackIsZero);

    [Theory]
    [WithFile(Calliphora_GrayscaleUncompressed16Bit, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeGray16_WithDeflateCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit16, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Deflate);

    [Theory]
    [WithFile(Calliphora_GrayscaleUncompressed16Bit, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeGray16_WithDeflateCompressionAndPredictor_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit16, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Deflate, TiffPredictor.Horizontal);

    [Theory]
    [WithFile(Calliphora_GrayscaleUncompressed16Bit, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeGray16_WithLzwCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit16, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Lzw);

    [Theory]
    [WithFile(Calliphora_GrayscaleUncompressed16Bit, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeGray16_WithLzwCompressionAndPredictor_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit16, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Lzw, TiffPredictor.Horizontal);

    [Theory]
    [WithFile(Calliphora_GrayscaleUncompressed16Bit, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeGray16_WithPackBitsCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit16, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.PackBits);

    [Theory]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeBiColor_BlackIsZero_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffPhotometricInterpretation.BlackIsZero);

    [Theory]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeBiColor_WhiteIsZero_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffPhotometricInterpretation.WhiteIsZero);

    [Theory]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeBiColor_WithDeflateCompression_BlackIsZero_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Deflate);

    [Theory]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeBiColor_WithDeflateCompression_WhiteIsZero_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffPhotometricInterpretation.WhiteIsZero, TiffCompression.Deflate);

    [Theory]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeBiColor_WithPackBitsCompression_BlackIsZero_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.PackBits);

    [Theory]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeBiColor_WithPackBitsCompression_WhiteIsZero_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffPhotometricInterpretation.WhiteIsZero, TiffCompression.PackBits);

    [Theory]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeBiColor_WithCcittGroup3FaxCompression_WhiteIsZero_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffPhotometricInterpretation.WhiteIsZero, TiffCompression.CcittGroup3Fax);

    [Theory]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeBiColor_WithCcittGroup3FaxCompression_BlackIsZero_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.CcittGroup3Fax);

    [Theory]
    [WithFile(Issues2255, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeBiColor_WithCcittGroup3FaxCompression_WithoutSpecifyingBitPerPixel_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, null, null, TiffCompression.CcittGroup3Fax, useExactComparer: false, compareTolerance: 0.025f);

    [Theory]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeBiColor_WithCcittGroup4FaxCompression_WhiteIsZero_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffPhotometricInterpretation.WhiteIsZero, TiffCompression.CcittGroup4Fax);

    [Theory]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeBiColor_WithCcittGroup4FaxCompression_BlackIsZero_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.CcittGroup4Fax);

    [Theory]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeBiColor_WithModifiedHuffmanCompression_WhiteIsZero_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffPhotometricInterpretation.WhiteIsZero, TiffCompression.Ccitt1D);

    [Theory]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeBiColor_WithModifiedHuffmanCompression_BlackIsZero_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit1, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.Ccitt1D);

    [Theory]
    [WithFile(Issue2909, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeBiColor_WithLzwCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, null, TiffCompression.Lzw, imageDecoder: TiffDecoder.Instance);

    [Theory]
    [WithFile(Issue2909, PixelTypes.Rgba32)]
    public void TiffEncoder_EncodeBiColor_WithDeflateCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffEncoderCore(provider, TiffBitsPerPixel.Bit24, null, TiffCompression.Deflate, imageDecoder: TiffDecoder.Instance);

    [Theory]
    [WithFile(GrayscaleUncompressed, PixelTypes.L8, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.PackBits)]
    [WithFile(GrayscaleUncompressed16Bit, PixelTypes.L16, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.PackBits)]
    [WithFile(RgbUncompressed, PixelTypes.Rgba32, TiffPhotometricInterpretation.Rgb, TiffCompression.Deflate)]
    [WithFile(RgbUncompressed, PixelTypes.Rgb24, TiffPhotometricInterpretation.Rgb, TiffCompression.None)]
    [WithFile(RgbUncompressed, PixelTypes.Rgba32, TiffPhotometricInterpretation.Rgb, TiffCompression.None)]
    [WithFile(RgbUncompressed, PixelTypes.Rgb48, TiffPhotometricInterpretation.Rgb, TiffCompression.None)]
    public void TiffEncoder_StripLength<TPixel>(TestImageProvider<TPixel> provider, TiffPhotometricInterpretation photometricInterpretation, TiffCompression compression)
        where TPixel : unmanaged, IPixel<TPixel> =>
        TestStripLength(provider, photometricInterpretation, compression);

    [Theory]
    [WithFile(PaletteDeflateMultistrip, PixelTypes.L8, TiffPhotometricInterpretation.PaletteColor, TiffCompression.Lzw)]
    public void TiffEncoder_StripLength_WithPalette<TPixel>(TestImageProvider<TPixel> provider, TiffPhotometricInterpretation photometricInterpretation, TiffCompression compression)
        where TPixel : unmanaged, IPixel<TPixel> =>
        TestStripLength(provider, photometricInterpretation, compression, false, 0.01f);

    [Theory]
    [WithFile(Calliphora_BiColorUncompressed, PixelTypes.L8, TiffPhotometricInterpretation.BlackIsZero, TiffCompression.CcittGroup3Fax)]
    public void TiffEncoder_StripLength_OutOfBounds<TPixel>(TestImageProvider<TPixel> provider, TiffPhotometricInterpretation photometricInterpretation, TiffCompression compression)
        where TPixel : unmanaged, IPixel<TPixel> =>
        //// CcittGroup3Fax compressed data length can be larger than the original length.
        Assert.Throws<Xunit.Sdk.TrueException>(() => TestStripLength(provider, photometricInterpretation, compression));

    [Theory]
    [WithTestPatternImages(287, 321, PixelTypes.Rgba32, TiffPhotometricInterpretation.Rgb)]
    [WithTestPatternImages(287, 321, PixelTypes.Rgba32, TiffPhotometricInterpretation.PaletteColor)]
    [WithTestPatternImages(287, 321, PixelTypes.Rgba32, TiffPhotometricInterpretation.BlackIsZero)]
    public void TiffEncode_WorksWithDiscontiguousBuffers<TPixel>(TestImageProvider<TPixel> provider, TiffPhotometricInterpretation photometricInterpretation)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        provider.LimitAllocatorBufferCapacity().InPixelsSqrt(200);
        using Image<TPixel> image = provider.GetImage();

        TiffEncoder encoder = new()
        { PhotometricInterpretation = photometricInterpretation };
        image.DebugSave(provider, encoder);
    }
}
