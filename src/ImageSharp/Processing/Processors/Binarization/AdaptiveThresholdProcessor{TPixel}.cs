// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

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
        private readonly PixelOperations<TPixel> pixelOpInstance;

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
            this.pixelOpInstance = PixelOperations<TPixel>.Instance;
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

            // Used ushort because the values should never exceed max ushort value.
            ushort startY = (ushort)intersect.Y;
            ushort endY = (ushort)intersect.Bottom;
            ushort startX = (ushort)intersect.X;
            ushort endX = (ushort)intersect.Right;

            ushort width = (ushort)intersect.Width;
            ushort height = (ushort)intersect.Height;

            // ClusterSize defines the size of cluster to used to check for average. Tweaked to support up to 4k wide pixels and not more. 4096 / 16 is 256 thus the '-1'
            byte clusterSize = (byte)Math.Truncate((width / 16f) - 1);

            // Using pooled 2d buffer for integer image table and temp memory to hold Rgb24 converted pixel data.
            using (Buffer2D<ulong> intImage = this.Configuration.MemoryAllocator.Allocate2D<ulong>(width, height))
            using (IMemoryOwner<Rgb24> tmpBuffer = this.Configuration.MemoryAllocator.Allocate<Rgb24>(width * height))
            {
                // Defines the rectangle section of the image to work on.
                var workingRectangle = Rectangle.FromLTRB(startX, startY, endX, endY);

                this.pixelOpInstance.ToRgb24(this.Configuration, source.GetPixelSpan(), tmpBuffer.GetSpan());

                for (ushort x = startX; x < endX; x++)
                {
                    Span<Rgb24> rgbSpan = tmpBuffer.GetSpan();
                    ulong sum = 0;
                    for (ushort y = startY; y < endY; y++)
                    {
                        ref Rgb24 rgb = ref rgbSpan[(width * y) + x];

                        sum += (ulong)(rgb.R + rgb.G + rgb.G);
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

                var operation = new RowOperation(workingRectangle, source, tmpBuffer, intImage, upper, lower, thresholdLimit, clusterSize, startX, endX, startY);
                ParallelRowIterator.IterateRows(
                    configuration,
                    workingRectangle,
                    in operation);
            }
        }

        private readonly struct RowOperation : IRowOperation
        {
            private readonly Rectangle bounds;
            private readonly ImageFrame<TPixel> source;
            private readonly IMemoryOwner<Rgb24> tmpBuffer;
            private readonly Buffer2D<ulong> intImage;
            private readonly TPixel upper;
            private readonly TPixel lower;
            private readonly float thresholdLimit;
            private readonly ushort startX;
            private readonly ushort endX;
            private readonly ushort startY;
            private readonly byte clusterSize;

            [MethodImpl(InliningOptions.ShortMethod)]
            public RowOperation(
                Rectangle bounds,
                ImageFrame<TPixel> source,
                IMemoryOwner<Rgb24> tmpBuffer,
                Buffer2D<ulong> intImage,
                TPixel upper,
                TPixel lower,
                float thresholdLimit,
                byte clusterSize,
                ushort startX,
                ushort endX,
                ushort startY)
            {
                this.bounds = bounds;
                this.source = source;
                this.tmpBuffer = tmpBuffer;
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
                Span<Rgb24> rgbSpan = this.tmpBuffer.GetSpan();
                ushort x1, y1, x2, y2;

                for (ushort x = this.startX; x < this.endX; x++)
                {
                    ref Rgb24 rgb = ref rgbSpan[(this.bounds.Width * y) + x];

                    x1 = (ushort)Math.Max(x - this.startX - this.clusterSize + 1, 0);
                    x2 = (ushort)Math.Min(x - this.startX + this.clusterSize + 1, this.bounds.Width - 1);
                    y1 = (ushort)Math.Max(y - this.startY - this.clusterSize + 1, 0);
                    y2 = (ushort)Math.Min(y - this.startY + this.clusterSize + 1, this.bounds.Height - 1);

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
