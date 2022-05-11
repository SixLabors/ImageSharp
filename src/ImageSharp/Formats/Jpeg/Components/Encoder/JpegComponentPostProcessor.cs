// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
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
            Size subsamplingFactors = this.component.SubSamplingDivisors;
            if (subsamplingFactors.Width != 1 || subsamplingFactors.Height != 1)
            {
                this.PackColorBuffer();
            }

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

            float averageMultiplier = 1f / (factors.Width * factors.Height);
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
                MultiplyToAverage(targetBufferRow, averageMultiplier);
            }

            static void SumVertical(Span<float> target, Span<float> source)
            {
                if (Avx.IsSupported)
                {
                    ref Vector256<float> targetVectorRef = ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(target));
                    ref Vector256<float> sourceVectorRef = ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(source));

                    // Spans are guaranteed to be multiple of 8 so no extra 'remainder' steps are needed
                    nint count = source.Length / Vector256<float>.Count;
                    for (nint i = 0; i < count; i++)
                    {
                        Unsafe.Add(ref targetVectorRef, i) = Avx.Add(Unsafe.Add(ref targetVectorRef, i), Unsafe.Add(ref sourceVectorRef, i));
                    }
                }
                else
                {
                    ref Vector<float> targetVectorRef = ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(target));
                    ref Vector<float> sourceVectorRef = ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(source));

                    nint count = source.Length / Vector<float>.Count;
                    for (nint i = 0; i < count; i++)
                    {
                        Unsafe.Add(ref targetVectorRef, i) += Unsafe.Add(ref sourceVectorRef, i);
                    }

                    ref float targetRef = ref MemoryMarshal.GetReference(target);
                    ref float sourceRef = ref MemoryMarshal.GetReference(source);
                    for (nint i = count * Vector<float>.Count; i < source.Length; i++)
                    {
                        Unsafe.Add(ref targetRef, i) += Unsafe.Add(ref sourceRef, i);
                    }
                }
            }

            static void SumHorizontal(Span<float> target, int factor)
            {
                if (Avx2.IsSupported)
                {
                    ref Vector256<float> targetRef = ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(target));

                    // Ideally we need to use log2: Numerics.Log2((uint)factor)
                    // but division by 2 works just fine in this case
                    // because factor value range is [1, 2, 4]
                    // log2(1) == 1 / 2 == 0
                    // log2(2) == 2 / 2 == 1
                    // log2(4) == 4 / 2 == 2
                    int haddIterationsCount = (int)((uint)factor / 2);
                    int length = target.Length / Vector256<float>.Count;
                    for (int i = 0; i < haddIterationsCount; i++)
                    {
                        length /= 2;
                        for (int j = 0; j < length; j++)
                        {
                            int indexLeft = j * 2;
                            int indexRight = indexLeft + 1;
                            Vector256<float> sum = Avx.HorizontalAdd(Unsafe.Add(ref targetRef, indexLeft), Unsafe.Add(ref targetRef, indexRight));
                            Unsafe.Add(ref targetRef, j) = Avx2.Permute4x64(sum.AsDouble(), 0b11_01_10_00).AsSingle();
                        }
                    }

                    int summedCount = length * factor * Vector256<float>.Count;
                    target = target.Slice(summedCount);
                }

                // scalar remainder
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
                if (Avx.IsSupported)
                {
                    ref Vector256<float> targetVectorRef = ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(target));

                    // Spans are guaranteed to be multiple of 8 so no extra 'remainder' steps are needed
                    nint count = target.Length / Vector256<float>.Count;
                    var multiplierVector = Vector256.Create(multiplier);
                    for (nint i = 0; i < count; i++)
                    {
                        Unsafe.Add(ref targetVectorRef, i) = Avx.Multiply(Unsafe.Add(ref targetVectorRef, i), multiplierVector);
                    }
                }
                else
                {
                    ref Vector<float> targetVectorRef = ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(target));

                    nint count = target.Length / Vector<float>.Count;
                    var multiplierVector = new Vector<float>(multiplier);
                    for (nint i = 0; i < count; i++)
                    {
                        Unsafe.Add(ref targetVectorRef, i) *= multiplierVector;
                    }

                    ref float targetRef = ref MemoryMarshal.GetReference(target);
                    for (nint i = count * Vector<float>.Count; i < target.Length; i++)
                    {
                        Unsafe.Add(ref targetRef, i) *= multiplier;
                    }
                }
            }
        }
    }
}
