// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.WebP.Lossy
{
    /// <summary>
    /// On-the-fly info about the current set of residuals.
    /// </summary>
    internal class Vp8Residual
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8Residual"/> class.
        /// </summary>
        public Vp8Residual()
        {
            this.Prob = new Vp8ProbaArray[3][];
            for (int i = 0; i < 3; i++)
            {
                this.Prob[i] = new Vp8ProbaArray[11];
            }
        }

        public int First { get; set; }

        public int Last { get; set; }

        public int CoeffType { get; set; }

        public short[] Coeffs { get; set; }

        public Vp8ProbaArray[][] Prob { get; }

        public void Init(int first, int coeffType)
        {
            this.First = first;
            this.CoeffType = coeffType;

            // TODO:
            // res->prob = enc->proba_.coeffs_[coeff_type];
            // res->stats = enc->proba_.stats_[coeff_type];
            // res->costs = enc->proba_.remapped_costs_[coeff_type];
        }

        public void SetCoeffs(Span<short> coeffs)
        {
            int n;
            this.Last = -1;
            for (n = 15; n >= 0; --n)
            {
                if (coeffs[n] != 0)
                {
                    this.Last = n;
                    break;
                }
            }

            this.Coeffs = coeffs.ToArray();
        }
    }
}
