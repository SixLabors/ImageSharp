// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
{
    /// <summary>
    /// Data needed to reconstruct a macroblock.
    /// </summary>
    internal class Vp8MacroBlockData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8MacroBlockData"/> class.
        /// </summary>
        public Vp8MacroBlockData()
        {
            this.Modes = new byte[16];
            this.Coeffs = new short[384];
        }

        /// <summary>
        /// Gets or sets the coefficients. 384 coeffs = (16+4+4) * 4*4.
        /// </summary>
        public short[] Coeffs { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether its intra4x4.
        /// </summary>
        public bool IsI4x4 { get; set; }

        /// <summary>
        /// Gets the modes. One 16x16 mode (#0) or sixteen 4x4 modes.
        /// </summary>
        public byte[] Modes { get; }

        /// <summary>
        /// Gets or sets the chroma prediction mode.
        /// </summary>
        public byte UvMode { get; set; }

        /// <summary>
        /// Gets or sets bit-wise info about the content of each sub-4x4 blocks (in decoding order).
        /// Each of the 4x4 blocks for y/u/v is associated with a 2b code according to:
        ///   code=0 -> no coefficient
        ///   code=1 -> only DC
        ///   code=2 -> first three coefficients are non-zero
        ///   code=3 -> more than three coefficients are non-zero
        /// This allows to call specialized transform functions.
        /// </summary>
        public uint NonZeroY { get; set; }

        /// <summary>
        /// Gets or sets bit-wise info about the content of each sub-4x4 blocks (in decoding order).
        /// Each of the 4x4 blocks for y/u/v is associated with a 2b code according to:
        ///   code=0 -> no coefficient
        ///   code=1 -> only DC
        ///   code=2 -> first three coefficients are non-zero
        ///   code=3 -> more than three coefficients are non-zero
        /// This allows to call specialized transform functions.
        /// </summary>
        public uint NonZeroUv { get; set; }

        public bool Skip { get; set; }

        public byte Segment { get; set; }
    }
}
