// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder
{
    /// <summary>
    /// Represents a single frame component.
    /// </summary>
    internal class JpegComponent : IDisposable
    {
        private readonly MemoryAllocator memoryAllocator;

        public JpegComponent(MemoryAllocator memoryAllocator, int horizontalFactor, int verticalFactor, byte quantizationTableIndex)
        {
            this.memoryAllocator = memoryAllocator;

            this.HorizontalSamplingFactor = horizontalFactor;
            this.VerticalSamplingFactor = verticalFactor;
            this.SamplingFactors = new Size(this.HorizontalSamplingFactor, this.VerticalSamplingFactor);

            this.QuantizationTableIndex = quantizationTableIndex;
        }

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

        public Buffer2D<Block8x8> SpectralBlocks { get; private set; }

        public Size SubSamplingDivisors { get; private set; }

        public int QuantizationTableIndex { get; }

        public Size SizeInBlocks { get; private set; }

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
        public int DcTableId { get; set; }

        /// <summary>
        /// Gets or sets the index for the AC Huffman table.
        /// </summary>
        public int AcTableId { get; set; }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.SpectralBlocks?.Dispose();
            this.SpectralBlocks = null;
        }

        /// <summary>
        /// Initializes component for future buffers initialization.
        /// </summary>
        /// <param name="frame">asdfasdf.</param>
        /// <param name="maxSubFactorH">Maximal horizontal subsampling factor among all the components.</param>
        /// <param name="maxSubFactorV">Maximal vertical subsampling factor among all the components.</param>
        public void Init(JpegFrame frame, int maxSubFactorH, int maxSubFactorV)
        {
            this.WidthInBlocks = (int)MathF.Ceiling(
                MathF.Ceiling(frame.PixelWidth / 8F) * this.HorizontalSamplingFactor / maxSubFactorH);

            this.HeightInBlocks = (int)MathF.Ceiling(
                MathF.Ceiling(frame.PixelHeight / 8F) * this.VerticalSamplingFactor / maxSubFactorV);

            int blocksPerLineForMcu = frame.McusPerLine * this.HorizontalSamplingFactor;
            int blocksPerColumnForMcu = frame.McusPerColumn * this.VerticalSamplingFactor;
            this.SizeInBlocks = new Size(blocksPerLineForMcu, blocksPerColumnForMcu);

            this.SubSamplingDivisors = new Size(maxSubFactorH, maxSubFactorV).DivideBy(this.SamplingFactors);

            if (this.SubSamplingDivisors.Width == 0 || this.SubSamplingDivisors.Height == 0)
            {
                JpegThrowHelper.ThrowBadSampling();
            }
        }

        public void AllocateSpectral(bool fullScan)
        {
            int spectralAllocWidth = this.SizeInBlocks.Width;
            int spectralAllocHeight = fullScan ? this.SizeInBlocks.Height : this.VerticalSamplingFactor;

            this.SpectralBlocks = this.memoryAllocator.Allocate2D<Block8x8>(spectralAllocWidth, spectralAllocHeight, AllocationOptions.Clean);
        }
    }
}
