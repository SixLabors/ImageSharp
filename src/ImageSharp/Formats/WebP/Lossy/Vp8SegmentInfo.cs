// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
{
    internal class Vp8SegmentInfo
    {
        /// <summary>
        /// Gets or sets the quantization matrix y1.
        /// </summary>
        public Vp8Matrix Y1 { get; set; }

        /// <summary>
        /// Gets or sets the quantization matrix y2.
        /// </summary>
        public Vp8Matrix Y2 { get; set; }

        /// <summary>
        /// Gets or sets the quantization matrix uv.
        /// </summary>
        public Vp8Matrix Uv { get; set; }

        /// <summary>
        /// Gets or sets the quant-susceptibility, range [-127,127]. Zero is neutral. Lower values indicate a lower risk of blurriness.
        /// </summary>
        public int Alpha { get; set; }

        /// <summary>
        /// Gets or sets the filter-susceptibility, range [0,255].
        /// </summary>
        public int Beta { get; set; }

        /// <summary>
        /// Gets or sets the final segment quantizer.
        /// </summary>
        public int Quant { get; set; }

        /// <summary>
        /// Gets or sets the final in-loop filtering strength.
        /// </summary>
        public int FStrength { get; set; }

        /// <summary>
        /// Gets or sets the max edge delta (for filtering strength).
        /// </summary>
        public int MaxEdge { get; set; }

        /// <summary>
        /// Gets or sets the penalty for using Intra4.
        /// </summary>
        public long I4Penalty { get; set; }
    }
}
