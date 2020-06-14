// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System.Diagnostics;

namespace SixLabors.ImageSharp.Formats.WebP.Lossless
{
    /// <summary>
    /// The GetLengthCost(costModel, k) are cached in a CostCacheInterval.
    /// </summary>
    [DebuggerDisplay("Start: {Start}, End: {End}, Cost: {Cost}")]
    internal class CostCacheInterval
    {
        public double Cost { get; set; }

        public int Start { get; set; }

        public int End { get; set; } // Exclusive.
    }
}
