// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// The base class for all cloning image processors.
    /// </summary>
    public abstract class CloningImageProcessor : IImageProcessor
    {
        /// <summary>
        /// Creates a pixel specific <see cref="ICloningImageProcessor{TPixel}"/> that is capable of executing
        /// the processing algorithm on an <see cref="Image{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <returns>The <see cref="ICloningImageProcessor{TPixel}"/></returns>
        public abstract ICloningImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Image<TPixel> source, Rectangle sourceRectangle)
            where TPixel : struct, IPixel<TPixel>;

        /// <inheritdoc/>
        IImageProcessor<TPixel> IImageProcessor.CreatePixelSpecificProcessor<TPixel>(Image<TPixel> source, Rectangle sourceRectangle)
            => this.CreatePixelSpecificProcessor(source, sourceRectangle);
    }
}
