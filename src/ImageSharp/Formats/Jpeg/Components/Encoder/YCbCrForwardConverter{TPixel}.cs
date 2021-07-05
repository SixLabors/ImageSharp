// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder
{
    internal static class YCbCrForwardConverter<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        public static void LoadAndStretchEdges(RowOctet<TPixel> source, Span<TPixel> dest, Point start, Size sampleSize, Size totalSize)
        {
            DebugGuard.MustBeBetweenOrEqualTo(start.X, 0, totalSize.Width - 1, nameof(start.X));
            DebugGuard.MustBeBetweenOrEqualTo(start.Y, 0, totalSize.Height - 1, nameof(start.Y));

            int width = Math.Min(sampleSize.Width, totalSize.Width - start.X);
            int height = Math.Min(sampleSize.Height, totalSize.Height - start.Y);

            uint byteWidth = (uint)(width * Unsafe.SizeOf<TPixel>());
            int remainderXCount = sampleSize.Width - width;

            ref byte blockStart = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<TPixel, byte>(dest));
            int rowSizeInBytes = sampleSize.Width * Unsafe.SizeOf<TPixel>();

            for (int y = 0; y < height; y++)
            {
                Span<TPixel> row = source[y];

                ref byte s = ref Unsafe.As<TPixel, byte>(ref row[start.X]);
                ref byte d = ref Unsafe.Add(ref blockStart, y * rowSizeInBytes);

                Unsafe.CopyBlock(ref d, ref s, byteWidth);

                ref TPixel last = ref Unsafe.Add(ref Unsafe.As<byte, TPixel>(ref d), width - 1);

                for (int x = 1; x <= remainderXCount; x++)
                {
                    Unsafe.Add(ref last, x) = last;
                }
            }

            int remainderYCount = sampleSize.Height - height;

            if (remainderYCount == 0)
            {
                return;
            }

            ref byte lastRowStart = ref Unsafe.Add(ref blockStart, (height - 1) * rowSizeInBytes);

            for (int y = 1; y <= remainderYCount; y++)
            {
                ref byte remStart = ref Unsafe.Add(ref lastRowStart, rowSizeInBytes * y);
                Unsafe.CopyBlock(ref remStart, ref lastRowStart, (uint)rowSizeInBytes);
            }
        }
    }
}
