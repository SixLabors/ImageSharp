// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;

using SixLabors.ImageSharp.Formats.Experimental.Tiff.Compression;
using SixLabors.ImageSharp.Formats.Experimental.Tiff.Constants;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff.Writers
{
    internal sealed class TiffBiColorWriter<TPixel> : TiffBaseColorWriter<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly Image<TPixel> imageBlackWhite;

        private IMemoryOwner<byte> pixelsAsGray;

        private IMemoryOwner<byte> bitStrip;

        public TiffBiColorWriter(ImageFrame<TPixel> image, MemoryAllocator memoryAllocator, Configuration configuration, TiffEncoderEntriesCollector entriesCollector)
            : base(image, memoryAllocator, configuration, entriesCollector)
        {
            // Convert image to black and white.
            this.imageBlackWhite = new Image<TPixel>(configuration, new ImageMetadata(), new[] { image.Clone() });
            this.imageBlackWhite.Mutate(img => img.BinaryDither(KnownDitherings.FloydSteinberg));
        }

        /// <inheritdoc/>
        public override int BitsPerPixel => 1;

        /// <inheritdoc/>
        protected override void EncodeStrip(int y, int height, TiffBaseCompressor compressor)
        {
            if (this.pixelsAsGray == null)
            {
                this.pixelsAsGray = this.MemoryAllocator.Allocate<byte>(height * this.Image.Width);
            }

            Span<byte> pixelAsGraySpan = this.pixelsAsGray.Slice(0, height * this.Image.Width);

            Span<TPixel> pixels = GetStripPixels(this.imageBlackWhite.GetRootFramePixelBuffer(), y, height);

            PixelOperations<TPixel>.Instance.ToL8Bytes(this.Configuration, pixels, pixelAsGraySpan, pixels.Length);

            if (compressor.Method == TiffCompression.CcittGroup3Fax || compressor.Method == TiffCompression.Ccitt1D)
            {
                // Special case for T4BitCompressor.
                compressor.CompressStrip(pixelAsGraySpan, height);
            }
            else
            {
                int bytesPerStrip = this.BytesPerRow * height;
                if (this.bitStrip == null)
                {
                    this.bitStrip = this.MemoryAllocator.AllocateManagedByteBuffer(bytesPerStrip);
                }

                Span<byte> rows = this.bitStrip.Slice(0, bytesPerStrip);
                rows.Clear();

                int grayPixelIndex = 0;
                for (int s = 0; s < height; s++)
                {
                    int bitIndex = 0;
                    int byteIndex = 0;
                    Span<byte> outputRow = rows.Slice(s * this.BytesPerRow);
                    for (int x = 0; x < this.Image.Width; x++)
                    {
                        int shift = 7 - bitIndex;
                        if (pixelAsGraySpan[grayPixelIndex++] == 255)
                        {
                            outputRow[byteIndex] |= (byte)(1 << shift);
                        }

                        bitIndex++;
                        if (bitIndex == 8)
                        {
                            byteIndex++;
                            bitIndex = 0;
                        }
                    }
                }

                compressor.CompressStrip(rows, height);
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            this.imageBlackWhite?.Dispose();
            this.pixelsAsGray?.Dispose();
            this.bitStrip?.Dispose();
        }
    }
}
