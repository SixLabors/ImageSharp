// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;

/// <summary>
/// Processes component spectral data and converts it to color data in 8-to-1 scale.
/// </summary>
internal sealed class DownScalingComponentProcessor8 : ComponentProcessor
{
    private readonly float dcDequantizatizer;

    public DownScalingComponentProcessor8(MemoryAllocator memoryAllocator, JpegFrame frame, IRawJpegData rawJpeg, Size postProcessorBufferSize, IJpegComponent component)
        : base(memoryAllocator, frame, postProcessorBufferSize, component, 1)
        => this.dcDequantizatizer = 0.125f * rawJpeg.QuantizationTables[component.QuantizationTableIndex][0];

    public override void CopyBlocksToColorBuffer(int spectralStep)
    {
        Buffer2D<Block8x8> spectralBuffer = this.Component.SpectralBlocks;

        float maximumValue = this.Frame.MaxColorChannelValue;
        float normalizationValue = MathF.Ceiling(maximumValue * 0.5F);

        int destAreaStride = this.ColorBuffer.Width;

        int blocksRowsPerStep = this.Component.SamplingFactors.Height;
        Size subSamplingDivisors = this.Component.SubSamplingDivisors;

        int yBlockStart = spectralStep * blocksRowsPerStep;

        for (int y = 0; y < blocksRowsPerStep; y++)
        {
            int yBuffer = y * this.BlockAreaSize.Height;

            Span<float> colorBufferRow = this.ColorBuffer.DangerousGetRowSpan(yBuffer);
            Span<Block8x8> blockRow = spectralBuffer.DangerousGetRowSpan(yBlockStart + y);

            for (int xBlock = 0; xBlock < spectralBuffer.Width; xBlock++)
            {
                float dc = ScaledFloatingPointDCT.TransformIDCT_1x1(blockRow[xBlock][0], this.dcDequantizatizer, normalizationValue, maximumValue);

                // Save to the intermediate buffer
                int xColorBufferStart = xBlock * this.BlockAreaSize.Width;
                ScaledCopyTo(
                    dc,
                    ref colorBufferRow[xColorBufferStart],
                    destAreaStride,
                    subSamplingDivisors.Width,
                    subSamplingDivisors.Height);
            }
        }
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    public static void ScaledCopyTo(float value, ref float destRef, int destStrideWidth, int horizontalScale, int verticalScale)
    {
        if (horizontalScale == 1 && verticalScale == 1)
        {
            destRef = value;
            return;
        }

        if (horizontalScale == 2 && verticalScale == 1)
        {
            CopyTo2x1Scale(value, ref destRef);
            return;
        }

        if (horizontalScale == 1 && verticalScale == 2)
        {
            CopyTo1x2Scale(value, ref destRef, (uint)destStrideWidth);
            return;
        }

        if (horizontalScale == 2 && verticalScale == 2)
        {
            CopyTo2x2Scale(value, ref destRef, (uint)destStrideWidth);
            return;
        }

        if (horizontalScale == 4 && verticalScale == 1)
        {
            CopyTo4x1Scale(value, ref destRef);
            return;
        }

        if (horizontalScale == 4 && verticalScale == 2)
        {
            CopyTo4x2Scale(value, ref destRef, (uint)destStrideWidth);
            return;
        }

        if (horizontalScale == 1 && verticalScale == 4)
        {
            CopyTo1x4Scale(value, ref destRef, (uint)destStrideWidth);
            return;
        }

        if (horizontalScale == 2 && verticalScale == 4)
        {
            CopyTo2x4Scale(value, ref destRef, (uint)destStrideWidth);
            return;
        }

        if (horizontalScale == 4 && verticalScale == 4)
        {
            CopyTo4x4Scale(value, ref destRef, (uint)destStrideWidth);
            return;
        }

        // The common 1x, 2x, and 4x integral scales are specialized above.
        // Uncommon legal factor-3 scales use the generic fallback.
        CopyArbitraryScale(value, ref destRef, destStrideWidth, horizontalScale, verticalScale);
    }

    [MethodImpl(InliningOptions.ColdPath)]
    private static float CopyArbitraryScale(float value, ref float destRef, int destStrideWidth, int horizontalScale, int verticalScale)
    {
        // The common 1x, 2x, and 4x integral scales are specialized above.
        // Uncommon legal factor-3 scales use the generic fallback.
        for (nuint y = 0; y < (uint)verticalScale; y++)
        {
            for (nuint x = 0; x < (uint)horizontalScale; x++)
            {
                Unsafe.Add(ref destRef, x) = value;
            }

            destRef = ref Unsafe.Add(ref destRef, (uint)destStrideWidth);
        }

        return destRef;
    }

    /// <summary>
    /// Writes a single source value to two horizontally adjacent samples.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static void CopyTo2x1Scale(float value, ref float areaOrigin)
    {
        areaOrigin = value;
        Unsafe.Add(ref areaOrigin, 1u) = value;
    }

    /// <summary>
    /// Writes a single source value to two vertically adjacent samples.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static void CopyTo1x2Scale(float value, ref float areaOrigin, uint areaStride)
    {
        areaOrigin = value;
        Unsafe.Add(ref areaOrigin, areaStride) = value;
    }

    /// <summary>
    /// Writes a single source value to a 2x2 rectangle.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static void CopyTo2x2Scale(float value, ref float areaOrigin, uint areaStride)
    {
        areaOrigin = value;
        Unsafe.Add(ref areaOrigin, 1u) = value;
        Unsafe.Add(ref areaOrigin, areaStride) = value;
        Unsafe.Add(ref areaOrigin, areaStride + 1u) = value;
    }

    /// <summary>
    /// Writes a single source value to four horizontally adjacent samples.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static void CopyTo4x1Scale(float value, ref float areaOrigin)
    {
        areaOrigin = value;
        Unsafe.Add(ref areaOrigin, 1u) = value;
        Unsafe.Add(ref areaOrigin, 2u) = value;
        Unsafe.Add(ref areaOrigin, 3u) = value;
    }

    /// <summary>
    /// Writes a single source value to a 4x2 rectangle.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static void CopyTo4x2Scale(float value, ref float areaOrigin, uint areaStride)
    {
        CopyTo4x1Scale(value, ref areaOrigin);

        ref float nextRow = ref Unsafe.Add(ref areaOrigin, areaStride);
        CopyTo4x1Scale(value, ref nextRow);
    }

    /// <summary>
    /// Writes a single source value to four vertically adjacent samples.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static void CopyTo1x4Scale(float value, ref float areaOrigin, uint areaStride)
    {
        areaOrigin = value;
        Unsafe.Add(ref areaOrigin, areaStride) = value;
        Unsafe.Add(ref areaOrigin, areaStride * 2u) = value;
        Unsafe.Add(ref areaOrigin, areaStride * 3u) = value;
    }

    /// <summary>
    /// Writes a single source value to a 2x4 rectangle.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static void CopyTo2x4Scale(float value, ref float areaOrigin, uint areaStride)
    {
        CopyTo2x1Scale(value, ref areaOrigin);

        ref float row1 = ref Unsafe.Add(ref areaOrigin, areaStride);
        CopyTo2x1Scale(value, ref row1);

        ref float row2 = ref Unsafe.Add(ref areaOrigin, areaStride * 2u);
        CopyTo2x1Scale(value, ref row2);

        ref float row3 = ref Unsafe.Add(ref areaOrigin, areaStride * 3u);
        CopyTo2x1Scale(value, ref row3);
    }

    /// <summary>
    /// Writes a single source value to a 4x4 rectangle.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static void CopyTo4x4Scale(float value, ref float areaOrigin, uint areaStride)
    {
        CopyTo4x1Scale(value, ref areaOrigin);

        ref float row1 = ref Unsafe.Add(ref areaOrigin, areaStride);
        CopyTo4x1Scale(value, ref row1);

        ref float row2 = ref Unsafe.Add(ref areaOrigin, areaStride * 2u);
        CopyTo4x1Scale(value, ref row2);

        ref float row3 = ref Unsafe.Add(ref areaOrigin, areaStride * 3u);
        CopyTo4x1Scale(value, ref row3);
    }
}
