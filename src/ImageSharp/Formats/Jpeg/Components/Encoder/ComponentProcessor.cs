// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder;

internal class ComponentProcessor : IDisposable
{
    private readonly Size blockAreaSize;

    private readonly Component component;

    private Block8x8F quantTable;

    public ComponentProcessor(MemoryAllocator memoryAllocator, Component component, Size postProcessorBufferSize, Block8x8F quantTable)
    {
        this.component = component;
        this.quantTable = quantTable;

        this.component = component;
        this.blockAreaSize = component.SubSamplingDivisors * 8;

        // alignment of 8 so each block stride can be sampled from a single 'ref pointer'
        this.ColorBuffer = memoryAllocator.Allocate2DOveraligned<float>(
            postProcessorBufferSize.Width,
            postProcessorBufferSize.Height,
            8,
            AllocationOptions.Clean);
    }

    /// <summary>
    /// Gets the temporary working buffer of color values.
    /// </summary>
    public Buffer2D<float> ColorBuffer { get; }

    public void CopyColorBufferToBlocks(int spectralStep)
    {
        Buffer2D<Block8x8> spectralBuffer = this.component.SpectralBlocks;
        int destAreaStride = this.ColorBuffer.Width;
        int yBlockStart = spectralStep * this.component.SamplingFactors.Height;

        Block8x8F workspaceBlock = default;

        // handle subsampling
        Size subsamplingFactors = this.component.SubSamplingDivisors;
        if (subsamplingFactors.Width != 1 || subsamplingFactors.Height != 1)
        {
            this.PackColorBuffer();
        }

        int blocksRowsPerStep = this.component.SamplingFactors.Height;

        for (int y = 0; y < blocksRowsPerStep; y++)
        {
            int yBuffer = y * this.blockAreaSize.Height;
            Span<float> colorBufferRow = this.ColorBuffer.DangerousGetRowSpan(yBuffer);
            Span<Block8x8> blockRow = spectralBuffer.DangerousGetRowSpan(yBlockStart + y);
            for (int xBlock = 0; xBlock < spectralBuffer.Width; xBlock++)
            {
                // load 8x8 block from 8 pixel strides
                int xColorBufferStart = xBlock * 8;
                workspaceBlock.ScaledCopyFrom(
                    ref colorBufferRow[xColorBufferStart],
                    destAreaStride);

                // level shift via -128f
                workspaceBlock.AddInPlace(-128f);

                // FDCT
                FloatingPointDCT.TransformFDCT(ref workspaceBlock);

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

        int packedWidth = this.ColorBuffer.Width / factors.Width;

        float averageMultiplier = 1f / (factors.Width * factors.Height);
        for (int i = 0; i < this.ColorBuffer.Height; i += factors.Height)
        {
            Span<float> sourceRow = this.ColorBuffer.DangerousGetRowSpan(i);

            // vertical sum
            for (int j = 1; j < factors.Height; j++)
            {
                SumVertical(sourceRow, this.ColorBuffer.DangerousGetRowSpan(i + j));
            }

            // horizontal sum
            SumHorizontal(sourceRow, factors.Width);

            // calculate average
            MultiplyToAverage(sourceRow, averageMultiplier);

            // copy to the first 8 slots
            sourceRow.Slice(0, packedWidth).CopyTo(this.ColorBuffer.DangerousGetRowSpan(i / factors.Height));
        }

        static void SumVertical(Span<float> target, Span<float> source)
        {
            if (Avx.IsSupported)
            {
                ref Vector256<float> targetVectorRef = ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(target));
                ref Vector256<float> sourceVectorRef = ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(source));

                // Spans are guaranteed to be multiple of 8 so no extra 'remainder' steps are needed
                DebugGuard.IsTrue(source.Length % 8 == 0, "source must be multiple of 8");
                nuint count = source.Vector256Count<float>();
                for (nuint i = 0; i < count; i++)
                {
                    Unsafe.Add(ref targetVectorRef, i) = Avx.Add(Unsafe.Add(ref targetVectorRef, i), Unsafe.Add(ref sourceVectorRef, i));
                }
            }
            else if (AdvSimd.IsSupported)
            {
                ref Vector128<float> targetVectorRef = ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(target));
                ref Vector128<float> sourceVectorRef = ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(source));

                // Spans are guaranteed to be multiple of 8 so no extra 'remainder' steps are needed
                DebugGuard.IsTrue(source.Length % 8 == 0, "source must be multiple of 8");
                nuint count = source.Vector128Count<float>();
                for (nuint i = 0; i < count; i++)
                {
                    Unsafe.Add(ref targetVectorRef, i) = AdvSimd.Add(Unsafe.Add(ref targetVectorRef, i), Unsafe.Add(ref sourceVectorRef, i));
                }
            }
            else
            {
                ref Vector<float> targetVectorRef = ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(target));
                ref Vector<float> sourceVectorRef = ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(source));

                nuint count = source.VectorCount<float>();
                for (nuint i = 0; i < count; i++)
                {
                    Unsafe.Add(ref targetVectorRef, i) += Unsafe.Add(ref sourceVectorRef, i);
                }

                ref float targetRef = ref MemoryMarshal.GetReference(target);
                ref float sourceRef = ref MemoryMarshal.GetReference(source);
                for (nuint i = count * (uint)Vector<float>.Count; i < (uint)source.Length; i++)
                {
                    Unsafe.Add(ref targetRef, i) += Unsafe.Add(ref sourceRef, i);
                }
            }
        }

        static void SumHorizontal(Span<float> target, int factor)
        {
            Span<float> source = target;
            if (Avx2.IsSupported)
            {
                ref Vector256<float> targetRef = ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(target));

                // Ideally we need to use log2: Numerics.Log2((uint)factor)
                // but division by 2 works just fine in this case
                uint haddIterationsCount = (uint)factor / 2;

                // Transform spans so that it only contains 'remainder'
                // values for the scalar fallback code
                int scalarRemainder = target.Length % (Vector<float>.Count * factor);
                int touchedCount = target.Length - scalarRemainder;
                source = source.Slice(touchedCount);
                target = target.Slice(touchedCount / factor);

                nuint length = Numerics.Vector256Count<float>(touchedCount);

                for (uint i = 0; i < haddIterationsCount; i++)
                {
                    length /= 2;

                    for (nuint j = 0; j < length; j++)
                    {
                        nuint indexLeft = j * 2;
                        nuint indexRight = indexLeft + 1;
                        Vector256<float> sum = Avx.HorizontalAdd(Unsafe.Add(ref targetRef, indexLeft), Unsafe.Add(ref targetRef, indexRight));
                        Unsafe.Add(ref targetRef, j) = Avx2.Permute4x64(sum.AsDouble(), 0b11_01_10_00).AsSingle();
                    }
                }
            }

            // scalar remainder
            for (int i = 0; i < source.Length / factor; i++)
            {
                target[i] = source[i * factor];
                for (int j = 1; j < factor; j++)
                {
                    target[i] += source[(i * factor) + j];
                }
            }
        }

        static void MultiplyToAverage(Span<float> target, float multiplier)
        {
            if (Avx.IsSupported)
            {
                ref Vector256<float> targetVectorRef = ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(target));

                // Spans are guaranteed to be multiple of 8 so no extra 'remainder' steps are needed
                DebugGuard.IsTrue(target.Length % 8 == 0, "target must be multiple of 8");
                nuint count = target.Vector256Count<float>();
                Vector256<float> multiplierVector = Vector256.Create(multiplier);
                for (nuint i = 0; i < count; i++)
                {
                    Unsafe.Add(ref targetVectorRef, i) = Avx.Multiply(Unsafe.Add(ref targetVectorRef, i), multiplierVector);
                }
            }
            else if (AdvSimd.IsSupported)
            {
                ref Vector128<float> targetVectorRef = ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(target));

                // Spans are guaranteed to be multiple of 8 so no extra 'remainder' steps are needed
                DebugGuard.IsTrue(target.Length % 8 == 0, "target must be multiple of 8");
                nuint count = target.Vector128Count<float>();
                Vector128<float> multiplierVector = Vector128.Create(multiplier);
                for (nuint i = 0; i < count; i++)
                {
                    Unsafe.Add(ref targetVectorRef, i) = AdvSimd.Multiply(Unsafe.Add(ref targetVectorRef, i), multiplierVector);
                }
            }
            else
            {
                ref Vector<float> targetVectorRef = ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(target));

                nuint count = target.VectorCount<float>();
                Vector<float> multiplierVector = new Vector<float>(multiplier);
                for (nuint i = 0; i < count; i++)
                {
                    Unsafe.Add(ref targetVectorRef, i) *= multiplierVector;
                }

                ref float targetRef = ref MemoryMarshal.GetReference(target);
                for (nuint i = count * (uint)Vector<float>.Count; i < (uint)target.Length; i++)
                {
                    Unsafe.Add(ref targetRef, i) *= multiplier;
                }
            }
        }
    }
}
