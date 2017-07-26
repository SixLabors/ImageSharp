// <copyright file="RotateFlip.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    using ImageSharp.PixelFormats;

    using ImageSharp.Processing;

    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Queues up an operation that provides access to the mutatable image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to rotate, flip, or both.</param>
        /// <param name="operation">The operations to perform on the source.</param>
        /// <returns>returns the current operations class to allow chaining of operations.</returns>
        public static IImageProcessingContext<TPixel> Apply<TPixel>(this IImageProcessingContext<TPixel> source, Action<Image<TPixel>> operation)
                where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new DelegateProcessor<TPixel>(operation));
    }
}
