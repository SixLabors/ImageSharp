// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Effects
{
    /// <summary>
    /// Defines an oil painting effect.
    /// </summary>
    public sealed class OilPaintingProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OilPaintingProcessor"/> class.
        /// </summary>
        /// <param name="levels">
        /// The number of intensity levels. Higher values result in a broader range of color intensities forming part of the result image.
        /// </param>
        /// <param name="brushSize">
        /// The number of neighboring pixels used in calculating each individual pixel value.
        /// </param>
        public OilPaintingProcessor(int levels, int brushSize)
        {
            Guard.MustBeGreaterThan(levels, 0, nameof(levels));
            Guard.MustBeGreaterThan(brushSize, 0, nameof(brushSize));

            this.Levels = levels;
            this.BrushSize = brushSize;
        }

        /// <summary>
        /// Gets the number of intensity levels.
        /// </summary>
        public int Levels { get; }

        /// <summary>
        /// Gets the brush size.
        /// </summary>
        public int BrushSize { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            where TPixel : unmanaged, IPixel<TPixel>
            => new OilPaintingProcessor<TPixel>(configuration, this, source, sourceRectangle);
    }
}
