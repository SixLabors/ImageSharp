// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Histograms;

namespace SixLabors.ImageSharp.Processing.Processors.Normalization;

/// <summary>
/// Applies adaptive histogram equalization to the image using a sliding window.
/// </summary>
/// <typeparam name="TPixel">The pixel format.</typeparam>
internal class AdaptiveHistogramEqualizationSlidingWindowProcessor<TPixel> : HistogramEqualizationProcessor<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AdaptiveHistogramEqualizationSlidingWindowProcessor{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="luminanceLevels">The number of different luminance levels. Typical values are 256 for 8-bit grayscale images
    /// or 65536 for 16-bit grayscale images.</param>
    /// <param name="clipHistogram">Indicating whether to clip the histogram bins at a specific value.</param>
    /// <param name="clipLimit">The histogram clip limit. Histogram bins which exceed this limit, will be capped at this value.</param>
    /// <param name="tiles">The number of tiles the image is split into (horizontal and vertically). Minimum value is 2. Maximum value is 100.</param>
    /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
    /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
    public AdaptiveHistogramEqualizationSlidingWindowProcessor(
        Configuration configuration,
        int luminanceLevels,
        bool clipHistogram,
        int clipLimit,
        int tiles,
        Image<TPixel> source,
        Rectangle sourceRectangle)
        : base(configuration, luminanceLevels, clipHistogram, clipLimit, source, sourceRectangle)
    {
        Guard.MustBeGreaterThanOrEqualTo(tiles, 2, nameof(tiles));
        Guard.MustBeLessThanOrEqualTo(tiles, 100, nameof(tiles));

        this.Tiles = tiles;
    }

    /// <summary>
    /// Gets the number of tiles the image is split into (horizontal and vertically) for the adaptive histogram equalization.
    /// </summary>
    private int Tiles { get; }

    /// <inheritdoc />
    protected override void OnFrameApply(ImageFrame<TPixel> source)
    {
        int windowWidth = source.Width / this.Tiles;
        HistogramEqualizationOperator operation = new(windowWidth * windowWidth);
        Vector3 weights = new(ColorNumerics.Bt709.X, ColorNumerics.Bt709.Y, ColorNumerics.Bt709.Z);

        Histogram.Apply<TPixel, Histogram.CountBin, Histogram.Cdf, HistogramEqualizationOperator>(
            this.Configuration,
            source,
            this.SourceRectangle,
            this.LuminanceLevels,
            weights,
            this.ClipHistogramEnabled,
            this.ClipLimit,
            Histogram.Region.SlidingWindow(windowWidth),
            in operation);
    }
}
