// <copyright file="JpegUtils.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace ImageSharp.Formats.Jpg
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    ///     Jpeg specific utilities and extension methods
    /// </summary>
    internal static unsafe class JpegUtils
    {
        /// <summary>
        /// Copy a region of an image into dest. De "outlier" area will be stretched out with pixels on the right and bottom of the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel type</typeparam>
        /// <param name="pixels">The input pixel acessor</param>
        /// <param name="dest">The destination <see cref="PixelArea{TColor}"/></param>
        /// <param name="sourceY">Starting Y coord</param>
        /// <param name="sourceX">Starting X coord</param>
        public static void CopyRGBBytesStretchedTo<TColor>(
            this PixelAccessor<TColor> pixels,
            PixelArea<TColor> dest,
            int sourceY,
            int sourceX)
            where TColor : struct, IPixel<TColor>
        {
            pixels.SafeCopyTo(dest, sourceY, sourceX);
            int stretchFromX = pixels.Width - sourceX;
            int stretchFromY = pixels.Height - sourceY;
            StretchPixels(dest, stretchFromX, stretchFromY);
        }

        // Nothing to stretch if (fromX, fromY) is outside the area, or is at (0,0)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsInvalidStretchStartingPosition<TColor>(PixelArea<TColor> area, int fromX, int fromY)
            where TColor : struct, IPixel<TColor>
        {
            return fromX <= 0 || fromY <= 0 || fromX >= area.Width || fromY >= area.Height;
        }

        private static void StretchPixels<TColor>(PixelArea<TColor> area, int fromX, int fromY)
            where TColor : struct, IPixel<TColor>
        {
            if (IsInvalidStretchStartingPosition(area, fromX, fromY))
            {
                return;
            }

            for (int y = 0; y < fromY; y++)
            {
                ref RGB24 ptrBase = ref GetRowStart(area, y);

                for (int x = fromX; x < area.Width; x++)
                {
                    // Copy the left neighbour pixel to the current one
                    Unsafe.Add(ref ptrBase, x) = Unsafe.Add(ref ptrBase, x - 1);
                }
            }

            for (int y = fromY; y < area.Height; y++)
            {
                ref RGB24 currBase = ref GetRowStart(area, y);
                ref RGB24 prevBase = ref GetRowStart(area, y - 1);

                for (int x = 0; x < area.Width; x++)
                {
                    // Copy the top neighbour pixel to the current one
                    Unsafe.Add(ref currBase, x) = Unsafe.Add(ref prevBase, x);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ref RGB24 GetRowStart<TColor>(PixelArea<TColor> area, int y)
            where TColor : struct, IPixel<TColor>
        {
            return ref Unsafe.As<byte, RGB24>(ref area.GetRowSpan(y).DangerousGetPinnableReference());
        }

        [StructLayout(LayoutKind.Sequential, Size = 3)]
        private struct RGB24
        {
        }
    }
}