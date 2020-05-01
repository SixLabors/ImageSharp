// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Converts the colors of the image recreating Tritanopia (Blue-Blind) color blindness.
    /// </summary>
    public sealed class TritanopiaProcessor : FilterProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TritanopiaProcessor"/> class.
        /// </summary>
        public TritanopiaProcessor()
            : base(KnownFilterMatrices.TritanopiaFilter)
        {
        }
    }
}