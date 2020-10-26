// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP.Lossy
{
    internal class Vp8SegmentInfo
    {
        /// <summary>
        /// quantization matrix y1.
        /// </summary>
        public Vp8Matrix Y1 { get; set; }

        /// <summary>
        /// quantization matrix y2.
        /// </summary>
        public Vp8Matrix Y2 { get; set; }

        /// <summary>
        /// quantization matrix uv.
        /// </summary>
        public Vp8Matrix Uv { get; set; }

        /// <summary>
        /// quant-susceptibility, range [-127,127]. Zero is neutral. Lower values indicate a lower risk of blurriness.
        /// </summary>
        public int Alpha { get; set; }

        /// <summary>
        /// filter-susceptibility, range [0,255].
        /// </summary>
        public int Beta { get; set; }

        /// <summary>
        /// final segment quantizer.
        /// </summary>
        public int Quant { get; set; }

        /// <summary>
        /// final in-loop filtering strength.
        /// </summary>
        public int FStrength { get; set; }

        /// <summary>
        /// max edge delta (for filtering strength).
        /// </summary>
        public int MaxEdge { get; set; }

        /// <summary>
        /// penalty for using Intra4.
        /// </summary>
        public long I4Penalty { get; set; }
    }
}
