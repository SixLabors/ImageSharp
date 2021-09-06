// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;

using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression
{
    /// <summary>
    /// The base tiff decompressor class.
    /// </summary>
    internal abstract class TiffBaseDecompressor : TiffBaseCompression
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TiffBaseDecompressor"/> class.
        /// </summary>
        /// <param name="memoryAllocator">The memory allocator.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="bitsPerPixel">The bits per pixel.</param>
        /// <param name="predictor">The predictor.</param>
        protected TiffBaseDecompressor(MemoryAllocator memoryAllocator, int width, int bitsPerPixel, TiffPredictor predictor = TiffPredictor.None)
         : base(memoryAllocator, width, bitsPerPixel, predictor)
        {
        }

        /// <summary>
        /// Decompresses image data into the supplied buffer.
        /// </summary>
        /// <param name="stream">The <see cref="Stream" /> to read image data from.</param>
        /// <param name="stripOffset">The strip offset of stream.</param>
        /// <param name="stripByteCount">The number of bytes to read from the input stream.</param>
        /// <param name="stripHeight">The height of the strip.</param>
        /// <param name="buffer">The output buffer for uncompressed data.</param>
        public void Decompress(BufferedReadStream stream, uint stripOffset, uint stripByteCount, int stripHeight, Span<byte> buffer)
        {
            if (stripByteCount > int.MaxValue)
            {
                TiffThrowHelper.ThrowImageFormatException("The StripByteCount value is too big.");
            }

            stream.Seek(stripOffset, SeekOrigin.Begin);
            this.Decompress(stream, (int)stripByteCount, stripHeight, buffer);

            if (stripOffset + stripByteCount < stream.Position)
            {
                TiffThrowHelper.ThrowImageFormatException("Out of range when reading a strip.");
            }
        }

        /// <summary>
        /// Decompresses image data into the supplied buffer.
        /// </summary>
        /// <param name="stream">The <see cref="Stream" /> to read image data from.</param>
        /// <param name="byteCount">The number of bytes to read from the input stream.</param>
        /// <param name="stripHeight">The height of the strip.</param>
        /// <param name="buffer">The output buffer for uncompressed data.</param>
        protected abstract void Decompress(BufferedReadStream stream, int byteCount, int stripHeight, Span<byte> buffer);
    }
}
