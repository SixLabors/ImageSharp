// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.IO.Compression;
using SixLabors.ImageSharp.Formats.Experimental.Tiff.Utils;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff.Compression
{
    /// <summary>
    /// Class to handle cases where TIFF image data is compressed using Deflate compression.
    /// </summary>
    /// <remarks>
    /// Note that the 'OldDeflate' compression type is identical to the 'Deflate' compression type.
    /// </remarks>
    internal class DeflateTiffCompression : TiffBaseCompression
    {
        public DeflateTiffCompression(MemoryAllocator allocator)
            : base(allocator)
        {
        }

        /// <inheritdoc/>
        public override void Decompress(Stream stream, int byteCount, Span<byte> buffer)
        {
            // Read the 'zlib' header information
            int cmf = stream.ReadByte();
            int flag = stream.ReadByte();

            if ((cmf & 0x0f) != 8)
            {
                TiffThrowHelper.ThrowBadZlibHeader(cmf);
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
            var subStream = new SubStream(stream, byteCount - headerLength);
            using (var deflateStream = new DeflateStream(subStream, CompressionMode.Decompress, true))
            {
                deflateStream.ReadFull(buffer);
            }
        }
    }
}
