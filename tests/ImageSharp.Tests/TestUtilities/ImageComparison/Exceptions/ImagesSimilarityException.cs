// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison
{
    using System;

    public class ImagesSimilarityException : Exception
    {
        public ImagesSimilarityException(string message)
            : base(message)
        {
        }
    }
}
