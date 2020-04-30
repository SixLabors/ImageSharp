// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Converts the colors of the image recreating Protanopia (Red-Blind) color blindness.
    /// </summary>
    public sealed class ProtanopiaProcessor : FilterProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProtanopiaProcessor"/> class.
        /// </summary>
        public ProtanopiaProcessor()
            : base(KnownFilterMatrices.ProtanopiaFilter)
        {
        }
    }
}