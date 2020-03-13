// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// The version number setting enables or disables certain features in the bitstream.
    /// </summary>
    internal class Vp8Profile
    {
        /// <summary>
        /// Gets or sets the reconstruction filter.
        /// </summary>
        public ReconstructionFilter ReconstructionFilter { get; set; }

        /// <summary>
        /// Gets or sets the loop filter.
        /// </summary>
        public LoopFilter LoopFilter { get; set; }
    }
}
