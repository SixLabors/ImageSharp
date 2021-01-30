// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff.Writers
{
    internal static class TiffColorWriterFactory
    {
        public static TiffBaseColorWriter Create(TiffEncodingMode mode, TiffStreamWriter output, MemoryAllocator memoryAllocator, Configuration configuration, TiffEncoderEntriesCollector entriesCollector)
        {
            switch (mode)
            {
                case TiffEncodingMode.ColorPalette:
                    return new TiffPaletteWriter(output, memoryAllocator, configuration, entriesCollector);
                case TiffEncodingMode.Gray:
                    return new TiffGrayWriter(output, memoryAllocator, configuration, entriesCollector);
                case TiffEncodingMode.BiColor:
                    return new TiffBiColorWriter(output, memoryAllocator, configuration, entriesCollector);
                default:
                    return new TiffRgbWriter(output, memoryAllocator, configuration, entriesCollector);
            }
        }
    }
}
