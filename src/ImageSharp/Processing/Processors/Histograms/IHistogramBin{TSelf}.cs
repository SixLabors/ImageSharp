// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Processing.Processors.Histograms;

/// <summary>
/// Defines the arithmetic for a histogram bin. Histogram code owns storage and sample traversal.
/// </summary>
/// <typeparam name="TSelf">The bin accumulator.</typeparam>
internal interface IHistogramBin<TSelf>
    where TSelf : unmanaged, IHistogramBin<TSelf>
{
    /// <summary>
    /// Gets or sets the sample count, including count changes made by histogram clipping.
    /// </summary>
    int Count { get; set; }

    /// <summary>
    /// Gets a value indicating whether accumulation needs the source color as well as its mapped index.
    /// </summary>
    static abstract bool RequiresSamples { get; }

    /// <summary>
    /// Gets a value indicating whether updates retain the first bin that reaches each maximum count.
    /// When false, a most-frequent query searches the bins and resolves ties by index.
    /// </summary>
    static virtual bool TrackMaximum => true;

    /// <summary>
    /// Sums counts in a contiguous page. This calculation must match adding the individual integer counts.
    /// </summary>
    /// <param name="bins">The bin page.</param>
    /// <returns>The total sample count.</returns>
    static virtual int SumCounts(ReadOnlySpan<TSelf> bins)
    {
        // Custom accumulators can interleave counts with other data. Their default reduction reads
        // only counts; compact count-only bins override this with the shared SIMD reduction.
        int sum = 0;
        foreach (TSelf bin in bins)
        {
            sum += bin.Count;
        }

        return sum;
    }

    /// <summary>
    /// Gets the first nonzero count in a page that contains samples.
    /// </summary>
    /// <param name="bins">An occupied bin page.</param>
    /// <returns>The first nonzero sample count.</returns>
    static virtual int GetFirstCount(ReadOnlySpan<TSelf> bins)
    {
        // Custom bins can contain other fields. Inspect their count property without assuming a packed integer layout.
        int i = 0;
        while (bins[i].Count == 0)
        {
            i++;
        }

        return bins[i].Count;
    }

    /// <summary>
    /// Adds or removes source samples and returns the updated count.
    /// </summary>
    /// <param name="sample">The unchanged source color, supplied when <see cref="RequiresSamples"/> is true.</param>
    /// <param name="delta">The signed sample count change.</param>
    /// <returns>The updated sample count.</returns>
    int Add(Vector4 sample, int delta);

    /// <summary>
    /// Adds another independently accumulated bin.
    /// </summary>
    /// <param name="source">The source bin, which remains unchanged.</param>
    void Merge(in TSelf source);
}
