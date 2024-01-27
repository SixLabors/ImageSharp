// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Webp.Lossy;

internal sealed class Vp8Histogram
{
    /// <summary>
    /// Size of histogram used by CollectHistogram.
    /// </summary>
    private const int MaxCoeffThresh = 31;

    private int maxValue;

    private int lastNonZero;

    /// <summary>
    /// Initializes a new instance of the <see cref="Vp8Histogram" /> class.
    /// </summary>
    public Vp8Histogram()
    {
        this.maxValue = 0;
        this.lastNonZero = 1;
    }

    public int GetAlpha()
    {
        // 'alpha' will later be clipped to [0..MAX_ALPHA] range, clamping outer
        // values which happen to be mostly noise. This leaves the maximum precision
        // for handling the useful small values which contribute most.
        int maxValue = this.maxValue;
        int lastNonZero = this.lastNonZero;
        int alpha = maxValue > 1 ? WebpConstants.AlphaScale * lastNonZero / maxValue : 0;
        return alpha;
    }

    public void CollectHistogram(Span<byte> reference, Span<byte> pred, int startBlock, int endBlock)
    {
        Span<int> scratch = stackalloc int[16];
        Span<short> output = stackalloc short[16];
        Span<int> distribution = stackalloc int[MaxCoeffThresh + 1];

        int j;
        for (j = startBlock; j < endBlock; j++)
        {
            Vp8Encoding.FTransform(reference[WebpLookupTables.Vp8DspScan[j]..], pred[WebpLookupTables.Vp8DspScan[j]..], output, scratch);

            // Convert coefficients to bin.
#if USE_SIMD_INTRINSICS
            if (Avx2.IsSupported)
            {
                // Load.
                ref short outputRef = ref MemoryMarshal.GetReference(output);
                Vector256<byte> out0 = Unsafe.As<short, Vector256<byte>>(ref outputRef);

                // v = abs(out) >> 3
                Vector256<ushort> abs0 = Avx2.Abs(out0.AsInt16());
                Vector256<short> v0 = Avx2.ShiftRightArithmetic(abs0.AsInt16(), 3);

                // bin = min(v, MAX_COEFF_THRESH)
                Vector256<short> min0 = Avx2.Min(v0, Vector256.Create((short)MaxCoeffThresh));

                // Store.
                Unsafe.As<short, Vector256<short>>(ref outputRef) = min0;

                // Convert coefficients to bin.
                for (int k = 0; k < 16; ++k)
                {
                    ++distribution[output[k]];
                }
            }
            else
#endif
            {
                for (int k = 0; k < 16; ++k)
                {
                    int v = Math.Abs(output[k]) >> 3;
                    int clippedValue = ClipMax(v, MaxCoeffThresh);
                    ++distribution[clippedValue];
                }
            }
        }

        this.SetHistogramData(distribution);
    }

    public void Merge(Vp8Histogram other)
    {
        if (this.maxValue > other.maxValue)
        {
            other.maxValue = this.maxValue;
        }

        if (this.lastNonZero > other.lastNonZero)
        {
            other.lastNonZero = this.lastNonZero;
        }
    }

    private void SetHistogramData(ReadOnlySpan<int> distribution)
    {
        int maxValue = 0;
        int lastNonZero = 1;
        for (int k = 0; k <= MaxCoeffThresh; ++k)
        {
            int value = distribution[k];
            if (value > 0)
            {
                if (value > maxValue)
                {
                    maxValue = value;
                }

                lastNonZero = k;
            }
        }

        this.maxValue = maxValue;
        this.lastNonZero = lastNonZero;
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static int ClipMax(int v, int max) => v > max ? max : v;
}
