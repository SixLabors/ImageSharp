// <copyright file="ResamplingWeightedProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Buffers;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Provides methods that allow the resizing of images using various algorithms.
    /// Adapted from <see href="http://www.realtimerendering.com/resources/GraphicsGems/gemsiii/filter_rcg.c"/>
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    internal abstract class ResamplingWeightedProcessor<TColor> : ImageProcessor<TColor>
        where TColor : struct, IPixel<TColor>
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
        protected Weights.Buffer HorizontalWeights { get; set; }

        /// <summary>
        /// Gets or sets the vertical weights.
        /// </summary>
        protected Weights.Buffer VerticalWeights { get; set; }

        /// <inheritdoc/>
        protected override void BeforeApply(ImageBase<TColor> source, Rectangle sourceRectangle)
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

        protected override void AfterApply(ImageBase<TColor> source, Rectangle sourceRectangle)
        {
            base.AfterApply(source, sourceRectangle);
            this.HorizontalWeights?.Dispose();
            this.HorizontalWeights = null;
            this.VerticalWeights?.Dispose();
            this.VerticalWeights = null;
        }

        /// <summary>
        /// Computes the weights to apply at each pixel when resizing.
        /// </summary>
        protected unsafe Weights.Buffer PrecomputeWeights(int destinationSize, int sourceSize)
        {
            float ratio = (float)sourceSize / destinationSize;
            float scale = ratio;

            if (scale < 1F)
            {
                scale = 1F;
            }

            IResampler sampler = this.Sampler;
            float radius = (float)Math.Ceiling(scale * sampler.Radius);
            Weights.Buffer result = new Weights.Buffer(sourceSize, destinationSize);
            
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

                result.Weights[i] = result.Slice(i, left, right);
                Weights ws = result.Weights[i];

                float* weights = ws.Ptr;

                for (int j = left; j <= right; j++)
                {
                    float weight = sampler.GetValue((j - center) / scale);
                    sum += weight;
                    weights[j - left] = weight;
                }

                // Normalise, best to do it here rather than in the pixel loop later on.
                if (sum > 0)
                {
                    for (int w = 0; w < ws.Length; w++)
                    {
                        weights[w] = weights[w] / sum;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Represents a collection of weights and their sum.
        /// </summary>
        protected unsafe struct Weights
        {
            /// <summary>
            /// The local left index position
            /// </summary>
            public int Left;

            public BufferSpan<float> Span;

            public float* Ptr => (float*)Span.PointerAtOffset;

            public int Length => Span.Length;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Weights(int left, BufferSpan<float> span)
            {
                this.Left = left;
                this.Span = span;
            }

            internal unsafe class Buffer : IDisposable
            {
                private PinnedImageBuffer<float> dataBuffer;

                public Weights[] Weights { get; }

                public float* DataPtr { get; }

                internal Weights Slice(int i, int leftIdx, int rightIdx)
                {
                    var span = dataBuffer.GetRowSpan(i).Slice(leftIdx, rightIdx - leftIdx);
                    return new Weights(leftIdx, span);
                }

                public Buffer(int sourceSize, int destinationSize)
                {
                    this.dataBuffer = new PinnedImageBuffer<float>(sourceSize, destinationSize);
                    this.dataBuffer.Clear();
                    this.DataPtr = (float*)this.dataBuffer.Pointer;
                    this.Weights = new Weights[destinationSize];
                }

                public void Dispose()
                {
                    this.dataBuffer.Dispose();
                }
            }
        }
    }
}