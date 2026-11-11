// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;

/// <summary>
/// Processes component spectral data and converts it to color data in 4-to-1 scale.
/// </summary>
internal sealed class DownScalingComponentProcessor4 : ComponentProcessor
{
    private Block8x8F dequantizationTable;

    public DownScalingComponentProcessor4(
        MemoryAllocator memoryAllocator,
        JpegFrame frame,
        IRawJpegData rawJpeg,
        Size postProcessorBufferSize,
        IJpegComponent component)
        : base(memoryAllocator, frame, postProcessorBufferSize, component, 2)
    {
        this.dequantizationTable = rawJpeg.QuantizationTables[component.QuantizationTableIndex];
        ScaledFloatingPointDCT.AdjustToIDCT(ref this.dequantizationTable);
    }

    public override void CopyBlocksToColorBuffer(int spectralStep, bool clearBlocks)
    {
        Buffer2D<Block8x8> spectralBuffer = this.Component.SpectralBlocks;

        float maximumValue = this.Frame.MaxColorChannelValue;

        int destAreaStride = this.ColorBuffer.Width;

        int blocksRowsPerStep = this.Component.SamplingFactors.Height;
        Size subSamplingDivisors = this.Component.SubSamplingDivisors;

        int yBlockStart = spectralStep * blocksRowsPerStep;

        for (int y = 0; y < blocksRowsPerStep; y++)
        {
            int yBuffer = y * this.BlockAreaSize.Height;

            Span<float> colorBufferRow = this.ColorBuffer.DangerousGetRowSpan(yBuffer);
            Span<Block8x8> blockRow = spectralBuffer.DangerousGetRowSpan(yBlockStart + y);

            // Each block covers 2x2 samples, multiplied by the subsampling divisors of the component.
            // The transform writes the samples directly into the color buffer.
            ScaledFloatingPointDCT.TransformIDCT2x2(
                blockRow,
                ref this.dequantizationTable,
                maximumValue,
                ref MemoryMarshal.GetReference(colorBufferRow),
                destAreaStride,
                subSamplingDivisors.Width,
                subSamplingDivisors.Height,
                clearBlocks);
        }
    }
}
