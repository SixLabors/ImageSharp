// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jpeg.Common;
using SixLabors.ImageSharp.Formats.Jpeg.Common.Decoder;
using SixLabors.ImageSharp.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components
{
    /// <summary>
    /// Represents a single frame component
    /// </summary>
    internal class PdfJsFrameComponent : IDisposable, IJpegComponent
    {
        private readonly MemoryManager memoryManager;
#pragma warning disable SA1401 // Fields should be private

        public PdfJsFrameComponent(MemoryManager memoryManager, PdfJsFrame frame, byte id, int horizontalFactor, int verticalFactor, byte quantizationTableIndex, int index)
        {
            this.memoryManager = memoryManager;
            this.Frame = frame;
            this.Id = id;
            this.HorizontalSamplingFactor = horizontalFactor;
            this.VerticalSamplingFactor = verticalFactor;
            this.QuantizationTableIndex = quantizationTableIndex;
            this.Index = index;
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
        public int HorizontalSamplingFactor { get; }

        /// <summary>
        /// Gets the vertical sampling factor.
        /// </summary>
        public int VerticalSamplingFactor { get; }

        /// <inheritdoc />
        public Buffer2D<Block8x8> SpectralBlocks { get; private set; }

        /// <inheritdoc />
        public Size SubSamplingDivisors { get; private set; }

        /// <inheritdoc />
        public int QuantizationTableIndex { get; }

        /// <inheritdoc />
        public int Index { get; }

        /// <inheritdoc />
        public Size SizeInBlocks => new Size(this.WidthInBlocks, this.HeightInBlocks);

        /// <inheritdoc />
        public Size SamplingFactors => new Size(this.HorizontalSamplingFactor, this.VerticalSamplingFactor);

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
            this.SpectralBlocks?.Dispose();
            this.SpectralBlocks = null;
        }

        public void Init()
        {
            this.WidthInBlocks = (int)MathF.Ceiling(
                MathF.Ceiling(this.Frame.SamplesPerLine / 8F) * this.HorizontalSamplingFactor / this.Frame.MaxHorizontalFactor);

            this.HeightInBlocks = (int)MathF.Ceiling(
                MathF.Ceiling(this.Frame.Scanlines / 8F) * this.VerticalSamplingFactor / this.Frame.MaxVerticalFactor);

            this.BlocksPerLineForMcu = this.Frame.McusPerLine * this.HorizontalSamplingFactor;
            this.BlocksPerColumnForMcu = this.Frame.McusPerColumn * this.VerticalSamplingFactor;

            // For 4-component images (either CMYK or YCbCrK), we only support two
            // hv vectors: [0x11 0x11 0x11 0x11] and [0x22 0x11 0x11 0x22].
            // Theoretically, 4-component JPEG images could mix and match hv values
            // but in practice, those two combinations are the only ones in use,
            // and it simplifies the applyBlack code below if we can assume that:
            // - for CMYK, the C and K channels have full samples, and if the M
            // and Y channels subsample, they subsample both horizontally and
            // vertically.
            // - for YCbCrK, the Y and K channels have full samples.
            if (this.Index == 0 || this.Index == 3)
            {
                this.SubSamplingDivisors = new Size(1, 1);
            }
            else
            {
                // TODO: Check division accuracy here. May need to divide by float
                this.SubSamplingDivisors = this.SamplingFactors.DivideBy(new Size(this.Frame.MaxHorizontalFactor, this.Frame.MaxVerticalFactor));
            }

            this.SpectralBlocks = this.memoryManager.Allocate2D<Block8x8>(this.SizeInBlocks.Width, this.SizeInBlocks.Height, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetBlockBufferOffset(int row, int col)
        {
            return 64 * (((this.WidthInBlocks + 1) * row) + col);
        }
    }
}