// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Stores general information about the Bitmap file.
    /// <see href="https://en.wikipedia.org/wiki/BMP_file_format" />
    /// </summary>
    /// <remarks>
    /// The first two bytes of the Bitmap file format
    /// (thus the Bitmap header) are stored in big-endian order.
    /// All of the other integer values are stored in little-endian format
    /// (i.e. least-significant byte first).
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal readonly struct BmpFileHeader
    {
        /// <summary>
        /// Defines of the data structure in the bitmap file.
        /// </summary>
        public const int Size = 14;

        public BmpFileHeader(short type, int fileSize, int reserved, int offset)
        {
            this.Type = type;
            this.FileSize = fileSize;
            this.Reserved = reserved;
            this.Offset = offset;
        }

        /// <summary>
        /// Gets the Bitmap identifier.
        /// The field used to identify the bitmap file: 0x42 0x4D
        /// (Hex code points for B and M)
        /// </summary>
        public short Type { get; }

        /// <summary>
        /// Gets the size of the bitmap file in bytes.
        /// </summary>
        public int FileSize { get; }

        /// <summary>
        /// Gets any reserved data; actual value depends on the application
        /// that creates the image.
        /// </summary>
        public int Reserved { get; }

        /// <summary>
        /// Gets the offset, i.e. starting address, of the byte where
        /// the bitmap data can be found.
        /// </summary>
        public int Offset { get; }

        public static BmpFileHeader Parse(Span<byte> data)
        {
            return MemoryMarshal.Cast<byte, BmpFileHeader>(data)[0];
        }

        public unsafe void WriteTo(Span<byte> buffer)
        {
            ref BmpFileHeader dest = ref Unsafe.As<byte, BmpFileHeader>(ref MemoryMarshal.GetReference(buffer));

            dest = this;
        }
    }
}