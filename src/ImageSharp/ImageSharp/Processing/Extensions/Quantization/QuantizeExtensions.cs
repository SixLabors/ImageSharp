// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

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
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Quantize(this IImageProcessingContext source) =>
            Quantize(source, KnownQuantizers.Octree);

        /// <summary>
        /// Applies quantization to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="quantizer">The quantizer to apply to perform the operation.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Quantize(this IImageProcessingContext source, IQuantizer quantizer) =>
            source.ApplyProcessor(new QuantizeProcessor(quantizer));
    }
}