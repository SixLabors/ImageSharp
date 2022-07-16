// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless
{
    /// <summary>
    /// To perform backward reference every pixel at index index_ is considered and
    /// the cost for the MAX_LENGTH following pixels computed. Those following pixels
    /// at index index_ + k (k from 0 to MAX_LENGTH) have a cost of:
    ///     cost = distance cost at index + GetLengthCost(costModel, k)
    /// and the minimum value is kept. GetLengthCost(costModel, k) is cached in an
    /// array of size MAX_LENGTH.
    /// Instead of performing MAX_LENGTH comparisons per pixel, we keep track of the
    /// minimal values using intervals of constant cost.
    /// An interval is defined by the index_ of the pixel that generated it and
    /// is only useful in a range of indices from start to end (exclusive), i.e.
    /// it contains the minimum value for pixels between start and end.
    /// Intervals are stored in a linked list and ordered by start. When a new
    /// interval has a better value, old intervals are split or removed. There are
    /// therefore no overlapping intervals.
    /// </summary>
    [DebuggerDisplay("Start: {Start}, End: {End}, Cost: {Cost}")]
    internal class CostInterval
    {
        public float Cost { get; set; }

        public int Start { get; set; }

        public int End { get; set; }

        public int Index { get; set; }

        public CostInterval Previous { get; set; }

        public CostInterval Next { get; set; }
    }
}
