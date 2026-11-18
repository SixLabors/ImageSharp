// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Comparison.Exceptions;

/// <summary>
/// The exception that is thrown when the number of frames of the compared images does not match.
/// </summary>
public class ImageFramesMismatchException : ImagesSimilarityException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImageFramesMismatchException"/> class.
    /// </summary>
    /// <param name="expectedCount">The expected frame count.</param>
    /// <param name="actualCount">The actual frame count.</param>
    public ImageFramesMismatchException(int expectedCount, int actualCount)
        : base($"The image frame count {actualCount} does not match the expected {expectedCount}!")
    {
        this.ExpectedCount = expectedCount;
        this.ActualCount = actualCount;
    }

    /// <summary>
    /// Gets the expected frame count of the compared images.
    /// </summary>
    public int ExpectedCount { get; }

    /// <summary>
    /// Gets the actual frame count of the compared images.
    /// </summary>
    public int ActualCount { get; }
}
