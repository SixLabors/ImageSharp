// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Overlays
{
    /// <summary>
    /// Defines a radial vignette effect applicable to an <see cref="Image"/>.
    /// </summary>
    public sealed class VignetteProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VignetteProcessor" /> class.
        /// </summary>
        /// <param name="options">The options effecting blending and composition.</param>
        /// <param name="color">The color of the vignette.</param>
        public VignetteProcessor(GraphicsOptions options, Color color)
        {
            this.VignetteColor = color;
            this.GraphicsOptions = options;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VignetteProcessor" /> class.
        /// </summary>
        /// <param name="options">The options effecting blending and composition.</param>
        /// <param name="color">The color of the vignette.</param>
        /// <param name="radiusX">The x-radius.</param>
        /// <param name="radiusY">The y-radius.</param>
        internal VignetteProcessor(GraphicsOptions options, Color color, ValueSize radiusX, ValueSize radiusY)
        {
            this.VignetteColor = color;
            this.RadiusX = radiusX;
            this.RadiusY = radiusY;
            this.GraphicsOptions = options;
        }

        /// <summary>
        /// Gets the options effecting blending and composition
        /// </summary>
        public GraphicsOptions GraphicsOptions { get; }

        /// <summary>
        /// Gets the vignette color to apply.
        /// </summary>
        public Color VignetteColor { get; }

        /// <summary>
        /// Gets the the x-radius.
        /// </summary>
        internal ValueSize RadiusX { get; }

        /// <summary>
        /// Gets the the y-radius.
        /// </summary>
        internal ValueSize RadiusY { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            where TPixel : unmanaged, IPixel<TPixel>
            => new VignetteProcessor<TPixel>(configuration, this, source, sourceRectangle);
    }
}
