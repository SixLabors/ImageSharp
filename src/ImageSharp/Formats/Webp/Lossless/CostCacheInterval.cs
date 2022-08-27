// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless
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
