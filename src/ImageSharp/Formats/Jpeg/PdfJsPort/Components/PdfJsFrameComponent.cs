// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components
{
    using SixLabors.ImageSharp.Formats.Jpeg.Common;

    /// <summary>
    /// Represents a single frame component
    /// </summary>
    internal class PdfJsFrameComponent : IDisposable, IJpegComponent
    {
#pragma warning disable SA1401 // Fields should be private

        public PdfJsFrameComponent(PdfJsFrame frame, byte id, int horizontalFactor, int verticalFactor, byte quantizationIdentifier)
        {
            this.Frame = frame;
            this.Id = id;
            this.HorizontalFactor = horizontalFactor;
            this.VerticalFactor = verticalFactor;
            this.QuantizationIdentifier = quantizationIdentifier;
        }

        /// <summary>
        /// Gets the component Id
        /// </summary>
        public byte Id { get; }

        /// <summary>
        /// Gets or sets Pred TODO: What does pred stand for?
        /// </summary>
        public int Pred { get; set; }

        /// <summary>
        /// Gets the horizontal sampling factor.
        /// </summary>
        public int HorizontalFactor { get; }

        /// <summary>
        /// Gets the vertical sampling factor.
        /// </summary>
        public int VerticalFactor { get; }

        /// <summary>
        /// Gets the identifier
        /// </summary>
        public byte QuantizationIdentifier { get; }

        /// <summary>
        /// Gets the block data
        /// </summary>
        public Buffer<short> BlockData { get; private set; }

        /// <summary>
        /// Gets the number of blocks per line
        /// </summary>
        public int WidthInBlocks { get; private set; }

        /// <summary>
        /// Gets the number of blocks per column
        /// </summary>
        public int HeightInBlocks { get; private set; }

        /// <summary>
        /// Gets or sets the index for the DC Huffman table
        /// </summary>
        public int DCHuffmanTableId { get; set; }

        /// <summary>
        /// Gets or sets the index for the AC Huffman table
        /// </summary>
        public int ACHuffmanTableId { get; set; }

        internal int BlocksPerLineForMcu { get; private set; }

        internal int BlocksPerColumnForMcu { get; private set; }

        public PdfJsFrame Frame { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.BlockData?.Dispose();
            this.BlockData = null;
        }

        public void Init()
        {
            this.WidthInBlocks = (int)MathF.Ceiling(
                MathF.Ceiling(this.Frame.SamplesPerLine / 8F) * this.HorizontalFactor / this.Frame.MaxHorizontalFactor);

            this.HeightInBlocks = (int)MathF.Ceiling(
                MathF.Ceiling(this.Frame.Scanlines / 8F) * this.VerticalFactor / this.Frame.MaxVerticalFactor);

            this.BlocksPerLineForMcu = this.Frame.McusPerLine * this.HorizontalFactor;
            this.BlocksPerColumnForMcu = this.Frame.McusPerColumn * this.VerticalFactor;

            int blocksBufferSize = 64 * this.BlocksPerColumnForMcu * (this.BlocksPerLineForMcu + 1);

            // Pooled. Disposed via frame disposal
            this.BlockData = Buffer<short>.CreateClean(blocksBufferSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetBlockBufferOffset(int row, int col)
        {
            return 64 * (((this.WidthInBlocks + 1) * row) + col);
        }

        public Span<short> GetBlockBuffer(int row, int col)
        {
            int offset = this.GetBlockBufferOffset(row, col);
            return this.BlockData.Span.Slice(offset, 64);
        }
    }
}