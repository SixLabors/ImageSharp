// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

internal sealed class JxlBlockContextMap
{
    public JxlBlockContextMap()
    {
        DefaultContextMap.CopyTo(this.ContextMap);
        this.ContextCount = this.ContextMap.Max() + 1;
        this.DcContextCount = 1;
    }

    private static ReadOnlySpan<byte> DefaultContextMap =>
    [
        0, 1, 2, 2, 3,  3,  4,  5,  6,  6,  6,  6,  6,
        7, 8, 9, 9, 10, 11, 12, 13, 14, 14, 14, 14, 14,
        7, 8, 9, 9, 10, 11, 12, 13, 14, 14, 14, 14, 14,
    ];

    public List<int>[] DcThresholds { get; } = [[], [], []];

    public List<uint> QfThresholds { get; set; } = [];

    public byte[] ContextMap { get; set; } = new byte[DefaultContextMap.Length];

    public int ContextCount { get; set; }

    public int DcContextCount { get; set; }

    public int AcContextCount => (this.ContextCount * JxlAcContext.NonZeroBuckets) + JxlAcContext.ZeroDensityContextCount;

    public int Context(int dcIndex, uint qf, int ord, int c)
    {
        int qfIndex = 0;
        for (int i = 0; i < this.QfThresholds.Count; i++)
        {
            uint t = this.QfThresholds[i];

            if (qf > t)
            {
                qfIndex++;
            }
        }

        int idx = c < 2 ? c ^ 1 : 2;
        idx = (idx * JxlForwardCoefficientOrder.OrderCount) + ord;
        idx = (idx * (this.QfThresholds.Count + 1)) + qfIndex;
        idx = (idx * this.DcContextCount) + dcIndex;
        return this.ContextMap[idx];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ZeroDensityContextOffset(int blockContext) =>
        (this.ContextCount * JxlAcContext.NonZeroBuckets) + JxlAcContext.ZeroDensityContextCount + blockContext;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int NonZeroContext(int nonZeroes, int blockContext)
    {
        if (nonZeroes >= 64)
        {
            nonZeroes = 64;
        }

        int ctx = nonZeroes < 8 ? nonZeroes : (4 + (nonZeroes / 2));
        return (ctx * this.ContextCount) + blockContext;
    }
}
