// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Webp.Lossy;

/// <summary>
/// Summarizes the coefficient distribution of one prediction candidate during macroblock analysis.
/// This is a value type so that mode analysis, which evaluates several candidates per macroblock,
/// does not allocate. Create instances with <c>new()</c> rather than <see langword="default"/>:
/// the constructor sets the last-non-zero index to 1, which the alpha computation of an
/// accumulated histogram expects.
/// </summary>
internal struct Vp8Histogram
{
    /// <summary>
    /// Size of histogram used by CollectHistogram.
    /// </summary>
    private const int MaxCoeffThresh = 31;

    private int maxValue;

    private int lastNonZero;

    /// <summary>
    /// Initializes a new instance of the <see cref="Vp8Histogram" /> struct.
    /// </summary>
    public Vp8Histogram()
    {
        this.maxValue = 0;
        this.lastNonZero = 1;
    }

    public readonly int GetAlpha()
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

    /// <summary>
    /// Raises the maximum value and the last-non-zero index of <paramref name="other"/> to those of this
    /// histogram, so that <paramref name="other"/> accumulates the best candidate of every block.
    /// </summary>
    /// <param name="other">The accumulated histogram to merge into.</param>
    public readonly void Merge(ref Vp8Histogram other)
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
