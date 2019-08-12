// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    internal static class TiffColorDecoderFactory<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        public static TiffColorDecoder<TPixel> Create(TiffColorType colorType, uint[] bitsPerSample, uint[] colorMap)
        {
            switch (colorType)
            {
                case TiffColorType.WhiteIsZero:
                    DebugGuard.IsTrue(bitsPerSample.Length == 1, "bitsPerSample");
                    DebugGuard.MustBeNull(colorMap, "colorMap");
                    return new WhiteIsZeroTiffColor<TPixel>(bitsPerSample);

                case TiffColorType.WhiteIsZero1:
                    DebugGuard.IsTrue(bitsPerSample.Length == 1 && bitsPerSample[0] == 1, "bitsPerSample");
                    DebugGuard.MustBeNull(colorMap, "colorMap");
                    return new WhiteIsZero1TiffColor<TPixel>();

                case TiffColorType.WhiteIsZero4:
                    DebugGuard.IsTrue(bitsPerSample.Length == 1 && bitsPerSample[0] == 4, "bitsPerSample");
                    DebugGuard.MustBeNull(colorMap, "colorMap");
                    return new WhiteIsZero4TiffColor<TPixel>();

                case TiffColorType.WhiteIsZero8:
                    DebugGuard.IsTrue(bitsPerSample.Length == 1 && bitsPerSample[0] == 8, "bitsPerSample");
                    DebugGuard.MustBeNull(colorMap, "colorMap");
                    return new WhiteIsZero8TiffColor<TPixel>();

                case TiffColorType.BlackIsZero:
                    DebugGuard.IsTrue(bitsPerSample.Length == 1, "bitsPerSample");
                    DebugGuard.MustBeNull(colorMap, "colorMap");
                    return new BlackIsZeroTiffColor<TPixel>(bitsPerSample);

                case TiffColorType.BlackIsZero1:
                    DebugGuard.IsTrue(bitsPerSample.Length == 1 && bitsPerSample[0] == 1, "bitsPerSample");
                    DebugGuard.MustBeNull(colorMap, "colorMap");
                    return new BlackIsZero1TiffColor<TPixel>();

                case TiffColorType.BlackIsZero4:
                    DebugGuard.IsTrue(bitsPerSample.Length == 1 && bitsPerSample[0] == 4, "bitsPerSample");
                    DebugGuard.MustBeNull(colorMap, "colorMap");
                    return new BlackIsZero4TiffColor<TPixel>();

                case TiffColorType.BlackIsZero8:
                    DebugGuard.IsTrue(bitsPerSample.Length == 1 && bitsPerSample[0] == 8, "bitsPerSample");
                    DebugGuard.MustBeNull(colorMap, "colorMap");
                    return new BlackIsZero8TiffColor<TPixel>();

                case TiffColorType.Rgb:
                    DebugGuard.NotNull(bitsPerSample, "bitsPerSample");
                    DebugGuard.MustBeNull(colorMap, "colorMap");
                    return new RgbTiffColor<TPixel>(bitsPerSample);

                case TiffColorType.Rgb888:
                    DebugGuard.IsTrue(
                        bitsPerSample.Length == 3
                        && bitsPerSample[0] == 8
                        && bitsPerSample[1] == 8
                        && bitsPerSample[2] == 8,
                        "bitsPerSample");
                    DebugGuard.MustBeNull(colorMap, "colorMap");
                    return new Rgb888TiffColor<TPixel>();

                case TiffColorType.PaletteColor:
                    DebugGuard.NotNull(bitsPerSample, "bitsPerSample");
                    DebugGuard.NotNull(colorMap, "colorMap");
                    return new PaletteTiffColor<TPixel>(bitsPerSample, colorMap);

                default:
                    throw new InvalidOperationException();
            }
        }

        public static RgbPlanarTiffColor<TPixel> CreatePlanar(TiffColorType colorType, uint[] bitsPerSample, uint[] colorMap)
        {
            switch (colorType)
            {
                case TiffColorType.RgbPlanar:
                    DebugGuard.NotNull(bitsPerSample, "bitsPerSample");
                    DebugGuard.MustBeNull(colorMap, "colorMap");
                    return new RgbPlanarTiffColor<TPixel>(bitsPerSample);

                default:
                    throw new InvalidOperationException();
            }
        }
    }
}
