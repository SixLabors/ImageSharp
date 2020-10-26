// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP.Lossy
{
    internal class Vp8Matrix
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8Matrix"/> class.
        /// </summary>
        public Vp8Matrix()
        {
            this.Q = new short[16];
            this.IQ = new short[16];
            this.Bias = new uint[16];
            this.ZThresh = new uint[16];
            this.Sharpen = new short[16];
        }

        /// <summary>
        /// quantizer steps.
        /// </summary>
        public short[] Q { get; }

        /// <summary>
        /// reciprocals, fixed point.
        /// </summary>
        public short[] IQ { get; }

        /// <summary>
        /// rounding bias.
        /// </summary>
        public uint[] Bias { get; }

        /// <summary>
        /// value below which a coefficient is zeroed.
        /// </summary>
        public uint[] ZThresh { get; }

        /// <summary>
        /// frequency boosters for slight sharpening.
        /// </summary>
        public short[] Sharpen { get; }
    }
}
