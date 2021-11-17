// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors
{
    /// <summary>
    /// Class to handle cases where TIFF image data is compressed using CCITT T4 compression.
    /// </summary>
    internal sealed class T4TiffCompression : TiffBaseDecompressor
    {
        private readonly FaxCompressionOptions faxCompressionOptions;

        private readonly byte whiteValue;

        private readonly byte blackValue;

        private readonly int width;

        /// <summary>
        /// Initializes a new instance of the <see cref="T4TiffCompression" /> class.
        /// </summary>
        /// <param name="allocator">The memory allocator.</param>
        /// <param name="fillOrder">The logical order of bits within a byte.</param>
        /// <param name="width">The image width.</param>
        /// <param name="bitsPerPixel">The number of bits per pixel.</param>
        /// <param name="faxOptions">Fax compression options.</param>
        /// <param name="photometricInterpretation">The photometric interpretation.</param>
        public T4TiffCompression(
            MemoryAllocator allocator,
            TiffFillOrder fillOrder,
            int width,
            int bitsPerPixel,
            FaxCompressionOptions faxOptions,
            TiffPhotometricInterpretation photometricInterpretation)
            : base(allocator, width, bitsPerPixel)
        {
            this.faxCompressionOptions = faxOptions;
            this.FillOrder = fillOrder;
            this.width = width;
            bool isWhiteZero = photometricInterpretation == TiffPhotometricInterpretation.WhiteIsZero;
            this.whiteValue = (byte)(isWhiteZero ? 0 : 1);
            this.blackValue = (byte)(isWhiteZero ? 1 : 0);
        }

        /// <summary>
        /// Gets the logical order of bits within a byte.
        /// </summary>
        private TiffFillOrder FillOrder { get; }

        /// <inheritdoc/>
        protected override void Decompress(BufferedReadStream stream, int byteCount, int stripHeight, Span<byte> buffer)
        {
            if (this.faxCompressionOptions.HasFlag(FaxCompressionOptions.TwoDimensionalCoding))
            {
                TiffThrowHelper.ThrowNotSupported("TIFF CCITT 2D compression is not yet supported");
            }

            bool eolPadding = this.faxCompressionOptions.HasFlag(FaxCompressionOptions.EolPadding);
            using var bitReader = new T4BitReader(stream, this.FillOrder, byteCount, this.Allocator, eolPadding);

            buffer.Clear();
            uint bitsWritten = 0;
            uint pixelWritten = 0;
            while (bitReader.HasMoreData)
            {
                bitReader.ReadNextRun();

                if (bitReader.RunLength > 0)
                {
                    this.WritePixelRun(buffer, bitReader, bitsWritten);

                    bitsWritten += bitReader.RunLength;
                    pixelWritten += bitReader.RunLength;
                }

                if (bitReader.IsEndOfScanLine)
                {
                    // Write padding bytes, if necessary.
                    uint pad = 8 - (bitsWritten % 8);
                    if (pad != 8)
                    {
                        BitWriterUtils.WriteBits(buffer, (int)bitsWritten, pad, 0);
                        bitsWritten += pad;
                    }

                    pixelWritten = 0;
                }
            }

            // Edge case for when we are at the last byte, but there are still some unwritten pixels left.
            if (pixelWritten > 0 && pixelWritten < this.width)
            {
                bitReader.ReadNextRun();
                this.WritePixelRun(buffer, bitReader, bitsWritten);
            }
        }

        private void WritePixelRun(Span<byte> buffer, T4BitReader bitReader, uint bitsWritten)
        {
            if (bitReader.IsWhiteRun)
            {
                BitWriterUtils.WriteBits(buffer, (int)bitsWritten, bitReader.RunLength, this.whiteValue);
            }
            else
            {
                BitWriterUtils.WriteBits(buffer, (int)bitsWritten, bitReader.RunLength, this.blackValue);
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
        }
    }
}
