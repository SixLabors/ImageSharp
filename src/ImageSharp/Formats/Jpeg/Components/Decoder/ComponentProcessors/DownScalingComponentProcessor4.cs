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
        float normalizationValue = MathF.Ceiling(maximumValue * 0.5F);

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
        if (horizontalScale == 1 && verticalScale == 1)
        {
            CopyTo1x1Scale(ref block, ref destRef, (uint)destStrideWidth);
            return;
        }

        if (horizontalScale == 2 && verticalScale == 2)
        {
            CopyTo2x2Scale(ref block, ref destRef, (uint)destStrideWidth);
            return;
        }

        if (horizontalScale == 2 && verticalScale == 1)
        {
            CopyTo2x1Scale(ref block, ref destRef, (uint)destStrideWidth);
            return;
        }

        if (horizontalScale == 1 && verticalScale == 2)
        {
            CopyTo1x2Scale(ref block, ref destRef, (uint)destStrideWidth);
            return;
        }

        if (horizontalScale == 4 && verticalScale == 1)
        {
            CopyTo4x1Scale(ref block, ref destRef, (uint)destStrideWidth);
            return;
        }

        if (horizontalScale == 4 && verticalScale == 2)
        {
            CopyTo4x2Scale(ref block, ref destRef, (uint)destStrideWidth);
            return;
        }

        if (horizontalScale == 1 && verticalScale == 4)
        {
            CopyTo1x4Scale(ref block, ref destRef, (uint)destStrideWidth);
            return;
        }

        if (horizontalScale == 2 && verticalScale == 4)
        {
            CopyTo2x4Scale(ref block, ref destRef, (uint)destStrideWidth);
            return;
        }

        if (horizontalScale == 4 && verticalScale == 4)
        {
            CopyTo4x4Scale(ref block, ref destRef, (uint)destStrideWidth);
            return;
        }

        // The common 1x, 2x, and 4x integral scales are specialized above.
        // Uncommon legal factor-3 scales use the generic fallback.
        CopyArbitraryScale(ref block, ref destRef, (uint)destStrideWidth, (uint)horizontalScale, (uint)verticalScale);
    }

    /// <summary>
    /// Copies a 2x2 reduced block directly into the destination buffer when no chroma expansion is needed.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static void CopyTo1x1Scale(ref Block8x8F block, ref float areaOrigin, uint areaStride)
    {
        ref float sourceBase = ref Unsafe.As<Block8x8F, float>(ref block);

        CopyRow2(ref sourceBase, ref areaOrigin, 0u, 0u, areaStride);
        CopyRow2(ref sourceBase, ref areaOrigin, 1u, 1u, areaStride);
    }

    /// <summary>
    /// Copies a 2x2 reduced block into the destination buffer while doubling only the horizontal axis.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static void CopyTo2x1Scale(ref Block8x8F block, ref float areaOrigin, uint areaStride)
    {
        ref float sourceBase = ref Unsafe.As<Block8x8F, float>(ref block);

        WidenRow2(ref sourceBase, ref areaOrigin, 0u, 0u, areaStride);
        WidenRow2(ref sourceBase, ref areaOrigin, 1u, 1u, areaStride);
    }

    /// <summary>
    /// Copies a 2x2 reduced block into the destination buffer while doubling only the vertical axis.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static void CopyTo1x2Scale(ref Block8x8F block, ref float areaOrigin, uint areaStride)
    {
        ref float sourceBase = ref Unsafe.As<Block8x8F, float>(ref block);

        CopyRow2(ref sourceBase, ref areaOrigin, 0u, 0u, areaStride);
        CopyRow2(ref sourceBase, ref areaOrigin, 0u, 1u, areaStride);
        CopyRow2(ref sourceBase, ref areaOrigin, 1u, 2u, areaStride);
        CopyRow2(ref sourceBase, ref areaOrigin, 1u, 3u, areaStride);
    }

    /// <summary>
    /// Copies a 2x2 reduced block into the destination buffer while doubling both axes.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static void CopyTo2x2Scale(ref Block8x8F block, ref float areaOrigin, uint areaStride)
    {
        ref float sourceBase = ref Unsafe.As<Block8x8F, float>(ref block);

        WidenRow2(ref sourceBase, ref areaOrigin, 0u, 0u, areaStride);
        WidenRow2(ref sourceBase, ref areaOrigin, 0u, 1u, areaStride);
        WidenRow2(ref sourceBase, ref areaOrigin, 1u, 2u, areaStride);
        WidenRow2(ref sourceBase, ref areaOrigin, 1u, 3u, areaStride);
    }

    /// <summary>
    /// Copies a 2x2 reduced block into the destination buffer while quadrupling only the horizontal axis.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static void CopyTo4x1Scale(ref Block8x8F block, ref float areaOrigin, uint areaStride)
    {
        ref float sourceBase = ref Unsafe.As<Block8x8F, float>(ref block);

        ExpandRow2(ref sourceBase, ref areaOrigin, 0u, 0u, areaStride);
        ExpandRow2(ref sourceBase, ref areaOrigin, 1u, 1u, areaStride);
    }

    /// <summary>
    /// Copies a 2x2 reduced block into the destination buffer while quadrupling horizontally and doubling vertically.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static void CopyTo4x2Scale(ref Block8x8F block, ref float areaOrigin, uint areaStride)
    {
        ref float sourceBase = ref Unsafe.As<Block8x8F, float>(ref block);

        ExpandRow2(ref sourceBase, ref areaOrigin, 0u, 0u, areaStride);
        ExpandRow2(ref sourceBase, ref areaOrigin, 0u, 1u, areaStride);
        ExpandRow2(ref sourceBase, ref areaOrigin, 1u, 2u, areaStride);
        ExpandRow2(ref sourceBase, ref areaOrigin, 1u, 3u, areaStride);
    }

    /// <summary>
    /// Copies a 2x2 reduced block into the destination buffer while quadrupling only the vertical axis.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static void CopyTo1x4Scale(ref Block8x8F block, ref float areaOrigin, uint areaStride)
    {
        ref float sourceBase = ref Unsafe.As<Block8x8F, float>(ref block);

        CopyRow2(ref sourceBase, ref areaOrigin, 0u, 0u, areaStride);
        CopyRow2(ref sourceBase, ref areaOrigin, 0u, 1u, areaStride);
        CopyRow2(ref sourceBase, ref areaOrigin, 0u, 2u, areaStride);
        CopyRow2(ref sourceBase, ref areaOrigin, 0u, 3u, areaStride);
        CopyRow2(ref sourceBase, ref areaOrigin, 1u, 4u, areaStride);
        CopyRow2(ref sourceBase, ref areaOrigin, 1u, 5u, areaStride);
        CopyRow2(ref sourceBase, ref areaOrigin, 1u, 6u, areaStride);
        CopyRow2(ref sourceBase, ref areaOrigin, 1u, 7u, areaStride);
    }

    /// <summary>
    /// Copies a 2x2 reduced block into the destination buffer while doubling horizontally and quadrupling vertically.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static void CopyTo2x4Scale(ref Block8x8F block, ref float areaOrigin, uint areaStride)
    {
        ref float sourceBase = ref Unsafe.As<Block8x8F, float>(ref block);

        WidenRow2(ref sourceBase, ref areaOrigin, 0u, 0u, areaStride);
        WidenRow2(ref sourceBase, ref areaOrigin, 0u, 1u, areaStride);
        WidenRow2(ref sourceBase, ref areaOrigin, 0u, 2u, areaStride);
        WidenRow2(ref sourceBase, ref areaOrigin, 0u, 3u, areaStride);
        WidenRow2(ref sourceBase, ref areaOrigin, 1u, 4u, areaStride);
        WidenRow2(ref sourceBase, ref areaOrigin, 1u, 5u, areaStride);
        WidenRow2(ref sourceBase, ref areaOrigin, 1u, 6u, areaStride);
        WidenRow2(ref sourceBase, ref areaOrigin, 1u, 7u, areaStride);
    }

    /// <summary>
    /// Copies a 2x2 reduced block into the destination buffer while quadrupling both axes.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static void CopyTo4x4Scale(ref Block8x8F block, ref float areaOrigin, uint areaStride)
    {
        ref float sourceBase = ref Unsafe.As<Block8x8F, float>(ref block);

        ExpandRow2(ref sourceBase, ref areaOrigin, 0u, 0u, areaStride);
        ExpandRow2(ref sourceBase, ref areaOrigin, 0u, 1u, areaStride);
        ExpandRow2(ref sourceBase, ref areaOrigin, 0u, 2u, areaStride);
        ExpandRow2(ref sourceBase, ref areaOrigin, 0u, 3u, areaStride);
        ExpandRow2(ref sourceBase, ref areaOrigin, 1u, 4u, areaStride);
        ExpandRow2(ref sourceBase, ref areaOrigin, 1u, 5u, areaStride);
        ExpandRow2(ref sourceBase, ref areaOrigin, 1u, 6u, areaStride);
        ExpandRow2(ref sourceBase, ref areaOrigin, 1u, 7u, areaStride);
    }

    /// <summary>
    /// Copies one two-sample row from the reduced block to the destination row.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static void CopyRow2(ref float sourceBase, ref float areaOrigin, nuint sourceRow, nuint destRow, uint areaStride)
    {
        ref float source = ref Unsafe.Add(ref sourceBase, sourceRow * 8u);
        ref float dest = ref Unsafe.Add(ref areaOrigin, destRow * areaStride);

        Unsafe.CopyBlock(
            ref Unsafe.As<float, byte>(ref dest),
            ref Unsafe.As<float, byte>(ref source),
            2u * sizeof(float));
    }

    /// <summary>
    /// Expands one two-sample row to four samples by duplicating each source value horizontally.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static void WidenRow2(ref float sourceBase, ref float areaOrigin, nuint sourceRow, nuint destRow, uint areaStride)
    {
        ref float source = ref Unsafe.Add(ref sourceBase, sourceRow * 8u);
        ref float dest = ref Unsafe.Add(ref areaOrigin, destRow * areaStride);

        float value0 = source;
        float value1 = Unsafe.Add(ref source, 1u);

        dest = value0;
        Unsafe.Add(ref dest, 1u) = value0;
        Unsafe.Add(ref dest, 2u) = value1;
        Unsafe.Add(ref dest, 3u) = value1;
    }

    /// <summary>
    /// Expands one two-sample row to eight samples by duplicating each source value four times horizontally.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static void ExpandRow2(ref float sourceBase, ref float areaOrigin, nuint sourceRow, nuint destRow, uint areaStride)
    {
        ref float source = ref Unsafe.Add(ref sourceBase, sourceRow * 8u);
        ref float dest = ref Unsafe.Add(ref areaOrigin, destRow * areaStride);

        float value0 = source;
        float value1 = Unsafe.Add(ref source, 1u);

        dest = value0;
        Unsafe.Add(ref dest, 1u) = value0;
        Unsafe.Add(ref dest, 2u) = value0;
        Unsafe.Add(ref dest, 3u) = value0;
        Unsafe.Add(ref dest, 4u) = value1;
        Unsafe.Add(ref dest, 5u) = value1;
        Unsafe.Add(ref dest, 6u) = value1;
        Unsafe.Add(ref dest, 7u) = value1;
    }

    /// <summary>
    /// Replicates each reduced sample into an arbitrary integral expansion rectangle for uncommon subsampling ratios.
    /// </summary>
    [MethodImpl(InliningOptions.ColdPath)]
    private static void CopyArbitraryScale(ref Block8x8F block, ref float areaOrigin, uint areaStride, uint horizontalScale, uint verticalScale)
    {
        for (nuint y = 0u; y < 2u; y++)
        {
            nuint yy = y * verticalScale;
            nuint y8 = y * 8u;

            for (nuint x = 0u; x < 2u; x++)
            {
                nuint xx = x * horizontalScale;

                float value = block[y8 + x];

                for (nuint i = 0u; i < verticalScale; i++)
                {
                    nuint baseIdx = ((yy + i) * areaStride) + xx;

                    for (nuint j = 0u; j < horizontalScale; j++)
                    {
                        // area[xx + j, yy + i] = value;
                        Unsafe.Add(ref areaOrigin, baseIdx + j) = value;
                    }
                }
            }
        }
    }
}
