// <copyright file="Resampler.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Samplers
{
    using System;

    /// <summary>
    /// Provides methods that allow the resampling of images using various algorithms.
    /// <see href="http://www.realtimerendering.com/resources/GraphicsGems/category.html#Image Processing_link"/>
    /// <see href="http://www.realtimerendering.com/resources/GraphicsGems/gemsiii/filter_rcg.c"/>
    /// </summary>
    public abstract class Resampler : ImageSampler
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Resampler"/> class.
        /// </summary>
        /// <param name="sampler">
        /// The sampler to perform the resize operation.
        /// </param>
        protected Resampler(IResampler sampler)
        {
            Guard.NotNull(sampler, nameof(sampler));

            this.Sampler = sampler;
        }

        /// <summary>
        /// Gets the sampler to perform the resize operation.
        /// </summary>
        public IResampler Sampler { get; }

        /// <summary>
        /// Gets or sets the horizontal weights.
        /// </summary>
        protected Weights[] HorizontalWeights { get; set; }

        /// <summary>
        /// Gets or sets the vertical weights.
        /// </summary>
        protected Weights[] VerticalWeights { get; set; }

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
            float scale = (float)destinationSize / sourceSize;
            IResampler sampler = this.Sampler;
            float radius = sampler.Radius;
            double left;
            double right;
            double weight;
            int index;
            int sum;

            Weights[] result = new Weights[destinationSize];

            // When shrinking, broaden the effective kernel support so that we still
            // visit every source pixel.
            if (scale < 1)
            {
                float width = radius / scale;
                float filterScale = 1 / scale;

                // Make the weights slices, one source for each column or row.
                for (int i = 0; i < destinationSize; i++)
                {
                    float centre = i / scale;
                    left = Math.Ceiling(centre - width);
                    right = Math.Floor(centre + width);

                    result[i] = new Weights
                    {
                        Sum = 0,
                        Values = new Weight[(int)Math.Floor((2 * width) + 1)]
                    };

                    for (double j = left; j <= right; j++)
                    {
                        weight = centre - j;
                        weight = sampler.GetValue((float)(weight / filterScale)) / filterScale;
                        if (j < 0)
                        {
                            index = (int)-j;
                        }
                        else if (j >= sourceSize)
                        {
                            index = (int)((sourceSize - j) + sourceSize - 1);
                        }
                        else
                        {
                            index = (int)j;
                        }

                        sum = (int)result[i].Sum++;
                        result[i].Values[sum] = new Weight(index, (float)weight);
                    }
                }
            }
            else
            {
                // Make the weights slices, one source for each column or row.
                for (int i = 0; i < destinationSize; i++)
                {
                    float centre = i / scale;
                    left = Math.Ceiling(centre - radius);
                    right = Math.Floor(centre + radius);
                    result[i] = new Weights
                    {
                        Sum = 0,
                        Values = new Weight[(int)((radius * 2) + 1)]
                    };

                    for (double j = left; j <= right; j++)
                    {
                        weight = centre - j;
                        weight = sampler.GetValue((float)weight);
                        if (j < 0)
                        {
                            index = (int)-j;
                        }
                        else if (j >= sourceSize)
                        {
                            index = (int)((sourceSize - j) + sourceSize - 1);
                        }
                        else
                        {
                            index = (int)j;
                        }

                        sum = (int)result[i].Sum++;
                        result[i].Values[sum] = new Weight(index, (float)weight);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Represents the weight to be added to a scaled pixel.
        /// </summary>
        protected struct Weight
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Weight"/> class.
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
            /// Gets the result of the interpolation algorithm.
            /// </summary>
            public float Value { get; }
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

            /// <summary>
            /// Gets or sets the sum.
            /// </summary>
            public float Sum { get; set; }
        }
    }
}