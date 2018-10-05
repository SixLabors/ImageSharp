using System;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Performs Bradley Adaptive Threshold filter against an image
    /// </summary>
    /// <typeparam name="TPixel">The pixel format of the image</typeparam>
    internal class AdaptiveThresholdProcessor<TPixel> : IImageProcessor<TPixel>
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

        public unsafe void Apply(Image<TPixel> source, Rectangle sourceRectangle)
        {
            ushort xStart = (ushort)Math.Max(0, sourceRectangle.X);
            ushort yStart = (ushort)Math.Max(0, sourceRectangle.Y);
            ushort xEnd = (ushort)Math.Min(xStart + sourceRectangle.Width, source.Width);
            ushort yEnd = (ushort)Math.Min(yStart + sourceRectangle.Height, source.Height);

            // Algorithm variables
            uint sum, count;
            ushort s = (ushort)Math.Truncate((xEnd / 16f) - 1);
            uint[,] intImage = new uint[yEnd, xEnd];

            // Trying to figure out how to do this
            // Using (Buffer2D<ulong> intImg = source.GetConfiguration().MemoryAllocator.Allocate2D<ulong>)
            Rgb24 rgb = default;

            for (ushort i = yStart; i < yEnd; i++)
            {
                Span<TPixel> span = source.GetPixelRowSpan(i);

                sum = 0;

                for (ushort j = xStart; j < xEnd; j++)
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

            for (ushort i = yStart; i < yEnd; i++)
            {
                Span<TPixel> span = source.GetPixelRowSpan(i);

                for (ushort j = xStart; j < xEnd; j++)
                {
                    x1 = (ushort)Math.Max(i - s + 1, 0);
                    x2 = (ushort)Math.Min(i + s + 1, yEnd - 1);
                    y1 = (ushort)Math.Max(j - s + 1, 0);
                    y2 = (ushort)Math.Min(j + s + 1, xEnd - 1);

                    count = (ushort)((x2 - x1) * (y2 - y1));

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