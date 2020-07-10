// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Adjusts an image so that its orientation is suitable for viewing. Adjustments are based on EXIF metadata embedded in the image.
    /// </summary>
    public sealed class AutoOrientProcessor : IImageProcessor
    {
        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            where TPixel : unmanaged, IPixel<TPixel>
            => new AutoOrientProcessor<TPixel>(configuration, source, sourceRectangle);
    }
}
