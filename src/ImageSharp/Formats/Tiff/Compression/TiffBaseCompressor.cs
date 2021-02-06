// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.Formats.Experimental.Tiff.Constants;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff.Compression
{
    internal abstract class TiffBaseCompressor : TiffBaseCompression
    {
        protected TiffBaseCompressor(Stream output, MemoryAllocator allocator, int width, int bitsPerPixel, TiffPredictor predictor = TiffPredictor.None)
            : base(allocator, width, bitsPerPixel, predictor)
            => this.Output = output;

        public abstract TiffEncoderCompression Method { get; }

        public Stream Output { get; }

        public abstract void Initialize(int rowsPerStrip);

        public abstract void CompressStrip(Span<byte> rows, int height);
    }
}
