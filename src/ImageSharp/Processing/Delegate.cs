// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Applies the given operation to the mutable image.
        /// Useful when we need to extract information like Width/Height to parameterize the next operation working on the <see cref="IImageProcessingContext{TPixel}"/> chain.
        /// To achieve this the method actually implements an "inline" <see cref="IImageProcessor{TPixel}"/> with <paramref name="operation"/> as it's processing logic.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to mutate.</param>
        /// <param name="operation">The operation to perform on the source.</param>
        /// <returns>The <see cref="IImageProcessingContext{TPixel}"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext<TPixel> Apply<TPixel>(this IImageProcessingContext<TPixel> source, Action<Image<TPixel>> operation)
                where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new DelegateProcessor<TPixel>(operation));
    }
}
