// <copyright file="JpegUtils.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace ImageSharp.Formats.Jpg
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    ///     Jpeg specific utilities and extension methods
    /// </summary>
    internal static unsafe class JpegUtils
    {
        /// <summary>
        /// Copy a region of an image into dest. De "outlier" area will be stretched out with pixels on the right and bottom of
        ///     the image.
        /// </summary>
        /// <typeparam name="TColor">
        /// The pixel type
        /// </typeparam>
        /// <param name="pixels">
        /// The input pixel acessor
        /// </param>
        /// <param name="dest">
        /// The destination <see cref="PixelArea{TColor}"/>
        /// </param>
        /// <param name="sourceY">
        /// Starting Y coord
        /// </param>
        /// <param name="sourceX">
        /// Starting X coord
        /// </param>
        public static void CopyRGBBytesStretchedTo<TColor>(
            this PixelAccessor<TColor> pixels,
            PixelArea<TColor> dest,
            int sourceY,
            int sourceX)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            pixels.CopyTo(dest, sourceY, sourceX);
            int stretchFromX = pixels.Width - sourceX;
            int stretchFromY = pixels.Height - sourceY;
            StretchPixels(dest, stretchFromX, stretchFromY);
        }

        /// <summary>
        /// Copy an RGB value
        /// </summary>
        /// <param name="source">
        /// Source pointer
        /// </param>
        /// <param name="dest">
        /// Destination pointer
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void CopyRgb(byte* source, byte* dest)
        {
            *dest++ = *source++; // R
            *dest++ = *source++; // G
            *dest = *source; // B
        }

        /// <summary>
        /// Writes data to "Define Quantization Tables" block for QuantIndex
        /// </summary>
        /// <param name="dqt">
        /// The "Define Quantization Tables" block
        /// </param>
        /// <param name="offset">
        /// Offset in dqt
        /// </param>
        /// <param name="i">
        /// The quantization index
        /// </param>
        /// <param name="q">
        /// The quantazation table to copy data from
        /// </param>
        internal static void WriteDataToDqt(byte[] dqt, ref int offset, QuantIndex i, ref Block8x8F q)
        {
            dqt[offset++] = (byte)i;
            for (int j = 0; j < Block8x8F.ScalarCount; j++)
            {
                dqt[offset++] = (byte)q[j];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsInvalidStretchArea<TColor>(PixelArea<TColor> area, int fromX, int fromY)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return fromX <= 0 || fromY <= 0 || fromX >= area.Width || fromY >= area.Height;
        }

        private static void StretchPixels<TColor>(PixelArea<TColor> area, int fromX, int fromY)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            if (IsInvalidStretchArea(area, fromX, fromY))
            {
                return;
            }

            for (int y = 0; y < fromY; y++)
            {
                byte* ptrBase = (byte*)area.DataPointer + (y * area.RowByteCount);

                for (int x = fromX; x < area.Width; x++)
                {
                    byte* prevPtr = ptrBase + ((x - 1) * 3);
                    byte* currPtr = ptrBase + (x * 3);

                    CopyRgb(prevPtr, currPtr);
                }
            }

            for (int y = fromY; y < area.Height; y++)
            {
                byte* currBase = (byte*)area.DataPointer + (y * area.RowByteCount);
                byte* prevBase = (byte*)area.DataPointer + ((y - 1) * area.RowByteCount);

                for (int x = 0; x < area.Width; x++)
                {
                    int x3 = 3 * x;
                    byte* currPtr = currBase + x3;
                    byte* prevPtr = prevBase + x3;

                    CopyRgb(prevPtr, currPtr);
                }
            }
        }
    }
}