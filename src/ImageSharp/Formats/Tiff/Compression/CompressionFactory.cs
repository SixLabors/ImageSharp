// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    internal static class CompressionFactory
    {
        /// <summary>
        /// Decompresses an image block from the input stream into the specified buffer.
        /// </summary>
        /// <param name="stream">The input stream.</param>
        /// <param name="compressionType">Type of the compression.</param>
        /// <param name="offset">The offset within the file of the image block.</param>
        /// <param name="byteCount">The size (in bytes) of the compressed data.</param>
        /// <param name="buffer">The buffer to write the uncompressed data.</param>
        public static void DecompressImageBlock(Stream stream, TiffCompressionType compressionType, uint offset, uint byteCount, byte[] buffer)
        {
            stream.Seek(offset, SeekOrigin.Begin);

            switch (compressionType)
            {
                case TiffCompressionType.None:
                    NoneTiffCompression.Decompress(stream, (int)byteCount, buffer);
                    break;
                case TiffCompressionType.PackBits:
                    PackBitsTiffCompression.Decompress(stream, (int)byteCount, buffer);
                    break;
                case TiffCompressionType.Deflate:
                    DeflateTiffCompression.Decompress(stream, (int)byteCount, buffer);
                    break;
                case TiffCompressionType.Lzw:
                    LzwTiffCompression.Decompress(stream, (int)byteCount, buffer);
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}
