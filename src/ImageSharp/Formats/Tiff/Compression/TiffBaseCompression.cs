// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Formats.Experimental.Tiff.Constants;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff.Compression
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

        public int Width { get; }

        public int BitsPerPixel { get; }

        public int BytesPerRow { get; }

        public TiffPredictor Predictor { get; }

        protected MemoryAllocator Allocator { get; }

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
