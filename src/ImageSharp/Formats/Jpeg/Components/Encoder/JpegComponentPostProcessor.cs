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

        private Block8x8F quantTable;

        public JpegComponentPostProcessor(MemoryAllocator memoryAllocator, JpegComponent component, Size postProcessorBufferSize, Block8x8F quantTable)
        {
            this.component = component;
            this.quantTable = quantTable;
            FastFloatingPointDCT.AdjustToFDCT(ref this.quantTable);

            this.component = component;
            this.blockAreaSize = component.SubSamplingDivisors * 8;
            this.ColorBuffer = memoryAllocator.Allocate2DOveraligned<float>(
                postProcessorBufferSize.Width,
                postProcessorBufferSize.Height,
                8 * component.SubSamplingDivisors.Height,
                AllocationOptions.Clean);
        }

        /// <summary>
        /// Gets the temporary working buffer of color values.
        /// </summary>
        public Buffer2D<float> ColorBuffer { get; }

        public void CopyColorBufferToBlocks(int spectralStep)
        {
            Buffer2D<Block8x8> spectralBuffer = this.component.SpectralBlocks;

            // should be this.frame.MaxColorChannelValue
            // but 12-bit jpegs are not supported currently
            float normalizationValue = -128f;

            int destAreaStride = this.ColorBuffer.Width * this.component.SubSamplingDivisors.Height;

            int yBlockStart = spectralStep * this.component.SamplingFactors.Height;

            Block8x8F workspaceBlock = default;

            // handle subsampling
            this.PackColorBuffer();

            for (int y = 0; y < spectralBuffer.Height; y++)
            {
                int yBuffer = y * this.blockAreaSize.Height;
                for (int xBlock = 0; xBlock < spectralBuffer.Width; xBlock++)
                {
                    Span<float> colorBufferRow = this.ColorBuffer.DangerousGetRowSpan(yBuffer);
                    Span<Block8x8> blockRow = spectralBuffer.DangerousGetRowSpan(yBlockStart + y);

                    // load 8x8 block from 8 pixel strides
                    int xColorBufferStart = xBlock * 8;
                    workspaceBlock.ScaledCopyFrom(
                        ref colorBufferRow[xColorBufferStart],
                        destAreaStride);

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

        private void PackColorBuffer()
        {
            Size factors = this.component.SubSamplingDivisors;

            if (factors.Width == 1 && factors.Height == 1)
            {
                return;
            }

            for (int i = 0; i < this.ColorBuffer.Height; i += factors.Height)
            {
                Span<float> targetBufferRow = this.ColorBuffer.DangerousGetRowSpan(i);

                // vertical sum
                for (int j = 1; j < factors.Height; j++)
                {
                    SumVertical(targetBufferRow, this.ColorBuffer.DangerousGetRowSpan(i + j));
                }

                // horizontal sum
                SumHorizontal(targetBufferRow, factors.Width);

                // calculate average
                float multiplier = 1f / (factors.Width * factors.Height);
                MultiplyToAverage(targetBufferRow, multiplier);
            }

            static void SumVertical(Span<float> target, Span<float> source)
            {
                for (int i = 0; i < target.Length; i++)
                {
                    target[i] += source[i];
                }
            }

            static void SumHorizontal(Span<float> target, int factor)
            {
                for (int i = 0; i < target.Length / factor; i++)
                {
                    target[i] = target[i * factor];
                    for (int j = 1; j < factor; j++)
                    {
                        target[i] += target[(i * factor) + j];
                    }
                }
            }

            static void MultiplyToAverage(Span<float> target, float multiplier)
            {
                for (int i = 0; i < target.Length; i++)
                {
                    target[i] *= multiplier;
                }
            }
        }
    }
}
