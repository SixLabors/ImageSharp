// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// The base class for all cloning image processors.
    /// </summary>
    public abstract class CloningImageProcessor : ICloningImageProcessor
    {
        /// <inheritdoc/>
        public abstract ICloningImageProcessor<TPixel> CreatePixelSpecificCloningProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            where TPixel : unmanaged, IPixel<TPixel>;

        /// <inheritdoc/>
        IImageProcessor<TPixel> IImageProcessor.CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            => this.CreatePixelSpecificCloningProcessor(configuration, source, sourceRectangle);
    }
}
