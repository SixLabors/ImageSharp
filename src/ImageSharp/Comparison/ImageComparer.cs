// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Comparison.Exceptions;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Comparison;

/// <summary>
/// Compares images and image frames and reports the similarity between them.
/// </summary>
/// <remarks>
/// <para>
/// Comparers are stateless, so a single instance can be reused for multiple comparisons.
/// </para>
/// <para>
/// The comparison is performed against the components of the pixel formats after conversion
/// to <see cref="RgbaVector"/>. When both formats use associated (premultiplied) alpha, the stored
/// RGB components are compared directly; otherwise the components are converted to
/// unassociated (straight) alpha first, so that differences at low alpha are not amplified.
/// </para>
/// </remarks>
public abstract class ImageComparer
{
    /// <summary>
    /// Gets the default image threshold of a tolerant comparer.
    /// </summary>
    /// <remarks>
    /// 1% of all pixels in a 100*100 pixel area are allowed to have a difference of 1 unit
    /// 257 = (1 / 255) * 65535.
    /// </remarks>
    public const float DefaultImageThreshold = 257F / (100 * 100 * 65535);

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageComparer"/> class.
    /// </summary>
    protected ImageComparer()
    {
    }

    /// <summary>
    /// Gets an <see cref="ImageComparer"/> that requires an exact match between the components
    /// of two pixels.
    /// </summary>
    public static ImageComparer Exact { get; } = new TolerantImageComparer(0, 0);

    /// <summary>
    /// Returns an instance of <see cref="TolerantImageComparer"/>.
    /// The individual manhattan pixel difference is only added to the total image difference when
    /// the individual difference is over <paramref name="perPixelManhattanThreshold"/>.
    /// </summary>
    /// <param name="imageThreshold">
    /// The maximal tolerated difference represented by a value between 0.0 and 1.0 scaled to 0 and 65535.
    /// </param>
    /// <param name="perPixelManhattanThreshold">
    /// The threshold of the individual pixels before they accumulate towards the overall difference.
    /// </param>
    /// <returns>An <see cref="ImageComparer"/> instance.</returns>
    public static ImageComparer Tolerant(
        float imageThreshold = DefaultImageThreshold,
        int perPixelManhattanThreshold = 0) =>
        new TolerantImageComparer(imageThreshold, perPixelManhattanThreshold);

    /// <summary>
    /// Returns an <see cref="ImageComparer"/> equivalent to
    /// <see cref="Tolerant(float, int)"/>(<paramref name="imageThresholdInPercents"/> / 100).
    /// </summary>
    /// <param name="imageThresholdInPercents">The maximal tolerated difference in percents.</param>
    /// <param name="perPixelManhattanThreshold">
    /// The threshold of the individual pixels before they accumulate towards the overall difference.
    /// </param>
    /// <returns>An <see cref="ImageComparer"/> instance.</returns>
    public static ImageComparer TolerantPercentage(float imageThresholdInPercents, int perPixelManhattanThreshold = 0)
        => Tolerant(imageThresholdInPercents / 100F, perPixelManhattanThreshold);

    /// <summary>
    /// Compares the root frames of two images.
    /// </summary>
    /// <param name="expected">The expected image.</param>
    /// <param name="actual">The actual image.</param>
    /// <returns>
    /// An <see cref="ImageSimilarityReport"/> describing the difference between the two images.
    /// </returns>
    /// <exception cref="ImageDimensionsMismatchException">
    /// The dimensions of the two images do not match.
    /// </exception>
    public ImageSimilarityReport CompareImagesOrFrames(Image expected, Image actual)
    {
        Guard.NotNull(expected, nameof(expected));
        Guard.NotNull(actual, nameof(actual));

        return this.CompareImagesOrFrames(0, expected.Frames.RootFrame, actual.Frames.RootFrame);
    }

    /// <summary>
    /// Compares two image frames.
    /// </summary>
    /// <param name="index">The index of the compared frames, used to identify the report.</param>
    /// <param name="expected">The expected image frame.</param>
    /// <param name="actual">The actual image frame.</param>
    /// <returns>
    /// An <see cref="ImageSimilarityReport"/> describing the difference between the two frames.
    /// </returns>
    /// <exception cref="ImageDimensionsMismatchException">
    /// The dimensions of the two frames do not match.
    /// </exception>
    public ImageSimilarityReport CompareImagesOrFrames(int index, ImageFrame expected, ImageFrame actual)
    {
        Guard.NotNull(expected, nameof(expected));
        Guard.NotNull(actual, nameof(actual));

        if (expected.Size != actual.Size)
        {
            throw new ImageDimensionsMismatchException(expected.Size, actual.Size);
        }

        FramePairVisitor visitor = new(this, index, actual);
        expected.Accept(visitor);

        // The visitor guarantees a report: the acceptance of a valid frame always dispatches the comparison.
        return visitor.Report!;
    }

