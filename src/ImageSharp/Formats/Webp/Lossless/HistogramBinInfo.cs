// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

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
