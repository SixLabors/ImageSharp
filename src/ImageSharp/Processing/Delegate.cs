﻿// <copyright file="RotateFlip.cs" company="James Jackson-South">
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
        /// Applies the given operation to the mutable image.
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