    /// <summary>
    /// Compares all frames of two images in order.
    /// </summary>
    /// <param name="expected">The expected image.</param>
    /// <param name="actual">The actual image.</param>
    /// <returns>
    /// The reports describing the differences of the frames which do not match. Frames which match
    /// are not represented in the result.
    /// </returns>
    /// <exception cref="ImageDimensionsMismatchException">
    /// The dimensions of the two images do not match.
    /// </exception>
    /// <exception cref="ImageFramesMismatchException">
    /// The images have a different number of frames.
    /// </exception>
    public IReadOnlyList<ImageSimilarityReport> CompareImages(Image expected, Image actual)
    {
        Guard.NotNull(expected, nameof(expected));
        Guard.NotNull(actual, nameof(actual));

        if (expected.Size != actual.Size)
        {
            throw new ImageDimensionsMismatchException(expected.Size, actual.Size);
        }

        if (expected.Frames.Count != actual.Frames.Count)
        {
            throw new ImageFramesMismatchException(expected.Frames.Count, actual.Frames.Count);
        }

        List<ImageSimilarityReport> result = [];
        for (int i = 0; i < expected.Frames.Count; i++)
        {
            ImageSimilarityReport report = this.CompareImagesOrFrames(i, expected.Frames[i], actual.Frames[i]);
            if (!report.IsEmpty)
            {
                result.Add(report);
            }
        }

        return result;
    }

    /// <summary>
    /// Compares two images and throws when any of their frames differ beyond the tolerance of this
    /// comparer.
    /// </summary>
    /// <param name="expected">The expected image.</param>
    /// <param name="actual">The actual image.</param>
    /// <exception cref="ImageDimensionsMismatchException">
    /// The dimensions of the two images do not match.
    /// </exception>
    /// <exception cref="ImageFramesMismatchException">
    /// The images have a different number of frames.
    /// </exception>
    /// <exception cref="ImageDifferenceIsOverThresholdException">
    /// The difference of any frame is over the threshold of this comparer.
    /// </exception>
    public void VerifySimilarity(Image expected, Image actual)
    {
        IReadOnlyList<ImageSimilarityReport> reports = this.CompareImages(expected, actual);
        if (reports.Count > 0)
        {
            throw new ImageDifferenceIsOverThresholdException(reports);
        }
    }

    /// <summary>
    /// Provides a pixel-specific implementation for a given comparison.
    /// </summary>
    /// <typeparam name="TPixelA">The pixel type of the expected frame.</typeparam>
    /// <typeparam name="TPixelB">The pixel type of the actual frame.</typeparam>
    /// <param name="index">The index of the compared frames, used to identify the report.</param>
    /// <param name="expected">The expected image frame.</param>
    /// <param name="actual">The actual image frame.</param>
    /// <returns>
    /// An <see cref="ImageSimilarityReport"/> describing the difference between the two frames.
    /// </returns>
    internal abstract ImageSimilarityReport CompareImagesOrFramesCore<TPixelA, TPixelB>(
        int index,
        ImageFrame<TPixelA> expected,
        ImageFrame<TPixelB> actual)
        where TPixelA : unmanaged, IPixel<TPixelA>
        where TPixelB : unmanaged, IPixel<TPixelB>;

    /// <summary>
    /// Resolves the pixel type of the expected frame, then dispatches the pixel type of the
    /// actual frame, so that a pixel-specific comparison of both frames can be performed.
    /// </summary>
    private sealed class FramePairVisitor : IImageFrameVisitor
    {
        private readonly ImageComparer comparer;
        private readonly int index;
        private readonly ImageFrame actual;

        public FramePairVisitor(ImageComparer comparer, int index, ImageFrame actual)
        {
            this.comparer = comparer;
            this.index = index;
            this.actual = actual;
        }

        public ImageSimilarityReport? Report { get; private set; }

        public void Visit<TPixelA>(ImageFrame<TPixelA> expected)
            where TPixelA : unmanaged, IPixel<TPixelA>
        {
            ActualFrameVisitor<TPixelA> visitor = new(this.comparer, this.index, expected);
            this.actual.Accept(visitor);
            this.Report = visitor.Report;
        }
    }

    /// <summary>
    /// Resolves the pixel type of the actual frame and performs the comparison of both typed frames.
    /// </summary>
    /// <typeparam name="TPixelA">The pixel type of the expected frame.</typeparam>
    private sealed class ActualFrameVisitor<TPixelA> : IImageFrameVisitor
        where TPixelA : unmanaged, IPixel<TPixelA>
    {
        private readonly ImageComparer comparer;
        private readonly int index;
        private readonly ImageFrame<TPixelA> expected;

        public ActualFrameVisitor(ImageComparer comparer, int index, ImageFrame<TPixelA> expected)
        {
            this.comparer = comparer;
            this.index = index;
            this.expected = expected;
        }

        public ImageSimilarityReport? Report { get; private set; }

        public void Visit<TPixelB>(ImageFrame<TPixelB> actual)
            where TPixelB : unmanaged, IPixel<TPixelB>
            => this.Report = this.comparer.CompareImagesOrFramesCore(this.index, this.expected, actual);
    }
}
