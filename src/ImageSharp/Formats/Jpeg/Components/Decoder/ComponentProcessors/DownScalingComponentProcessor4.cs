// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;

/// <summary>
/// Processes component spectral data and converts it to color data in 4-to-1 scale.
/// </summary>
internal sealed class DownScalingComponentProcessor4 : ComponentProcessor
{
    private Block8x8F dequantizationTable;

    public DownScalingComponentProcessor4(MemoryAllocator memoryAllocator, JpegFrame frame, IRawJpegData rawJpeg, Size postProcessorBufferSize, IJpegComponent component)
        : base(memoryAllocator, frame, postProcessorBufferSize, component, 2)
    {
        this.dequantizationTable = rawJpeg.QuantizationTables[component.QuantizationTableIndex];
        ScaledFloatingPointDCT.AdjustToIDCT(ref this.dequantizationTable);
    }

    public override void CopyBlocksToColorBuffer(int spectralStep)
    {
        Buffer2D<Block8x8> spectralBuffer = this.Component.SpectralBlocks;

        float maximumValue = this.Frame.MaxColorChannelValue;
        float normalizationValue = MathF.Ceiling(maximumValue * 0.5F);  // /2

        int destAreaStride = this.ColorBuffer.Width;

        int blocksRowsPerStep = this.Component.SamplingFactors.Height;
        Size subSamplingDivisors = this.Component.SubSamplingDivisors;

        Block8x8F workspaceBlock = default;

        int yBlockStart = spectralStep * blocksRowsPerStep;

        for (int y = 0; y < blocksRowsPerStep; y++)
        {
            int yBuffer = y * this.BlockAreaSize.Height;

            Span<float> colorBufferRow = this.ColorBuffer.DangerousGetRowSpan(yBuffer);
            Span<Block8x8> blockRow = spectralBuffer.DangerousGetRowSpan(yBlockStart + y);

            for (int xBlock = 0; xBlock < spectralBuffer.Width; xBlock++)
            {
                // Integer to float
                workspaceBlock.LoadFrom(ref blockRow[xBlock]);

                // IDCT/Normalization/Range
                ScaledFloatingPointDCT.TransformIDCT_2x2(ref workspaceBlock, ref this.dequantizationTable, normalizationValue, maximumValue);

                // Save to the intermediate buffer
                int xColorBufferStart = xBlock * this.BlockAreaSize.Width;
                ScaledCopyTo(
                    ref workspaceBlock,
                    ref colorBufferRow[xColorBufferStart],
                    destAreaStride,
                    subSamplingDivisors.Width,
                    subSamplingDivisors.Height);
            }
        }
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    public static void ScaledCopyTo(ref Block8x8F block, ref float destRef, int destStrideWidth, int horizontalScale, int verticalScale)
    {
        // TODO: Optimize: implement all cases with scale-specific, loopless code!
        CopyArbitraryScale(ref block, ref destRef, (uint)destStrideWidth, (uint)horizontalScale, (uint)verticalScale);

        [MethodImpl(InliningOptions.ColdPath)]
        static void CopyArbitraryScale(ref Block8x8F block, ref float areaOrigin, uint areaStride, uint horizontalScale, uint verticalScale)
        {
            for (nuint y = 0; y < 2; y++)
            {
                nuint yy = y * verticalScale;
                nuint y8 = y * 8;

                for (nuint x = 0; x < 2; x++)
                {
                    nuint xx = x * horizontalScale;

                    float value = block[y8 + x];

                    for (nuint i = 0; i < verticalScale; i++)
                    {
                        nuint baseIdx = ((yy + i) * areaStride) + xx;

                        for (nuint j = 0; j < horizontalScale; j++)
                        {
                            // area[xx + j, yy + i] = value;
                            Unsafe.Add(ref areaOrigin, baseIdx + j) = value;
                        }
                    }
                }
            }
        }
    }
}
