// <copyright file="ResamplingWeightedProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;

    /// <summary>
    /// Provides methods that allow the resizing of images using various algorithms.
    /// Adapted from <see href="http://www.realtimerendering.com/resources/GraphicsGems/gemsiii/filter_rcg.c"/>
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public abstract class ResamplingWeightedProcessor<TColor> : ImageProcessor<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ResamplingWeightedProcessor{TColor}"/> class.
        /// </summary>
        /// <param name="sampler">The sampler to perform the resize operation.</param>
        /// <param name="width">The target width.</param>
        /// <param name="height">The target height.</param>
        /// <param name="resizeRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the target image object to draw to.
        /// </param>
        protected ResamplingWeightedProcessor(IResampler sampler, int width, int height, Rectangle resizeRectangle)
        {
            Guard.NotNull(sampler, nameof(sampler));
            Guard.MustBeGreaterThan(width, 0, nameof(width));
            Guard.MustBeGreaterThan(height, 0, nameof(height));

            this.Sampler = sampler;
            this.Width = width;
            this.Height = height;
            this.ResizeRectangle = resizeRectangle;
        }

        /// <summary>
        /// Gets the sampler to perform the resize operation.
        /// </summary>
        public IResampler Sampler { get; }

        /// <summary>
        /// Gets the width.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the height.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets the resize rectangle.
        /// </summary>
        public Rectangle ResizeRectangle { get; }

        /// <summary>
        /// Gets or sets the horizontal weights.
        /// </summary>
        protected Weights[] HorizontalWeights { get; set; }

        /// <summary>
        /// Gets or sets the vertical weights.
        /// </summary>
        protected Weights[] VerticalWeights { get; set; }

        /// <inheritdoc/>
        protected override void BeforeApply(ImageBase<TColor> source, Rectangle sourceRectangle)
        {
            if (!(this.Sampler is NearestNeighborResampler))
            {
                this.HorizontalWeights = this.PrecomputeWeights(this.ResizeRectangle.Width, sourceRectangle.Width);
                this.VerticalWeights = this.PrecomputeWeights(this.ResizeRectangle.Height, sourceRectangle.Height);
            }
        }

        /// <summary>
        /// Computes the weights to apply at each pixel when resizing.
        /// </summary>
        /// <param name="destinationSize">The destination section size.</param>
        /// <param name="sourceSize">The source section size.</param>
        /// <returns>
        /// The <see cref="T:Weights[]"/>.
        /// </returns>
        protected Weights[] PrecomputeWeights(int destinationSize, int sourceSize)
        {
            float ratio = (float)sourceSize / destinationSize;
            float scale = ratio;

            if (scale < 1F)
            {
                scale = 1F;
            }

            IResampler sampler = this.Sampler;
            float radius = (float)Math.Ceiling(scale * sampler.Radius);
            Weights[] result = new Weights[destinationSize];

            for (int i = 0; i < destinationSize; i++)
            {
                float center = ((i + .5F) * ratio) - .5F;

                // Keep inside bounds.
                int left = (int)Math.Ceiling(center - radius);
                if (left < 0)
                {
                    left = 0;
                }

                int right = (int)Math.Floor(center + radius);
                if (right > sourceSize - 1)
                {
                    right = sourceSize - 1;
                }

                float sum = 0;
                result[i] = new Weights();
                Weight[] weights = new Weight[right - left + 1];

                for (int j = left; j <= right; j++)
                {
                    float weight = sampler.GetValue((j - center) / scale);
                    sum += weight;
                    weights[j - left] = new Weight(j, weight);
                }

                // Normalise, best to do it here rather than in the pixel loop later on.
                if (sum > 0)
                {
                    for (int w = 0; w < weights.Length; w++)
                    {
                        weights[w].Value = weights[w].Value / sum;
                    }
                }

                result[i].Values = weights;
            }

            return result;
        }

        /// <summary>
        /// Represents the weight to be added to a scaled pixel.
        /// </summary>
        protected struct Weight
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Weight"/> struct.
            /// </summary>
            /// <param name="index">The index.</param>
            /// <param name="value">The value.</param>
            public Weight(int index, float value)
            {
                this.Index = index;
                this.Value = value;
            }

            /// <summary>
            /// Gets the pixel index.
            /// </summary>
            public int Index { get; }

            /// <summary>
            /// Gets or sets the result of the interpolation algorithm.
            /// </summary>
            public float Value { get; set; }
        }

        /// <summary>
        /// Represents a collection of weights and their sum.
        /// </summary>
        protected class Weights
        {
            /// <summary>
            /// Gets or sets the values.
            /// </summary>
            public Weight[] Values { get; set; }
        }
    }
}