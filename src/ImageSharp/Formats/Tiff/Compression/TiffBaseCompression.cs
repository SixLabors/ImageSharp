// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression
{
    internal abstract class TiffBaseCompression : IDisposable
    {
        private bool isDisposed;

        protected TiffBaseCompression(MemoryAllocator allocator, int width, int bitsPerPixel, TiffPredictor predictor = TiffPredictor.None)
        {
            this.Allocator = allocator;
            this.Width = width;
            this.BitsPerPixel = bitsPerPixel;
            this.Predictor = predictor;
            this.BytesPerRow = ((width * bitsPerPixel) + 7) / 8;
        }

        /// <summary>
        /// Gets the image width.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the bits per pixel.
        /// </summary>
        public int BitsPerPixel { get; }

        /// <summary>
        /// Gets the bytes per row.
        /// </summary>
        public int BytesPerRow { get; }

        /// <summary>
        /// Gets the predictor to use. Should only be used with deflate or lzw compression.
        /// </summary>
        public TiffPredictor Predictor { get; }

        /// <summary>
        /// Gets the memory allocator.
        /// </summary>
        protected MemoryAllocator Allocator { get; }

        /// <inheritdoc />
        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.isDisposed = true;
            this.Dispose(true);
        }

        protected abstract void Dispose(bool disposing);
    }
}
