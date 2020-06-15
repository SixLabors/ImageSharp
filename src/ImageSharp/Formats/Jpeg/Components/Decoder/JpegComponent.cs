// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Represents a single frame component.
    /// </summary>
    internal sealed class JpegComponent : IDisposable, IJpegComponent
    {
        private readonly MemoryAllocator memoryAllocator;

        public JpegComponent(MemoryAllocator memoryAllocator, JpegFrame frame, byte id, int horizontalFactor, int verticalFactor, byte quantizationTableIndex, int index)
        {
            this.memoryAllocator = memoryAllocator;
            this.Frame = frame;
            this.Id = id;

            // Validate sampling factors.
            if (horizontalFactor == 0 || verticalFactor == 0)
            {
                JpegThrowHelper.ThrowBadSampling();
            }

            this.HorizontalSamplingFactor = horizontalFactor;
            this.VerticalSamplingFactor = verticalFactor;
            this.SamplingFactors = new Size(this.HorizontalSamplingFactor, this.VerticalSamplingFactor);

            if (quantizationTableIndex > 3)
            {
                JpegThrowHelper.ThrowBadQuantizationTable();
            }

            this.QuantizationTableIndex = quantizationTableIndex;
            this.Index = index;
        }

        /// <summary>
        /// Gets the component id.
        /// </summary>
        public byte Id { get; }

        /// <summary>
        /// Gets or sets DC coefficient predictor.
        /// </summary>
        public int DcPredictor { get; set; }

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
        public Size SizeInBlocks { get; private set; }

        /// <inheritdoc />
        public Size SamplingFactors { get; set; }

        /// <summary>
        /// Gets the number of blocks per line.
        /// </summary>
        public int WidthInBlocks { get; private set; }

        /// <summary>
        /// Gets the number of blocks per column.
        /// </summary>
        public int HeightInBlocks { get; private set; }

        /// <summary>
        /// Gets or sets the index for the DC Huffman table.
        /// </summary>
        public int DCHuffmanTableId { get; set; }

        /// <summary>
        /// Gets or sets the index for the AC Huffman table.
        /// </summary>
        public int ACHuffmanTableId { get; set; }

        public JpegFrame Frame { get; }

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

            int blocksPerLineForMcu = this.Frame.McusPerLine * this.HorizontalSamplingFactor;
            int blocksPerColumnForMcu = this.Frame.McusPerColumn * this.VerticalSamplingFactor;
            this.SizeInBlocks = new Size(blocksPerLineForMcu, blocksPerColumnForMcu);

            JpegComponent c0 = this.Frame.Components[0];
            this.SubSamplingDivisors = c0.SamplingFactors.DivideBy(this.SamplingFactors);

            if (this.SubSamplingDivisors.Width == 0 || this.SubSamplingDivisors.Height == 0)
            {
                JpegThrowHelper.ThrowBadSampling();
            }

            int totalNumberOfBlocks = blocksPerColumnForMcu * (blocksPerLineForMcu + 1);
            int width = this.WidthInBlocks + 1;
            int height = totalNumberOfBlocks / width;

            this.SpectralBlocks = this.memoryAllocator.Allocate2D<Block8x8>(width, height, AllocationOptions.Clean);
        }
    }
}
