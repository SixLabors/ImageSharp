// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Applies bokeh blur processing to the image.
    /// </summary>
    public sealed class BokehBlurProcessor : IImageProcessor
    {
        /// <summary>
        /// The default radius used by the parameterless constructor.
        /// </summary>
        public const int DefaultRadius = 32;

        /// <summary>
        /// The default component count used by the parameterless constructor.
        /// </summary>
        public const int DefaultComponents = 2;

        /// <summary>
        /// The default gamma used by the parameterless constructor.
        /// </summary>
        public const float DefaultGamma = 3F;

        /// <summary>
        /// Initializes a new instance of the <see cref="BokehBlurProcessor"/> class.
        /// </summary>
        public BokehBlurProcessor()
            : this(DefaultRadius, DefaultComponents, DefaultGamma)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BokehBlurProcessor"/> class.
        /// </summary>
        /// <param name="radius">
        /// The 'radius' value representing the size of the area to sample.
        /// </param>
        /// <param name="components">
        /// The number of components to use to approximate the original 2D bokeh blur convolution kernel.
        /// </param>
        /// <param name="gamma">
        /// The gamma highlight factor to use to further process the image.
        /// </param>
        public BokehBlurProcessor(int radius, int components, float gamma)
        {
            Guard.MustBeGreaterThanOrEqualTo(gamma, 1, nameof(gamma));

            this.Radius = radius;
            this.Components = components;
            this.Gamma = gamma;
        }

        /// <summary>
        /// Gets the radius.
        /// </summary>
        public int Radius { get; }

        /// <summary>
        /// Gets the number of components.
        /// </summary>
        public int Components { get; }

        /// <summary>
        /// Gets the gamma highlight factor to use when applying the effect.
        /// </summary>
        public float Gamma { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            where TPixel : struct, IPixel<TPixel>
            => new BokehBlurProcessor<TPixel>(configuration, this, source, sourceRectangle);
    }
}
