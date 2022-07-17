// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Buffers;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Defines extensions that allow the computation of image integrals on an <see cref="Image"/>
    /// </summary>
    public static partial class ProcessingExtensions
    {
        /// <summary>
        /// Apply an image integral. <See href="https://en.wikipedia.org/wiki/Summed-area_table"/>
        /// </summary>
        /// <param name="source">The image on which to apply the integral.</param>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <returns>The <see cref="Buffer2D{T}"/> containing all the sums.</returns>
        public static Buffer2D<ulong> CalculateIntegralImage<TPixel>(this Image<TPixel> source)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Configuration configuration = source.GetConfiguration();

            int endY = source.Height;
            int endX = source.Width;

            Buffer2D<ulong> intImage = configuration.MemoryAllocator.Allocate2D<ulong>(source.Width, source.Height);
            ulong sumX0 = 0;
            Buffer2D<TPixel> sourceBuffer = source.Frames.RootFrame.PixelBuffer;

            using (IMemoryOwner<L8> tempRow = configuration.MemoryAllocator.Allocate<L8>(source.Width))
            {
                Span<L8> tempSpan = tempRow.GetSpan();
                Span<TPixel> sourceRow = sourceBuffer.DangerousGetRowSpan(0);
                Span<ulong> destRow = intImage.DangerousGetRowSpan(0);

                PixelOperations<TPixel>.Instance.ToL8(configuration, sourceRow, tempSpan);

                // First row
                for (int x = 0; x < endX; x++)
                {
                    sumX0 += tempSpan[x].PackedValue;
                    destRow[x] = sumX0;
                }

                Span<ulong> previousDestRow = destRow;

                // All other rows
                for (int y = 1; y < endY; y++)
                {
                    sourceRow = sourceBuffer.DangerousGetRowSpan(y);
                    destRow = intImage.DangerousGetRowSpan(y);

                    PixelOperations<TPixel>.Instance.ToL8(configuration, sourceRow, tempSpan);

                    // Process first column
                    sumX0 = tempSpan[0].PackedValue;
                    destRow[0] = sumX0 + previousDestRow[0];

                    // Process all other colmns
                    for (int x = 1; x < endX; x++)
                    {
                        sumX0 += tempSpan[x].PackedValue;
                        destRow[x] = sumX0 + previousDestRow[x];
                    }

                    previousDestRow = destRow;
                }
            }

            return intImage;
        }
    }
}
