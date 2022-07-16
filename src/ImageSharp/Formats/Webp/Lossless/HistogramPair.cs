// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless
{
    /// <summary>
    /// Pair of histograms. Negative Idx1 value means that pair is out-of-date.
    /// </summary>
    [DebuggerDisplay("Idx1: {Idx1}, Idx2: {Idx2}, CostDiff: {CostDiff}, CostCombo: {CostCombo}")]
    internal class HistogramPair
    {
        public int Idx1 { get; set; }

        public int Idx2 { get; set; }

        public double CostDiff { get; set; }

        public double CostCombo { get; set; }
    }
}
