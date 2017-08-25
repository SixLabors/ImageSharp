// <copyright file="ResamplingWeightedProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Runtime.CompilerServices;

    using ImageSharp.PixelFormats;
    using SixLabors.Primitives;

    /// <summary>
    /// Provides methods that allow the resizing of images using various algorithms.
    /// Adapted from <see href="http://www.realtimerendering.com/resources/GraphicsGems/gemsiii/filter_rcg.c"/>
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal abstract partial class ResamplingWeightedProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ResamplingWeightedProcessor{TPixel}"/> class.
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
        protected WeightsBuffer HorizontalWeights { get; set; }

        /// <summary>
        /// Gets or sets the vertical weights.
        /// </summary>
        protected WeightsBuffer VerticalWeights { get; set; }

        /// <summary>
        /// Computes the weights to apply at each pixel when resizing.
        /// </summary>
        /// <param name="destinationSize">The destination size</param>
        /// <param name="sourceSize">The source size</param>
        /// <returns>The <see cref="WeightsBuffer"/></returns>
        // TODO: Made internal to simplify experimenting with weights data. Make it protected again when finished figuring out how to optimize all the stuff!
        internal unsafe WeightsBuffer PrecomputeWeights(int destinationSize, int sourceSize)
        {
            float ratio = (float)sourceSize / destinationSize;
            float scale = ratio;

            if (scale < 1F)
            {
                scale = 1F;
            }

            IResampler sampler = this.Sampler;
            float radius = MathF.Ceiling(scale * sampler.Radius);
            var result = new WeightsBuffer(sourceSize, destinationSize);

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

                WeightsWindow ws = result.GetWeightsWindow(i, left, right);
                result.Weights[i] = ws;

                ref float weightsBaseRef = ref ws.GetStartReference();

                for (int j = left; j <= right; j++)
                {
                    float weight = sampler.GetValue((j - center) / scale);
                    sum += weight;

                    // weights[j - left] = weight:
                    Unsafe.Add(ref weightsBaseRef, j - left) = weight;
                }

                // Normalise, best to do it here rather than in the pixel loop later on.
                if (sum > 0)
                {
                    for (int w = 0; w < ws.Length; w++)
                    {
                        // weights[w] = weights[w] / sum:
                        ref float wRef = ref Unsafe.Add(ref weightsBaseRef, w);
                        wRef = wRef / sum;
                    }
                }
            }

            return result;
        }

        /// <inheritdoc/>
        protected override void BeforeApply(ImageBase<TPixel> source, Rectangle sourceRectangle)
        {
            if (!(this.Sampler is NearestNeighborResampler))
            {
                this.HorizontalWeights = this.PrecomputeWeights(
                    this.ResizeRectangle.Width,
                    sourceRectangle.Width);

                this.VerticalWeights = this.PrecomputeWeights(
                    this.ResizeRectangle.Height,
                    sourceRectangle.Height);
            }
        }

        /// <inheritdoc />
        protected override void AfterApply(ImageBase<TPixel> source, Rectangle sourceRectangle)
        {
            base.AfterApply(source, sourceRectangle);
            this.HorizontalWeights?.Dispose();
            this.HorizontalWeights = null;
            this.VerticalWeights?.Dispose();
            this.VerticalWeights = null;
        }
    }
}