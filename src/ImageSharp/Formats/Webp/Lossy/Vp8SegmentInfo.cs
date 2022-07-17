// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
{
    internal class Vp8SegmentInfo
    {
        /// <summary>
        /// Gets the quantization matrix y1.
        /// </summary>
#pragma warning disable SA1401 // Fields should be private
        public Vp8Matrix Y1;

        /// <summary>
        /// Gets the quantization matrix y2.
        /// </summary>
        public Vp8Matrix Y2;

        /// <summary>
        /// Gets the quantization matrix uv.
        /// </summary>
        public Vp8Matrix Uv;
#pragma warning restore SA1401 // Fields should be private

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

        /// <summary>
        /// Gets or sets the minimum distortion required to trigger filtering record.
        /// </summary>
        public int MinDisto { get; set; }

        public int LambdaI16 { get; set; }

        public int LambdaI4 { get; set; }

        public int TLambda { get; set; }

        public int LambdaUv { get; set; }

        public int LambdaMode { get; set; }

        public void StoreMaxDelta(Span<short> dcs)
        {
            // We look at the first three AC coefficients to determine what is the average
            // delta between each sub-4x4 block.
            int v0 = Math.Abs(dcs[1]);
            int v1 = Math.Abs(dcs[2]);
            int v2 = Math.Abs(dcs[4]);
            int maxV = v1 > v0 ? v1 : v0;
            maxV = v2 > maxV ? v2 : maxV;
            if (maxV > this.MaxEdge)
            {
                this.MaxEdge = maxV;
            }
        }
    }
}
