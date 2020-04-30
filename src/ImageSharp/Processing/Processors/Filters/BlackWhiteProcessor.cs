// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Applies a black and white filter matrix to the image.
    /// </summary>
    public sealed class BlackWhiteProcessor : FilterProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BlackWhiteProcessor"/> class.
        /// </summary>
        public BlackWhiteProcessor()
            : base(KnownFilterMatrices.BlackWhiteFilter)
        {
        }
    }
}