// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.IO;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression
{
    internal abstract class TiffBaseCompressor : TiffBaseCompression
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TiffBaseCompressor"/> class.
        /// </summary>
        /// <param name="output">The output stream to write the compressed image to.</param>
        /// <param name="allocator">The memory allocator.</param>
        /// <param name="width">The image width.</param>
        /// <param name="bitsPerPixel">Bits per pixel.</param>
        /// <param name="predictor">The predictor to use (should only be used with deflate or lzw compression). Defaults to none.</param>
        protected TiffBaseCompressor(Stream output, MemoryAllocator allocator, int width, int bitsPerPixel, TiffPredictor predictor = TiffPredictor.None)
            : base(allocator, width, bitsPerPixel, predictor)
            => this.Output = output;

        /// <summary>
        /// Gets the compression method to use.
        /// </summary>
        public abstract TiffCompression Method { get; }

        /// <summary>
        /// Gets the output stream to write the compressed image to.
        /// </summary>
        public Stream Output { get; }

        /// <summary>
        /// Does any initialization required for the compression.
        /// </summary>
        /// <param name="rowsPerStrip">The number of rows per strip.</param>
        public abstract void Initialize(int rowsPerStrip);

        /// <summary>
        /// Compresses a strip of the image.
        /// </summary>
        /// <param name="rows">Image rows to compress.</param>
        /// <param name="height">Image height.</param>
        public abstract void CompressStrip(Span<byte> rows, int height);
    }
}
