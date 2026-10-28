// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
using SixLabors.ImageSharp.Formats.Tiff.Compression;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Readers;

internal static class TiffColorDecoderFactory<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    public static TiffBaseColorDecoder<TPixel> Create(
        ImageFrameMetadata metadata,
        Configuration configuration,
        MemoryAllocator memoryAllocator,
        TiffColorType colorType,
        TiffBitsPerSample bitsPerSample,
        TiffExtraSampleType? extraSampleType,
        ushort[] colorMap,
        Rational[] referenceBlackAndWhite,
        Rational[] ycbcrCoefficients,
        ushort[] ycbcrSubSampling,
        TiffDecoderCompressionType compression,
        ByteOrder byteOrder)
    {
        switch (colorType)
        {
            case TiffColorType.WhiteIsZero:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new TiffBitGrayscaleDecoder<TPixel>(configuration, bitsPerSample.Channel0, whiteIsZero: true);

            case TiffColorType.WhiteIsZero1:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1 && bitsPerSample.Channel0 == 1, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new TiffBitGrayscaleDecoder<TPixel>(configuration, 1, whiteIsZero: true);

            case TiffColorType.WhiteIsZero4:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1 && bitsPerSample.Channel0 == 4, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new TiffBitGrayscaleDecoder<TPixel>(configuration, 4, whiteIsZero: true);

            case TiffColorType.WhiteIsZero8:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1 && bitsPerSample.Channel0 == 8, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new WhiteIsZero8TiffColor<TPixel>(configuration);

            case TiffColorType.WhiteIsZero16:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1 && bitsPerSample.Channel0 == 16, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new TiffGrayL16Reader<TPixel>(configuration, byteOrder == ByteOrder.BigEndian, whiteIsZero: true);

            case TiffColorType.WhiteIsZero24:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1 && bitsPerSample.Channel0 == 24, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new TiffColorDecoder<TPixel, TiffGray24Source, TiffGrayOperator>(
                    configuration,
                    new TiffGray24Source(byteOrder == ByteOrder.BigEndian, whiteIsZero: true),
                    new TiffGrayOperator(1F / 0xFFFFFF));

            case TiffColorType.WhiteIsZero32:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1 && bitsPerSample.Channel0 == 32, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new TiffColorDecoder<TPixel, TiffGray32Source, TiffGrayOperator>(
                    configuration,
                    new TiffGray32Source(byteOrder == ByteOrder.BigEndian, whiteIsZero: true),
                    new TiffGrayOperator(1F / uint.MaxValue));

            case TiffColorType.WhiteIsZero32Float:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1 && bitsPerSample.Channel0 == 32, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new TiffColorDecoder<TPixel, TiffGrayFloat32Source, TiffGrayFloatOperator>(
                    configuration,
                    new TiffGrayFloat32Source(byteOrder == ByteOrder.BigEndian),
                    new TiffGrayFloatOperator(whiteIsZero: true));

            case TiffColorType.BlackIsZero:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new TiffBitGrayscaleDecoder<TPixel>(configuration, bitsPerSample.Channel0, whiteIsZero: false);

            case TiffColorType.BlackIsZero1:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1 && bitsPerSample.Channel0 == 1, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new TiffBitGrayscaleDecoder<TPixel>(configuration, 1, whiteIsZero: false);

            case TiffColorType.BlackIsZero4:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1 && bitsPerSample.Channel0 == 4, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new TiffBitGrayscaleDecoder<TPixel>(configuration, 4, whiteIsZero: false);

            case TiffColorType.BlackIsZero8:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1 && bitsPerSample.Channel0 == 8, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new BlackIsZero8TiffColor<TPixel>(configuration);

            case TiffColorType.BlackIsZero16:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1 && bitsPerSample.Channel0 == 16, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new TiffGrayL16Reader<TPixel>(configuration, byteOrder == ByteOrder.BigEndian, whiteIsZero: false);

            case TiffColorType.BlackIsZero24:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1 && bitsPerSample.Channel0 == 24, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new TiffColorDecoder<TPixel, TiffGray24Source, TiffGrayOperator>(
                    configuration,
                    new TiffGray24Source(byteOrder == ByteOrder.BigEndian, whiteIsZero: false),
                    new TiffGrayOperator(1F / 0xFFFFFF));

            case TiffColorType.BlackIsZero32:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1 && bitsPerSample.Channel0 == 32, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new TiffColorDecoder<TPixel, TiffGray32Source, TiffGrayOperator>(
                    configuration,
                    new TiffGray32Source(byteOrder == ByteOrder.BigEndian, whiteIsZero: false),
                    new TiffGrayOperator(1F / uint.MaxValue));

            case TiffColorType.BlackIsZero32Float:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1 && bitsPerSample.Channel0 == 32, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new TiffColorDecoder<TPixel, TiffGrayFloat32Source, TiffGrayFloatOperator>(
                    configuration,
                    new TiffGrayFloat32Source(byteOrder == ByteOrder.BigEndian),
                    new TiffGrayFloatOperator(whiteIsZero: false));

            case TiffColorType.Rgb:
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbTiffColor<TPixel>(configuration, bitsPerSample);

            case TiffColorType.Rgb222:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 3
                    && bitsPerSample.Channel2 == 2
                    && bitsPerSample.Channel1 == 2
                    && bitsPerSample.Channel0 == 2,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbTiffColor<TPixel>(configuration, bitsPerSample);

            case TiffColorType.Rgba2222:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 4
                    && bitsPerSample.Channel3 == 2
                    && bitsPerSample.Channel2 == 2
                    && bitsPerSample.Channel1 == 2
                    && bitsPerSample.Channel0 == 2,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbaTiffColor<TPixel>(configuration, extraSampleType, bitsPerSample);

            case TiffColorType.Rgb333:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 3
                    && bitsPerSample.Channel2 == 3
                    && bitsPerSample.Channel1 == 3
                    && bitsPerSample.Channel0 == 3,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbTiffColor<TPixel>(configuration, bitsPerSample);

            case TiffColorType.Rgba3333:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 4
                    && bitsPerSample.Channel3 == 3
                    && bitsPerSample.Channel2 == 3
                    && bitsPerSample.Channel1 == 3
                    && bitsPerSample.Channel0 == 3,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbaTiffColor<TPixel>(configuration, extraSampleType, bitsPerSample);

            case TiffColorType.Rgb444:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 3
                    && bitsPerSample.Channel2 == 4
                    && bitsPerSample.Channel1 == 4
                    && bitsPerSample.Channel0 == 4,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new TiffNibbleColorDecoder<TPixel, TiffRgb444Source, TiffNibbleOperator>(
                    configuration,
                    default,
                    new TiffNibbleOperator(hasAlpha: false, associated: false));

            case TiffColorType.Rgba4444:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 4
                    && bitsPerSample.Channel3 == 4
                    && bitsPerSample.Channel2 == 4
                    && bitsPerSample.Channel1 == 4
                    && bitsPerSample.Channel0 == 4,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new TiffNibbleColorDecoder<TPixel, TiffRgba4444Source, TiffNibbleOperator>(
                    configuration,
                    default,
                    new TiffNibbleOperator(hasAlpha: true, associated: extraSampleType == TiffExtraSampleType.AssociatedAlphaData));

            case TiffColorType.Rgb555:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 3
                    && bitsPerSample.Channel2 == 5
                    && bitsPerSample.Channel1 == 5
                    && bitsPerSample.Channel0 == 5,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbTiffColor<TPixel>(configuration, bitsPerSample);

            case TiffColorType.Rgba5555:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 4
                    && bitsPerSample.Channel3 == 5
                    && bitsPerSample.Channel2 == 5
                    && bitsPerSample.Channel1 == 5
                    && bitsPerSample.Channel0 == 5,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbaTiffColor<TPixel>(configuration, extraSampleType, bitsPerSample);

            case TiffColorType.Rgb666:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 3
                    && bitsPerSample.Channel2 == 6
                    && bitsPerSample.Channel1 == 6
                    && bitsPerSample.Channel0 == 6,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbTiffColor<TPixel>(configuration, bitsPerSample);

            case TiffColorType.Rgba6666:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 4
                    && bitsPerSample.Channel3 == 6
                    && bitsPerSample.Channel2 == 6
                    && bitsPerSample.Channel1 == 6
                    && bitsPerSample.Channel0 == 6,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbaTiffColor<TPixel>(configuration, extraSampleType, bitsPerSample);

            case TiffColorType.Rgb888:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 3
                    && bitsPerSample.Channel2 == 8
                    && bitsPerSample.Channel1 == 8
                    && bitsPerSample.Channel0 == 8,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new Rgb888TiffColor<TPixel>(configuration);

            case TiffColorType.Rgba8888:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 4
                    && bitsPerSample.Channel0 is 8 or 16
                    && bitsPerSample.Channel1 == bitsPerSample.Channel0
                    && bitsPerSample.Channel2 == bitsPerSample.Channel0
                    && bitsPerSample.Channel3 == bitsPerSample.Channel0,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new Rgba8888TiffColor<TPixel>(configuration, memoryAllocator, extraSampleType);

            case TiffColorType.Rgb101010:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 3
                    && bitsPerSample.Channel2 == 10
                    && bitsPerSample.Channel1 == 10
                    && bitsPerSample.Channel0 == 10,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbTiffColor<TPixel>(configuration, bitsPerSample);

            case TiffColorType.Rgba10101010:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 4
                    && bitsPerSample.Channel3 == 10
                    && bitsPerSample.Channel2 == 10
                    && bitsPerSample.Channel1 == 10
                    && bitsPerSample.Channel0 == 10,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbaTiffColor<TPixel>(configuration, extraSampleType, bitsPerSample);

            case TiffColorType.Rgb121212:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 3
                    && bitsPerSample.Channel2 == 12
                    && bitsPerSample.Channel1 == 12
                    && bitsPerSample.Channel0 == 12,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbTiffColor<TPixel>(configuration, bitsPerSample);

            case TiffColorType.Rgba12121212:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 4
                    && bitsPerSample.Channel3 == 12
                    && bitsPerSample.Channel2 == 12
                    && bitsPerSample.Channel1 == 12
                    && bitsPerSample.Channel0 == 12,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbaTiffColor<TPixel>(configuration, extraSampleType, bitsPerSample);

            case TiffColorType.Rgb141414:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 3
                    && bitsPerSample.Channel2 == 14
                    && bitsPerSample.Channel1 == 14
                    && bitsPerSample.Channel0 == 14,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbTiffColor<TPixel>(configuration, bitsPerSample);

            case TiffColorType.Rgba14141414:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 4
                    && bitsPerSample.Channel3 == 14
                    && bitsPerSample.Channel2 == 14
                    && bitsPerSample.Channel1 == 14
                    && bitsPerSample.Channel0 == 14,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbaTiffColor<TPixel>(configuration, extraSampleType, bitsPerSample);

            case TiffColorType.Rgb161616:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 3
                    && bitsPerSample.Channel2 == 16
                    && bitsPerSample.Channel1 == 16
                    && bitsPerSample.Channel0 == 16,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new Rgb161616TiffColor<TPixel>(configuration, isBigEndian: byteOrder == ByteOrder.BigEndian);

            case TiffColorType.Rgba16161616:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 4
                    && bitsPerSample.Channel3 == 16
                    && bitsPerSample.Channel2 == 16
                    && bitsPerSample.Channel1 == 16
                    && bitsPerSample.Channel0 == 16,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                if (byteOrder == ByteOrder.BigEndian)
                {
                    return new TiffRgba64BigEndianDecoder<TPixel>(configuration, extraSampleType == TiffExtraSampleType.AssociatedAlphaData);
                }

                return new TiffRgba64LittleEndianDecoder<TPixel>(configuration, extraSampleType == TiffExtraSampleType.AssociatedAlphaData);

            case TiffColorType.Rgb242424:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 3
                    && bitsPerSample.Channel2 == 24
                    && bitsPerSample.Channel1 == 24
                    && bitsPerSample.Channel0 == 24,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new TiffColorDecoder<TPixel, TiffRgb24Source, TiffRgbOperator>(
                    configuration,
                    new TiffRgb24Source(byteOrder == ByteOrder.BigEndian, planar: false),
                    new TiffRgbOperator(1F / 0xFFFFFF));

            case TiffColorType.Rgba24242424:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 4
                    && bitsPerSample.Channel3 == 24
                    && bitsPerSample.Channel2 == 24
                    && bitsPerSample.Channel1 == 24
                    && bitsPerSample.Channel0 == 24,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new TiffColorDecoder<TPixel, TiffRgba24Source, TiffRgbaOperator>(
                    configuration,
                    new TiffRgba24Source(byteOrder == ByteOrder.BigEndian, planar: false),
                    new TiffRgbaOperator(1F / 0xFFFFFF, extraSampleType.HasValue && extraSampleType == TiffExtraSampleType.AssociatedAlphaData));

            case TiffColorType.Rgb323232:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 3
                    && bitsPerSample.Channel2 == 32
                    && bitsPerSample.Channel1 == 32
                    && bitsPerSample.Channel0 == 32,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");

                if (typeof(TPixel) == typeof(Rgb96) && !IccProfile.TryGetIccProfileForColorConversion(metadata.IccProfile, out _))
                {
                    // Without ICC, native uint pixels keep every low bit. ICC instead needs a floating row before final packing.
                    return new Rgb323232TiffColor<TPixel>(isBigEndian: byteOrder == ByteOrder.BigEndian);
                }

                return new TiffColorDecoder<TPixel, TiffRgb32Source, TiffRgbOperator>(
                    configuration,
                    new TiffRgb32Source(byteOrder == ByteOrder.BigEndian, planar: false),
                    new TiffRgbOperator(1F / uint.MaxValue));

            case TiffColorType.Rgba32323232:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 4
                    && bitsPerSample.Channel3 == 32
                    && bitsPerSample.Channel2 == 32
                    && bitsPerSample.Channel1 == 32
                    && bitsPerSample.Channel0 == 32,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");

                if (typeof(TPixel) == typeof(Rgba128) && extraSampleType != TiffExtraSampleType.AssociatedAlphaData
                    && !IccProfile.TryGetIccProfileForColorConversion(metadata.IccProfile, out _))
                {
                    // Without ICC, direct uint packing preserves all low bits. Profiled samples use the floating row below.
                    return new Rgba32323232TiffColor<TPixel>(isBigEndian: byteOrder == ByteOrder.BigEndian);
                }

                return new TiffRgba32PackedDecoder<TPixel>(
                    configuration,
                    byteOrder == ByteOrder.BigEndian,
                    extraSampleType == TiffExtraSampleType.AssociatedAlphaData);

            case TiffColorType.RgbFloat323232:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 3
                    && bitsPerSample.Channel2 == 32
                    && bitsPerSample.Channel1 == 32
                    && bitsPerSample.Channel0 == 32,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new TiffColorDecoder<TPixel, TiffFloat32Source, TiffFloatRgbOperator>(
                    configuration,
                    new TiffFloat32Source(byteOrder == ByteOrder.BigEndian, hasAlpha: false),
                    default);

            case TiffColorType.RgbaFloat32323232:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 4
                    && bitsPerSample.Channel3 == 32
                    && bitsPerSample.Channel2 == 32
                    && bitsPerSample.Channel1 == 32
                    && bitsPerSample.Channel0 == 32,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new TiffColorDecoder<TPixel, TiffFloat32Source, TiffFloatRgbaOperator>(
                    configuration,
                    new TiffFloat32Source(byteOrder == ByteOrder.BigEndian, hasAlpha: true),
                    new TiffFloatRgbaOperator(extraSampleType == TiffExtraSampleType.AssociatedAlphaData));

            case TiffColorType.PaletteColor:
                DebugGuard.NotNull(colorMap, "colorMap");
                return new PaletteTiffColor<TPixel>(bitsPerSample, colorMap, extraSampleType, metadata, configuration);

            case TiffColorType.YCbCr:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 3
                    && bitsPerSample.Channel2 == 8
                    && bitsPerSample.Channel1 == 8
                    && bitsPerSample.Channel0 == 8,
                    "bitsPerSample");

                return new YCbCrTiffColor<TPixel>(configuration, metadata, memoryAllocator, referenceBlackAndWhite, ycbcrCoefficients, ycbcrSubSampling);

            case TiffColorType.CieLab:
            case TiffColorType.CieLab161616:

                DebugGuard.IsTrue(bitsPerSample.Channels == 3, "bitsPerSample");

                if (bitsPerSample.Channel0 == 8)
                {
                    return new TiffLabColorDecoder<TPixel, TiffLab8Source>(configuration, new TiffLab8Source(planar: false), metadata, memoryAllocator);
                }

                return new TiffLabColorDecoder<TPixel, TiffLab16Source>(
                    configuration,
                    new TiffLab16Source(byteOrder == ByteOrder.BigEndian, planar: false),
                    metadata,
                    memoryAllocator);

            case TiffColorType.Cmyk:
            case TiffColorType.Cmyk16161616:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 4
                    && bitsPerSample.Channel0 is 8 or 16
                    && bitsPerSample.Channel1 == bitsPerSample.Channel0
                    && bitsPerSample.Channel2 == bitsPerSample.Channel0
                    && bitsPerSample.Channel3 == bitsPerSample.Channel0,
                    "bitsPerSample");

                return new CmykTiffColor<TPixel>(configuration, metadata, memoryAllocator, bitsPerSample.Channel0, byteOrder == ByteOrder.BigEndian);

            default:
                throw TiffThrowHelper.InvalidColorType(colorType.ToString());
        }
    }

    public static TiffBasePlanarColorDecoder<TPixel> CreatePlanar(
        ImageFrameMetadata metadata,
        Configuration configuration,
        MemoryAllocator allocator,
        TiffColorType colorType,
        TiffBitsPerSample bitsPerSample,
        TiffExtraSampleType? extraSampleType,
        ushort[] colorMap,
        Rational[] referenceBlackAndWhite,
        Rational[] ycbcrCoefficients,
        ushort[] ycbcrSubSampling,
        ByteOrder byteOrder)
    {
        switch (colorType)
        {
            case TiffColorType.Rgb888Planar:
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbPlanarTiffColor<TPixel>(configuration, bitsPerSample);

            case TiffColorType.Rgba8888Planar:
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbaPlanarTiffColor<TPixel>(configuration, extraSampleType, bitsPerSample);

            case TiffColorType.YCbCrPlanar:
                return new YCbCrPlanarTiffColor<TPixel>(configuration, metadata, allocator, referenceBlackAndWhite, ycbcrCoefficients, ycbcrSubSampling);

            case TiffColorType.CieLabPlanar:
            case TiffColorType.CieLab161616Planar:
                return bitsPerSample.Channel0 == 8
                    ? new TiffLabPlanarColorDecoder<TPixel, TiffLab8Source>(configuration, new TiffLab8Source(planar: true), metadata, allocator)
                    : new TiffLabPlanarColorDecoder<TPixel, TiffLab16Source>(
                        configuration,
                        new TiffLab16Source(byteOrder == ByteOrder.BigEndian, planar: true),
                        metadata,
                        allocator);

            case TiffColorType.Rgb161616Planar:
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new TiffRgb48PlanarDecoder<TPixel>(configuration, byteOrder == ByteOrder.BigEndian);

            case TiffColorType.Rgba16161616Planar:
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new TiffRgba64PlanarDecoder<TPixel>(
                    configuration,
                    byteOrder == ByteOrder.BigEndian,
                    extraSampleType == TiffExtraSampleType.AssociatedAlphaData);

            case TiffColorType.Rgb242424Planar:
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new TiffPlanarColorDecoder<TPixel, TiffRgb24Source, TiffRgbOperator>(
                    configuration,
                    new TiffRgb24Source(byteOrder == ByteOrder.BigEndian, planar: true),
                    new TiffRgbOperator(1F / 0xFFFFFF));

            case TiffColorType.Rgba24242424Planar:
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new TiffPlanarColorDecoder<TPixel, TiffRgba24Source, TiffRgbaOperator>(
                    configuration,
                    new TiffRgba24Source(byteOrder == ByteOrder.BigEndian, planar: true),
                    new TiffRgbaOperator(1F / 0xFFFFFF, extraSampleType.HasValue && extraSampleType == TiffExtraSampleType.AssociatedAlphaData));

            case TiffColorType.Rgb323232Planar:
                DebugGuard.IsTrue(colorMap == null, "colorMap");

                if (typeof(TPixel) == typeof(Rgb96) && !IccProfile.TryGetIccProfileForColorConversion(metadata.IccProfile, out _))
                {
                    // Without ICC, direct integer packing preserves every bit. ICC uses the floating row before final packing.
                    return new Rgb32PlanarTiffColor<TPixel>(byteOrder == ByteOrder.BigEndian);
                }

                return new TiffPlanarColorDecoder<TPixel, TiffRgb32Source, TiffRgbOperator>(
                    configuration,
                    new TiffRgb32Source(byteOrder == ByteOrder.BigEndian, planar: true),
                    new TiffRgbOperator(1F / uint.MaxValue));

            case TiffColorType.Rgba32323232Planar:
                DebugGuard.IsTrue(colorMap == null, "colorMap");

                if (typeof(TPixel) == typeof(Rgba128) && extraSampleType != TiffExtraSampleType.AssociatedAlphaData
                    && !IccProfile.TryGetIccProfileForColorConversion(metadata.IccProfile, out _))
                {
                    // Without ICC, direct uint packing preserves all low bits. Profiled samples use the floating row below.
                    return new Rgba32PlanarTiffColor<TPixel>(byteOrder == ByteOrder.BigEndian);
                }

                return new TiffPlanarColorDecoder<TPixel, TiffRgba32Source, TiffRgbaOperator>(
                    configuration,
                    new TiffRgba32Source(byteOrder == ByteOrder.BigEndian, planar: true),
                    new TiffRgbaOperator(1F / uint.MaxValue, extraSampleType == TiffExtraSampleType.AssociatedAlphaData));

            default:
                throw TiffThrowHelper.InvalidColorType(colorType.ToString());
        }
    }
}
