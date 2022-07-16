// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
{
    /// <summary>
    /// Contextual macroblock information.
    /// </summary>
    internal class Vp8MacroBlock
    {
        /// <summary>
        /// Gets or sets non-zero AC/DC coeffs (4bit for luma + 4bit for chroma).
        /// </summary>
        public uint NoneZeroAcDcCoeffs { get; set; }

        /// <summary>
        /// Gets or sets non-zero DC coeff (1bit).
        /// </summary>
        public uint NoneZeroDcCoeffs { get; set; }
    }
}
