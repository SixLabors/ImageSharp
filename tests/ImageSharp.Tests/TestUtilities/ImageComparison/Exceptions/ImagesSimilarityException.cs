// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using System;

public class ImagesSimilarityException : Exception
{
    public ImagesSimilarityException(string message)
        : base(message)
    {
    }
}
