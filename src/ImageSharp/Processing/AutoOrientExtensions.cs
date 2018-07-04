// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds extensions that allow the application of auto-orientation operations to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class AutoOrientExtensions
    {
        /// <summary>
        /// Adjusts an image so that its orientation is suitable for viewing. Adjustments are based on EXIF metadata embedded in the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to auto rotate.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> AutoOrient<TPixel>(this IImageProcessingContext<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new AutoOrientProcessor<TPixel>());
    }
}