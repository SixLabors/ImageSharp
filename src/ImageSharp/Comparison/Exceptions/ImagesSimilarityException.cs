// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;

namespace SixLabors.ImageSharp.Comparison.Exceptions;

/// <summary>
/// The exception that is thrown when the similarity between two images does not meet the
/// requirements of the comparison.
/// </summary>
public class ImagesSimilarityException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImagesSimilarityException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ImagesSimilarityException(string message)
        : base(message)
    {
    }
}
