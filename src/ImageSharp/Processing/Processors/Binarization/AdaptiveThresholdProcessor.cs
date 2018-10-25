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

namespace SixLabors.ImageSharp.Processing.Processors
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
            : this(NamedColors<TPixel>.White, NamedColors<TPixel>.Black)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AdaptiveThresholdProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="upper">Color for upper threshold</param>
        /// <param name="lower">Color for lower threshold</param>
        public AdaptiveThresholdProcessor(TPixel upper, TPixel lower)
        {
            this.pixelOpInstance = PixelOperations<TPixel>.Instance;

            this.Upper = upper;
            this.Lower = lower;
        }

        /// <summary>
        /// Gets or sets upper color limit for thresholding
        /// </summary>
        public TPixel Upper { get; set; }

        /// <summary>
        /// Gets or sets lower color limit for threshold
        /// </summary>
        public TPixel Lower { get; set; }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            var intersect = Rectangle.Intersect(sourceRectangle, source.Bounds());
            ushort startY = (ushort)intersect.Y;
            ushort endY = (ushort)intersect.Bottom;
            ushort startX = (ushort)intersect.X;
            ushort endX = (ushort)intersect.Right;

            ushort width = (ushort)intersect.Width;
            ushort height = (ushort)intersect.Height;

            // Tweaked to support upto 4k wide pixels and not more. 4096 / 16 is 256 thus the '-1'
            byte clusterSize = (byte)((width / 16) - 1);

            float threshold = 0.85f;

            // Using pooled 2d buffer for integer image table
            using (Buffer2D<ulong> intImage = configuration.MemoryAllocator.Allocate2D<ulong>(width, height))
            {
                var workingRectangle = Rectangle.FromLTRB(startX, startY, endX, endY);

                ParallelHelper.IterateRowsWithTempBuffer<Rgb24>(
                    workingRectangle,
                    configuration,
                    (rows, memory) =>
                    {
                        ulong sum = 0;

                        Span<Rgb24> tmpSpan = memory.Span;

                        for (int i = rows.Min; i < rows.Max; i++)
                        {
                            this.pixelOpInstance.ToRgb24(source.GetPixelRowSpan(i), tmpSpan);

                            sum = 0;

                            for (int j = startX; j < endX; j++)
                            {
                                ref Rgb24 rgb = ref tmpSpan[j];

                                sum += (ulong)(rgb.R + rgb.G + rgb.B);

                                if (i != 0)
                                {
                                    intImage[i, j] = intImage[i - 1, j] + sum;
                                }
                                else
                                {
                                    intImage[i, j] = sum;
                                }
                            }
                        }
                    });

                ParallelHelper.IterateRowsWithTempBuffer<Rgb24>(
                    workingRectangle,
                    configuration,
                    (rows, memory) =>
                    {
                        ushort x1, x2, y1, y2;
                        uint count = 0;
                        long sum = 0;

                        Span<Rgb24> tmpSpan = memory.Span;

                        for (int i = rows.Min; i < rows.Max; i++)
                        {
                            Span<TPixel> originalSpan = source.GetPixelRowSpan(i);
                            this.pixelOpInstance.ToRgb24(originalSpan, tmpSpan);

                            for (int j = startX; j < endX; j++)
                            {
                                ref Rgb24 rgb = ref tmpSpan[j];

                                x1 = (ushort)Math.Max(i - clusterSize + 1, 0);
                                x2 = (ushort)Math.Min(i + clusterSize + 1, endY - 1);
                                y1 = (ushort)Math.Max(j - clusterSize + 1, 0);
                                y2 = (ushort)Math.Min(j + clusterSize + 1, endX - 1);

                                count = (uint)((x2 - x1) * (y2 - y1));

                                sum = (long)(intImage[x2, y2] - intImage[x1, y2] - intImage[x2, y1] + intImage[x1, y1]);

                                if ((rgb.R + rgb.G + rgb.B) * count < sum * threshold)
                                {
                                    originalSpan[j] = this.Lower;
                                }
                                else
                                {
                                    originalSpan[j] = this.Upper;
                                }
                            }
                        }
                    });
            }
        }
    }
}