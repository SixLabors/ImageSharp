// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;

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
        /// <param name="color">The color of the vignette.</param>
        public VignetteProcessor(Color color)
            : this(color, GraphicsOptions.Default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VignetteProcessor" /> class.
        /// </summary>
        /// <param name="color">The color of the vignette.</param>
        /// <param name="options">The options effecting blending and composition.</param>
        public VignetteProcessor(Color color, GraphicsOptions options)
        {
            this.VignetteColor = color;
            this.GraphicsOptions = options;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VignetteProcessor" /> class.
        /// </summary>
        /// <param name="color">The color of the vignette.</param>
        /// <param name="radiusX">The x-radius.</param>
        /// <param name="radiusY">The y-radius.</param>
        /// <param name="options">The options effecting blending and composition.</param>
        internal VignetteProcessor(Color color, ValueSize radiusX, ValueSize radiusY, GraphicsOptions options)
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
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>()
            where TPixel : struct, IPixel<TPixel>
        {
            return new VignetteProcessor<TPixel>(this);
        }
    }
}