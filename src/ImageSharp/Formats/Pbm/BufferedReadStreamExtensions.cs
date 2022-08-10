// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Pbm
{
    /// <summary>
    /// Extensions methods for <see cref="BufferedReadStream"/>.
    /// </summary>
    internal static class BufferedReadStreamExtensions
    {
        /// <summary>
        /// Skip over any whitespace or any comments.
        /// </summary>
        public static void SkipWhitespaceAndComments(this BufferedReadStream stream)
        {
            bool isWhitespace;
            do
            {
                int val = stream.ReadByte();

                // Comments start with '#' and end at the next new-line.
                if (val == 0x23)
                {
                    int innerValue;
                    do
                    {
                        innerValue = stream.ReadByte();
                    }
                    while (innerValue != 0x0a);

                    // Continue searching for whitespace.
                    val = innerValue;
                }

                isWhitespace = val is 0x09 or 0x0a or 0x0d or 0x20;
            }
            while (isWhitespace);
            stream.Seek(-1, SeekOrigin.Current);
        }

        /// <summary>
        /// Read a decimal text value.
        /// </summary>
        /// <returns>The integer value of the decimal.</returns>
        public static int ReadDecimal(this BufferedReadStream stream)
        {
            int value = 0;
            while (true)
            {
                int current = stream.ReadByte() - 0x30;
                if ((uint)current > 9)
                {
                    break;
                }

                value = (value * 10) + current;
            }

            return value;
        }

        /// <summary>
        /// Reads a 32 bit float.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="scratch">A scratch buffer of size 4 bytes.</param>
        /// <param name="byteOrder">The byte order. Defaults to little endian.</param>
        /// <returns>the float value.</returns>
        public static float ReadSingle(this BufferedReadStream stream, Span<byte> scratch, ByteOrder byteOrder = ByteOrder.LittleEndian)
        {
            stream.Read(scratch, 0, 4);
            int intValue;
            if (byteOrder == ByteOrder.LittleEndian)
            {
                intValue = BinaryPrimitives.ReadInt32LittleEndian(scratch);
            }
            else
            {
                intValue = BinaryPrimitives.ReadInt32BigEndian(scratch);
            }

            return Unsafe.As<int, float>(ref intValue);
        }

        /// <summary>
        /// Reads a 16 bit float.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="scratch">A scratch buffer of size 2 bytes.</param>
        /// <param name="byteOrder">The byte order. Defaults to little endian.</param>
        /// <returns>The float value.</returns>
        public static float ReadHalfSingle(this BufferedReadStream stream, Span<byte> scratch, ByteOrder byteOrder = ByteOrder.LittleEndian)
        {
            stream.Read(scratch, 0, 2);
            ushort shortValue;
            if (byteOrder == ByteOrder.LittleEndian)
            {
                shortValue = BinaryPrimitives.ReadUInt16LittleEndian(scratch);
            }
            else
            {
                shortValue = BinaryPrimitives.ReadUInt16BigEndian(scratch);
            }

            return HalfTypeHelper.Unpack(shortValue);
        }
    }
}
