// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;

using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression
{
    /// <summary>
    /// Base tiff decompressor class.
    /// </summary>
    internal abstract class TiffBaseCompression
    {
        private readonly MemoryAllocator allocator;

        private TiffPhotometricInterpretation photometricInterpretation;

        private int width;

        public TiffBaseCompression(MemoryAllocator allocator) => this.allocator = allocator;

        public TiffBaseCompression(MemoryAllocator allocator, TiffPhotometricInterpretation photometricInterpretation, int width)
        {
            this.allocator = allocator;
            this.photometricInterpretation = photometricInterpretation;
            this.width = width;
        }

        protected MemoryAllocator Allocator => this.allocator;

        protected TiffPhotometricInterpretation PhotometricInterpretation => this.photometricInterpretation;

        protected int Width => this.width;

        /// <summary>
        /// Decompresses image data into the supplied buffer.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to read image data from.</param>
        /// <param name="byteCount">The number of bytes to read from the input stream.</param>
        /// <param name="buffer">The output buffer for uncompressed data.</param>
        public abstract void Decompress(Stream stream, int byteCount, Span<byte> buffer);
    }
}
