// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Histograms;

namespace SixLabors.ImageSharp.Processing.Processors.Normalization;

/// <summary>
/// Applies global histogram equalization.
/// </summary>
/// <typeparam name="TPixel">The pixel format.</typeparam>
internal class GlobalHistogramEqualizationProcessor<TPixel> : HistogramEqualizationProcessor<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GlobalHistogramEqualizationProcessor{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The processing configuration.</param>
    /// <param name="luminanceLevels">The number of bounded luminance bins.</param>
    /// <param name="clipHistogram">Whether counts are clipped.</param>
    /// <param name="clipLimit">The count limit.</param>
    /// <param name="source">The source image.</param>
    /// <param name="sourceRectangle">The processing region.</param>
    public GlobalHistogramEqualizationProcessor(
        Configuration configuration,
        int luminanceLevels,
        bool clipHistogram,
        int clipLimit,
        Image<TPixel> source,
        Rectangle sourceRectangle)
        : base(configuration, luminanceLevels, clipHistogram, clipLimit, source, sourceRectangle)
    {
    }

    /// <inheritdoc />
    protected override void OnFrameApply(ImageFrame<TPixel> source)
    {
        HistogramEqualizationOperator operation = new(source.Width * source.Height);
        Vector3 weights = new(ColorNumerics.Bt709.X, ColorNumerics.Bt709.Y, ColorNumerics.Bt709.Z);

        Histogram.Apply<TPixel, Histogram.CountBin, Histogram.Cdf, HistogramEqualizationOperator>(
            this.Configuration,
            source,
            this.SourceRectangle,
            this.LuminanceLevels,
            weights,
            this.ClipHistogramEnabled,
            this.ClipLimit,
            Histogram.Region.Global,
            in operation);
    }
}
