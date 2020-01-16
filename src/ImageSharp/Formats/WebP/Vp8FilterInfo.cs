// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Filter information.
    /// </summary>
    internal class Vp8FilterInfo
    {
        /// <summary>
        /// Gets or sets the filter limit in [3..189], or 0 if no filtering.
        /// </summary>
        public sbyte Limit { get; set; }

        /// <summary>
        /// Gets or sets the inner limit in [1..63].
        /// </summary>
        public sbyte InnerLevel { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to do inner filtering.
        /// </summary>
        public bool InnerFiltering { get; set; }

        /// <summary>
        /// Gets or sets the high edge variance threshold in [0..2].
        /// </summary>
        public sbyte HighEdgeVarianceThreshold { get; set; }
    }
}
