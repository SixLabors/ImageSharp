// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder
{
    internal class JpegComponentPostProcessor : IDisposable
    {
        private readonly Size blockAreaSize;

        private readonly JpegComponent component;

        private readonly int blockRowsPerStep;

        private Block8x8F quantTable;

        public JpegComponentPostProcessor(MemoryAllocator memoryAllocator, JpegComponent component, Size postProcessorBufferSize, Block8x8F quantTable)
        {
            this.component = component;
            this.quantTable = quantTable;
            FastFloatingPointDCT.AdjustToFDCT(ref this.quantTable);

            this.component = component;
            this.blockAreaSize = this.component.SubSamplingDivisors * 8;
            this.ColorBuffer = memoryAllocator.Allocate2DOveraligned<float>(
                postProcessorBufferSize.Width,
                postProcessorBufferSize.Height,
                this.blockAreaSize.Height);

            this.blockRowsPerStep = postProcessorBufferSize.Height / 8 / this.component.SubSamplingDivisors.Height;
        }

        /// <summary>
        /// Gets the temporary working buffer of color values.
        /// </summary>
        public Buffer2D<float> ColorBuffer { get; }

        public void CopyColorBufferToBlocks(int spectralStep)
        {
            Buffer2D<Block8x8> spectralBuffer = this.component.SpectralBlocks;

            // should be this.frame.MaxColorChannelValue
            // but currently 12-bit jpegs are not supported
            float maximumValue = 255f;
            float normalizationValue = -128f;

            int blocksRowsPerStep = this.component.SamplingFactors.Height;

            int destAreaStride = this.ColorBuffer.Width;

            int yBlockStart = spectralStep * this.blockRowsPerStep;

            Size subSamplingDivisors = this.component.SubSamplingDivisors;

            Block8x8F workspaceBlock = default;

            for (int y = 0; y < this.blockRowsPerStep; y++)
            {
                int yBuffer = y * this.blockAreaSize.Height;

                for (int xBlock = 0; xBlock < spectralBuffer.Width; xBlock++)
                {
                    Span<float> colorBufferRow = this.ColorBuffer.DangerousGetRowSpan(yBuffer);
                    Span<Block8x8> blockRow = spectralBuffer.DangerousGetRowSpan(yBlockStart + y);

                    // load 8x8 block from 8 pixel strides
                    int xColorBufferStart = xBlock * this.blockAreaSize.Width;
                    workspaceBlock.ScaledCopyFrom(
                        ref colorBufferRow[xColorBufferStart],
                        destAreaStride,
                        subSamplingDivisors.Width,
                        subSamplingDivisors.Height);

                    // multiply by maximum (for debug only, should be done in color converter)
                    workspaceBlock.MultiplyInPlace(maximumValue);

                    // level shift via -128f
                    workspaceBlock.AddInPlace(normalizationValue);

                    // FDCT
                    FastFloatingPointDCT.TransformFDCT(ref workspaceBlock);

                    // Quantize and save to spectral blocks
                    Block8x8F.Quantize(ref workspaceBlock, ref blockRow[xBlock], ref this.quantTable);
                }
            }
        }

        public Span<float> GetColorBufferRowSpan(int row)
            => this.ColorBuffer.DangerousGetRowSpan(row);

        public void Dispose()
            => this.ColorBuffer.Dispose();
    }
}
