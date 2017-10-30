using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Helpers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    internal class RotateInterpolationProcessor<TPixel> : RotateProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <inheritdoc/>
        protected override void OnApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            if (this.OptimizedApply(source, configuration))
            {
                return;
            }

            int height = this.CanvasRectangle.Height;
            int width = this.CanvasRectangle.Width;
            Matrix3x2 matrix = this.GetCenteredMatrix(source, this.ProcessMatrix);
            Rectangle sourceBounds = source.Bounds();

            using (var targetPixels = new PixelAccessor<TPixel>(width, height))
            {
                Parallel.For(
                    0,
                    height,
                    new ParallelOptions { MaxDegreeOfParallelism = 1 }, //configuration.ParallelOptions,
                    y =>
                    {
                        Span<TPixel> targetRow = targetPixels.GetRowSpan(y);

                        for (int x = 0; x < width; x++)
                        {
                            var transformedPoint = PointF.Rotate(new Point(x, y), matrix);
                            var rounded = Point.Round(transformedPoint);
                            if (sourceBounds.Contains(rounded.X, rounded.Y))
                            {
                                int ceilX = unchecked((int)MathF.Ceiling(transformedPoint.X));
                                int ceilY = unchecked((int)MathF.Ceiling(transformedPoint.Y));
                                int floorX = unchecked((int)MathF.Floor(transformedPoint.X));
                                int floorY = unchecked((int)MathF.Floor(transformedPoint.Y));

                                // Clamp sampling pixels to the source image edge
                                ceilX = Math.Min(ceilX, sourceBounds.Width - 1);
                                floorX = Math.Max(0, floorX);
                                ceilY = Math.Min(ceilY, sourceBounds.Height - 1);
                                floorY = Math.Max(0, floorY);

                                // At the image edge, take the whole value of source
                                float wx1, wx2, wy1, wy2;
                                if (ceilX == floorX)
                                {
                                    wx1 = 1;
                                    wx2 = 0;
                                }
                                else
                                {
                                    wx1 = ceilX - transformedPoint.X;
                                    wx2 = transformedPoint.X - floorX;
                                }
                                if (ceilY == floorY)
                                {
                                    wy1 = 1;
                                    wy2 = 0;
                                }
                                else
                                {
                                    wy1 = ceilY - transformedPoint.Y;
                                    wy2 = transformedPoint.Y - floorY;
                                }

                                var topLeft = source[floorX, ceilY].ToVector4();
                                var topRight = source[ceilX, ceilY].ToVector4();
                                var bottomLeft = source[floorX, floorY].ToVector4();
                                var bottomRight = source[ceilX, floorY].ToVector4();

                                Vector4 interpolated = (wy2 * ((topLeft * wx1) + (topRight * wx2))) +
                                                       (wy1 * ((bottomLeft * wx1) + (bottomRight * wx2)));

                                targetRow[x].PackFromVector4(interpolated);
                            }
                        }
                    });

                source.SwapPixelsBuffers(targetPixels);
            }
        }

    }
}
