// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Processing.Processors.Histograms;

namespace SixLabors.ImageSharp.Processing.Processors.Normalization;

/// <summary>
/// Calculates equalized luminance between adjacent tile centers without owning histogram storage.
/// </summary>
/// <param name="size">The image dimensions used to determine tile sample areas.</param>
/// <param name="tiles">The configured number of tiles per axis.</param>
internal readonly struct HistogramTileEqualizationOperator(Size size, int tiles) : IHistogramOperation<Histogram.CountBin, float>
{
    private readonly int area = (int)MathF.Ceiling(size.Width / (float)tiles) * (int)MathF.Ceiling(size.Height / (float)tiles);

    /// <inheritdoc />
    public static bool UsesCumulativeCounts => true;

    /// <inheritdoc />
    public float GetResult(ref Histogram<Histogram.CountBin>.Query histogram, Vector4 source, int index)
    {
        histogram.GetTileCdfs(index, out Histogram.Cdf topLeft, out Histogram.Cdf topRight, out Histogram.Cdf bottomLeft, out Histogram.Cdf bottomRight);

        // The four lanes hold the top-left, top-right, bottom-left and bottom-right tile counts.
        // A uniform tile has zero remaining area after its first occupied count is subtracted;
        // retain the existing rule that divides that tile's zero CDF by its complete sample area.
        Vector128<float> counts = Vector128.ConvertToSingle(
            Vector128.Create(topLeft.Cumulative, topRight.Cumulative, bottomLeft.Cumulative, bottomRight.Cumulative));

        Vector128<float> minimum = Vector128.ConvertToSingle(Vector128.Create(topLeft.Minimum, topRight.Minimum, bottomLeft.Minimum, bottomRight.Minimum));
        Vector128<float> denominator = Vector128.Create((float)this.area) - minimum;
        denominator = Vector128.ConditionalSelect(Vector128.Equals(denominator, Vector128<float>.Zero), Vector128.Create((float)this.area), denominator);
        Vector4 values = (counts / denominator).AsVector4();

        // Interpolate horizontally first, then vertically, preserving the existing arithmetic order.
        float top = values.X + ((values.Y - values.X) * histogram.HorizontalWeight);
        float bottom = values.Z + ((values.W - values.Z) * histogram.HorizontalWeight);
        return top + ((bottom - top) * histogram.VerticalWeight);
    }

    /// <inheritdoc />
    public Vector4 Invoke(Vector4 source, in float data) => new(data, data, data, source.W);

    /// <inheritdoc />
    public Vector128<float> Invoke(Vector128<float> source, ReadOnlySpan<float> data)
    {
        Vector128<float> alpha = Vector128.Create(0, 0, 0, -1).AsSingle();
        return Vector128.ConditionalSelect(alpha, source, Vector128.Create(data[0]));
    }

    /// <inheritdoc />
    public Vector256<float> Invoke(Vector256<float> source, ReadOnlySpan<float> data)
    {
        // Two luminances become [L0 L0 L0 L0, L1 L1 L1 L1]. Replace only RGB; retain each source alpha.
        Vector64<float> values = Vector64.LoadUnsafe(ref MemoryMarshal.GetReference(data));
        Vector128<float> pair = Vector128.Create(values, values);
        Vector256<float> repeated = Vector256.Create(pair, pair);
        Vector256<float> colors = Vector256.ShuffleNative(repeated, Vector256.Create(0, 0, 0, 0, 1, 1, 1, 1));
        Vector256<float> alpha = Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
        return Vector256.ConditionalSelect(alpha, source, colors);
    }

    /// <inheritdoc />
    public Vector512<float> Invoke(Vector512<float> source, ReadOnlySpan<float> data)
    {
        // Four luminances fill four RGBA groups. The alpha mask leaves the source W lanes unchanged.
        Vector128<float> values = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(data));
        Vector256<float> pair = Vector256.Create(values, values);
        Vector512<float> repeated = Vector512.Create(pair, pair);
        Vector512<float> colors = Vector512.ShuffleNative(repeated, Vector512.Create(0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3));
        Vector512<float> alpha = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
        return Vector512.ConditionalSelect(alpha, source, colors);
    }
}
