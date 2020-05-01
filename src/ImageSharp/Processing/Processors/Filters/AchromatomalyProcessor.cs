// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Converts the colors of the image recreating Achromatomaly (Color desensitivity) color blindness.
    /// </summary>
    public sealed class AchromatomalyProcessor : FilterProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AchromatomalyProcessor"/> class.
        /// </summary>
        public AchromatomalyProcessor()
            : base(KnownFilterMatrices.AchromatomalyFilter)
        {
        }
    }
}