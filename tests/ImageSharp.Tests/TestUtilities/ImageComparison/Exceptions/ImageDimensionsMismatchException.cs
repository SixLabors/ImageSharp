// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison
{
    public class ImageDimensionsMismatchException : ImagesSimilarityException
    {
        public ImageDimensionsMismatchException(Size expectedSize, Size actualSize)
            : base($"The image dimensions {actualSize} do not match the expected {expectedSize}!")
        {
            this.ExpectedSize = expectedSize;
            this.ActualSize = actualSize;
        }

        public Size ExpectedSize { get; }

        public Size ActualSize { get; }
    }
}
