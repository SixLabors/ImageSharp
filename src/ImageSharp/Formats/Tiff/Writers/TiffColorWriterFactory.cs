// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Tiff.Writers;

/// <summary>
/// Selects the existing TIFF sample writer for the resolved frame options. JPEG uses TiffJpegWriter instead.
/// </summary>
internal static class TiffColorWriterFactory
{
    /// <summary>
    /// Creates a writer for one frame without changing its pixels or metadata.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="photometricInterpretation">The resolved sample interpretation.</param>
    /// <param name="image">The source frame.</param>
    /// <param name="encodingSize">The encoded region size.</param>
    /// <param name="quantizer">The palette quantizer.</param>
    /// <param name="pixelSamplingStrategy">The palette sampling strategy.</param>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="configuration">The pixel conversion configuration.</param>
    /// <param name="entriesCollector">The directory entries.</param>
    /// <param name="bitsPerPixel">The resolved bits per pixel.</param>
    /// <param name="sampleFormat">The resolved sample format.</param>
    /// <param name="clearTransparentPixels">Whether to set RGB to zero for fully transparent pixels in encoder-owned storage.</param>
    /// <param name="converter">The converter from sRGB to the retained profile, or null when no ICC conversion is required.</param>
    /// <returns>The selected writer.</returns>
    public static TiffBaseColorWriter<TPixel> Create<TPixel>(
        TiffPhotometricInterpretation photometricInterpretation,
        ImageFrame<TPixel> image,
        Size encodingSize,
        IQuantizer quantizer,
        IPixelSamplingStrategy pixelSamplingStrategy,
        MemoryAllocator memoryAllocator,
        Configuration configuration,
        TiffEncoderEntriesCollector entriesCollector,
        int bitsPerPixel,
        TiffSampleFormat sampleFormat,
        bool clearTransparentPixels,
        ColorProfileConverter? converter)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        bool whiteIsZero = photometricInterpretation == TiffPhotometricInterpretation.WhiteIsZero;
        if (sampleFormat == TiffSampleFormat.Float)
        {
            // Float layouts share one writer. Their total depth selects one, three or four 32-bit samples without integer packing.
            return new TiffFloatWriter<TPixel>(
                image,
                encodingSize,
                memoryAllocator,
                configuration,
                entriesCollector,
                bitsPerPixel,
                whiteIsZero,
                converter,
                clearTransparentPixels);
        }

        switch (photometricInterpretation)
        {
            case TiffPhotometricInterpretation.YCbCr:
                return new TiffYCbCrWriter<TPixel>(image, encodingSize, memoryAllocator, configuration, entriesCollector, converter, clearTransparentPixels);

            case TiffPhotometricInterpretation.CieLab:
                return new TiffLabWriter<TPixel>(
                    image,
                    encodingSize,
                    memoryAllocator,
                    configuration,
                    entriesCollector,
                    bitsPerPixel,
                    converter,
                    clearTransparentPixels);

            case TiffPhotometricInterpretation.Separated:
                return new TiffCmykWriter<TPixel>(
                    image,
                    encodingSize,
                    memoryAllocator,
                    configuration,
                    entriesCollector,
                    bitsPerPixel,
                    converter,
                    clearTransparentPixels);

            case TiffPhotometricInterpretation.PaletteColor:
                return new TiffPaletteWriter<TPixel>(
                    image,
                    encodingSize,
                    quantizer,
                    pixelSamplingStrategy,
                    memoryAllocator,
                    configuration,
                    entriesCollector,
                    bitsPerPixel,
                    converter,
                    clearTransparentPixels);

            case TiffPhotometricInterpretation.BlackIsZero:
            case TiffPhotometricInterpretation.WhiteIsZero:
                return bitsPerPixel switch
                {
                    1 => new TiffBiColorWriter<TPixel>(
                        image,
                        encodingSize,
                        memoryAllocator,
                        configuration,
                        entriesCollector,
                        converter,
                        clearTransparentPixels),

                    16 => new TiffGrayL16Writer<TPixel>(
                        image,
                        encodingSize,
                        memoryAllocator,
                        configuration,
                        entriesCollector,
                        converter,
                        clearTransparentPixels,
                        whiteIsZero),

                    _ => new TiffGrayWriter<TPixel>(
                        image,
                        encodingSize,
                        memoryAllocator,
                        configuration,
                        entriesCollector,
                        converter,
                        clearTransparentPixels,
                        whiteIsZero)
                };

            default:
                return bitsPerPixel switch
                {
                    48 or 64 => new TiffRgb16Writer<TPixel>(
                        image,
                        encodingSize,
                        memoryAllocator,
                        configuration,
                        entriesCollector,
                        bitsPerPixel,
                        converter,
                        clearTransparentPixels),

                    96 or 128 => new TiffRgb32Writer<TPixel>(
                        image,
                        encodingSize,
                        memoryAllocator,
                        configuration,
                        entriesCollector,
                        bitsPerPixel,
                        converter,
                        clearTransparentPixels),

                    _ => new TiffRgbWriter<TPixel>(
                        image,
                        encodingSize,
                        memoryAllocator,
                        configuration,
                        entriesCollector,
                        converter,
                        clearTransparentPixels)
                };
        }
    }
}
