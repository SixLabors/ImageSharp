// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    internal sealed class DownScalingComponentProcessor2 : ComponentProcessor
    {
        private readonly IRawJpegData rawJpeg;

        public DownScalingComponentProcessor2(MemoryAllocator memoryAllocator, JpegFrame frame, IRawJpegData rawJpeg, Size postProcessorBufferSize, IJpegComponent component)
            : base(memoryAllocator, frame, postProcessorBufferSize, component, 4)
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
                    TransformIDCT_4x4(ref workspaceBlock, ref dequantTable, normalizationValue, maximumValue);

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

        public static void TransformIDCT_4x4(ref Block8x8F block, ref Block8x8F dequantTable, float normalizationValue, float maxValue)
        {
            const int DCTSIZE = 8;
            const float FIX_0_541196100 = 0.541196100f;
            const float FIX_0_765366865 = 0.765366865f;
            const float FIX_1_847759065 = 1.847759065f;

            // input block is transposed so term indices must be tranposed too
            float tmp0, tmp2, tmp10, tmp12;
            float z1, z2, z3;

            for (int ctr = 0; ctr < 4; ctr++)
            {
                // Even part
                tmp0 = block[ctr * DCTSIZE] * dequantTable[ctr * DCTSIZE];
                tmp2 = block[(ctr * DCTSIZE) + 2] * dequantTable[(ctr * DCTSIZE) + 2];

                tmp10 = tmp0 + tmp2;
                tmp12 = tmp0 - tmp2;

                // Odd part
                z2 = block[(ctr * DCTSIZE) + 1] * dequantTable[(ctr * DCTSIZE) + 1];
                z3 = block[(ctr * DCTSIZE) + 3] * dequantTable[(ctr * DCTSIZE) + 3];

                z1 = (z2 + z3) * FIX_0_541196100;
                tmp0 = z1 + (z2 * FIX_0_765366865);
                tmp2 = z1 - (z3 * FIX_1_847759065);

                /* Final output stage */
                block[ctr + 4] = tmp10 + tmp0;
                block[ctr + 28] = tmp10 - tmp0;
                block[ctr + 12] = tmp12 + tmp2;
                block[ctr + 20] = tmp12 - tmp2;
            }

            for (int ctr = 0; ctr < 4; ctr++)
            {
                // Even part
                tmp0 = block[(ctr * 8) + 0 + 4];
                tmp2 = block[(ctr * 8) + 2 + 4];

                tmp10 = tmp0 + tmp2;
                tmp12 = tmp0 - tmp2;

                // Odd part
                z2 = block[(ctr * 8) + 1 + 4];
                z3 = block[(ctr * 8) + 3 + 4];

                z1 = (z2 + z3) * FIX_0_541196100;
                tmp0 = z1 + (z2 * FIX_0_765366865);
                tmp2 = z1 - (z3 * FIX_1_847759065);

                /* Final output stage */
                block[(ctr * 8) + 0] = (float)Math.Round(Numerics.Clamp(tmp10 + tmp0 + normalizationValue, 0, maxValue));
                block[(ctr * 8) + 3] = (float)Math.Round(Numerics.Clamp(tmp10 - tmp0 + normalizationValue, 0, maxValue));
                block[(ctr * 8) + 1] = (float)Math.Round(Numerics.Clamp(tmp12 + tmp2 + normalizationValue, 0, maxValue));
                block[(ctr * 8) + 2] = (float)Math.Round(Numerics.Clamp(tmp12 - tmp2 + normalizationValue, 0, maxValue));
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ScaledCopyTo(ref Block8x8F block, ref float destRef, int destStrideWidth, int horizontalScale, int verticalScale)
        {
            // TODO: Optimize: implement all cases with scale-specific, loopless code!
            CopyArbitraryScale(ref block, ref destRef, destStrideWidth, horizontalScale, verticalScale);

            [MethodImpl(InliningOptions.ColdPath)]
            static void CopyArbitraryScale(ref Block8x8F block, ref float areaOrigin, int areaStride, int horizontalScale, int verticalScale)
            {
                for (int y = 0; y < 4; y++)
                {
                    int yy = y * verticalScale;
                    int y8 = y * 8;

                    for (int x = 0; x < 4; x++)
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
