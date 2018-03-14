// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Quantization.Processors;

namespace SixLabors.ImageSharp.Processing.Quantization
{
    /// <summary>
    /// Adds extensions that allow the application of quantizing algorithms to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class QuantizeExtensions
    {
        /// <summary>
        /// Applies quantization to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="mode">The quantization mode to apply to perform the operation.</param>
        /// <param name="maxColors">The maximum number of colors to return. Defaults to 256.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Quantize<TPixel>(this IImageProcessingContext<TPixel> source, QuantizationMode mode = QuantizationMode.Octree, int maxColors = 256)
            where TPixel : struct, IPixel<TPixel>
        {
            IQuantizer<TPixel> quantizer;
            switch (mode)
            {
                case QuantizationMode.Wu:
                    quantizer = new WuQuantizer<TPixel>();
                    break;

                case QuantizationMode.Palette:
                    quantizer = new PaletteQuantizer<TPixel>();
                    break;

                default:
                    quantizer = new OctreeQuantizer<TPixel>();
                    break;
            }

            return Quantize(source, quantizer, maxColors);
        }

        /// <summary>
        /// Applies quantization to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="quantizer">The quantizer to apply to perform the operation.</param>
        /// <param name="maxColors">The maximum number of colors to return.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Quantize<TPixel>(this IImageProcessingContext<TPixel> source, IQuantizer<TPixel> quantizer, int maxColors)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new QuantizeProcessor<TPixel>(quantizer, maxColors));
    }
}