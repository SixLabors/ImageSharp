// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Experimental.WebP.Lossy
{
    /// <summary>
    /// Data for all frame-persistent probabilities.
    /// </summary>
    internal class Vp8Proba
    {
        private const int MbFeatureTreeProbs = 3;

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8Proba"/> class.
        /// </summary>
        public Vp8Proba()
        {
            this.Segments = new uint[MbFeatureTreeProbs];
            this.Bands = new Vp8BandProbas[WebPConstants.NumTypes, WebPConstants.NumBands];
            this.BandsPtr = new Vp8BandProbas[WebPConstants.NumTypes][];

            for (int i = 0; i < WebPConstants.NumTypes; i++)
            {
                for (int j = 0; j < WebPConstants.NumBands; j++)
                {
                    this.Bands[i, j] = new Vp8BandProbas();
                }
            }

            for (int i = 0; i < WebPConstants.NumTypes; i++)
            {
                this.BandsPtr[i] = new Vp8BandProbas[16 + 1];
            }
        }

        public uint[] Segments { get; }

        public Vp8BandProbas[,] Bands { get; }

        public Vp8BandProbas[][] BandsPtr { get; }
    }
}
