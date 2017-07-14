// <copyright file="AutoOrient.cs" company="James Jackson-South">
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
        /// Adjusts an image so that its orientation is suitable for viewing. Adjustments are based on EXIF metadata embedded in the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to auto rotate.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessorApplicator<TPixel> AutoOrient<TPixel>(this IImageProcessorApplicator<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new Processing.Processors.AutoRotateProcessor<TPixel>());
    }
}