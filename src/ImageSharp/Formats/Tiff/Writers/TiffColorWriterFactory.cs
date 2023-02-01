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
        IQuantizer quantizer,
        IPixelSamplingStrategy pixelSamplingStrategy,
        MemoryAllocator memoryAllocator,
        Configuration configuration,
        TiffEncoderEntriesCollector entriesCollector,
        int bitsPerPixel)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        switch (photometricInterpretation)
        {
            case TiffPhotometricInterpretation.PaletteColor:
                return new TiffPaletteWriter<TPixel>(image, quantizer, pixelSamplingStrategy, memoryAllocator, configuration, entriesCollector, bitsPerPixel);
            case TiffPhotometricInterpretation.BlackIsZero:
            case TiffPhotometricInterpretation.WhiteIsZero:
                return bitsPerPixel switch
                {
                    1 => new TiffBiColorWriter<TPixel>(image, memoryAllocator, configuration, entriesCollector),
                    16 => new TiffGrayL16Writer<TPixel>(image, memoryAllocator, configuration, entriesCollector),
                    _ => new TiffGrayWriter<TPixel>(image, memoryAllocator, configuration, entriesCollector)
                };

            default:
                return new TiffRgbWriter<TPixel>(image, memoryAllocator, configuration, entriesCollector);
        }
    }
}
