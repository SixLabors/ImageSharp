// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Data needed to reconstruct a macroblock.
    /// </summary>
    internal class Vp8MacroBlockData
    {
        public Vp8MacroBlockData()
        {
            this.Modes = new byte[16];
            this.Coeffs = new short[384];
        }

        /// <summary>
        /// Gets or sets the coefficient. 384 coeffs = (16+4+4) * 4*4.
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

        public uint NonZeroY { get; set; }

        public uint NonZeroUv { get; set; }

        public byte Skip { get; set; }

        public byte Segment { get; set; }
    }
}
