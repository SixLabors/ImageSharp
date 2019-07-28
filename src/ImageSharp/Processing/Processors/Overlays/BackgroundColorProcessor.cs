// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Overlays
{
    /// <summary>
    /// Defines a processing operation to replace the background color of an <see cref="Image"/>.
    /// </summary>
    public sealed class BackgroundColorProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BackgroundColorProcessor"/> class.
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to set the background color to.</param>
        /// <param name="options">The options defining blending algorithm and amount.</param>
        public BackgroundColorProcessor(Color color, GraphicsOptions options)
        {
            this.Color = color;
            this.GraphicsOptions = options;
        }

        /// <summary>
        /// Gets the Graphics options to alter how processor is applied.
        /// </summary>
        public GraphicsOptions GraphicsOptions { get; }

        /// <summary>
        /// Gets the background color value.
        /// </summary>
        public Color Color { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>()
            where TPixel : struct, IPixel<TPixel>
        {
            return new BackgroundColorProcessor<TPixel>(this);
        }
    }
}