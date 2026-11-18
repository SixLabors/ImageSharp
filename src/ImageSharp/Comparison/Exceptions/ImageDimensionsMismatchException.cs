// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Comparison.Exceptions;

/// <summary>
/// The exception that is thrown when the dimensions of the compared images or frames do not match.
/// </summary>
public class ImageDimensionsMismatchException : ImagesSimilarityException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImageDimensionsMismatchException"/> class.
    /// </summary>
    /// <param name="expectedSize">The expected size.</param>
    /// <param name="actualSize">The actual size.</param>
    public ImageDimensionsMismatchException(Size expectedSize, Size actualSize)
        : base($"The image dimensions {actualSize} do not match the expected {expectedSize}!")
    {
        this.ExpectedSize = expectedSize;
        this.ActualSize = actualSize;
    }

    /// <summary>
    /// Gets the expected size of the compared images or frames.
    /// </summary>
    public Size ExpectedSize { get; }

    /// <summary>
    /// Gets the actual size of the compared images or frames.
    /// </summary>
    public Size ActualSize { get; }
}
