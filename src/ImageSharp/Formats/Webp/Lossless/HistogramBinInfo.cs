// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp.Lossless
{
    internal struct HistogramBinInfo
    {
        /// <summary>
        /// Position of the histogram that accumulates all histograms with the same binId.
        /// </summary>
        public short First;

        /// <summary>
        /// Number of combine failures per binId.
        /// </summary>
        public ushort NumCombineFailures;
    }
}
