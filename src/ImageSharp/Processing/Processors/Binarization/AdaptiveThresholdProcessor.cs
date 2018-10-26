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
        /// <param name="threshold">Threshold limit</param>
        public AdaptiveThresholdProcessor(float threshold)
            : this(NamedColors<TPixel>.White, NamedColors<TPixel>.Black, threshold)
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
        /// <param name="threshold">Threshold limit</param>
        public AdaptiveThresholdProcessor(TPixel upper, TPixel lower, float threshold)
        {
            this.pixelOpInstance = PixelOperations<TPixel>.Instance;

            this.Upper = upper;
            this.Lower = lower;
            this.ThresholdLimit = threshold;
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
            var intersect = Rectangle.Intersect(sourceRectangle, source.Bounds());
            ushort startY = (ushort)intersect.Y;
            ushort endY = (ushort)intersect.Bottom;
            ushort startX = (ushort)intersect.X;
            ushort endX = (ushort)intersect.Right;

            ushort width = (ushort)intersect.Width;
            ushort height = (ushort)intersect.Height;

            // Tweaked to support upto 4k wide pixels and not more. 4096 / 16 is 256 thus the '-1'
            byte clusterSize = (byte)((width / 16) - 1);

            // Using pooled 2d buffer for integer image table
            using (Buffer2D<ulong> intImage = configuration.MemoryAllocator.Allocate2D<ulong>(width, height))
            using (IMemoryOwner<Rgb24> tmpBuffer = configuration.MemoryAllocator.Allocate<Rgb24>(width * height))
            {
                Rectangle workingRectangle = Rectangle.FromLTRB(startX, startY, endX, endY);

                this.pixelOpInstance.ToRgb24(source.GetPixelSpan(), tmpBuffer.GetSpan());

                ParallelHelper.IterateRows(
                    workingRectangle,
                    configuration,
                    rows =>
                    {
                        Span<Rgb24> rgbSpan = tmpBuffer.GetSpan();
                        uint sum;
                        for (int x = startX; x < endX; x++)
                        {
                            sum = 0;
                            for (int y = rows.Min; y < rows.Max; y++)
                            {
                                ref Rgb24 rgb = ref rgbSpan[(width * y) + x];
                                sum += (uint)(rgb.R + rgb.G + rgb.B);

                                if (x > 0)
                                {
                                    intImage[x - startX, y - startY] = intImage[x - 1 - startX, y - startY] + sum;
                                }
                                else
                                {
                                    intImage[x - startX, y - startY] = sum;
                                }
                            }
                        }
                    });

                ParallelHelper.IterateRows(
                    workingRectangle,
                    configuration,
                    rows =>
                    {
                        ushort x1, x2, y1, y2;
                        Span<Rgb24> rgbSpan = tmpBuffer.GetSpan();
                        long sum = 0;
                        uint count = 0;

                        for (int x = startX; x < endX; x++)
                        {
                            for (int y = rows.Min; y < rows.Max; y++)
                            {
                                ref Rgb24 rgb = ref rgbSpan[(width * y) + x];
                                x1 = (ushort)Math.Max(x - clusterSize + 1 - startX, 0);
                                x2 = (ushort)Math.Min(x + clusterSize + 1 - startX, endX - startX - 1);
                                y1 = (ushort)Math.Max(y - clusterSize + 1 - startY, 0);
                                y2 = (ushort)Math.Min(y + clusterSize + 1 - startY, endY - startY - 1);

                                count = (uint)((x2 - x1) * (y2 - y1));

                                sum = (long)(intImage[x2, y2] - intImage[x2, y1] - intImage[x1, y2] + intImage[x1, y1]);

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