// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
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

            // For block i, the transform reads 8x8 samples from columns 8 * i to 8 * i + 7.
            // Then it applies the FDCT, quantizes the coefficients, and writes them to spectral block i.
            FloatingPointDCT.TransformFDCT(
                ref MemoryMarshal.GetReference(colorBufferRow),
                destAreaStride,
                ref this.quantTable,
                blockRow);
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

            // This call sums and averages each horizontal group in one pass.
            // It writes the results to the first packedWidth values of the destination row.
            Span<float> destinationRow = this.ColorBuffer.DangerousGetRowSpan(i / factors.Height)[..packedWidth];
            SumHorizontalAndAverage(sourceRow, destinationRow, factors.Width, averageMultiplier);
        }

        static void SumVertical(Span<float> target, Span<float> source)

            // Exact destination overlap is supported, so each accumulated row remains in target.
            => TensorPrimitives.Add(target, source, target);
    }

    /// <summary>
    /// Sums each group of <paramref name="factor"/> adjacent values and multiplies the sum by <paramref name="multiplier"/>.
    /// </summary>
    /// <remarks>
    /// Destination value k comes from source values factor * k to factor * k + factor - 1.
    /// For factors 2 and 4, each sum adds pairs, then pairs of pairs: (s0 + s1) + (s2 + s3). Other factors add in order.
    /// The destination can start at the start of the source. Each step loads its source values before it stores
    /// its results, and its results end before the source values of the next step start.
    /// </remarks>
    /// <param name="source">The vertical sums. It holds at least <paramref name="factor"/> times the destination length.</param>
    /// <param name="destination">The averages.</param>
    /// <param name="factor">The horizontal subsampling factor.</param>
    /// <param name="multiplier">The reciprocal of the number of source samples for each destination sample.</param>
    private static void SumHorizontalAndAverage(Span<float> source, Span<float> destination, int factor, float multiplier)
    {
        if (factor == 1)
        {
            TensorPrimitives.Multiply(source[..destination.Length], multiplier, destination);
            return;
        }

        ref float src = ref MemoryMarshal.GetReference(source);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        int length = destination.Length;
        int k = 0;

        // Each step makes one vector of results. Factor 2 adds the pairs of two source vectors. Factor 4 adds the
        // pairs of four source vectors, then the pairs of those sums. AddPairwise keeps the source order.
        if (factor is 2 or 4)
        {
            if (Vector512.IsHardwareAccelerated)
            {
                Vector512<float> scale = Vector512.Create(multiplier);
                for (; k <= length - Vector512<float>.Count; k += Vector512<float>.Count)
                {
                    ref float s = ref Unsafe.Add(ref src, (nuint)(k * factor));
                    Vector512<float> sums = Vector512_.AddPairwise(Vector512.LoadUnsafe(ref s), Vector512.LoadUnsafe(ref s, 16));
                    if (factor == 4)
                    {
                        Vector512<float> upper = Vector512_.AddPairwise(Vector512.LoadUnsafe(ref s, 32), Vector512.LoadUnsafe(ref s, 48));
                        sums = Vector512_.AddPairwise(sums, upper);
                    }

                    (sums * scale).StoreUnsafe(ref dst, (nuint)k);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                Vector256<float> scale = Vector256.Create(multiplier);
                for (; k <= length - Vector256<float>.Count; k += Vector256<float>.Count)
                {
                    ref float s = ref Unsafe.Add(ref src, (nuint)(k * factor));
                    Vector256<float> sums = Vector256_.AddPairwise(Vector256.LoadUnsafe(ref s), Vector256.LoadUnsafe(ref s, 8));
                    if (factor == 4)
                    {
                        Vector256<float> upper = Vector256_.AddPairwise(Vector256.LoadUnsafe(ref s, 16), Vector256.LoadUnsafe(ref s, 24));
                        sums = Vector256_.AddPairwise(sums, upper);
                    }

                    (sums * scale).StoreUnsafe(ref dst, (nuint)k);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                // The 128-bit horizontal addition already keeps the source order: (l01, l23, r01, r23).
                Vector128<float> scale = Vector128.Create(multiplier);
                for (; k <= length - Vector128<float>.Count; k += Vector128<float>.Count)
                {
                    ref float s = ref Unsafe.Add(ref src, (nuint)(k * factor));
                    Vector128<float> sums = Vector128_.HorizontalAdd(Vector128.LoadUnsafe(ref s), Vector128.LoadUnsafe(ref s, 4));
                    if (factor == 4)
                    {
                        Vector128<float> upper = Vector128_.HorizontalAdd(Vector128.LoadUnsafe(ref s, 8), Vector128.LoadUnsafe(ref s, 12));
                        sums = Vector128_.HorizontalAdd(sums, upper);
                    }

                    (sums * scale).StoreUnsafe(ref dst, (nuint)k);
                }
            }
        }

        for (; k < length; k++)
        {
            ref float s = ref Unsafe.Add(ref src, (nuint)(k * factor));
            float sum;
            if (factor == 4)
            {
                sum = (s + Unsafe.Add(ref s, 1)) + (Unsafe.Add(ref s, 2) + Unsafe.Add(ref s, 3));
            }
            else
            {
                sum = s;
                for (int j = 1; j < factor; j++)
                {
                    sum += Unsafe.Add(ref s, j);
                }
            }

            Unsafe.Add(ref dst, k) = sum * multiplier;
        }
    }
}
