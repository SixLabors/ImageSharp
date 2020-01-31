// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Holds information for decoding a lossy webp image.
    /// </summary>
    internal class Vp8Decoder
    {
        public Vp8Decoder()
        {
            this.DeQuantMatrices = new Vp8QuantMatrix[WebPConstants.NumMbSegments];
        }

        public Vp8FrameHeader FrameHeader { get; set; }

        public Vp8PictureHeader PictureHeader { get; set; }

        public Vp8FilterHeader FilterHeader { get; set; }

        public Vp8SegmentHeader SegmentHeader { get; set; }

        public bool Dither { get; set; }

        /// <summary>
        /// Gets or sets dequantization matrices (one set of DC/AC dequant factor per segment).
        /// </summary>
        public Vp8QuantMatrix[] DeQuantMatrices { get; private set; }

        public Vp8Proba Probabilities { get; set; }

        /// <summary>
        /// Gets or sets the width in macroblock units.
        /// </summary>
        public int MbWidth { get; set; }

        /// <summary>
        /// Gets or sets the height in macroblock units.
        /// </summary>
        public int MbHeight { get; set; }

        /// <summary>
        /// Gets or sets the current x position in macroblock units.
        /// </summary>
        public int MbX { get; set; }

        /// <summary>
        /// Gets or sets the current y position in macroblock units.
        /// </summary>
        public int MbY { get; set; }

        /// <summary>
        /// Gets or sets the parsed reconstruction data.
        /// </summary>
        public Vp8MacroBlockData[] MacroBlockData { get; set; }

        /// <summary>
        /// Gets or sets contextual macroblock infos.
        /// </summary>
        public Vp8MacroBlock[] MacroBlockInfo { get; set; }

        /// <summary>
        /// Gets or sets filter strength info.
        /// </summary>
        public Vp8FilterInfo FilterInfo { get; set; }
    }
}
