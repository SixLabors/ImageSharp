// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

// ReSharper disable InconsistentNaming
using System.Numerics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;
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

    /// <summary>
    /// Decoding preserves finite HDR samples within the half-precision range and bounds nonfinite samples.
    /// </summary>
    /// <remarks>
    /// The positive-infinity input comes from
    /// <see href="https://github.com/SixLabors/ImageSharp/security/advisories/GHSA-j3p4-wp97-rph4">CVE-2026-106113</see>.
    /// The complete decode-and-equalize reproduction remains in HistogramEqualizationTests.GlobalHistogramEqualization_Cve2026106113.
    /// </remarks>
    /// <param name="hex">The encoded floating-point TIFF.</param>
    /// <param name="intensity">The intensity stored in the encoded TIFF.</param>
    [Theory]
    [InlineData(
        "49492A00080000000A0000010400010000000800000001010400010000000100000002010300010000002000000003010300010000000100" +
                "0000060103000100000001000000110104000100000086000000150103000100000001000000160104000100000001000000170104000100" +
                "000020000000530103000100000003000000000000000000807F0000807F0000807F0000807F0000807F0000807F0000807F0000807F",
        float.PositiveInfinity)]
    [InlineData(
        "49492A00080000000A0000010400010000000800000001010400010000000100000002010300010000002000000003010300010000000100" +
                "0000060103000100000001000000110104000100000086000000150103000100000001000000160104000100000001000000170104000100" +
                "000020000000530103000100000003000000000000000000C07F0000C07F0000C07F0000C07F0000C07F0000C07F0000C07F0000C07F",
        float.NaN)]
    [InlineData(
        "49492A00080000000A0000010400010000000800000001010400010000000100000002010300010000002000000003010300010000000100" +
                "0000060103000100000001000000110104000100000086000000150103000100000001000000160104000100000001000000170104000100" +
                "000020000000530103000100000003000000000000000000004000000040000000400000004000000040000000400000004000000040",
        2F)]
    [InlineData(
        "49492A00080000000A0000010400010000000800000001010400010000000100000002010300010000002000000003010300010000000100" +
                "0000060103000100000001000000110104000100000086000000150103000100000001000000160104000100000001000000170104000100" +
                "000020000000530103000100000003000000000000000000003F0000003F0000003F0000003F0000003F0000003F0000003F0000003F",
        .5F)]
    public void Decode_FloatingPointSamples_UsesFiniteHalfVector4ColorRange(string hex, float intensity)
    {
        byte[] data = Convert.FromHexString(hex);
        using Image<HalfVector4> image = Image.Load<HalfVector4>(data);
        Assert.Equal(new Size(8, 1), image.Size);

        // Decoding uses the color conversion API: NaN maps to the finite minimum, and positive infinity maps to the finite maximum.
        // Finite HDR samples such as 2 remain unchanged. These bounds come from Half storage, not the SDR interval [0,1].
        float expectedIntensity = float.IsNaN(intensity) ? (float)Half.MinValue : Math.Clamp(intensity, (float)Half.MinValue, (float)Half.MaxValue);
        Vector4 expected = new(expectedIntensity, expectedIntensity, expectedIntensity, 1F);

        for (int x = 0; x < image.Width; x++)
        {
            // Check stored components too: output conversion must not conceal a nonfinite value left by the decoder.
            Assert.Equal(expected, image[x, 0].ToVector4());
            Assert.Equal(expected, image[x, 0].ToScaledVector4());
        }
    }

    [Theory]
    [WithFile(MultiframeDifferentVariants, PixelTypes.Rgba32)]
    public void ThrowsNotSupported<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => Assert.Throws<NotSupportedException>(() => provider.GetImage(TiffDecoder.Instance));

    /// <summary>
    /// Checks the existing sixteen-bit CMYK sample against an independent decoder.
    /// </summary>
    /// <param name="provider">The real sixteen-bit CMYK image.</param>
    [Theory]
    [WithFile(Cmyk64BitDeflate, PixelTypes.Rgba32)]
    public void Decode_Cmyk16_MatchesReference(TestImageProvider<Rgba32> provider)
        => TestTiffDecoder(provider, useExactComparer: false);

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

    /// <summary>
    /// Palette TIFFs with LZW compression and horizontal prediction preserve colors for both palette ranges.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="provider">The TIFF image provider.</param>
    [Theory]
    [WithFile(Issue3182ColorMap16Bit, PixelTypes.Rgba64)]
    [WithFile(Issue3182ColorMap8Bit, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_PaletteWithLzwAndPredictor<TPixel>(TestImageProvider<TPixel> provider)
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

    /// <summary>
    /// Compares existing grayscale fixtures with Magick across SIMD and scalar feature modes.
    /// </summary>
    /// <typeparam name="TPixel">The decoded pixel type.</typeparam>
    /// <param name="provider">The source image provider.</param>
    [Theory]
    [WithFile(Flower2BitGray, PixelTypes.Rgba32)]
    [WithFile(Flower6BitGray, PixelTypes.Rgba32)]
    [WithFile(Flower8BitGray, PixelTypes.Rgba32)]
    [WithFile(Flower10BitGray, PixelTypes.Rgba32)]
    [WithFile(Flower12BitGray, PixelTypes.Rgba32)]
    [WithFile(Flower14BitGray, PixelTypes.Rgba32)]
    [WithFile(Flower16BitGray, PixelTypes.Rgba32 | PixelTypes.Rgba64)]
    [WithFile(Flower16BitGrayLittleEndian, PixelTypes.Rgba32 | PixelTypes.Rgba64)]
    [WithFile(Flower16BitGrayMinIsWhiteBigEndian, PixelTypes.Rgba32 | PixelTypes.Rgba64)]
    [WithFile(Flower16BitGrayMinIsWhiteLittleEndian, PixelTypes.Rgba32 | PixelTypes.Rgba64)]
    public void TiffDecoder_GrayscaleReference_FeatureModes<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            PixelTests<TPixel>.AssertExistingReference,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic,
            provider);

    /// <summary>
    /// Compares existing RGB and RGBA planar fixtures with Magick across SIMD modes.
    /// </summary>
    /// <typeparam name="TPixel">The decoded pixel type.</typeparam>
    /// <param name="provider">The source image provider.</param>
    [Theory]
    [WithFile(FlowerRgb888Planar6Strips, PixelTypes.Rgba32)]
    [WithFile(FlowerRgb888Planar15Strips, PixelTypes.Rgba32)]
    [WithFile(Rgba8BitPlanarUnassociatedAlpha, PixelTypes.Rgba32)]
    [WithFile(Rgba16BitPlanarUnassociatedAlphaLittleEndian, PixelTypes.Rgba32)]
    [WithFile(Rgba16BitPlanarUnassociatedAlphaBigEndian, PixelTypes.Rgba32)]
    public void TiffDecoder_PlanarReference_FeatureModes<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            PixelTests<TPixel>.AssertExistingReference,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic,
            provider);

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
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            AssertYcbcrReference,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic,
            provider);

    /// <summary>
    /// Compares the full YCbCr image with its checked-in reference in the selected hardware mode.
    /// </summary>
    /// <param name="providerDump">The serialized image provider.</param>
    private static void AssertYcbcrReference(string providerDump)
    {
        TestImageProvider<Rgba32> provider = FeatureTestRunner.DeserializeForXunit<TestImageProvider<Rgba32>>(providerDump);
        using Image<Rgba32> image = provider.GetImage(TiffDecoder.Instance);

        // Magick's YCbCr interpretation differs, so this family uses its checked-in exact reference.
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
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            AssertCmykReference,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic,
            provider);

    /// <summary>
    /// Compares the full CMYK image with its checked-in reference in the selected hardware mode.
    /// </summary>
    /// <param name="providerDump">The serialized image provider.</param>
    private static void AssertCmykReference(string providerDump)
    {
        TestImageProvider<Rgba32> provider = FeatureTestRunner.DeserializeForXunit<TestImageProvider<Rgba32>>(providerDump);
        using Image<Rgba32> image = provider.GetImage(TiffDecoder.Instance);

        // Magick's CMYK interpretation differs, so this family uses its checked-in exact reference.
        image.CompareToReferenceOutput(ImageComparer.Exact, provider);
    }

    [Theory]
    [WithFile(Icc.PerceptualCmyk, PixelTypes.Rgba32)]
    [WithFile(Icc.PerceptualCieLab, PixelTypes.Rgba32)]
    [WithFile(Icc.PerceptualRgb8, PixelTypes.Rgba32)]
    [WithFile(Icc.PerceptualRgb16, PixelTypes.Rgba32)]
    public void Decode_WhenColorProfileHandlingIsConvert_ApplyIccProfile<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(TiffDecoder.Instance, new DecoderOptions { ColorProfileHandling = ColorProfileHandling.Convert });

        image.DebugSave(provider);

        // ICC power approximation can move final rounding. Limit the total RGB error to three eight-bit codes per pixel.
        image.CompareToReferenceOutput(ImageComparer.Tolerant(0F, 3 * 257), provider);
        Assert.Null(image.Metadata.IccProfile);
    }

    /// <summary>
    /// Verifies conversion of the reporter's CMYK profile using its relative-colorimetric intent.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="provider">The image provider.</param>
    [Theory]
    [WithFile(Issue3198, PixelTypes.Rgba32)]
    public void Decode_CmykIcc_Issue3198<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new() { ColorProfileHandling = ColorProfileHandling.Convert };
        using Image<TPixel> image = provider.GetImage(TiffDecoder.Instance, options);
        image.DebugSave(provider);

        // LittleCMS 2.19 generated the reference from the embedded profile to CompactSrgbV4Profile,
        // using relative colorimetric intent without black-point compensation.

        // After final rounding, limit the total RGB error to three eight-bit codes per pixel.
        image.CompareToReferenceOutput(ImageComparer.Tolerant(0F, 3 * 257), provider);
        Assert.Null(image.Metadata.IccProfile);
        Assert.Null(image.Frames.RootFrame.Metadata.IccProfile);
    }

    [Theory]
    [WithFile(Icc.PerceptualCmyk, PixelTypes.Rgba32)]
    [WithFile(Icc.PerceptualCieLab, PixelTypes.Rgba32)]
    [WithFile(Icc.PerceptualRgb8, PixelTypes.Rgba32)]
    [WithFile(Icc.PerceptualRgb16, PixelTypes.Rgba32)]
    [WithFile(Issue3198, PixelTypes.Rgba32)]
    [WithFile(Issues2454_A, PixelTypes.Rgba32)]
    [WithFile(Issues2454_B, PixelTypes.Rgba32)]
    [WithFile(Icc.PerceptualRgb8Webp, PixelTypes.Rgba32 | PixelTypes.Rgba32P | PixelTypes.Rgba64 | PixelTypes.RgbaVector | PixelTypes.RgbaVectorP)]
    public void Decode_WhenColorProfileHandlingIsPreserve_PreservesIccProfile<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions preserveOptions = new() { ColorProfileHandling = ColorProfileHandling.Preserve, SkipMetadata = true };
        using Image<TPixel> preserved = provider.GetImage(TiffDecoder.Instance, preserveOptions);

        DecoderOptions convertOptions = new() { ColorProfileHandling = ColorProfileHandling.Convert };
        using Image<TPixel> converted = provider.GetImage(TiffDecoder.Instance, convertOptions);

        ImageInfo sourceInfo = Image.Identify(TestFile.Create(provider.SourceFileOrDescription).Bytes);
        TiffFrameMetadata sourceFrameMetadata = sourceInfo.FrameMetadataCollection[0].GetTiffMetadata();
        TiffFrameMetadata preservedFrameMetadata = preserved.Frames.RootFrame.Metadata.GetTiffMetadata();
        TiffFrameMetadata convertedFrameMetadata = converted.Frames.RootFrame.Metadata.GetTiffMetadata();

        Assert.NotNull(preserved.Metadata.IccProfile);
        Assert.NotSame(preserved.Frames.RootFrame.Metadata.IccProfile, preserved.Metadata.IccProfile);
        Assert.Equal(
            preserved.Frames.RootFrame.Metadata.IccProfile.ToByteArray(),
            preserved.Metadata.IccProfile.ToByteArray());

        Assert.Equal(sourceInfo.Metadata.IccProfile.ToByteArray(), preserved.Metadata.IccProfile.ToByteArray());
        Assert.Equal(sourceInfo.FrameMetadataCollection[0].IccProfile.ToByteArray(), preserved.Frames.RootFrame.Metadata.IccProfile.ToByteArray());

        // ICC conversion changes pixels, while TIFF metadata retains the source file's encoded sample layout.
        Assert.Equal(sourceFrameMetadata.PhotometricInterpretation, preservedFrameMetadata.PhotometricInterpretation);
        Assert.Equal(sourceFrameMetadata.BitsPerSample, preservedFrameMetadata.BitsPerSample);
        Assert.Equal(sourceFrameMetadata.BitsPerPixel, preservedFrameMetadata.BitsPerPixel);
        Assert.Equal(sourceFrameMetadata.PhotometricInterpretation, convertedFrameMetadata.PhotometricInterpretation);
        Assert.Equal(sourceFrameMetadata.BitsPerSample, convertedFrameMetadata.BitsPerSample);
        Assert.Equal(sourceFrameMetadata.BitsPerPixel, convertedFrameMetadata.BitsPerPixel);

        ImageComparer.Exact.VerifySimilarity(converted, preserved);

        Assert.Null(converted.Metadata.IccProfile);
        Assert.Null(converted.Frames.RootFrame.Metadata.IccProfile);
    }

    [Theory]
    [WithFile(Issues2454_A, PixelTypes.Rgba32)]
    [WithFile(Issues2454_B, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_YccK<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(TiffDecoder.Instance);
        image.DebugSave(provider);

        // Preserve keeps the converted pixels, so both modes use the existing ICC reference.
        provider.Utility.TestName = nameof(this.TiffDecoder_CanDecode_YccK_ICC);

        // Approximate ICC curves can move final rounding. Limit the total RGB error to three eight-bit codes per pixel.
        image.CompareToReferenceOutput(ImageComparer.Tolerant(0F, 3 * 257), provider);
    }

    [Theory]
    [WithFile(Issues3031, PixelTypes.Rgba64)]
    [WithFile(Rgba16BitAssociatedAlphaBigEndian, PixelTypes.Rgba64)]
    [WithFile(Rgba16BitAssociatedAlphaLittleEndian, PixelTypes.Rgba64)]
    public void TiffDecoder_CanDecode_64Bit_WithAssociatedAlpha<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(TiffDecoder.Instance);
        PngEncoder encoder = new()
        {
            BitDepth = PngBitDepth.Bit16,
            ColorType = PngColorType.RgbWithAlpha
        };

        // This exact Rgba64 comparison requires a 16-bit reference. The Windows reference encoder uses a 32-bit
        // System.Drawing bitmap and would otherwise quantize each channel to 8 bits while producing the PNG.
        image.DebugSave(provider, testOutputDetails: null, extension: "png", encoder: encoder);

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

        // Approximate ICC curves can move final rounding. Limit the total RGB error to three eight-bit codes per pixel.
        image.CompareToReferenceOutput(ImageComparer.Tolerant(0F, 3 * 257), provider);
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
    [WithFile(RgbWithStripsJpegCompressed, PixelTypes.Rgba32 | PixelTypes.Rgba32P | PixelTypes.Rgba64 | PixelTypes.RgbaVector | PixelTypes.RgbaVectorP)]
    [WithFile(YCbCrJpegCompressed, PixelTypes.Rgba32)]
    [WithFile(YCbCrJpegCompressed2, PixelTypes.Rgba32)]
    [WithFile(RgbJpegCompressedNoJpegTable, PixelTypes.Rgba32 | PixelTypes.Rgba32P | PixelTypes.Rgba64 | PixelTypes.RgbaVector | PixelTypes.RgbaVectorP)]
    [WithFile(GrayscaleJpegCompressed, PixelTypes.Rgba32 | PixelTypes.Rgba32P | PixelTypes.Rgba64 | PixelTypes.RgbaVector | PixelTypes.RgbaVectorP)]
    [WithFile(Issues2123, PixelTypes.Rgba32, 0.110f)]
    public void TiffDecoder_CanDecode_JpegCompressed<TPixel>(TestImageProvider<TPixel> provider, float tolerance = 0.001f)
        where TPixel : unmanaged, IPixel<TPixel> => TestTiffDecoder(provider, useExactComparer: false, compareTolerance: tolerance);

    [Theory]
    [WithFile(RgbOldJpegCompressed, PixelTypes.Rgba32 | PixelTypes.Rgba32P | PixelTypes.Rgba64 | PixelTypes.RgbaVector | PixelTypes.RgbaVectorP)]
    [WithFile(RgbOldJpegCompressed2, PixelTypes.Rgba32, 0.1003f)]
    [WithFile(RgbOldJpegCompressed3, PixelTypes.Rgba32)]
    [WithFile(RgbOldJpegCompressedGray, PixelTypes.Rgba32 | PixelTypes.Rgba32P | PixelTypes.Rgba64 | PixelTypes.RgbaVector | PixelTypes.RgbaVectorP)]
    [WithFile(YCbCrOldJpegCompressed, PixelTypes.Rgba32)]
    public void TiffDecoder_CanDecode_OldJpegCompressed<TPixel>(TestImageProvider<TPixel> provider, float tolerance = 0.001f)
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
            ImageComparer.Tolerant(tolerance),
            ReferenceDecoder,
            decoderOptions);
    }

    /// <summary>
    /// Checks nested WebP output against an independent decoder across integer, floating and associated pixel formats.
    /// </summary>
    /// <typeparam name="TPixel">The requested output pixel format.</typeparam>
    /// <param name="provider">The real TIFF fixture.</param>
    [Theory]
    [WithFile(WebpCompressed, PixelTypes.Rgba32 | PixelTypes.Rgba32P | PixelTypes.Rgba64 | PixelTypes.RgbaVector | PixelTypes.RgbaVectorP)]
    [WithFile(WebpCompressedRgb, PixelTypes.Rgba32 | PixelTypes.Rgba32P | PixelTypes.Rgba64 | PixelTypes.RgbaVector | PixelTypes.RgbaVectorP)]
    [WithFile(WebpCompressedRgbTiled, PixelTypes.Rgba32 | PixelTypes.Rgba32P | PixelTypes.Rgba64 | PixelTypes.RgbaVector | PixelTypes.RgbaVectorP)]
    [WithFile(WebpCompressedRgbaAlphaOmitted, PixelTypes.Rgba32 | PixelTypes.Rgba32P | PixelTypes.Rgba64 | PixelTypes.RgbaVector | PixelTypes.RgbaVectorP)]
    [WithFile(WebpCompressedLosslessRgbaOpaque, PixelTypes.Rgba32 | PixelTypes.Rgba32P | PixelTypes.Rgba64 | PixelTypes.RgbaVector | PixelTypes.RgbaVectorP)]
    public void TiffDecoder_CanDecode_WebpCompressed<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // The fixtures cover multiple strips, cropped edge tiles and RGBA headers whose WebP data omits an opaque alpha plane.
        // Comparing the requested TPixel output catches RGB-only staging and incorrect copies of nested pixel bytes.
        TestTiffDecoder(provider, useExactComparer: false);
    }

    /// <summary>
    /// Checks that TIFF passes its profile to WebP and applies the source transform once.
    /// </summary>
    /// <typeparam name="TPixel">The requested output pixel format.</typeparam>
    /// <param name="provider">The profiled TIFF fixture.</param>
    [Theory]
    [WithFile(Icc.PerceptualRgb8Webp, PixelTypes.Rgba32 | PixelTypes.Rgba32P | PixelTypes.Rgba64 | PixelTypes.RgbaVector | PixelTypes.RgbaVectorP)]
    public void Decode_WebpIcc_ConvertsOnce<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new() { ColorProfileHandling = ColorProfileHandling.Convert };
        using Image<TPixel> image = provider.GetImage(TiffDecoder.Instance, options);

        // libtiff recompressed Perceptual_RGB8 losslessly, retaining its source RGB samples and IFD profile.
        // Compare with the uncompressed source in TPixel to check the nested decoder's full output, including out-of-range float RGB.
        using Image<TPixel> uncompressed = Image.Load<TPixel>(options, TestFile.Create(Icc.PerceptualRgb8).Bytes);

        // The same RGB samples and profile must agree per pixel. Allow one sixteen-bit code per RGB channel for float rounding.
        ImageComparer.Tolerant(0F, 3).VerifySimilarity(uncompressed, image);

        image.DebugSave(provider);

        // The independent CMM can round eight-bit source colors differently. No pixel may exceed three byte codes in total.
        image.CompareToOriginal(provider, ImageComparer.Tolerant(0F, 3 * (ushort.MaxValue / byte.MaxValue)), ReferenceDecoder, options);

        Assert.Null(image.Metadata.IccProfile);
        Assert.Null(image.Frames.RootFrame.Metadata.IccProfile);
    }

    /// <summary>
    /// Rejects planar WebP before the decoder selects a raw channel-buffer path.
    /// </summary>
    /// <param name="path">An existing strip or tile WebP TIFF.</param>
    [Theory]
    [InlineData(WebpCompressedRgb)]
    [InlineData(WebpCompressedRgbTiled)]
    public void Parse_WebpPlanarLayout_Throws(string path)
    {
        using FileStream stream = File.OpenRead(TestFile.Create(path).FullPath);
        DirectoryReader reader = new(stream, Configuration.Default.MemoryAllocator);
        ExifProfile tags = reader.Read()[0];

        // Change only the layout tag from a real fixture to test rejection of the layout that libtiff's WebP codec excludes.
        tags.SetValue(ExifTag.PlanarConfiguration, (ushort)TiffPlanarConfiguration.Planar);
        TiffFrameMetadata metadata = TiffFrameMetadata.Parse(tags);
        TiffDecoderCore decoder = new(new DecoderOptions());

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => decoder.VerifyAndParse(tags, metadata));
        Assert.Equal("WebP compression is not supported with planar configuration", exception.Message);
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

        // The image is handcrafted to simulate issue 2679. ImageMagick throws an exception and cannot decode it,
        // so compare against reference output instead.
        image.DebugSave(provider, appendPixelTypeToFileName: false);

        // JPEG has already expanded the subsampled chroma before TIFF receives the RGB pixels.
        // Comparing the whole image catches TIFF incorrectly applying the subsampling a second time.
        image.CompareToReferenceOutput(ImageComparer.Exact, provider, appendPixelTypeToFileName: false);
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

    /// <summary>
    /// Checks that identification ignores an invalid ICC header length under the default ancillary-error policy.
    /// </summary>
    [Fact]
    public void Identify_MalformedIccProfile_IgnoresNonCriticalErrorsByDefault()
    {
        ImageInfo info = Image.Identify(TestFile.Create(TestImages.Tiff.Icc.InvalidProfileSize).Bytes);

        Assert.Equal(new Size(100, 100), info.Size);
        Assert.Null(info.Metadata.IccProfile);
        Assert.Null(info.FrameMetadataCollection[0].IccProfile);
    }

    /// <summary>
    /// Checks that decoding ignores the invalid profile while retaining the image under the default ancillary-error policy.
    /// </summary>
    [Fact]
    public void Decode_MalformedIccProfile_IgnoresNonCriticalErrorsByDefault()
    {
        using Image<Rgba32> image = Image.Load<Rgba32>(TestFile.Create(TestImages.Tiff.Icc.InvalidProfileSize).Bytes);

        Assert.Equal(new Size(100, 100), image.Size);
        Assert.Null(image.Metadata.IccProfile);
        Assert.Null(image.Frames.RootFrame.Metadata.IccProfile);
    }

    /// <summary>
    /// Checks strict ICC validation during identification.
    /// </summary>
    [Fact]
    public void Identify_MalformedIccProfile_ThrowsWithStrict()
    {
        DecoderOptions options = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        byte[] data = TestFile.Create(TestImages.Tiff.Icc.InvalidProfileSize).Bytes;

        Assert.Throws<InvalidIccProfileException>(() => Image.Identify(options, data));
    }

    /// <summary>
    /// Checks strict ICC validation during decoding.
    /// </summary>
    [Fact]
    public void Decode_MalformedIccProfile_ThrowsWithStrict()
    {
        DecoderOptions options = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        byte[] data = TestFile.Create(TestImages.Tiff.Icc.InvalidProfileSize).Bytes;

        Assert.Throws<InvalidIccProfileException>(() =>
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(options, data);
        });
    }

    /// <summary>
    /// Provides a non-generic RemoteExecutor callback whose declaring type retains the requested pixel format.
    /// </summary>
    /// <typeparam name="TPixel">The decoded pixel type.</typeparam>
    private static class PixelTests<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// Compares the decoded fixture with the independent reference at the requested pixel precision.
        /// </summary>
        /// <param name="providerDump">The serialized source image provider.</param>
        public static void AssertExistingReference(string providerDump)
        {
            TestImageProvider<TPixel> provider = FeatureTestRunner.DeserializeForXunit<TestImageProvider<TPixel>>(providerDump);
            TestTiffDecoder(provider);
        }
    }
}
