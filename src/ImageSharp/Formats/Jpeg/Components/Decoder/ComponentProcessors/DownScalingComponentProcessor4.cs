// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    internal sealed class DownScalingComponentProcessor4 : ComponentProcessor
    {
        private readonly IRawJpegData rawJpeg;

        public DownScalingComponentProcessor4(MemoryAllocator memoryAllocator, JpegFrame frame, IRawJpegData rawJpeg, Size postProcessorBufferSize, IJpegComponent component)
            : base(memoryAllocator, frame, postProcessorBufferSize, component, 2)
            => this.rawJpeg = rawJpeg;

        public override void CopyBlocksToColorBuffer(int spectralStep)
        {
            Buffer2D<Block8x8> spectralBuffer = this.Component.SpectralBlocks;

            float maximumValue = this.Frame.MaxColorChannelValue;
            float normalizationValue = MathF.Ceiling(maximumValue / 2);

            int destAreaStride = this.ColorBuffer.Width;

            int blocksRowsPerStep = this.Component.SamplingFactors.Height;
            Size subSamplingDivisors = this.Component.SubSamplingDivisors;

            Block8x8F dequantTable = this.rawJpeg.QuantizationTables[this.Component.QuantizationTableIndex];
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
                    TransformIDCT_2x2(ref workspaceBlock, ref dequantTable, normalizationValue, maximumValue);

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

        public static void TransformIDCT_2x2(ref Block8x8F block, ref Block8x8F dequantTable, float normalizationValue, float maxValue)
        {
            // input block is transposed so term indices must be tranposed too
            float tmp0, tmp1, tmp2, tmp3, tmp4, tmp5;

            // 0
            //   => 0 1
            // 8
            tmp4 = block[0] * dequantTable[0];
            tmp5 = block[1] * dequantTable[1];
            tmp0 = tmp4 + tmp5;
            tmp2 = tmp4 - tmp5;

            // 1
            //   => 8 9
            // 9
            tmp4 = block[8] * dequantTable[8];
            tmp5 = block[9] * dequantTable[9];
            tmp1 = tmp4 + tmp5;
            tmp3 = tmp4 - tmp5;

            // Row 0
            block[0] = (float)Math.Round(Numerics.Clamp(tmp0 + tmp1 + normalizationValue, 0, maxValue));
            block[1] = (float)Math.Round(Numerics.Clamp(tmp0 - tmp1 + normalizationValue, 0, maxValue));

            // Row 1
            block[8] = (float)Math.Round(Numerics.Clamp(tmp2 + tmp3 + normalizationValue, 0, maxValue));
            block[9] = (float)Math.Round(Numerics.Clamp(tmp2 - tmp3 + normalizationValue, 0, maxValue));
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ScaledCopyTo(ref Block8x8F block, ref float destRef, int destStrideWidth, int horizontalScale, int verticalScale)
        {
            // TODO: Optimize: implement all cases with scale-specific, loopless code!
            CopyArbitraryScale(ref block, ref destRef, destStrideWidth, horizontalScale, verticalScale);

            [MethodImpl(InliningOptions.ColdPath)]
            static void CopyArbitraryScale(ref Block8x8F block, ref float areaOrigin, int areaStride, int horizontalScale, int verticalScale)
            {
                for (int y = 0; y < 2; y++)
                {
                    int yy = y * verticalScale;
                    int y8 = y * 8;

                    for (int x = 0; x < 2; x++)
                    {
                        int xx = x * horizontalScale;

                        float value = block[y8 + x];

                        for (int i = 0; i < verticalScale; i++)
                        {
                            int baseIdx = ((yy + i) * areaStride) + xx;

                            for (int j = 0; j < horizontalScale; j++)
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
}
