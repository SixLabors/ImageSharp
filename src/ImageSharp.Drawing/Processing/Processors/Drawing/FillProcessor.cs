// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Drawing
{
    /// <summary>
    /// Defines a processor to fill an <see cref="Image"/> with the given <see cref="IBrush"/>
    /// using blending defined by the given <see cref="GraphicsOptions"/>.
    /// </summary>
    public class FillProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FillProcessor"/> class.
        /// </summary>
        /// <param name="options">The <see cref="GraphicsOptions"/> defining how to blend the brush pixels over the image pixels.</param>
        /// <param name="brush">The brush to use for filling.</param>
        public FillProcessor(GraphicsOptions options, IBrush brush)
        {
            this.Brush = brush;
            this.Options = options;
        }

        /// <summary>
        /// Gets the <see cref="IBrush"/> used for filling the destination image.
        /// </summary>
        public IBrush Brush { get; }

        /// <summary>
        /// Gets the <see cref="GraphicsOptions"/> defining how to blend the brush pixels over the image pixels.
        /// </summary>
        public GraphicsOptions Options { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            where TPixel : struct, IPixel<TPixel>
            => new FillProcessor<TPixel>(configuration, this, source, sourceRectangle);
    }
}
