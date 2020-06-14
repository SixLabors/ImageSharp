// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Formats.WebP.Lossless
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
        public short NumCombineFailures;
    }
}
