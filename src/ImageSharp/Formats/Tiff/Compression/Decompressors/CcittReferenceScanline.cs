// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors
{
    /// <summary>
    /// Represents a reference scan line for CCITT 2D decoding.
    /// </summary>
    internal readonly ref struct CcittReferenceScanline
    {
        private readonly ReadOnlySpan<byte> scanLine;
        private readonly int width;
        private readonly byte whiteByte;

        /// <summary>
        /// Initializes a new instance of the <see cref="CcittReferenceScanline"/> struct.
        /// </summary>
        /// <param name="whiteIsZero">Indicates, if white is zero, otherwise black is zero.</param>
        /// <param name="scanLine">The scan line.</param>
        public CcittReferenceScanline(bool whiteIsZero, ReadOnlySpan<byte> scanLine)
        {
            this.scanLine = scanLine;
            this.width = scanLine.Length;
            this.whiteByte = whiteIsZero ? (byte)0 : (byte)255;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CcittReferenceScanline"/> struct.
        /// </summary>
        /// <param name="whiteIsZero">Indicates, if white is zero, otherwise black is zero.</param>
        /// <param name="width">The width of the scanline.</param>
        public CcittReferenceScanline(bool whiteIsZero, int width)
        {
            this.scanLine = default;
            this.width = width;
            this.whiteByte = whiteIsZero ? (byte)0 : (byte)255;
        }

        public bool IsEmpty => this.scanLine.IsEmpty;

        /// <summary>
        /// Finds b1: The first changing element on the reference line to the right of a0 and of opposite color to a0.
        /// </summary>
        /// <param name="a0">The reference or starting element om the coding line.</param>
        /// <param name="a0Byte">Fill byte.</param>
        /// <returns>Position of b1.</returns>
        public int FindB1(int a0, byte a0Byte)
        {
            if (this.IsEmpty)
            {
                return this.FindB1ForImaginaryWhiteLine(a0, a0Byte);
            }

            return this.FindB1ForNormalLine(a0, a0Byte);
        }

        /// <summary>
        /// Finds b2: The next changing element to the right of b1 on the reference line.
        /// </summary>
        /// <param name="b1">The first changing element on the reference line to the right of a0 and opposite of color to a0.</param>
        /// <returns>Position of b1.</returns>
        public int FindB2(int b1)
        {
            if (this.IsEmpty)
            {
                return this.FindB2ForImaginaryWhiteLine();
            }

            return this.FindB2ForNormalLine(b1);
        }

        private int FindB1ForImaginaryWhiteLine(int a0, byte a0Byte)
        {
            if (a0 < 0)
            {
                if (a0Byte != this.whiteByte)
                {
                    return 0;
                }
            }

            return this.width;
        }

        private int FindB1ForNormalLine(int a0, byte a0Byte)
        {
            int offset = 0;
            if (a0 < 0)
            {
                if (a0Byte != this.scanLine[0])
                {
                    return 0;
                }
            }
            else
            {
                offset = a0;
            }

            ReadOnlySpan<byte> searchSpace = this.scanLine.Slice(offset);
            byte searchByte = (byte)~a0Byte;
            int index = searchSpace.IndexOf(searchByte);
            if (index < 0)
            {
                return this.scanLine.Length;
            }

            if (index != 0)
            {
                return offset + index;
            }

            searchByte = (byte)~searchSpace[0];
            index = searchSpace.IndexOf(searchByte);
            if (index < 0)
            {
                return this.scanLine.Length;
            }

            searchSpace = searchSpace.Slice(index);
            offset += index;
            index = searchSpace.IndexOf((byte)~searchByte);
            if (index < 0)
            {
                return this.scanLine.Length;
            }

            return index + offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindB2ForImaginaryWhiteLine() => this.width;

        private int FindB2ForNormalLine(int b1)
        {
            if (b1 >= this.scanLine.Length)
            {
                return this.scanLine.Length;
            }

            byte searchByte = (byte)~this.scanLine[b1];
            int offset = b1 + 1;
            ReadOnlySpan<byte> searchSpace = this.scanLine.Slice(offset);
            int index = searchSpace.IndexOf(searchByte);
            if (index == -1)
            {
                return this.scanLine.Length;
            }

            return offset + index;
        }
    }
}
