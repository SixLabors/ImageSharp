// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Normalization;

/// <summary>
/// Defines a processor that normalizes the histogram of an image.
/// </summary>
/// <typeparam name="TPixel">The pixel format.</typeparam>
internal abstract class HistogramEqualizationProcessor<TPixel> : ImageProcessor<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HistogramEqualizationProcessor{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="luminanceLevels">The number of different luminance levels. Typical values are 256 for 8-bit grayscale images
    /// or 65536 for 16-bit grayscale images.</param>
    /// <param name="clipHistogram">Indicates, if histogram bins should be clipped.</param>
    /// <param name="clipLimit">The histogram clip limit. Histogram bins which exceed this limit, will be capped at this value.</param>
    /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
    /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
    protected HistogramEqualizationProcessor(
        Configuration configuration,
        int luminanceLevels,
        bool clipHistogram,
        int clipLimit,
        Image<TPixel> source,
        Rectangle sourceRectangle)
        : base(configuration, source, sourceRectangle)
    {
        Guard.MustBeGreaterThan(luminanceLevels, 0, nameof(luminanceLevels));
        Guard.MustBeGreaterThan(clipLimit, 1, nameof(clipLimit));

        this.LuminanceLevels = luminanceLevels;
        this.ClipHistogramEnabled = clipHistogram;
        this.ClipLimit = clipLimit;
    }

    /// <summary>
    /// Gets the number of luminance levels.
    /// </summary>
    public int LuminanceLevels { get; }

    /// <summary>
    /// Gets a value indicating whether to clip the histogram bins at a specific value.
    /// </summary>
    public bool ClipHistogramEnabled { get; }

    /// <summary>
    /// Gets the histogram clip limit. Histogram bins which exceed this limit, will be capped at this value.
    /// </summary>
    public int ClipLimit { get; }
}
