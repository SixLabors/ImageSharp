// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Processing.Processors.Histograms;

namespace SixLabors.ImageSharp.Processing.Processors.Normalization;

/// <summary>
/// Converts cumulative counts to gray while retaining source alpha. Global and windowed equalization use the same operation.
/// </summary>
internal readonly struct HistogramEqualizationOperator : IHistogramOperation<Histogram.CountBin, Histogram.Cdf>
{
    private readonly int area;

    /// <summary>
    /// Initializes a new instance of the <see cref="HistogramEqualizationOperator"/> struct.
    /// </summary>
    /// <param name="area">The existing image or window area used for normalization.</param>
    public HistogramEqualizationOperator(int area) => this.area = area;

    /// <inheritdoc />
    public static bool UsesCumulativeCounts => true;

    /// <inheritdoc />
    public Histogram.Cdf GetResult(ref Histogram<Histogram.CountBin>.Query histogram, Vector4 source, int index) => histogram.GetCdf(index);

    /// <inheritdoc />
    public Vector4 Invoke(Vector4 source, in Histogram.Cdf data)
    {
        float luminance = data.Cumulative / (float)(this.area - data.Minimum);
        return new Vector4(luminance, luminance, luminance, source.W);
    }

    /// <inheritdoc />
    public Vector128<float> Invoke(Vector128<float> source, ReadOnlySpan<Histogram.Cdf> data)
    {
        // One register contains one pixel. Compute its gray value once and keep the original alpha lane.
        float luminance = data[0].Cumulative / (float)(this.area - data[0].Minimum);
        Vector128<float> alpha = Vector128.Create(0, 0, 0, -1).AsSingle();
        return Vector128.ConditionalSelect(alpha, source, Vector128.Create(luminance));
    }

    /// <inheritdoc />
    public Vector256<float> Invoke(Vector256<float> source, ReadOnlySpan<Histogram.Cdf> data)
    {
        // Results are [cumulative minimum] integer pairs. Repeat each pair's values across its pixel's four lanes.
        ref int first = ref Unsafe.As<Histogram.Cdf, int>(ref MemoryMarshal.GetReference(data));
        Vector128<int> pairs = Vector128.LoadUnsafe(ref first);
        Vector256<int> repeated = Vector256.Create(pairs, pairs);
        Vector256<int> cumulative = Vector256.ShuffleNative(repeated, Vector256.Create(0, 0, 0, 0, 2, 2, 2, 2));
        Vector256<int> minimum = Vector256.ShuffleNative(repeated, Vector256.Create(1, 1, 1, 1, 3, 3, 3, 3));

        // Subtract in integer units before conversion, as the scalar calculation does. Division does not clip HDR values.
        Vector256<float> numerator = Vector256.ConvertToSingle(cumulative);
        Vector256<float> denominator = Vector256.ConvertToSingle(Vector256.Create(this.area) - minimum);
        Vector256<float> alpha = Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
        return Vector256.ConditionalSelect(alpha, source, numerator / denominator);
    }

    /// <inheritdoc />
    public Vector512<float> Invoke(Vector512<float> source, ReadOnlySpan<Histogram.Cdf> data)
    {
        // Results are [cumulative minimum] integer pairs. Repeat each pair's values across its pixel's four lanes.
        ref int first = ref Unsafe.As<Histogram.Cdf, int>(ref MemoryMarshal.GetReference(data));
        Vector256<int> pairs = Vector256.LoadUnsafe(ref first);
        Vector512<int> repeated = Vector512.Create(pairs, pairs);
        Vector512<int> cumulative = Vector512.ShuffleNative(repeated, Vector512.Create(0, 0, 0, 0, 2, 2, 2, 2, 4, 4, 4, 4, 6, 6, 6, 6));
        Vector512<int> minimum = Vector512.ShuffleNative(repeated, Vector512.Create(1, 1, 1, 1, 3, 3, 3, 3, 5, 5, 5, 5, 7, 7, 7, 7));

        // Subtract in integer units before conversion, as the scalar calculation does. Division does not clip HDR values.
        Vector512<float> numerator = Vector512.ConvertToSingle(cumulative);
        Vector512<float> denominator = Vector512.ConvertToSingle(Vector512.Create(this.area) - minimum);
        Vector512<float> alpha = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
        return Vector512.ConditionalSelect(alpha, source, numerator / denominator);
    }
}
