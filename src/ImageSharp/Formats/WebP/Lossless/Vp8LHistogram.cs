// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Formats.WebP.Lossless
{
    internal class Vp8LHistogram
    {
        public Vp8LHistogram(Vp8LBackwardRefs refs, int paletteCodeBits)
        {
            if (paletteCodeBits >= 0)
            {
                this.PaletteCodeBits = paletteCodeBits;
            }

            //HistogramClear();
            // TODO: VP8LHistogramStoreRefs(refs);
        }

        public Vp8LHistogram()
        {
            this.Red = new uint[WebPConstants.NumLiteralCodes];
            this.Blue = new uint[WebPConstants.NumLiteralCodes];
            this.Alpha = new uint[WebPConstants.NumLiteralCodes];
            this.Distance = new uint[WebPConstants.NumLiteralCodes];
            this.Literal = new uint[WebPConstants.NumLiteralCodes]; // TODO: is this enough?
        }

        public int PaletteCodeBits { get; }

        /// <summary>
        /// Cached value of bit cost.
        /// </summary>
        public double BitCost { get; }

        /// <summary>
        /// Cached value of literal entropy costs.
        /// </summary>
        public double LiteralCost { get; }

        /// <summary>
        /// Cached value of red entropy costs.
        /// </summary>
        public double RedCost { get; }

        /// <summary>
        /// Cached value of blue entropy costs.
        /// </summary>
        public double BlueCost { get; }

        public uint[] Red { get; }

        public uint[] Blue { get; }

        public uint[] Alpha { get; }

        public uint[] Literal { get; }

        public uint[] Distance { get; }

        public double EstimateBits()
        {
            // TODO: implement this.
            return 0.0;
        }
    }
}
