// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Globalization;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Comparison;

/// <summary>
/// The result of a single image or image frame comparison, containing the similarity metrics and
/// the frames which were compared.
/// </summary>
public class ImageSimilarityReport
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImageSimilarityReport"/> class.
    /// </summary>
    /// <param name="index">The index of the compared frames.</param>
    /// <param name="expectedImageFrame">The expected image frame.</param>
    /// <param name="actualImageFrame">The actual image frame.</param>
    /// <param name="totalNormalizedDifference">
    /// The total difference normalized by the image dimensions and the component count.
    /// </param>
    /// <param name="isEmpty">
    /// Whether the difference is within the tolerance of the comparer which produced the report.
    /// </param>
    internal ImageSimilarityReport(
        int index,
        ImageFrame expectedImageFrame,
        ImageFrame actualImageFrame,
        float totalNormalizedDifference,
        bool isEmpty)
    {
        this.Index = index;
        this.ExpectedImageFrame = expectedImageFrame;
        this.ActualImageFrame = actualImageFrame;
        this.TotalNormalizedDifference = totalNormalizedDifference;
        this.IsEmpty = isEmpty;
    }

    /// <summary>
    /// Gets the index of the compared frames.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets the expected image frame of the comparison.
    /// </summary>
    public ImageFrame ExpectedImageFrame { get; }

    /// <summary>
    /// Gets the actual image frame of the comparison.
    /// </summary>
    public ImageFrame ActualImageFrame { get; }

    /// <summary>
    /// Gets the total difference of the frames, normalized by the image dimensions and the
    /// component count. A value between 0.0 and 1.0, where 0.0 means the frames are equal and 1.0
    /// means every component of every pixel differs by the full component range.
    /// </summary>
    public float TotalNormalizedDifference { get; }

    /// <summary>
    /// Gets the total difference of the frames as a percentage string.
    /// </summary>
    public string DifferencePercentageString
    {
        get
        {
            if (this.TotalNormalizedDifference == 0)
            {
                return "0%";
            }

            return string.Create(CultureInfo.InvariantCulture, $"{this.TotalNormalizedDifference * 100:0.0000}%");
        }
    }

    /// <summary>
    /// Gets a value indicating whether the difference is within the tolerance of the comparer
    /// which produced the report.
    /// </summary>
    public bool IsEmpty { get; }

    /// <summary>
    /// Creates a difference image for the compared frames. Pixels with equal components are
    /// rendered as mid-gray. Every component of the actual image which differs is rendered as
    /// white when it is above the component of the expected image, and black when it is below.
    /// Rendering is per RGB channel; the difference image is fully opaque, so alpha differences
    /// are reflected in the total difference only.
    /// </summary>
    /// <returns>The difference image.</returns>
    /// <exception cref="InvalidOperationException">
    /// The report does not contain the compared frames.
    /// </exception>
    public Image CreateDifferenceImage()
    {
        if (this.ExpectedImageFrame is null || this.ActualImageFrame is null)
        {
            throw new InvalidOperationException("The report does not contain the compared frames.");
        }

        DifferenceImageVisitor visitor = new(this.ExpectedImageFrame, this.ActualImageFrame);
        this.ExpectedImageFrame.Accept(visitor);

        // The visitor guarantees an image: the acceptance of a valid frame always dispatches the rendering.
        return visitor.DifferenceImage!;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return this.IsEmpty ? "[SimilarImages]" : $"Total difference: {this.DifferencePercentageString}";
    }

    /// <summary>
    /// Resolves the pixel type of the expected frame, then dispatches the pixel type of the
    /// actual frame, so that a pixel-specific difference image of both frames can be created.
    /// </summary>
    private sealed class DifferenceImageVisitor : IImageFrameVisitor
    {
        private readonly ImageFrame actual;

        public DifferenceImageVisitor(ImageFrame expectedImageFrame, ImageFrame actualImageFrame)
        {
            this.ExpectedImageFrame = expectedImageFrame;
            this.actual = actualImageFrame;
        }

        public ImageFrame ExpectedImageFrame { get; }

        public Image? DifferenceImage { get; private set; }

        public void Visit<TPixelA>(ImageFrame<TPixelA> expected)
            where TPixelA : unmanaged, IPixel<TPixelA>
        {
            ActualFrameVisitor<TPixelA> visitor = new(expected, this.actual);
            this.actual.Accept(visitor);
            this.DifferenceImage = visitor.DifferenceImage;
        }
    }

    /// <summary>
    /// Creates the difference image of both typed frames.
    /// </summary>
    /// <typeparam name="TPixelA">The pixel type of the expected frame.</typeparam>
    private sealed class ActualFrameVisitor<TPixelA> : IImageFrameVisitor
        where TPixelA : unmanaged, IPixel<TPixelA>
    {
        private readonly ImageFrame<TPixelA> expected;
        private readonly ImageFrame actual;

        public ActualFrameVisitor(ImageFrame<TPixelA> expected, ImageFrame actual)
        {
            this.expected = expected;
            this.actual = actual;
        }

        public Image? DifferenceImage { get; private set; }

        public void Visit<TPixelB>(ImageFrame<TPixelB> actual)
            where TPixelB : unmanaged, IPixel<TPixelB>
            => this.DifferenceImage = ImageComparisonEngine.CreateDifferenceImage(this.expected, actual);
    }
}
