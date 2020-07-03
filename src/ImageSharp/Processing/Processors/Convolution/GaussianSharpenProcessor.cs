// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Defines Gaussian sharpening by a (Sigma, Radius) pair.
    /// </summary>
    public sealed class GaussianSharpenProcessor : IImageProcessor
    {
        /// <summary>
        /// The default value for <see cref="Sigma"/>.
        /// </summary>
        public const float DefaultSigma = 3f;

        /// <summary>
        /// Initializes a new instance of the <see cref="GaussianSharpenProcessor"/> class.
        /// </summary>
        public GaussianSharpenProcessor()
            : this(DefaultSigma, ConvolutionProcessorHelpers.GetDefaultGaussianRadius(DefaultSigma))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GaussianSharpenProcessor"/> class.
        /// </summary>
        /// <param name="sigma">The 'sigma' value representing the weight of the blur.</param>
        public GaussianSharpenProcessor(float sigma)
            : this(sigma, ConvolutionProcessorHelpers.GetDefaultGaussianRadius(sigma))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GaussianSharpenProcessor"/> class.
        /// </summary>
        /// <param name="radius">
        /// The 'radius' value representing the size of the area to sample.
        /// </param>
        public GaussianSharpenProcessor(int radius)
            : this(radius / 3F, radius)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GaussianSharpenProcessor"/> class.
        /// </summary>
        /// <param name="sigma">
        /// The 'sigma' value representing the weight of the blur.
        /// </param>
        /// <param name="radius">
        /// The 'radius' value representing the size of the area to sample.
        /// This should be at least twice the sigma value.
        /// </param>
        public GaussianSharpenProcessor(float sigma, int radius)
        {
            this.Sigma = sigma;
            this.Radius = radius;
        }

        /// <summary>
        /// Gets the sigma value representing the weight of the blur
        /// </summary>
        public float Sigma { get; }

        /// <summary>
        /// Gets the radius defining the size of the area to sample.
        /// </summary>
        public int Radius { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            where TPixel : unmanaged, IPixel<TPixel>
            => new GaussianSharpenProcessor<TPixel>(configuration, this, source, sourceRectangle);
    }
}
