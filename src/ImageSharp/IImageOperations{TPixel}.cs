// <copyright file="IImageFormat.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using ImageSharp.Formats;
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing;
    using SixLabors.Primitives;

    /// <summary>
    /// An interface to queue up image operations.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format</typeparam>
    public interface IImageOperations<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Adds the processor to the current setr of image operations to be applied.
        /// </summary>
        /// <param name="processor">The processor to apply</param>
        /// <param name="rectangle">The area to apply it to</param>
        /// <returns>returns the current optinoatins class to allow chaining of oprations.</returns>
        IImageOperations<TPixel> ApplyProcessor(IImageProcessor<TPixel> processor, Rectangle rectangle);

        /// <summary>
        /// Adds the processor to the current setr of image operations to be applied.
        /// </summary>
        /// <param name="processor">The processor to apply</param>
        /// <returns>returns the current optinoatins class to allow chaining of oprations.</returns>
        IImageOperations<TPixel> ApplyProcessor(IImageProcessor<TPixel> processor);
    }
}