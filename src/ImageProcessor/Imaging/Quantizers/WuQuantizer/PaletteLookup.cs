using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace nQuant
{
    class PaletteLookup
    {
        private int mMask;
        private Dictionary<int, List<LookupNode>> mLookup = new Dictionary<int, List<LookupNode>>(255);
        private List<LookupNode> Palette { get; set; }

        public PaletteLookup(List<Pixel> palette)
        {
            Palette = new List<LookupNode>(palette.Count);
            for (int paletteIndex = 0; paletteIndex < palette.Count; paletteIndex++)
            {
                Palette.Add(new LookupNode { Pixel = palette[paletteIndex], PaletteIndex = (byte)paletteIndex });
            }
            BuildLookup(palette);
        }

        public byte GetPaletteIndex(Pixel pixel)
        {

            int pixelKey = pixel.Argb & mMask;
            List<LookupNode> bucket;
            if (!mLookup.TryGetValue(pixelKey, out bucket))
            {
                bucket = Palette;
            }

            if (bucket.Count == 1)
            {
                return bucket[0].PaletteIndex;
            }

            int bestDistance = int.MaxValue;
            byte bestMatch = 0;
            for (int lookupIndex = 0; lookupIndex < bucket.Count; lookupIndex++)
            {
                var lookup = bucket[lookupIndex];
                var lookupPixel = lookup.Pixel;

                var deltaAlpha = pixel.Alpha - lookupPixel.Alpha;
                int distance = deltaAlpha * deltaAlpha;

                var deltaRed = pixel.Red - lookupPixel.Red;
                distance += deltaRed * deltaRed;

                var deltaGreen = pixel.Green - lookupPixel.Green;
                distance += deltaGreen * deltaGreen;

                var deltaBlue = pixel.Blue - lookupPixel.Blue;
                distance += deltaBlue * deltaBlue;

                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                bestMatch = lookup.PaletteIndex;
            }
            return bestMatch;
        }

        private void BuildLookup(List<Pixel> palette)
        {
            int mask = GetMask(palette);
            foreach (LookupNode lookup in Palette)
            {
                int pixelKey = lookup.Pixel.Argb & mask;

                List<LookupNode> bucket;
                if (!mLookup.TryGetValue(pixelKey, out bucket))
                {
                    bucket = new List<LookupNode>();
                    mLookup[pixelKey] = bucket;
                }
                bucket.Add(lookup);
            }

            mMask = mask;
        }

        private static int GetMask(List<Pixel> palette)
        {
            IEnumerable<byte> alphas = from pixel in palette
                                       select pixel.Alpha;
            byte maxAlpha = alphas.Max();
            int uniqueAlphas = alphas.Distinct().Count();

            IEnumerable<byte> reds = from pixel in palette
                                     select pixel.Red;
            byte maxRed = reds.Max();
            int uniqueReds = reds.Distinct().Count();

            IEnumerable<byte> greens = from pixel in palette
                                       select pixel.Green;
            byte maxGreen = greens.Max();
            int uniqueGreens = greens.Distinct().Count();

            IEnumerable<byte> blues = from pixel in palette
                                      select pixel.Blue;
            byte maxBlue = blues.Max();
            int uniqueBlues = blues.Distinct().Count();

            double totalUniques = uniqueAlphas + uniqueReds + uniqueGreens + uniqueBlues;

            const double AvailableBits = 8f;

            byte alphaMask = ComputeBitMask(maxAlpha, Convert.ToInt32(Math.Round(uniqueAlphas / totalUniques * AvailableBits)));
            byte redMask = ComputeBitMask(maxRed, Convert.ToInt32(Math.Round(uniqueReds / totalUniques * AvailableBits)));
            byte greenMask = ComputeBitMask(maxGreen, Convert.ToInt32(Math.Round(uniqueGreens / totalUniques * AvailableBits)));
            byte blueMask = ComputeBitMask(maxAlpha, Convert.ToInt32(Math.Round(uniqueBlues / totalUniques * AvailableBits)));

            Pixel maskedPixel = new Pixel(alphaMask, redMask, greenMask, blueMask);
            return maskedPixel.Argb;
        }

        private static byte ComputeBitMask(byte max, int bits)
        {
            byte mask = 0;

            if (bits != 0)
            {
                byte highestSetBitIndex = HighestSetBitIndex(max);


                for (int i = 0; i < bits; i++)
                {
                    mask <<= 1;
                    mask++;
                }

                for (int i = 0; i <= highestSetBitIndex - bits; i++)
                {
                    mask <<= 1;
                }
            }
            return mask;
        }

        private static byte HighestSetBitIndex(byte value)
        {
            byte index = 0;
            for (int i = 0; i < 8; i++)
            {
                if (0 != (value & 1))
                {
                    index = (byte)i;
                }
                value >>= 1;
            }
            return index;
        }

        private struct LookupNode
        {
            public Pixel Pixel;
            public byte PaletteIndex;
        }
    }
}
