// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO.Compression;

using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Formats.Experimental.Tiff.Constants;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff.Compression.Decompressors
{
    /// <summary>
    /// Class to handle cases where TIFF image data is compressed using Deflate compression.
    /// </summary>
    /// <remarks>
    /// Note that the 'OldDeflate' compression type is identical to the 'Deflate' compression type.
    /// </remarks>
    internal class DeflateTiffCompression : TiffBaseDecompresor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeflateTiffCompression" /> class.
        /// </summary>
        /// <param name="memoryAllocator">The memoryAllocator to use for buffer allocations.</param>
        /// <param name="width">The image width.</param>
        /// <param name="bitsPerPixel">The bits used per pixel.</param>
        /// <param name="predictor">The tiff predictor used.</param>
        public DeflateTiffCompression(MemoryAllocator memoryAllocator, int width, int bitsPerPixel, TiffPredictor predictor)
            : base(memoryAllocator, width, bitsPerPixel, predictor)
        {
        }

        /// <inheritdoc/>
        protected override void Decompress(BufferedReadStream stream, int byteCount, Span<byte> buffer)
        {
            long pos = stream.Position;
            using (var deframeStream = new ZlibInflateStream(
                stream,
                () =>
                {
                    int left = (int)(byteCount - (stream.Position - pos));
                    return left > 0 ? left : 0;
                }))
            {
                deframeStream.AllocateNewBytes(byteCount, true);
                DeflateStream dataStream = deframeStream.CompressedStream;
                dataStream.Read(buffer, 0, buffer.Length);
            }

            if (this.Predictor == TiffPredictor.Horizontal)
            {
                HorizontalPredictor.Undo(buffer, this.Width, this.BitsPerPixel);
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
        }
    }
}
