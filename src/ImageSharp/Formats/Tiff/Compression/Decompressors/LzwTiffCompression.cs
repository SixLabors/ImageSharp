// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Formats.Experimental.Tiff.Constants;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff.Compression.Decompressors
{
    /// <summary>
    /// Class to handle cases where TIFF image data is compressed using LZW compression.
    /// </summary>
    internal class LzwTiffCompression : TiffBaseDecompresor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LzwTiffCompression" /> class.
        /// </summary>
        /// <param name="memoryAllocator">The memoryAllocator to use for buffer allocations.</param>
        /// <param name="width">The image width.</param>
        /// <param name="bitsPerPixel">The bits used per pixel.</param>
        /// <param name="predictor">The tiff predictor used.</param>
        public LzwTiffCompression(MemoryAllocator memoryAllocator, int width, int bitsPerPixel, TiffPredictor predictor)
            : base(memoryAllocator, width, bitsPerPixel, predictor)
        {
        }

        /// <inheritdoc/>
        protected override void Decompress(BufferedReadStream stream, int byteCount, Span<byte> buffer)
        {
            var decoder = new TiffLzwDecoder(stream);
            decoder.DecodePixels(buffer);

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
