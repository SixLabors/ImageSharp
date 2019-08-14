// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Class to handle cases where TIFF image data is compressed using Deflate compression.
    /// </summary>
    /// <remarks>
    /// Note that the 'OldDeflate' compression type is identical to the 'Deflate' compression type.
    /// </remarks>
    internal static class DeflateTiffCompression
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
            // Read the 'zlib' header information
            int cmf = stream.ReadByte();
            int flag = stream.ReadByte();

            if ((cmf & 0x0f) != 8)
            {
                throw new Exception($"Bad compression method for ZLIB header: cmf={cmf}");
            }

            // If the 'fdict' flag is set then we should skip the next four bytes
            bool fdict = (flag & 32) != 0;

            if (fdict)
            {
                stream.ReadByte();
                stream.ReadByte();
                stream.ReadByte();
                stream.ReadByte();
            }

            // The subsequent data is the Deflate compressed data (except for the last four bytes of checksum)
            int headerLength = fdict ? 10 : 6;
            SubStream subStream = new SubStream(stream, byteCount - headerLength);
            using (DeflateStream deflateStream = new DeflateStream(subStream, CompressionMode.Decompress, true))
            {
                deflateStream.ReadFull(buffer);
            }
        }
    }
}
