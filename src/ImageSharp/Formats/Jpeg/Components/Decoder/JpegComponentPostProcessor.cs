// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Encapsulates spectral data to rgba32 processing for one component.
    /// </summary>
    internal class JpegComponentPostProcessor : IDisposable
    {
        /// <summary>
        /// The size of the area in <see cref="ColorBuffer"/> corresponding to one 8x8 Jpeg block
        /// </summary>
        private readonly Size blockAreaSize;

        /// <summary>
        /// Jpeg frame instance containing required decoding metadata.
        /// </summary>
        private readonly JpegFrame frame;

        /// <summary>
        /// Initializes a new instance of the <see cref="JpegComponentPostProcessor"/> class.
        /// </summary>
        public JpegComponentPostProcessor(MemoryAllocator memoryAllocator, JpegFrame frame, IRawJpegData rawJpeg, Size postProcessorBufferSize, IJpegComponent component)
        {
            this.frame = frame;

            this.Component = component;
            this.RawJpeg = rawJpeg;
            this.blockAreaSize = this.Component.SubSamplingDivisors * 8;
            this.ColorBuffer = memoryAllocator.Allocate2DOveraligned<float>(
                postProcessorBufferSize.Width,
                postProcessorBufferSize.Height,
                this.blockAreaSize.Height);

            this.BlockRowsPerStep = postProcessorBufferSize.Height / 8 / this.Component.SubSamplingDivisors.Height;
        }

        public IRawJpegData RawJpeg { get; }

        /// <summary>
        /// Gets the <see cref="Component"/>
        /// </summary>
        public IJpegComponent Component { get; }

        /// <summary>
        /// Gets the temporary working buffer of color values.
        /// </summary>
        public Buffer2D<float> ColorBuffer { get; }

        /// <summary>
        /// Gets <see cref="IJpegComponent.SizeInBlocks"/>
        /// </summary>
        public Size SizeInBlocks => this.Component.SizeInBlocks;

        /// <summary>
        /// Gets the maximal number of block rows being processed in one step.
        /// </summary>
        public int BlockRowsPerStep { get; }

        /// <inheritdoc />
        public void Dispose() => this.ColorBuffer.Dispose();

        /// <summary>
        /// Convert raw spectral data to color data for a specified MCU row.
        /// </summary>
        /// <param name="mcuRow">MCU row to convert.</param>
        public void CopyBlocksToColorBuffer(int mcuRow)
        {
            Buffer2D<Block8x8> spectralBuffer = this.Component.SpectralBlocks;

            float maximumValue = this.frame.MaxColorChannelValue;

            int destAreaStride = this.ColorBuffer.Width;

            int yBlockStart = mcuRow * this.BlockRowsPerStep;

            Size subSamplingDivisors = this.Component.SubSamplingDivisors;

            Block8x8F dequantTable = this.RawJpeg.QuantizationTables[this.Component.QuantizationTableIndex];
            Block8x8F workspaceBlock = default;

            for (int y = 0; y < this.BlockRowsPerStep; y++)
            {
                int yBlock = yBlockStart + y;
                int yBuffer = y * this.blockAreaSize.Height;

                Span<float> colorBufferRow = this.ColorBuffer.GetRowSpan(yBuffer);
                Span<Block8x8> blockRow = spectralBuffer.GetRowSpan(yBlock);
                for (int xBlock = 0; xBlock < blockRow.Length; xBlock++)
                {
                    int xBuffer = xBlock * this.blockAreaSize.Width;
                    ref float destAreaOrigin = ref colorBufferRow[xBuffer];

                    workspaceBlock.LoadFrom(ref blockRow[xBlock]);

                    // Dequantize
                    workspaceBlock.MultiplyInPlace(ref dequantTable);

                    // Convert from spectral to color
                    FastFloatingPointDCT.TransformIDCT(ref workspaceBlock);

                    // To conform better to libjpeg we actually NEED TO loose precision here.
                    // This is because they store blocks as Int16 between all the operations.
                    // To be "more accurate", we need to emulate this by rounding!
                    workspaceBlock.NormalizeColorsAndRoundInPlace(maximumValue);

                    // Write to color buffer acording to sampling factors
                    workspaceBlock.ScaledCopyTo(
                        ref destAreaOrigin,
                        destAreaStride,
                        subSamplingDivisors.Width,
                        subSamplingDivisors.Height);
                }
            }
        }

        /// <summary>
        /// Clear spectral buffer for further reuse.
        /// </summary>
        /// <remarks>
        /// Baseline decoder reuse same spectral buffers for each MCU row.
        /// Decoder may not refill all values of the spectral data. Because of
        /// that spectral buffers may have some old data from previous MCU rows.
        /// </remarks>
        public void ClearSpectralBuffers()
        {
            Buffer2D<Block8x8> spectralBlocks = this.Component.SpectralBlocks;
            for (int i = 0; i < spectralBlocks.Height; i++)
            {
                spectralBlocks.GetRowSpan(i).Clear();
            }
        }
    }
}
