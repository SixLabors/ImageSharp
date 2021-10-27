// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
{
    /// <summary>
    /// Class to accumulate score and info during RD-optimization and mode evaluation.
    /// </summary>
    internal class Vp8ModeScore
    {
        public const long MaxCost = 0x7fffffffffffffL;

        /// <summary>
        /// Distortion multiplier (equivalent of lambda).
        /// </summary>
        private const int RdDistoMult = 256;

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8ModeScore"/> class.
        /// </summary>
        public Vp8ModeScore()
        {
            this.YDcLevels = new short[16];
            this.YAcLevels = new short[16 * 16];
            this.UvLevels = new short[(4 + 4) * 16];

            this.ModesI4 = new byte[16];
            this.Derr = new int[2, 3];
        }

        /// <summary>
        /// Gets or sets the distortion.
        /// </summary>
        public long D { get; set; }

        /// <summary>
        /// Gets or sets the spectral distortion.
        /// </summary>
        public long SD { get; set; }

        /// <summary>
        /// Gets or sets the header bits.
        /// </summary>
        public long H { get; set; }

        /// <summary>
        /// Gets or sets the rate.
        /// </summary>
        public long R { get; set; }

        /// <summary>
        /// Gets or sets the score.
        /// </summary>
        public long Score { get; set; }

        /// <summary>
        /// Gets the quantized levels for luma-DC.
        /// </summary>
        public short[] YDcLevels { get; }

        /// <summary>
        /// Gets the quantized levels for luma-AC.
        /// </summary>
        public short[] YAcLevels { get; }

        /// <summary>
        /// Gets the quantized levels for chroma.
        /// </summary>
        public short[] UvLevels { get; }

        /// <summary>
        /// Gets or sets the mode number for intra16 prediction.
        /// </summary>
        public int ModeI16 { get; set; }

        /// <summary>
        /// Gets the mode numbers for intra4 predictions.
        /// </summary>
        public byte[] ModesI4 { get; }

        /// <summary>
        /// Gets or sets the mode number of chroma prediction.
        /// </summary>
        public int ModeUv { get; set; }

        /// <summary>
        /// Gets or sets the Non-zero blocks.
        /// </summary>
        public uint Nz { get; set; }

        /// <summary>
        /// Gets the diffusion errors.
        /// </summary>
        public int[,] Derr { get; }

        public void InitScore()
        {
            this.D = 0;
            this.SD = 0;
            this.R = 0;
            this.H = 0;
            this.Nz = 0;
            this.Score = MaxCost;
        }

        public void CopyScore(Vp8ModeScore other)
        {
            this.D = other.D;
            this.SD = other.SD;
            this.R = other.R;
            this.H = other.H;
            this.Nz = other.Nz;      // note that nz is not accumulated, but just copied.
            this.Score = other.Score;
        }

        public void AddScore(Vp8ModeScore other)
        {
            this.D += other.D;
            this.SD += other.SD;
            this.R += other.R;
            this.H += other.H;
            this.Nz |= other.Nz;     // here, new nz bits are accumulated.
            this.Score += other.Score;
        }

        public void SetRdScore(int lambda) => this.Score = ((this.R + this.H) * lambda) + (RdDistoMult * (this.D + this.SD));
    }
}
