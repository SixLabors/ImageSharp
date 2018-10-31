// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.ParallelUtils;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Binarization
{
    /// <summary>
    /// Performs Bradley Adaptive Threshold filter against an image
    /// </summary>
    /// <typeparam name="TPixel">The pixel format of the image</typeparam>
    internal class AdaptiveThresholdProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly PixelOperations<TPixel> pixelOpInstance;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdaptiveThresholdProcessor{TPixel}"/> class.
        /// </summary>
        public AdaptiveThresholdProcessor()
            : this(NamedColors<TPixel>.White, NamedColors<TPixel>.Black, 0.85f)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AdaptiveThresholdProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="thresholdLimit">Threshold limit</param>
        public AdaptiveThresholdProcessor(float thresholdLimit)
            : this(NamedColors<TPixel>.White, NamedColors<TPixel>.Black, thresholdLimit)
        {
        }

        public AdaptiveThresholdProcessor(TPixel upper, TPixel lower)
            : this(upper, lower, 0.85f)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AdaptiveThresholdProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="upper">Color for upper threshold</param>
        /// <param name="lower">Color for lower threshold</param>
        /// <param name="thresholdLimit">Threshold limit</param>
        public AdaptiveThresholdProcessor(TPixel upper, TPixel lower, float thresholdLimit)
        {
            this.pixelOpInstance = PixelOperations<TPixel>.Instance;

            this.Upper = upper;
            this.Lower = lower;
            this.ThresholdLimit = thresholdLimit;
        }

        /// <summary>
        /// Gets or sets upper color limit for thresholding
        /// </summary>
        public TPixel Upper { get; set; }

        /// <summary>
        /// Gets or sets lower color limit for threshold
        /// </summary>
        public TPixel Lower { get; set; }

        /// <summary>
        /// Gets or sets the value for threshold limit
        /// </summary>
        public float ThresholdLimit { get; set; }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            Rectangle intersect = Rectangle.Intersect(sourceRectangle, source.Bounds());

            // Used ushort because the values should never exceed max ushort value
            ushort startY = (ushort)intersect.Y;
            ushort endY = (ushort)intersect.Bottom;
            ushort startX = (ushort)intersect.X;
            ushort endX = (ushort)intersect.Right;

            ushort width = (ushort)intersect.Width;
            ushort height = (ushort)intersect.Height;

            // ClusterSize defines the size of cluster to used to check for average. Tweaked to support upto 4k wide pixels and not more. 4096 / 16 is 256 thus the '-1'
            byte clusterSize = (byte)Math.Truncate((width / 16f) - 1);

            // Using pooled 2d buffer for integer image table and temp memory to hold Rgb24 converted pixel data
            using (Buffer2D<ulong> intImage = configuration.MemoryAllocator.Allocate2D<ulong>(width, height))
            using (IMemoryOwner<Rgb24> tmpBuffer = configuration.MemoryAllocator.Allocate<Rgb24>(width * height))
            {
                // Defines the rectangle section of the image to work on
                Rectangle workingRectangle = Rectangle.FromLTRB(startX, startY, endX, endY);

                this.pixelOpInstance.ToRgb24(source.GetPixelSpan(), tmpBuffer.GetSpan());

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

                ParallelHelper.IterateRows(
                    workingRectangle,
                    configuration,
                    rows =>
                    {
                        Span<Rgb24> rgbSpan = tmpBuffer.GetSpan();
                        ushort x1, y1, x2, y2;
                        uint count = 0;
                        long sum = 0;

                        for (ushort x = startX; x < endX; x++)
                        {
                            for (ushort y = (ushort)rows.Min; y < (ushort)rows.Max; y++)
                            {
                                ref Rgb24 rgb = ref rgbSpan[(width * y) + x];

                                x1 = (ushort)Math.Max(x - startX - clusterSize + 1, 0);
                                x2 = (ushort)Math.Min(x - startX + clusterSize + 1, width - 1);
                                y1 = (ushort)Math.Max(y - startY - clusterSize + 1, 0);
                                y2 = (ushort)Math.Min(y - startY + clusterSize + 1, height - 1);

                                count = (uint)((x2 - x1) * (y2 - y1));
                                sum = (long)Math.Min(intImage[x2, y2] - intImage[x1, y2] - intImage[x2, y1] + intImage[x1, y1], long.MaxValue);

                                if ((rgb.R + rgb.G + rgb.B) * count <= sum * this.ThresholdLimit)
                                {
                                    source[x, y] = this.Lower;
                                }
                                else
                                {
                                    source[x, y] = this.Upper;
                                }
                            }
                        }
                    });
            }
        }
    }
}