// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
{
    /// <summary>
    /// Class for organizing convergence in either size or PSNR.
    /// </summary>
    internal class PassStats
    {
        public PassStats(long targetSize, float targetPsnr, int qMin, int qMax, int quality)
        {
            bool doSizeSearch = targetSize != 0;

            this.IsFirst = true;
            this.Dq = 10.0f;
            this.Qmin = qMin;
            this.Qmax = qMax;
            this.Q = Numerics.Clamp(quality, qMin, qMax);
            this.LastQ = this.Q;
            this.Target = doSizeSearch ? targetSize
                : targetPsnr > 0.0f ? targetPsnr
                : 40.0f;   // default, just in case
            this.Value = 0.0f;
            this.LastValue = 0.0f;
            this.DoSizeSearch = doSizeSearch;
        }

        public bool IsFirst { get; set; }

        public float Dq { get; set; }

        public float Q { get; set; }

        public float LastQ { get; set; }

        public float Qmin { get; }

        public float Qmax { get; }

        public double Value { get; set; } // PSNR or size

        public double LastValue { get; set; }

        public double Target { get; }

        public bool DoSizeSearch { get; }

        public float ComputeNextQ()
        {
            float dq;
            if (this.IsFirst)
            {
                dq = this.Value > this.Target ? -this.Dq : this.Dq;
                this.IsFirst = false;
            }
            else if (this.Value != this.LastValue)
            {
                double slope = (this.Target - this.Value) / (this.LastValue - this.Value);
                dq = (float)(slope * (this.LastQ - this.Q));
            }
            else
            {
                dq = 0.0f;  // we're done?!
            }

            // Limit variable to avoid large swings.
            this.Dq = Numerics.Clamp(dq, -30.0f, 30.0f);
            this.LastQ = this.Q;
            this.LastValue = this.Value;
            this.Q = Numerics.Clamp(this.Q + this.Dq, this.Qmin, this.Qmax);

            return this.Q;
        }
    }
}
