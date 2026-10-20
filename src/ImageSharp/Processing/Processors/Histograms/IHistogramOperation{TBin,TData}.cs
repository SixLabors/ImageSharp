// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Processing.Processors.Histograms;

/// <summary>
/// Defines one output calculation in scalar and SIMD forms. Histogram code owns traversal, dispatch and storage.
/// </summary>
/// <typeparam name="TBin">The bin accumulator supplied by the consumer.</typeparam>
/// <typeparam name="TData">The histogram result supplied for each source pixel.</typeparam>
internal interface IHistogramOperation<TBin, TData>
    where TBin : unmanaged, IHistogramBin<TBin>
    where TData : unmanaged
{
    /// <summary>
    /// Gets a value indicating whether stable regions should prepare cumulative counts before calculation.
    /// </summary>
    static virtual bool UsesCumulativeCounts => false;

    /// <summary>
    /// Calculates the result used by the output operation for one source pixel.
    /// </summary>
    /// <param name="histogram">The borrowed histogram data. The histogram runner retains its storage.</param>
    /// <param name="source">The unchanged source color.</param>
    /// <param name="index">The source color's mapped measurement.</param>
    /// <returns>The calculation data for the scalar or SIMD output operation.</returns>
    TData GetResult(ref Histogram<TBin>.Query histogram, Vector4 source, int index);

    /// <summary>
    /// Calculates one output color.
    /// </summary>
    /// <param name="source">The unchanged straight source color.</param>
    /// <param name="data">The histogram result for this pixel.</param>
    /// <returns>The output color.</returns>
    Vector4 Invoke(Vector4 source, in TData data);

    /// <summary>
    /// Calculates 1 output color in packed RGBA order.
    /// </summary>
    /// <param name="source">Straight RGBA colors, with one color in each 128-bit lane.</param>
    /// <param name="data">One histogram result per packed color.</param>
    /// <returns>The output colors in the same order.</returns>
    Vector128<float> Invoke(Vector128<float> source, ReadOnlySpan<TData> data);

    /// <summary>
    /// Calculates 2 output colors in packed RGBA order.
    /// </summary>
    /// <param name="source">Straight RGBA colors, with one color in each 128-bit lane.</param>
    /// <param name="data">One histogram result per packed color.</param>
    /// <returns>The output colors in the same order.</returns>
    Vector256<float> Invoke(Vector256<float> source, ReadOnlySpan<TData> data);

    /// <summary>
    /// Calculates 4 output colors in packed RGBA order.
    /// </summary>
    /// <param name="source">Straight RGBA colors, with one color in each 128-bit lane.</param>
    /// <param name="data">One histogram result per packed color.</param>
    /// <returns>The output colors in the same order.</returns>
    Vector512<float> Invoke(Vector512<float> source, ReadOnlySpan<TData> data);
}
