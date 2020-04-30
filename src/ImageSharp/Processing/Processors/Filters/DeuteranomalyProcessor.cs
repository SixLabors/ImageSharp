// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Converts the colors of the image recreating Deuteranomaly (Green-Weak) color blindness.
    /// </summary>
    public sealed class DeuteranomalyProcessor : FilterProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeuteranomalyProcessor"/> class.
        /// </summary>
        public DeuteranomalyProcessor()
            : base(KnownFilterMatrices.DeuteranomalyFilter)
        {
        }
    }
}