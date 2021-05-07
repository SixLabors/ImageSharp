// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Formats.Tiff.Compression;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Writers
{
    /// <summary>
    /// The base class for composite color types: 8-bit gray, 24-bit RGB (4-bit gray, 16-bit (565/555) RGB, 32-bit RGB, CMYK, YCbCr).
    /// </summary>
    internal abstract class TiffCompositeColorWriter<TPixel> : TiffBaseColorWriter<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private IManagedByteBuffer rowBuffer;

        protected TiffCompositeColorWriter(ImageFrame<TPixel> image, MemoryAllocator memoryAllocator, Configuration configuration, TiffEncoderEntriesCollector entriesCollector)
            : base(image, memoryAllocator, configuration, entriesCollector)
        {
        }

        protected override void EncodeStrip(int y, int height, TiffBaseCompressor compressor)
        {
            if (this.rowBuffer == null)
            {
                this.rowBuffer = this.MemoryAllocator.AllocateManagedByteBuffer(this.BytesPerRow * height);
            }

            this.rowBuffer.Clear();

            Span<byte> rowSpan = this.rowBuffer.GetSpan().Slice(0, this.BytesPerRow * height);

            Span<TPixel> pixels = GetStripPixels(this.Image.PixelBuffer, y, height);

            this.EncodePixels(pixels, rowSpan);
            compressor.CompressStrip(rowSpan, height);
        }

        protected abstract void EncodePixels(Span<TPixel> pixels, Span<byte> buffer);

        /// <inheritdoc />
        protected override void Dispose(bool disposing) => this.rowBuffer?.Dispose();
    }
}
