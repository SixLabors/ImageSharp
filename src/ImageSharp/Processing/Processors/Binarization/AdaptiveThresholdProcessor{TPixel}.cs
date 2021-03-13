// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Binarization
{
    /// <summary>
    /// Performs Bradley Adaptive Threshold filter against an image.
    /// </summary>
    internal class AdaptiveThresholdProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly AdaptiveThresholdProcessor definition;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdaptiveThresholdProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="definition">The <see cref="AdaptiveThresholdProcessor"/> defining the processor parameters.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public AdaptiveThresholdProcessor(Configuration configuration, AdaptiveThresholdProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            this.definition = definition;
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            var intersect = Rectangle.Intersect(this.SourceRectangle, source.Bounds());

            Configuration configuration = this.Configuration;
            TPixel upper = this.definition.Upper.ToPixel<TPixel>();
            TPixel lower = this.definition.Lower.ToPixel<TPixel>();
            float thresholdLimit = this.definition.ThresholdLimit;

            int startY = intersect.Y;
            int endY = intersect.Bottom;
            int startX = intersect.X;
            int endX = intersect.Right;

            int width = intersect.Width;
            int height = intersect.Height;

            // ClusterSize defines the size of cluster to used to check for average. Tweaked to support up to 4k wide pixels and not more. 4096 / 16 is 256 thus the '-1'
            byte clusterSize = (byte)Math.Truncate((width / 16f) - 1);

            // Using pooled 2d buffer for integer image table and temp memory to hold Rgb24 converted pixel data.
            using (Buffer2D<ulong> intImage = this.Configuration.MemoryAllocator.Allocate2D<ulong>(width, height))
            {
                Rgba32 rgb = default;
                for (int x = startX; x < endX; x++)
                {
                    ulong sum = 0;
                    for (int y = startY; y < endY; y++)
                    {
                        Span<TPixel> row = source.GetPixelRowSpan(y);
                        ref TPixel rowRef = ref MemoryMarshal.GetReference(row);
                        ref TPixel color = ref Unsafe.Add(ref rowRef, x);
                        color.ToRgba32(ref rgb);

                        sum += (ulong)(rgb.R + rgb.G + rgb.B);

                        if (x - startX != 0)
                        {
                            intImage[x - startX, y - startY] = intImage[x - startX - 1, y - startY] + sum;
                        }
                        else
                        {
                            intImage[x - startX, y - startY] = sum;
                        }
                    }
                }

                var operation = new RowOperation(intersect, source, intImage, upper, lower, thresholdLimit, clusterSize, startX, endX, startY);
                ParallelRowIterator.IterateRows(
                    configuration,
                    intersect,
                    in operation);
            }
        }

        private readonly struct RowOperation : IRowOperation
        {
            private readonly Rectangle bounds;
            private readonly ImageFrame<TPixel> source;
            private readonly Buffer2D<ulong> intImage;
            private readonly TPixel upper;
            private readonly TPixel lower;
            private readonly float thresholdLimit;
            private readonly int startX;
            private readonly int endX;
            private readonly int startY;
            private readonly byte clusterSize;

            [MethodImpl(InliningOptions.ShortMethod)]
            public RowOperation(
                Rectangle bounds,
                ImageFrame<TPixel> source,
                Buffer2D<ulong> intImage,
                TPixel upper,
                TPixel lower,
                float thresholdLimit,
                byte clusterSize,
                int startX,
                int endX,
                int startY)
            {
                this.bounds = bounds;
                this.source = source;
                this.intImage = intImage;
                this.upper = upper;
                this.lower = lower;
                this.thresholdLimit = thresholdLimit;
                this.startX = startX;
                this.endX = endX;
                this.startY = startY;
                this.clusterSize = clusterSize;
            }

            /// <inheritdoc/>
            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(int y)
            {
                Rgba32 rgb = default;
                Span<TPixel> pixelRow = this.source.GetPixelRowSpan(y);

                for (int x = this.startX; x < this.endX; x++)
                {
                    TPixel pixel = pixelRow[x];
                    pixel.ToRgba32(ref rgb);

                    var x1 = Math.Max(x - this.startX - this.clusterSize + 1, 0);
                    var x2 = Math.Min(x - this.startX + this.clusterSize + 1, this.bounds.Width - 1);
                    var y1 = Math.Max(y - this.startY - this.clusterSize + 1, 0);
                    var y2 = Math.Min(y - this.startY + this.clusterSize + 1, this.bounds.Height - 1);

                    var count = (uint)((x2 - x1) * (y2 - y1));
                    var sum = (long)Math.Min(this.intImage[x2, y2] - this.intImage[x1, y2] - this.intImage[x2, y1] + this.intImage[x1, y1], long.MaxValue);

                    if ((rgb.R + rgb.G + rgb.B) * count <= sum * this.thresholdLimit)
                    {
                        this.source[x, y] = this.lower;
                    }
                    else
                    {
                        this.source[x, y] = this.upper;
                    }
                }
            }
        }
    }
}
