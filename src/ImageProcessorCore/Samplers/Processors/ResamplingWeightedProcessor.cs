// <copyright file="ResamplingWeightedProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System;

    /// <summary>
    /// Provides methods that allow the resizing of images using various algorithms.
    /// </summary>
    /// <typeparam name="T">The pixel format.</typeparam>
    /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
    public abstract class ResamplingWeightedProcessor<T, TP> : ImageSampler<T, TP>
        where T : IPackedVector<TP>
        where TP : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ResamplingWeightedProcessor{T,TP}"/> class.
        /// </summary>
        /// <param name="sampler">
        /// The sampler to perform the resize operation.
        /// </param>
        protected ResamplingWeightedProcessor(IResampler sampler)
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

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<T, TP> target, ImageBase<T, TP> source, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            if (!(this.Sampler is NearestNeighborResampler))
            {
                this.HorizontalWeights = this.PrecomputeWeights(targetRectangle.Width, sourceRectangle.Width);
                this.VerticalWeights = this.PrecomputeWeights(targetRectangle.Height, sourceRectangle.Height);
            }
        }

        /// <inheritdoc/>
        protected override void AfterApply(ImageBase<T, TP> target, ImageBase<T, TP> source, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            // Copy the pixels over.
            if (source.Bounds == target.Bounds && sourceRectangle == targetRectangle)
            {
                target.ClonePixels(target.Width, target.Height, source.Pixels);
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
            float scale = (float)destinationSize / sourceSize;
            IResampler sampler = this.Sampler;
            float radius = sampler.Radius;
            double left;
            double right;
            float weight;
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
                        Values = new Weight[(int)(right - left + 1)]
                    };

                    for (double j = left; j <= right; j++)
                    {
                        weight = sampler.GetValue((float)((centre - j) / filterScale)) / filterScale;
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
                        result[i].Values[sum] = new Weight(index, weight);
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
                        Values = new Weight[(int)(right - left + 1)]
                    };

                    for (double j = left; j <= right; j++)
                    {
                        weight = sampler.GetValue((float)(centre - j));
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
                        result[i].Values[sum] = new Weight(index, weight);
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