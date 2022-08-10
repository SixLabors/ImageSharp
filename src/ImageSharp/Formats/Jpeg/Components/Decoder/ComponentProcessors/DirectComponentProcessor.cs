// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Processes component spectral data and converts it to color data in 1-to-1 scale.
    /// </summary>
    internal sealed class DirectComponentProcessor : ComponentProcessor
    {
        private Block8x8F dequantizationTable;

        public DirectComponentProcessor(MemoryAllocator memoryAllocator, JpegFrame frame, IRawJpegData rawJpeg, Size postProcessorBufferSize, IJpegComponent component)
            : base(memoryAllocator, frame, postProcessorBufferSize, component, blockSize: 8)
        {
            this.dequantizationTable = rawJpeg.QuantizationTables[component.QuantizationTableIndex];
            FloatingPointDCT.AdjustToIDCT(ref this.dequantizationTable);
        }

        public override void CopyBlocksToColorBuffer(int spectralStep)
        {
            Buffer2D<Block8x8> spectralBuffer = this.Component.SpectralBlocks;

            float maximumValue = this.Frame.MaxColorChannelValue;

            int destAreaStride = this.ColorBuffer.Width;

            int blocksRowsPerStep = this.Component.SamplingFactors.Height;

            int yBlockStart = spectralStep * blocksRowsPerStep;

            Size subSamplingDivisors = this.Component.SubSamplingDivisors;

            Block8x8F workspaceBlock = default;

            for (int y = 0; y < blocksRowsPerStep; y++)
            {
                int yBuffer = y * this.BlockAreaSize.Height;

                Span<float> colorBufferRow = this.ColorBuffer.DangerousGetRowSpan(yBuffer);
                Span<Block8x8> blockRow = spectralBuffer.DangerousGetRowSpan(yBlockStart + y);

                for (int xBlock = 0; xBlock < spectralBuffer.Width; xBlock++)
                {
                    // Integer to float
                    workspaceBlock.LoadFrom(ref blockRow[xBlock]);

                    // Dequantize
                    workspaceBlock.MultiplyInPlace(ref this.dequantizationTable);

                    // Convert from spectral to color
                    FloatingPointDCT.TransformIDCT(ref workspaceBlock);

                    // To conform better to libjpeg we actually NEED TO loose precision here.
                    // This is because they store blocks as Int16 between all the operations.
                    // To be "more accurate", we need to emulate this by rounding!
                    workspaceBlock.NormalizeColorsAndRoundInPlace(maximumValue);

                    // Write to color buffer acording to sampling factors
                    int xColorBufferStart = xBlock * this.BlockAreaSize.Width;
                    workspaceBlock.ScaledCopyTo(
                        ref colorBufferRow[xColorBufferStart],
                        destAreaStride,
                        subSamplingDivisors.Width,
                        subSamplingDivisors.Height);
                }
            }
        }
    }
}
