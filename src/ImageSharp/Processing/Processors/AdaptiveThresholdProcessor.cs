using System;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Performs Bradley Adaptive Threshold filter against an image
    /// </summary>
    /// <typeparam name="TPixel">The pixel format</typeparam>
    internal class AdaptiveThresholdProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        public AdaptiveThresholdProcessor() { }

        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            //? What does this do?
            // int startY = sourceRectangle.Y;
            // int endY = sourceRectangle.Bottom;
            // int startX = sourceRectangle.X;
            // int endX = sourceRectangle.Right;
            // int minX = Math.Max(0, startX);
            // int maxX = Math.Min(source.Width, endX);
            // int minY = Math.Max(0, startY);
            // int maxY = Math.Min(source.Height, endY);

            // Algorithm variables
            ulong sum, count;
            ushort s = (ushort)Math.Truncate(source.Width / 16.0);
            UInt64[,] intImage = new UInt64[source.Height, source.Width];

            Rgb24 rgb = default;

            for (ushort i = 0; i < source.Height; i++)
            {
                Span<TPixel> span = source.GetPixelRowSpan(i);

                sum = 0;

                for (ushort j = 0; j < span.Length; j++)
                {
                    span[j].ToRgb24(ref rgb);

                    sum += (uint)(rgb.R + rgb.G + rgb.B);

                    if (i != 0)
                        intImage[i, j] = intImage[i - 1, j] + sum;
                    else
                        intImage[i, j] = sum;
                }
            }

            ushort x1, x2, y1, y2;
            for (ushort i = 0; i < source.Height; i++)
            {
                Span<TPixel> span = source.GetPixelRowSpan(i);

                for (ushort j = 0; j < span.Length; j++)
                {
                    x1 = (ushort)Math.Max(i - s, 0);
                    x2 = (ushort)Math.Min(i + s, source.Height - 1);
                    y1 = (ushort)Math.Max(j - s, 0);
                    y2 = (ushort)Math.Min(j + s, source.Width - 1);

                    count = (ushort)((x2 - x1) * (y2 - y1));

                    sum = intImage[x2, y2] - intImage[x1, y2] - intImage[x2, y1] + intImage[x1, y1];

                    span[j].ToRgb24(ref rgb);

                    if (j > span.Length / 2) { }

                    if ((UInt64)(rgb.R + rgb.G + rgb.B) * count < sum * (1.0 - 0.15))
                        span[j] = NamedColors<TPixel>.Black;
                    else
                        span[j] = NamedColors<TPixel>.White;
                }
            }
        }
    }
}