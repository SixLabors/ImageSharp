// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff.Compression.Compressors
{
    internal class PackBitsCompressor : TiffBaseCompressor
    {
        private IManagedByteBuffer pixelData;

        public PackBitsCompressor(Stream output, MemoryAllocator allocator, int width, int bitsPerPixel)
            : base(output, allocator, width, bitsPerPixel)
        {
        }

        /// <inheritdoc/>
        public override TiffEncoderCompression Method => TiffEncoderCompression.PackBits;

        /// <inheritdoc/>
        public override void Initialize(int rowsPerStrip)
        {
            int additionalBytes = ((this.BytesPerRow + 126) / 127) + 1;
            this.pixelData = this.Allocator.AllocateManagedByteBuffer(this.BytesPerRow + additionalBytes);
        }

        /// <inheritdoc/>
        public override void CompressStrip(Span<byte> rows, int height)
        {
            DebugGuard.IsTrue(rows.Length % height == 0, "Invalid height");
            DebugGuard.IsTrue(this.BytesPerRow == rows.Length / height, "The widths must match");

            Span<byte> span = this.pixelData.GetSpan();
            for (int i = 0; i < height; i++)
            {
                Span<byte> row = rows.Slice(i * this.BytesPerRow, this.BytesPerRow);
                int size = PackBitsWriter.PackBits(row, span);
                this.Output.Write(span.Slice(0, size));
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing) => this.pixelData?.Dispose();
    }
}
