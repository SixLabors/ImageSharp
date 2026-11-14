// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Comparison;

/// <summary>
/// An <see cref="ImageComparer"/> which tolerates a configurable amount of difference between
/// images and image frames.
/// </summary>
public class TolerantImageComparer : ImageComparer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TolerantImageComparer"/> class.
    /// </summary>
    /// <param name="imageThreshold">
    /// The maximal tolerated difference represented by a value between 0.0 and 1.0 scaled to 0 and 65535.
    /// </param>
    /// <param name="perPixelManhattanThreshold">
    /// The threshold of the individual pixels before they accumulate towards the overall difference.
    /// Floating-point component differences are expressed in sixteen-bit units to retain existing
    /// threshold values. The value is the sum of the absolute component differences:
    /// <see href="https://en.wikipedia.org/wiki/Taxicab_geometry"/>.
    /// </param>
    public TolerantImageComparer(float imageThreshold, int perPixelManhattanThreshold = 0)
    {
        Guard.MustBeGreaterThanOrEqualTo(imageThreshold, 0, nameof(imageThreshold));

        this.ImageThreshold = imageThreshold;
        this.PerPixelManhattanThreshold = perPixelManhattanThreshold;
    }

    /// <summary>
    /// <para>
    /// Gets the maximal tolerated difference represented by a value between 0.0 and 1.0 scaled to 0 and 65535.
    /// Examples of percentage differences on a single pixel:
    /// 1. PixelA = (65535,65535,65535,0) PixelB =(0,0,0,65535) leads to 100% difference on a single pixel
    /// 2. PixelA = (65535,65535,65535,0) PixelB =(65535,65535,65535,65535) leads to 25% difference on a single pixel
    /// 3. PixelA = (65535,65535,65535,0) PixelB =(32767,32767,32767,32767) leads to 50% difference on a single pixel
    /// </para>
    /// <para>
    /// The total differences is the sum of all pixel differences normalized by image dimensions!
    /// The individual distances are calculated using the Manhattan function:
    /// <see href="https://en.wikipedia.org/wiki/Taxicab_geometry"/>
    /// ImageThresholdInPercents = 1/255 =  257/65535 means that we allow one unit difference per channel on a 1x1 image
    /// ImageThresholdInPercents = 1/(100*100*255) = 257/(100*100*65535) means that we allow only one unit difference per channel on a 100x100 image
    /// </para>
    /// </summary>
    public float ImageThreshold { get; }

    /// <summary>
    /// Gets the threshold of the individual pixels before they accumulate towards the overall difference.
    /// Floating-point component differences are expressed in sixteen-bit units to retain existing threshold values.
    /// The value is the sum of the absolute component differences:
    /// <see href="https://en.wikipedia.org/wiki/Taxicab_geometry"/>
    /// </summary>
    public int PerPixelManhattanThreshold { get; }

    /// <inheritdoc />
    internal override ImageSimilarityReport CompareImagesOrFramesCore<TPixelA, TPixelB>(
        int index,
        ImageFrame<TPixelA> expected,
        ImageFrame<TPixelB> actual)
        => ImageComparisonEngine.CompareFrames(index, expected, actual, this.ImageThreshold, this.PerPixelManhattanThreshold);
}
