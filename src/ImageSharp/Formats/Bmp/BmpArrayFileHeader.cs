// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Bmp
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal readonly struct BmpArrayFileHeader
    {
        public BmpArrayFileHeader(short type, int size, int offsetToNext, short width, short height)
        {
            this.Type = type;
            this.Size = size;
            this.OffsetToNext = offsetToNext;
            this.ScreenWidth = width;
            this.ScreenHeight = height;
        }

        /// <summary>
        /// Gets the Bitmap identifier.
        /// The field used to identify the bitmap file: 0x42 0x41 (Hex code points for B and A).
        /// </summary>
        public short Type { get; }

        /// <summary>
        /// Gets the size of this header.
        /// </summary>
        public int Size { get; }

        /// <summary>
        /// Gets the offset to next OS2BMPARRAYFILEHEADER.
        /// This offset is calculated from the starting byte of the file. A value of zero indicates that this header is for the last image in the array list.
        /// </summary>
        public int OffsetToNext { get; }

        /// <summary>
        /// Gets the width of the image display in pixels.
        /// </summary>
        public short ScreenWidth { get; }

        /// <summary>
        /// Gets the height of the image display in pixels.
        /// </summary>
        public short ScreenHeight { get; }

        public static BmpArrayFileHeader Parse(Span<byte> data)
        {
            return MemoryMarshal.Cast<byte, BmpArrayFileHeader>(data)[0];
        }
    }
}
