// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Applies a filter matrix recreating an old Kodachrome camera effect matrix to the image
    /// </summary>
    public sealed class KodachromeProcessor : FilterProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="KodachromeProcessor"/> class.
        /// </summary>
        public KodachromeProcessor()
            : base(KnownFilterMatrices.KodachromeFilter)
        {
        }
    }
}