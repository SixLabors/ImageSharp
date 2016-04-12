// <copyright file="Resampler.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Samplers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

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
            float xscale = destinationSize / (float)sourceSize;
            float width;
            IResampler sampler = this.Sampler;
            float fwidth = sampler.Radius;
            float fscale;
            double left;
            double right;
            double weight = 0;
            int n = 0;
            int k;

            Weights[] result = new Weights[destinationSize];

            // When expanding, broaden the effective kernel support so that we still
            // visit every source pixel.
            if (xscale < 0)
            {
                width = sampler.Radius / xscale;
                fscale = 1 / xscale;

                // Make the weights slices, one source for each column or row.
                for (int i = 0; i < destinationSize; i++)
                {
                    float centre = i / xscale;
                    left = Math.Ceiling(centre - width);
                    right = Math.Floor(centre + width);
                    float sum = 0;
                    result[i] = new Weights();
                    List<Weight> builder = new List<Weight>();
                    for (double j = left; j <= right; j++)
                    {
                        weight = centre - j;
                        weight = sampler.GetValue((float)weight / fscale) / fscale;
                        if (j < 0)
                        {
                            n = (int)-j;
                        }
                        else if (j >= sourceSize)
                        {
                            n = (int)((sourceSize - j) + sourceSize - 1);
                        }
                        else
                        {
                            n = (int)j;
                        }

                        sum++;
                        builder.Add(new Weight(n, (float)weight));
                    }

                    result[i].Values = builder.ToArray();
                    result[i].Sum = sum;
                }
            }
            else
            {
                // Make the weights slices, one source for each column or row.
                for (int i = 0; i < destinationSize; i++)
                {
                    float centre = i / xscale;
                    left = Math.Ceiling(centre - fwidth);
                    right = Math.Floor(centre + fwidth);
                    float sum = 0;
                    result[i] = new Weights();

                    List<Weight> builder = new List<Weight>();
                    for (double j = left; j <= right; j++)
                    {
                        weight = centre - j;
                        weight = sampler.GetValue((float)weight);
                        if (j < 0)
                        {
                            n = (int)-j;
                        }
                        else if (j >= sourceSize)
                        {
                            n = (int)((sourceSize - j) + sourceSize - 1);
                        }
                        else
                        {
                            n = (int)j;
                        }

                        sum++;
                        builder.Add(new Weight(n, (float)weight));
                    }

                    result[i].Values = builder.ToArray();
                    result[i].Sum = sum;
                }
            }

            return result;
        }

        //protected Weights[] PrecomputeWeights(int destinationSize, int sourceSize)
        //{
        //    IResampler sampler = this.Sampler;
        //    float ratio = sourceSize / (float)destinationSize;
        //    float scale = ratio;

        //    // When shrinking, broaden the effective kernel support so that we still
        //    // visit every source pixel.
        //    if (scale < 1)
        //    {
        //        scale = 1;
        //    }

        //    float scaledRadius = (float)Math.Ceiling(scale * sampler.Radius);
        //    Weights[] result = new Weights[destinationSize];

        //    // Make the weights slices, one source for each column or row.
        //    Parallel.For(
        //        0,
        //        destinationSize,
        //        i =>
        //        {
        //            float center = ((i + .5f) * ratio) - 0.5f;
        //            int start = (int)Math.Ceiling(center - scaledRadius);

        //            if (start < 0)
        //            {
        //                start = 0;
        //            }

        //            int end = (int)Math.Floor(center + scaledRadius);

        //            if (end > sourceSize)
        //            {
        //                end = sourceSize;

        //                if (end < start)
        //                {
        //                    end = start;
        //                }
        //            }

        //            float sum = 0;
        //            result[i] = new Weights();

        //            List<Weight> builder = new List<Weight>();
        //            for (int a = start; a < end; a++)
        //            {
        //                float w = sampler.GetValue((a - center) / scale);

        //                if (w < 0 || w > 0)
        //                {
        //                    sum += w;
        //                    builder.Add(new Weight(a, w));
        //                }
        //            }

        //            // Normalise the values
        //            if (sum > 0 || sum < 0)
        //            {
        //                builder.ForEach(w => w.Value /= sum);
        //            }

        //            result[i].Values = builder.ToArray();
        //            result[i].Sum = sum;
        //        });

        //    return result;
        //}

        /// <summary>
        /// Represents the weight to be added to a scaled pixel.
        /// </summary>
        protected class Weight
        {
            /// <summary>
            /// The pixel index.
            /// </summary>
            public readonly int Index;

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

            /// <summary>
            /// Gets or sets the sum.
            /// </summary>
            public float Sum { get; set; }
        }
    }
}
