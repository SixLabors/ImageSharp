// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Defines extensions that allow the computation of image integrals on an <see cref="Image"/>
    /// </summary>
    public static class ImagingUtilities
    {
        /// <summary>
        /// Apply an image integral. See https://en.wikipedia.org/wiki/Summed-area_table
        /// </summary>
        /// <param name="source">The image on which to apply the integral.</param>
        /// <returns>The <see cref="Buffer2D{ulong}" containing all the sums</returns>
        public static Buffer2D<ulong> CalculateIntegralImage(this Image<L8> source)
        {
            Configuration configuration = source.GetConfiguration();

            int startY = 0;
            int endY = source.Height;
            int startX = 0;
            int endX = source.Width;

            Buffer2D<ulong> intImage = configuration.MemoryAllocator.Allocate2D<ulong>(source.Width, source.Height);

            for (int x = startX; x < endX; x++)
            {
                ulong sum = 0;
                for (int y = startY; y < endY; y++)
                {
                    Span<L8> row = source.GetPixelRowSpan(y);
                    ref L8 rowRef = ref MemoryMarshal.GetReference(row);
                    ref L8 color = ref Unsafe.Add(ref rowRef, x);

                    sum += (ulong)color.PackedValue;

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

            return intImage;
        }

        /// <summary>
        /// Apply an image integral. See https://en.wikipedia.org/wiki/Summed-area_table
        /// </summary>
        /// <param name="source">The image on which to apply the integral.</param>
        /// <returns>The <see cref="Buffer2D{ulong}" containing all the sums</returns>
        public static Buffer2D<ulong> CalculateIntegralImage(this Image<Rgba32> source)
        {
            Configuration configuration = source.GetConfiguration();

            int startY = 0;
            int endY = source.Height;
            int startX = 0;
            int endX = source.Width;

            Buffer2D<ulong> intImage = configuration.MemoryAllocator.Allocate2D<ulong>(source.Width, source.Height);

            for (int x = startX; x < endX; x++)
            {
                ulong sum = 0;
                for (int y = startY; y < endY; y++)
                {
                    Span<Rgba32> row = source.GetPixelRowSpan(y);
                    ref Rgba32 rowRef = ref MemoryMarshal.GetReference(row);
                    ref Rgba32 color = ref Unsafe.Add(ref rowRef, x);

                    sum += (ulong)(color.R + color.G + color.B);

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

            return intImage;
        }
    }
}
