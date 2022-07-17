// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp.Lossless
{
    /// <summary>
    /// Data container to keep track of cost range for the three dominant entropy symbols.
    /// </summary>
    internal class DominantCostRange
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DominantCostRange"/> class.
        /// </summary>
        public DominantCostRange()
        {
            this.LiteralMax = 0.0d;
            this.LiteralMin = double.MaxValue;
            this.RedMax = 0.0d;
            this.RedMin = double.MaxValue;
            this.BlueMax = 0.0d;
            this.BlueMin = double.MaxValue;
        }

        public double LiteralMax { get; set; }

        public double LiteralMin { get; set; }

        public double RedMax { get; set; }

        public double RedMin { get; set; }

        public double BlueMax { get; set; }

        public double BlueMin { get; set; }

        public void UpdateDominantCostRange(Vp8LHistogram h)
        {
            if (this.LiteralMax < h.LiteralCost)
            {
                this.LiteralMax = h.LiteralCost;
            }

            if (this.LiteralMin > h.LiteralCost)
            {
                this.LiteralMin = h.LiteralCost;
            }

            if (this.RedMax < h.RedCost)
            {
                this.RedMax = h.RedCost;
            }

            if (this.RedMin > h.RedCost)
            {
                this.RedMin = h.RedCost;
            }

            if (this.BlueMax < h.BlueCost)
            {
                this.BlueMax = h.BlueCost;
            }

            if (this.BlueMin > h.BlueCost)
            {
                this.BlueMin = h.BlueCost;
            }
        }

        public int GetHistoBinIndex(Vp8LHistogram h, int numPartitions)
        {
            int binId = GetBinIdForEntropy(this.LiteralMin, this.LiteralMax, h.LiteralCost, numPartitions);
            binId = (binId * numPartitions) + GetBinIdForEntropy(this.RedMin, this.RedMax, h.RedCost, numPartitions);
            binId = (binId * numPartitions) + GetBinIdForEntropy(this.BlueMin, this.BlueMax, h.BlueCost, numPartitions);

            return binId;
        }

        private static int GetBinIdForEntropy(double min, double max, double val, int numPartitions)
        {
            double range = max - min;
            if (range > 0.0d)
            {
                double delta = val - min;
                return (int)((numPartitions - 1e-6) * delta / range);
            }
            else
            {
                return 0;
            }
        }
    }
}
