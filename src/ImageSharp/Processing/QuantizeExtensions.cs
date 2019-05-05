// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Defines extensions that allow the application of quantizing algorithms on an <see cref="Image"/>
    /// using Mutate/Clone.
    /// </summary>
    public static class QuantizeExtensions
    {
        /// <summary>
        /// Applies quantization to the image using the <see cref="OctreeQuantizer"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext<TPixel> Quantize<TPixel>(this IImageProcessingContext<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => Quantize(source, KnownQuantizers.Octree);

        /// <summary>
        /// Applies quantization to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="quantizer">The quantizer to apply to perform the operation.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext<TPixel> Quantize<TPixel>(this IImageProcessingContext<TPixel> source, IQuantizer quantizer)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new QuantizeProcessor<TPixel>(quantizer));
    }
}