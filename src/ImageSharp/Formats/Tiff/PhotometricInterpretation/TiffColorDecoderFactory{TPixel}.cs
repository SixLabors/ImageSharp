// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff.Compression;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

internal static class TiffColorDecoderFactory<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    public static TiffBaseColorDecoder<TPixel> Create(
        ImageFrameMetadata metadata,
        DecoderOptions options,
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
                return new WhiteIsZeroTiffColor<TPixel>(bitsPerSample);

            case TiffColorType.WhiteIsZero1:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1 && bitsPerSample.Channel0 == 1, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new WhiteIsZero1TiffColor<TPixel>();

            case TiffColorType.WhiteIsZero4:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1 && bitsPerSample.Channel0 == 4, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new WhiteIsZero4TiffColor<TPixel>();

            case TiffColorType.WhiteIsZero8:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1 && bitsPerSample.Channel0 == 8, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new WhiteIsZero8TiffColor<TPixel>();

            case TiffColorType.WhiteIsZero16:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1 && bitsPerSample.Channel0 == 16, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new WhiteIsZero16TiffColor<TPixel>(byteOrder == ByteOrder.BigEndian);

            case TiffColorType.WhiteIsZero24:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1 && bitsPerSample.Channel0 == 24, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new WhiteIsZero24TiffColor<TPixel>(byteOrder == ByteOrder.BigEndian);

            case TiffColorType.WhiteIsZero32:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1 && bitsPerSample.Channel0 == 32, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new WhiteIsZero32TiffColor<TPixel>(byteOrder == ByteOrder.BigEndian);

            case TiffColorType.WhiteIsZero32Float:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1 && bitsPerSample.Channel0 == 32, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new WhiteIsZero32FloatTiffColor<TPixel>(byteOrder == ByteOrder.BigEndian);

            case TiffColorType.BlackIsZero:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new BlackIsZeroTiffColor<TPixel>(bitsPerSample);

            case TiffColorType.BlackIsZero1:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1 && bitsPerSample.Channel0 == 1, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new BlackIsZero1TiffColor<TPixel>();

            case TiffColorType.BlackIsZero4:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1 && bitsPerSample.Channel0 == 4, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new BlackIsZero4TiffColor<TPixel>();

            case TiffColorType.BlackIsZero8:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1 && bitsPerSample.Channel0 == 8, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new BlackIsZero8TiffColor<TPixel>(configuration);

            case TiffColorType.BlackIsZero16:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1 && bitsPerSample.Channel0 == 16, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new BlackIsZero16TiffColor<TPixel>(configuration, byteOrder == ByteOrder.BigEndian);

            case TiffColorType.BlackIsZero24:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1 && bitsPerSample.Channel0 == 24, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new BlackIsZero24TiffColor<TPixel>(byteOrder == ByteOrder.BigEndian);

            case TiffColorType.BlackIsZero32:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1 && bitsPerSample.Channel0 == 32, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new BlackIsZero32TiffColor<TPixel>(byteOrder == ByteOrder.BigEndian);

            case TiffColorType.BlackIsZero32Float:
                DebugGuard.IsTrue(bitsPerSample.Channels == 1 && bitsPerSample.Channel0 == 32, "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new BlackIsZero32FloatTiffColor<TPixel>(byteOrder == ByteOrder.BigEndian);

            case TiffColorType.Rgb:
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbTiffColor<TPixel>(bitsPerSample);

            case TiffColorType.Rgb222:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 3
                    && bitsPerSample.Channel2 == 2
                    && bitsPerSample.Channel1 == 2
                    && bitsPerSample.Channel0 == 2,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbTiffColor<TPixel>(bitsPerSample);

            case TiffColorType.Rgba2222:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 4
                    && bitsPerSample.Channel3 == 2
                    && bitsPerSample.Channel2 == 2
                    && bitsPerSample.Channel1 == 2
                    && bitsPerSample.Channel0 == 2,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbaTiffColor<TPixel>(extraSampleType, bitsPerSample);

            case TiffColorType.Rgb333:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 3
                    && bitsPerSample.Channel2 == 3
                    && bitsPerSample.Channel1 == 3
                    && bitsPerSample.Channel0 == 3,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbTiffColor<TPixel>(bitsPerSample);

            case TiffColorType.Rgba3333:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 4
                    && bitsPerSample.Channel3 == 3
                    && bitsPerSample.Channel2 == 3
                    && bitsPerSample.Channel1 == 3
                    && bitsPerSample.Channel0 == 3,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbaTiffColor<TPixel>(extraSampleType, bitsPerSample);

            case TiffColorType.Rgb444:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 3
                    && bitsPerSample.Channel2 == 4
                    && bitsPerSample.Channel1 == 4
                    && bitsPerSample.Channel0 == 4,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new Rgb444TiffColor<TPixel>();

            case TiffColorType.Rgba4444:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 4
                    && bitsPerSample.Channel3 == 4
                    && bitsPerSample.Channel2 == 4
                    && bitsPerSample.Channel1 == 4
                    && bitsPerSample.Channel0 == 4,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbaTiffColor<TPixel>(extraSampleType, bitsPerSample);

            case TiffColorType.Rgb555:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 3
                    && bitsPerSample.Channel2 == 5
                    && bitsPerSample.Channel1 == 5
                    && bitsPerSample.Channel0 == 5,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbTiffColor<TPixel>(bitsPerSample);

            case TiffColorType.Rgba5555:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 4
                    && bitsPerSample.Channel3 == 5
                    && bitsPerSample.Channel2 == 5
                    && bitsPerSample.Channel1 == 5
                    && bitsPerSample.Channel0 == 5,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbaTiffColor<TPixel>(extraSampleType, bitsPerSample);

            case TiffColorType.Rgb666:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 3
                    && bitsPerSample.Channel2 == 6
                    && bitsPerSample.Channel1 == 6
                    && bitsPerSample.Channel0 == 6,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbTiffColor<TPixel>(bitsPerSample);

            case TiffColorType.Rgba6666:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 4
                    && bitsPerSample.Channel3 == 6
                    && bitsPerSample.Channel2 == 6
                    && bitsPerSample.Channel1 == 6
                    && bitsPerSample.Channel0 == 6,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbaTiffColor<TPixel>(extraSampleType, bitsPerSample);

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
                    && bitsPerSample.Channel3 == 8
                    && bitsPerSample.Channel2 == 8
                    && bitsPerSample.Channel1 == 8
                    && bitsPerSample.Channel0 == 8,
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
                return new RgbTiffColor<TPixel>(bitsPerSample);

            case TiffColorType.Rgba10101010:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 4
                    && bitsPerSample.Channel3 == 10
                    && bitsPerSample.Channel2 == 10
                    && bitsPerSample.Channel1 == 10
                    && bitsPerSample.Channel0 == 10,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbaTiffColor<TPixel>(extraSampleType, bitsPerSample);

            case TiffColorType.Rgb121212:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 3
                    && bitsPerSample.Channel2 == 12
                    && bitsPerSample.Channel1 == 12
                    && bitsPerSample.Channel0 == 12,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbTiffColor<TPixel>(bitsPerSample);

            case TiffColorType.Rgba12121212:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 4
                    && bitsPerSample.Channel3 == 12
                    && bitsPerSample.Channel2 == 12
                    && bitsPerSample.Channel1 == 12
                    && bitsPerSample.Channel0 == 12,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbaTiffColor<TPixel>(extraSampleType, bitsPerSample);

            case TiffColorType.Rgb141414:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 3
                    && bitsPerSample.Channel2 == 14
                    && bitsPerSample.Channel1 == 14
                    && bitsPerSample.Channel0 == 14,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbTiffColor<TPixel>(bitsPerSample);

            case TiffColorType.Rgba14141414:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 4
                    && bitsPerSample.Channel3 == 14
                    && bitsPerSample.Channel2 == 14
                    && bitsPerSample.Channel1 == 14
                    && bitsPerSample.Channel0 == 14,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbaTiffColor<TPixel>(extraSampleType, bitsPerSample);

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
                return new Rgba16161616TiffColor<TPixel>(configuration, memoryAllocator, extraSampleType, isBigEndian: byteOrder == ByteOrder.BigEndian);

            case TiffColorType.Rgb242424:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 3
                    && bitsPerSample.Channel2 == 24
                    && bitsPerSample.Channel1 == 24
                    && bitsPerSample.Channel0 == 24,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new Rgb242424TiffColor<TPixel>(isBigEndian: byteOrder == ByteOrder.BigEndian);

            case TiffColorType.Rgba24242424:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 4
                    && bitsPerSample.Channel3 == 24
                    && bitsPerSample.Channel2 == 24
                    && bitsPerSample.Channel1 == 24
                    && bitsPerSample.Channel0 == 24,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new Rgba24242424TiffColor<TPixel>(extraSampleType, isBigEndian: byteOrder == ByteOrder.BigEndian);

            case TiffColorType.Rgb323232:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 3
                    && bitsPerSample.Channel2 == 32
                    && bitsPerSample.Channel1 == 32
                    && bitsPerSample.Channel0 == 32,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new Rgb323232TiffColor<TPixel>(isBigEndian: byteOrder == ByteOrder.BigEndian);

            case TiffColorType.Rgba32323232:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 4
                    && bitsPerSample.Channel3 == 32
                    && bitsPerSample.Channel2 == 32
                    && bitsPerSample.Channel1 == 32
                    && bitsPerSample.Channel0 == 32,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new Rgba32323232TiffColor<TPixel>(extraSampleType, isBigEndian: byteOrder == ByteOrder.BigEndian);

            case TiffColorType.RgbFloat323232:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 3
                    && bitsPerSample.Channel2 == 32
                    && bitsPerSample.Channel1 == 32
                    && bitsPerSample.Channel0 == 32,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbFloat323232TiffColor<TPixel>(isBigEndian: byteOrder == ByteOrder.BigEndian);

            case TiffColorType.RgbaFloat32323232:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 4
                    && bitsPerSample.Channel3 == 32
                    && bitsPerSample.Channel2 == 32
                    && bitsPerSample.Channel1 == 32
                    && bitsPerSample.Channel0 == 32,
                    "bitsPerSample");
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbaFloat32323232TiffColor<TPixel>(isBigEndian: byteOrder == ByteOrder.BigEndian);

            case TiffColorType.PaletteColor:
                DebugGuard.NotNull(colorMap, "colorMap");
                return new PaletteTiffColor<TPixel>(bitsPerSample, colorMap);

            case TiffColorType.YCbCr:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 3
                    && bitsPerSample.Channel2 == 8
                    && bitsPerSample.Channel1 == 8
                    && bitsPerSample.Channel0 == 8,
                    "bitsPerSample");
                return new YCbCrTiffColor<TPixel>(memoryAllocator, referenceBlackAndWhite, ycbcrCoefficients, ycbcrSubSampling);

            case TiffColorType.CieLab:

                DebugGuard.IsTrue(bitsPerSample.Channels == 3, "bitsPerSample");

                if (bitsPerSample.Channel0 == 8)
                {
                    return new CieLab8TiffColor<TPixel>();
                }

                return new CieLab16TiffColor<TPixel>(
                    configuration,
                    options,
                    metadata,
                    memoryAllocator,
                    byteOrder == ByteOrder.BigEndian);

            case TiffColorType.Cmyk:
                DebugGuard.IsTrue(
                    bitsPerSample.Channels == 4
                    && bitsPerSample.Channel3 == 8
                    && bitsPerSample.Channel2 == 8
                    && bitsPerSample.Channel1 == 8
                    && bitsPerSample.Channel0 == 8,
                    "bitsPerSample");
                return new CmykTiffColor<TPixel>(compression, configuration, options, metadata, memoryAllocator);

            default:
                throw TiffThrowHelper.InvalidColorType(colorType.ToString());
        }
    }

    public static TiffBasePlanarColorDecoder<TPixel> CreatePlanar(
        ImageFrameMetadata metadata,
        DecoderOptions options,
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
                return new RgbPlanarTiffColor<TPixel>(bitsPerSample);

            case TiffColorType.Rgba8888Planar:
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new RgbaPlanarTiffColor<TPixel>(extraSampleType, bitsPerSample);

            case TiffColorType.YCbCrPlanar:
                return new YCbCrPlanarTiffColor<TPixel>(referenceBlackAndWhite, ycbcrCoefficients, ycbcrSubSampling);

            case TiffColorType.CieLabPlanar:
                return bitsPerSample.Channel0 == 8
                    ? new CieLab8PlanarTiffColor<TPixel>()
                    : new CieLab16PlanarTiffColor<TPixel>(
                        configuration,
                        options,
                        metadata,
                        allocator,
                        byteOrder == ByteOrder.BigEndian);

            case TiffColorType.Rgb161616Planar:
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new Rgb16PlanarTiffColor<TPixel>(byteOrder == ByteOrder.BigEndian);

            case TiffColorType.Rgba16161616Planar:
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new Rgba16PlanarTiffColor<TPixel>(extraSampleType, byteOrder == ByteOrder.BigEndian);

            case TiffColorType.Rgb242424Planar:
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new Rgb24PlanarTiffColor<TPixel>(byteOrder == ByteOrder.BigEndian);

            case TiffColorType.Rgba24242424Planar:
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new Rgba24PlanarTiffColor<TPixel>(extraSampleType, byteOrder == ByteOrder.BigEndian);

            case TiffColorType.Rgb323232Planar:
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new Rgb32PlanarTiffColor<TPixel>(byteOrder == ByteOrder.BigEndian);

            case TiffColorType.Rgba32323232Planar:
                DebugGuard.IsTrue(colorMap == null, "colorMap");
                return new Rgba32PlanarTiffColor<TPixel>(extraSampleType, byteOrder == ByteOrder.BigEndian);

            default:
                throw TiffThrowHelper.InvalidColorType(colorType.ToString());
        }
    }
}
