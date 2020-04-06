// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// The version number of the frame header enables or disables certain features in the bitstream.
    /// +---------+-------------------------+-------------+
    /// | Version | Reconstruction Filter   | Loop Filter |
    /// +---------+-------------------------+-------------+
    /// | 0       | Bicubic                 | Normal      |
    /// |         |                         |             |
    /// | 1       | Bilinear                | Simple      |
    /// |         |                         |             |
    /// | 2       | Bilinear                | None        |
    /// |         |                         |             |
    /// | 3       | None                    | None        |
    /// |         |                         |             |
    /// | Other   | Reserved for future use |             |
    /// +---------+-------------------------+-------------+
    /// See paragraph 9, https://tools.ietf.org/html/rfc6386.
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
