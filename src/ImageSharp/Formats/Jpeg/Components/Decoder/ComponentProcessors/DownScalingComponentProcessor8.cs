// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
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
            float normalizationValue = MathF.Ceiling(maximumValue / 2);

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

            if (horizontalScale == 2 && verticalScale == 2)
            {
                destRef = value;
                Unsafe.Add(ref destRef, 1) = value;
                Unsafe.Add(ref destRef, 0 + (nint)(uint)destStrideWidth) = value;
                Unsafe.Add(ref destRef, 1 + (nint)(uint)destStrideWidth) = value;
                return;
            }

            // TODO: Optimize: implement all cases with scale-specific, loopless code!
            for (int y = 0; y < verticalScale; y++)
            {
                for (int x = 0; x < horizontalScale; x++)
                {
                    Unsafe.Add(ref destRef, (nint)(uint)x) = value;
                }

                destRef = ref Unsafe.Add(ref destRef, (nint)(uint)destStrideWidth);
            }
        }
    }
}
