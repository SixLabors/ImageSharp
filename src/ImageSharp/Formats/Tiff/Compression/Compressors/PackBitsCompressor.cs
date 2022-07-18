// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Buffers;
using System.IO;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Compressors
{
    internal sealed class PackBitsCompressor : TiffBaseCompressor
    {
        private IMemoryOwner<byte> pixelData;

        public PackBitsCompressor(Stream output, MemoryAllocator allocator, int width, int bitsPerPixel)
            : base(output, allocator, width, bitsPerPixel)
        {
        }

        /// <inheritdoc/>
        public override TiffCompression Method => TiffCompression.PackBits;

        /// <inheritdoc/>
        public override void Initialize(int rowsPerStrip)
        {
            int additionalBytes = ((this.BytesPerRow + 126) / 127) + 1;
            this.pixelData = this.Allocator.Allocate<byte>(this.BytesPerRow + additionalBytes);
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
