using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;

namespace nQuant
{
    class PaletteBuffer
    {
        public PaletteBuffer(int colorCount)
        {
            Alphas = new int[colorCount + 1];
            Reds = new int[colorCount + 1];
            Greens = new int[colorCount + 1];
            Blues = new int[colorCount + 1];
            Sums = new int[colorCount + 1];
        }

        public ColorPalette BuildPalette(ColorPalette palette)
        {
            var alphas = this.Alphas;
            var reds = this.Reds;
            var greens = this.Greens;
            var blues = this.Blues;
            var sums = this.Sums;

            for (var paletteIndex = 0; paletteIndex < Sums.Length; paletteIndex++)
            {
                if (sums[paletteIndex] > 0)
                {
                    alphas[paletteIndex] /= sums[paletteIndex];
                    reds[paletteIndex] /= sums[paletteIndex];
                    greens[paletteIndex] /= sums[paletteIndex];
                    blues[paletteIndex] /= sums[paletteIndex];
                }

                var color = Color.FromArgb(alphas[paletteIndex], reds[paletteIndex], greens[paletteIndex], blues[paletteIndex]);
                palette.Entries[paletteIndex] = color;
            }

            return palette;
        }

        public int[] Alphas { get; set; }
        public int[] Reds { get; set; }
        public int[] Greens { get; set; }
        public int[] Blues { get; set; }
        public int[] Sums { get; set; }
    }
}