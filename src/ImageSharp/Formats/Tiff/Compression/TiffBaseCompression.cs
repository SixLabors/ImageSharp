// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;

using SixLabors.ImageSharp.Formats.Experimental.Tiff.Constants;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff.Compression
{
    /// <summary>
    /// Base tiff decompressor class.
    /// </summary>
    internal abstract class TiffBaseCompression
    {
        private readonly MemoryAllocator allocator;

        private readonly TiffPhotometricInterpretation photometricInterpretation;

        private readonly int width;

        private readonly int bitsPerPixel;

        private readonly TiffPredictor predictor;

        protected TiffBaseCompression(MemoryAllocator allocator) => this.allocator = allocator;

        protected TiffBaseCompression(MemoryAllocator allocator, TiffPhotometricInterpretation photometricInterpretation, int width)
            : this(allocator)
        {
            this.photometricInterpretation = photometricInterpretation;
            this.width = width;
        }

        protected TiffBaseCompression(MemoryAllocator allocator, int width, int bitsPerPixel, TiffPredictor predictor)
            : this(allocator)
        {
            this.width = width;
            this.bitsPerPixel = bitsPerPixel;
            this.predictor = predictor;
        }

        protected MemoryAllocator Allocator => this.allocator;

        protected TiffPhotometricInterpretation PhotometricInterpretation => this.photometricInterpretation;

        protected int Width => this.width;

        protected int BitsPerPixel => this.bitsPerPixel;

        protected TiffPredictor Predictor => this.predictor;

        /// <summary>
        /// Decompresses image data into the supplied buffer.
        /// </summary>
        /// <param name="stream">The <see cref="Stream" /> to read image data from.</param>
        /// <param name="stripOffset">The strip offset of stream.</param>
        /// <param name="stripByteCount">The number of bytes to read from the input stream.</param>
        /// <param name="buffer">The output buffer for uncompressed data.</param>
        public void Decompress(BufferedReadStream stream, uint stripOffset, uint stripByteCount, Span<byte> buffer)
        {
            if (stripByteCount > int.MaxValue)
            {
                TiffThrowHelper.ThrowImageFormatException("The StripByteCount value is too big.");
            }

            stream.Seek(stripOffset, SeekOrigin.Begin);
            this.Decompress(stream, (int)stripByteCount, buffer);

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
        /// <param name="buffer">The output buffer for uncompressed data.</param>
        protected abstract void Decompress(BufferedReadStream stream, int byteCount, Span<byte> buffer);
    }
}
