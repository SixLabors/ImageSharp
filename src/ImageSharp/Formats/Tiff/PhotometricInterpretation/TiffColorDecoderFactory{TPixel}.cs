// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    internal static class TiffColorDecoderFactory<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        public static TiffBaseColorDecoder<TPixel> Create(TiffColorType colorType, ushort[] bitsPerSample, ushort[] colorMap)
        {
            switch (colorType)
            {
                case TiffColorType.WhiteIsZero:
                    DebugGuard.IsTrue(bitsPerSample.Length == 1, "bitsPerSample");
                    DebugGuard.IsTrue(colorMap == null, "colorMap");
                    return new WhiteIsZeroTiffColor<TPixel>(bitsPerSample);

                case TiffColorType.WhiteIsZero1:
                    DebugGuard.IsTrue(bitsPerSample.Length == 1 && bitsPerSample[0] == 1, "bitsPerSample");
                    DebugGuard.IsTrue(colorMap == null, "colorMap");
                    return new WhiteIsZero1TiffColor<TPixel>();

                case TiffColorType.WhiteIsZero4:
                    DebugGuard.IsTrue(bitsPerSample.Length == 1 && bitsPerSample[0] == 4, "bitsPerSample");
                    DebugGuard.IsTrue(colorMap == null, "colorMap");
                    return new WhiteIsZero4TiffColor<TPixel>();

                case TiffColorType.WhiteIsZero8:
                    DebugGuard.IsTrue(bitsPerSample.Length == 1 && bitsPerSample[0] == 8, "bitsPerSample");
                    DebugGuard.IsTrue(colorMap == null, "colorMap");
                    return new WhiteIsZero8TiffColor<TPixel>();

                case TiffColorType.BlackIsZero:
                    DebugGuard.IsTrue(bitsPerSample.Length == 1, "bitsPerSample");
                    DebugGuard.IsTrue(colorMap == null, "colorMap");
                    return new BlackIsZeroTiffColor<TPixel>(bitsPerSample);

                case TiffColorType.BlackIsZero1:
                    DebugGuard.IsTrue(bitsPerSample.Length == 1 && bitsPerSample[0] == 1, "bitsPerSample");
                    DebugGuard.IsTrue(colorMap == null, "colorMap");
                    return new BlackIsZero1TiffColor<TPixel>();

                case TiffColorType.BlackIsZero4:
                    DebugGuard.IsTrue(bitsPerSample.Length == 1 && bitsPerSample[0] == 4, "bitsPerSample");
                    DebugGuard.IsTrue(colorMap == null, "colorMap");
                    return new BlackIsZero4TiffColor<TPixel>();

                case TiffColorType.BlackIsZero8:
                    DebugGuard.IsTrue(bitsPerSample.Length == 1 && bitsPerSample[0] == 8, "bitsPerSample");
                    DebugGuard.IsTrue(colorMap == null, "colorMap");
                    return new BlackIsZero8TiffColor<TPixel>();

                case TiffColorType.Rgb:
                    DebugGuard.NotNull(bitsPerSample, "bitsPerSample");
                    DebugGuard.IsTrue(colorMap == null, "colorMap");
                    return new RgbTiffColor<TPixel>(bitsPerSample);

                case TiffColorType.Rgb222:
                    DebugGuard.IsTrue(
                        bitsPerSample.Length == 3
                        && bitsPerSample[2] == 2
                        && bitsPerSample[1] == 2
                        && bitsPerSample[0] == 2,
                        "bitsPerSample");
                    DebugGuard.IsTrue(colorMap == null, "colorMap");
                    return new RgbTiffColor<TPixel>(bitsPerSample);

                case TiffColorType.Rgb444:
                    DebugGuard.IsTrue(
                        bitsPerSample.Length == 3
                        && bitsPerSample[2] == 4
                        && bitsPerSample[1] == 4
                        && bitsPerSample[0] == 4,
                        "bitsPerSample");
                    DebugGuard.IsTrue(colorMap == null, "colorMap");
                    return new Rgb444TiffColor<TPixel>();

                case TiffColorType.Rgb888:
                    DebugGuard.IsTrue(
                        bitsPerSample.Length == 3
                        && bitsPerSample[2] == 8
                        && bitsPerSample[1] == 8
                        && bitsPerSample[0] == 8,
                        "bitsPerSample");
                    DebugGuard.IsTrue(colorMap == null, "colorMap");
                    return new Rgb888TiffColor<TPixel>();

                case TiffColorType.Rgb101010:
                    DebugGuard.IsTrue(
                        bitsPerSample.Length == 3
                        && bitsPerSample[2] == 10
                        && bitsPerSample[1] == 10
                        && bitsPerSample[0] == 10,
                        "bitsPerSample");
                    DebugGuard.IsTrue(colorMap == null, "colorMap");
                    return new RgbTiffColor<TPixel>(bitsPerSample);

                case TiffColorType.Rgb121212:
                    DebugGuard.IsTrue(
                        bitsPerSample.Length == 3
                        && bitsPerSample[2] == 12
                        && bitsPerSample[1] == 12
                        && bitsPerSample[0] == 12,
                        "bitsPerSample");
                    DebugGuard.IsTrue(colorMap == null, "colorMap");
                    return new RgbTiffColor<TPixel>(bitsPerSample);

                case TiffColorType.Rgb141414:
                    DebugGuard.IsTrue(
                        bitsPerSample.Length == 3
                        && bitsPerSample[2] == 14
                        && bitsPerSample[1] == 14
                        && bitsPerSample[0] == 14,
                        "bitsPerSample");
                    DebugGuard.IsTrue(colorMap == null, "colorMap");
                    return new RgbTiffColor<TPixel>(bitsPerSample);

                case TiffColorType.Rgb161616:
                    DebugGuard.IsTrue(
                        bitsPerSample.Length == 3
                        && bitsPerSample[2] == 16
                        && bitsPerSample[1] == 16
                        && bitsPerSample[0] == 16,
                        "bitsPerSample");
                    DebugGuard.IsTrue(colorMap == null, "colorMap");
                    return new RgbTiffColor<TPixel>(bitsPerSample);

                case TiffColorType.PaletteColor:
                    DebugGuard.NotNull(bitsPerSample, "bitsPerSample");
                    DebugGuard.NotNull(colorMap, "colorMap");
                    return new PaletteTiffColor<TPixel>(bitsPerSample, colorMap);

                default:
                    throw TiffThrowHelper.InvalidColorType(colorType.ToString());
            }
        }

        public static RgbPlanarTiffColor<TPixel> CreatePlanar(TiffColorType colorType, ushort[] bitsPerSample, ushort[] colorMap)
        {
            switch (colorType)
            {
                case TiffColorType.RgbPlanar:
                    DebugGuard.NotNull(bitsPerSample, "bitsPerSample");
                    DebugGuard.IsTrue(colorMap == null, "colorMap");
                    return new RgbPlanarTiffColor<TPixel>(bitsPerSample);

                default:
                    throw TiffThrowHelper.InvalidColorType(colorType.ToString());
            }
        }
    }
}
