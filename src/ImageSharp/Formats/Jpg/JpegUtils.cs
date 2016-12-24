namespace ImageSharp.Formats
{
    using System;
    using System.Runtime.CompilerServices;

    internal static unsafe class JpegUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void CopyRgb(byte* source, byte* dest)
        {
            *dest++ = *source++; // R
            *dest++ = *source++; // G
            *dest = *source; // B
        }

        private static unsafe void StretchPixels<TColor>(PixelArea<TColor> area, int fromX, int fromY)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            if (IsInvalidStretchArea(area, fromX, fromY))
            {
                return;
            }

            for (int y = 0; y < fromY; y++)
            {
                byte* ptrBase = (byte*)area.DataPointer + y * area.RowByteCount;

                for (int x = fromX; x < area.Width; x++)
                {
                    byte* prevPtr = ptrBase + (x - 1) * 3;
                    byte* currPtr = ptrBase + x * 3;

                    CopyRgb(prevPtr, currPtr);
                }
            }

            for (int y = fromY; y < area.Height; y++)
            {
                byte* currBase = (byte*)area.DataPointer + y * area.RowByteCount;
                byte* prevBase = (byte*)area.DataPointer + (y - 1) * area.RowByteCount;

                for (int x = 0; x < area.Width; x++)
                {
                    int x3 = 3 * x;
                    byte* currPtr = currBase + x3;
                    byte* prevPtr = prevBase + x3;

                    CopyRgb(prevPtr, currPtr);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsInvalidStretchArea<TColor>(PixelArea<TColor> area, int fromX, int fromY) where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return fromX <= 0 || fromY <= 0 || fromX >= area.Width || fromY >= area.Height;
        }

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
    }
}