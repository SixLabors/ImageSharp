// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Formats.Experimental.Tiff.Constants;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff.Compression.Compressors
{
    internal class DeflateCompressor : TiffBaseCompressor
    {
        private readonly DeflateCompressionLevel compressionLevel;

        private readonly MemoryStream memoryStream = new MemoryStream();

        public DeflateCompressor(Stream output, MemoryAllocator allocator, int width, int bitsPerPixel, TiffPredictor predictor, DeflateCompressionLevel compressionLevel)
            : base(output, allocator, width, bitsPerPixel, predictor)
            => this.compressionLevel = compressionLevel;

        /// <inheritdoc/>
        public override TiffEncoderCompression Method => TiffEncoderCompression.Deflate;

        /// <inheritdoc/>
        public override void Initialize(int rowsPerStrip)
        {
        }

        /// <inheritdoc/>
        public override void CompressStrip(Span<byte> rows, int height)
        {
            this.memoryStream.Seek(0, SeekOrigin.Begin);
            using var stream = new ZlibDeflateStream(this.Allocator, this.memoryStream, this.compressionLevel);

            if (this.Predictor == TiffPredictor.Horizontal)
            {
                HorizontalPredictor.ApplyHorizontalPrediction(rows, this.BytesPerRow, this.BitsPerPixel);
            }

            stream.Write(rows);

            stream.Flush();
            stream.Dispose();

            int size = (int)this.memoryStream.Position;

#if !NETSTANDARD1_3
            byte[] buffer = this.memoryStream.GetBuffer();
            this.Output.Write(buffer, 0, size);
#else
            this.memoryStream.SetLength(size);
            this.memoryStream.Position = 0;
            this.memoryStream.CopyTo(this.Output);
#endif
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
        }
    }
}
