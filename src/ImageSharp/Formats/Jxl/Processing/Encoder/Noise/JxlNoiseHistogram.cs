// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics.Tensors;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Noise;

internal sealed class JxlNoiseHistogram
{
    private const int Bins = 256;

    private readonly uint[] bins = new uint[Bins];

    public int Mode
    {
        get
        {
            int maxIdx = 0;

            for (int i = 0; i < Bins; i++)
            {
                if (this.bins[i] > this.bins[maxIdx])
                {
                    maxIdx = i;
                }
            }

            return maxIdx;
        }
    }

    /// <summary>
    /// Gets the Inter-quartile range.
    /// </summary>
    public double Iqr => this.Quantile(0.75) - this.Quantile(0.25);

    public void Increment(float x) => this.bins[Index(x)]++;

    public uint Get(float x) => this.bins[Index(x)];

    public uint Bin(int bin) => this.bins[bin];

    public double Quantile(double q01)
    {
        long total = 1 + TensorPrimitives.Sum((ReadOnlySpan<uint>)this.bins.AsSpan());
        long target = (long)q01 * total;
        long sum = 0;
        int i = 0;

        for (; i < Bins; i++)
        {
            sum += this.bins[i];

            if (sum == target)
            {
                return i + 0.5;
            }

            if (sum > target)
            {
                break;
            }
        }

        int next = i + 1;

        while ((uint)next < this.bins.Length && this.bins[next] == 0)
        {
            next++;
        }

        double excess = target - sum;
        double weightNext = this.bins[Index(next)] / excess;

        return ClampX((next * weightNext) + (i * (1.0 - weightNext)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ClampX(double x) => Math.Clamp(x, 0, Bins - 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Index(float x) => (int)ClampX((int)x);
}
