// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
{
    internal class Vp8Matrix
    {
        private static readonly int[][] BiasMatrices =
        {
            // [luma-ac,luma-dc,chroma][dc,ac]
            new[] { 96, 110 },
            new[] { 96, 108 },
            new[] { 110, 115 }
        };

        // Sharpening by (slightly) raising the hi-frequency coeffs.
        // Hack-ish but helpful for mid-bitrate range. Use with care.
        private static readonly byte[] FreqSharpening = { 0, 30, 60, 90, 30, 60, 90, 90, 60, 90, 90, 90, 90, 90, 90, 90 };

        /// <summary>
        /// Number of descaling bits for sharpening bias.
        /// </summary>
        private const int SharpenBits = 11;

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8Matrix"/> class.
        /// </summary>
        public Vp8Matrix()
        {
            this.Q = new ushort[16];
            this.IQ = new ushort[16];
            this.Bias = new uint[16];
            this.ZThresh = new uint[16];
            this.Sharpen = new short[16];
        }

        /// <summary>
        /// Gets the quantizer steps.
        /// </summary>
        public ushort[] Q { get; }

        /// <summary>
        /// Gets the reciprocals, fixed point.
        /// </summary>
        public ushort[] IQ { get; }

        /// <summary>
        /// Gets the rounding bias.
        /// </summary>
        public uint[] Bias { get; }

        /// <summary>
        /// Gets the value below which a coefficient is zeroed.
        /// </summary>
        public uint[] ZThresh { get; }

        /// <summary>
        /// Gets the frequency boosters for slight sharpening.
        /// </summary>
        public short[] Sharpen { get; }

        /// <summary>
        /// Returns the average quantizer.
        /// </summary>
        /// <returns>The average quantizer.</returns>
        public int Expand(int type)
        {
            int sum;
            int i;
            for (i = 0; i < 2; ++i)
            {
                int isAcCoeff = (i > 0) ? 1 : 0;
                int bias = BiasMatrices[type][isAcCoeff];
                this.IQ[i] = (ushort)((1 << WebpConstants.QFix) / this.Q[i]);
                this.Bias[i] = (uint)this.BIAS(bias);

                // zthresh is the exact value such that QUANTDIV(coeff, iQ, B) is:
                //   * zero if coeff <= zthresh
                //   * non-zero if coeff > zthresh
                this.ZThresh[i] = ((1 << WebpConstants.QFix) - 1 - this.Bias[i]) / this.IQ[i];
            }

            for (i = 2; i < 16; ++i)
            {
                this.Q[i] = this.Q[1];
                this.IQ[i] = this.IQ[1];
                this.Bias[i] = this.Bias[1];
                this.ZThresh[i] = this.ZThresh[1];
            }

            for (sum = 0, i = 0; i < 16; ++i)
            {
                if (type == 0)
                {
                    // We only use sharpening for AC luma coeffs.
                    this.Sharpen[i] = (short)((FreqSharpening[i] * this.Q[i]) >> SharpenBits);
                }
                else
                {
                    this.Sharpen[i] = 0;
                }

                sum += this.Q[i];
            }

            return (sum + 8) >> 4;
        }

        private int BIAS(int b)
        {
            return b << (WebpConstants.QFix - 8);
        }
    }
}
