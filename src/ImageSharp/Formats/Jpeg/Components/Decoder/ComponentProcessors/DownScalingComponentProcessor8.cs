// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    internal sealed class DownScalingComponentProcessor8 : ComponentProcessor
    {
        private readonly float dcDequantizer;

        public DownScalingComponentProcessor8(MemoryAllocator memoryAllocator, JpegFrame frame, IRawJpegData rawJpeg, Size postProcessorBufferSize, IJpegComponent component)
            : base(memoryAllocator, frame, postProcessorBufferSize, component, 1)
            => this.dcDequantizer = rawJpeg.QuantizationTables[component.QuantizationTableIndex][0];

        public override void CopyBlocksToColorBuffer(int spectralStep)
        {
            Buffer2D<Block8x8> spectralBuffer = this.Component.SpectralBlocks;

            float maximumValue = this.Frame.MaxColorChannelValue;

            int blocksRowsPerStep = this.Component.SamplingFactors.Height;

            int yBlockStart = spectralStep * blocksRowsPerStep;

            for (int y = 0; y < blocksRowsPerStep; y++)
            {
                int yBuffer = y * this.BlockAreaSize.Height;

                Span<float> colorBufferRow = this.ColorBuffer.DangerousGetRowSpan(yBuffer);
                Span<Block8x8> blockRow = spectralBuffer.DangerousGetRowSpan(yBlockStart + y);

                for (int xBlock = 0; xBlock < spectralBuffer.Width; xBlock++)
                {
                    // get Direct current term - averaged 8x8 pixel value
                    float dc = blockRow[xBlock][0];

                    // dequantization
                    dc *= this.dcDequantizer;

                    // Normalize & round
                    dc = (float)Math.Round(Numerics.Clamp(dc + MathF.Ceiling(maximumValue / 2), 0, maximumValue));

                    // Save to the intermediate buffer
                    int xColorBufferStart = xBlock * this.BlockAreaSize.Width;
                    colorBufferRow[xColorBufferStart] = dc;
                }
            }
        }
    }
}
