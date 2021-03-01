// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff.Writers
{
    internal static class TiffColorWriterFactory
    {
        public static TiffBaseColorWriter<TPixel> Create<TPixel>(
            TiffEncodingMode mode,
            ImageFrame<TPixel> image,
            IQuantizer quantizer,
            MemoryAllocator memoryAllocator,
            Configuration configuration,
            TiffEncoderEntriesCollector entriesCollector,
            int bitsPerPixel)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            switch (mode)
            {
                case TiffEncodingMode.ColorPalette:
                    return new TiffPaletteWriter<TPixel>(image, quantizer, memoryAllocator, configuration, entriesCollector, bitsPerPixel);
                case TiffEncodingMode.Gray:
                    return new TiffGrayWriter<TPixel>(image, memoryAllocator, configuration, entriesCollector);
                case TiffEncodingMode.BiColor:
                    return new TiffBiColorWriter<TPixel>(image, memoryAllocator, configuration, entriesCollector);
                default:
                    return new TiffRgbWriter<TPixel>(image, memoryAllocator, configuration, entriesCollector);
            }
        }
    }
}
