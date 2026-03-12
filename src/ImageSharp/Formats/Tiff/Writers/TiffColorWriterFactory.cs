// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Tiff.Writers;

internal static class TiffColorWriterFactory
{
    public static TiffBaseColorWriter<TPixel> Create<TPixel>(
        TiffPhotometricInterpretation? photometricInterpretation,
        ImageFrame<TPixel> image,
        Size encodingSize,
        IQuantizer quantizer,
        IPixelSamplingStrategy pixelSamplingStrategy,
        MemoryAllocator memoryAllocator,
        Configuration configuration,
        TiffEncoderEntriesCollector entriesCollector,
        int bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel>
        => photometricInterpretation switch
        {
            TiffPhotometricInterpretation.PaletteColor => new TiffPaletteWriter<TPixel>(image, encodingSize, quantizer, pixelSamplingStrategy, memoryAllocator, configuration, entriesCollector, bitsPerPixel),
            TiffPhotometricInterpretation.BlackIsZero or TiffPhotometricInterpretation.WhiteIsZero => bitsPerPixel switch
            {
                1 => new TiffBiColorWriter<TPixel>(image, encodingSize, memoryAllocator, configuration, entriesCollector),
                16 => new TiffGrayL16Writer<TPixel>(image, encodingSize, memoryAllocator, configuration, entriesCollector),
                _ => new TiffGrayWriter<TPixel>(image, encodingSize, memoryAllocator, configuration, entriesCollector)
            },
            _ => new TiffRgbWriter<TPixel>(image, encodingSize, memoryAllocator, configuration, entriesCollector),
        };
}
