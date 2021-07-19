// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Normalization
{
    /// <summary>
    /// Normalize an image by stretching the dynamic range to full contrast
    /// Applicable to an <see cref="Image"/>.
    /// </summary>
    public class AutoLevelProcessor : IImageProcessor
    {
        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            where TPixel : unmanaged, IPixel<TPixel>
            => new AutoLevelProcessor<TPixel>(configuration, source, sourceRectangle);
    }
}
