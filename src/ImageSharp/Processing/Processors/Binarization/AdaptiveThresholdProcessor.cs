using System;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
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
            var interest = Rectangle.Intersect(sourceRectangle, source.Bounds());
            ushort startY = (ushort)interest.Y;
            ushort endY = (ushort)interest.Bottom;
            ushort startX = (ushort)interest.X;
            ushort endX = (ushort)interest.Right;

            ushort width = (ushort)(endX - startX);
            ushort height = (ushort)(endY - startY);

            // Algorithm variables //
            ulong sum;
            uint count;

            // Tweaked to support upto 4k wide pixels
            ushort s = (ushort)Math.Truncate((width / 16f) - 1);

            // Trying to figure out how to do this
            using (Buffer2D<ulong> intImage = configuration.MemoryAllocator.Allocate2D<ulong>(width, height, AllocationOptions.Clean))
            {
                Rgb24 rgb = default;

                for (ushort i = startY; i < endY; i++)
                {
                    Span<TPixel> span = source.GetPixelRowSpan(i);

                    sum = 0;

                    for (ushort j = startX; j < endX; j++)
                    {
                        span[j].ToRgb24(ref rgb);

                        sum += (uint)(rgb.R + rgb.G + rgb.B);

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

                // How can I parallelize this?
                ushort x1, x2, y1, y2;

                for (ushort i = startY; i < endY; i++)
                {
                    Span<TPixel> span = source.GetPixelRowSpan(i);

                    for (ushort j = startX; j < endX; j++)
                    {
                        x1 = (ushort)Math.Max(i - s + 1, 0);
                        x2 = (ushort)Math.Min(i + s + 1, endY - 1);
                        y1 = (ushort)Math.Max(j - s + 1, 0);
                        y2 = (ushort)Math.Min(j + s + 1, endX - 1);

                        count = (uint)((x2 - x1) * (y2 - y1));

                        sum = intImage[x2, y2] - intImage[x1, y2] - intImage[x2, y1] + intImage[x1, y1];

                        span[j].ToRgb24(ref rgb);

                        if ((rgb.R + rgb.G + rgb.B) * count < sum * (1.0 - 0.15))
                        {
                            span[j] = this.Lower;
                        }
                        else
                        {
                            span[j] = this.Upper;
                        }
                    }
                }
            }
        }
    }
}