// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Processing.Processors.Normalization;

/// <summary>
/// Applies a luminance histogram equilization to the image.
/// </summary>
public class AutoLevelProcessor : HistogramEqualizationProcessor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AutoLevelProcessor"/> class.
    /// It uses the exact minimum and maximum values found in the luminance channel, as the BlackPoint and WhitePoint to linearly stretch the colors
    /// (and histogram) of the image.
    /// </summary>
    /// <param name="luminanceLevels">The number of different luminance levels. Typical values are 256 for 8-bit grayscale images
    /// or 65536 for 16-bit grayscale images.</param>
    /// <param name="clipHistogram">Indicating whether to clip the histogram bins at a specific value.</param>
    /// <param name="clipLimit">The histogram clip limit. Histogram bins which exceed this limit, will be capped at this value.</param>
    /// <param name="syncChannels">Whether to apply a synchronized luminance value to each color channel.</param>
    public AutoLevelProcessor(
        int luminanceLevels,
        bool clipHistogram,
        int clipLimit,
        bool syncChannels)
        : base(luminanceLevels, clipHistogram, clipLimit)
    {
        this.SyncChannels = syncChannels;
    }

    /// <summary>
    /// Gets a value indicating whether to apply a synchronized luminance value to each color channel.
    /// </summary>
    public bool SyncChannels { get; }

    /// <inheritdoc />
    public override IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
        => new AutoLevelProcessor<TPixel>(
            configuration,
            this.LuminanceLevels,
            this.ClipHistogram,
            this.ClipLimit,
            this.SyncChannels,
            source,
            sourceRectangle);
}
