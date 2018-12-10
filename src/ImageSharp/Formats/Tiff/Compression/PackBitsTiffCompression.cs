// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Class to handle cases where TIFF image data is compressed using PackBits compression.
    /// </summary>
    internal static class PackBitsTiffCompression
    {
        /// <summary>
        /// Decompresses image data into the supplied buffer.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to read image data from.</param>
        /// <param name="byteCount">The number of bytes to read from the input stream.</param>
        /// <param name="buffer">The output buffer for uncompressed data.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Decompress(Stream stream, int byteCount, byte[] buffer)
        {
            byte[] compressedData = ArrayPool<byte>.Shared.Rent(byteCount);

            try
            {
                stream.ReadFull(compressedData, byteCount);
                int compressedOffset = 0;
                int decompressedOffset = 0;

                while (compressedOffset < byteCount)
                {
                    byte headerByte = compressedData[compressedOffset];

                    if (headerByte <= (byte)127)
                    {
                        int literalOffset = compressedOffset + 1;
                        int literalLength = compressedData[compressedOffset] + 1;

                        Array.Copy(compressedData, literalOffset, buffer, decompressedOffset, literalLength);

                        compressedOffset += literalLength + 1;
                        decompressedOffset += literalLength;
                    }
                    else if (headerByte == (byte)0x80)
                    {
                        compressedOffset += 1;
                    }
                    else
                    {
                        byte repeatData = compressedData[compressedOffset + 1];
                        int repeatLength = 257 - headerByte;

                        ArrayCopyRepeat(repeatData, buffer, decompressedOffset, repeatLength);

                        compressedOffset += 2;
                        decompressedOffset += repeatLength;
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(compressedData);
            }
        }

        private static void ArrayCopyRepeat(byte value, byte[] destinationArray, int destinationIndex, int length)
        {
            for (int i = 0; i < length; i++)
            {
                destinationArray[i + destinationIndex] = value;
            }
        }
    }
}
