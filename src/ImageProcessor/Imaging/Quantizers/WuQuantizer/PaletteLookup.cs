// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PaletteLookup.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Stores the indexed color palette of an image for fast access.
//   Adapted from <see href="https://github.com/drewnoakes" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Quantizers.WuQuantizer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using ImageProcessor.Imaging.Colors;

    /// <summary>
    /// Stores the indexed color palette of an image for fast access.
    /// Adapted from <see href="https://github.com/drewnoakes" />
    /// </summary>
    internal class PaletteLookup
    {
        /// <summary>
        /// The dictionary for caching lookup nodes.
        /// </summary>
        private Dictionary<int, LookupNode[]> lookupNodes;

        /// <summary>
        /// The palette mask.
        /// </summary>
        private int paletteMask;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteLookup"/> class.
        /// </summary>
        /// <param name="palette">
        /// The palette.
        /// </param>
        public PaletteLookup(Color32[] palette)
        {
            this.Palette = new LookupNode[palette.Length];
            for (int paletteIndex = 0; paletteIndex < palette.Length; paletteIndex++)
            {
                this.Palette[paletteIndex] = new LookupNode
                {
                    Color32 = palette[paletteIndex],
                    PaletteIndex = (byte)paletteIndex
                };
            }

            this.BuildLookup(palette);
        }

        /// <summary>
        /// Gets or sets the palette.
        /// </summary>
        private LookupNode[] Palette { get; set; }

        /// <summary>
        /// Gets palette index for the given pixel.
        /// </summary>
        /// <param name="pixel">
        /// The pixel to return the index for.
        /// </param>
        /// <returns>
        /// The <see cref="byte"/> representing the index.
        /// </returns>
        public byte GetPaletteIndex(Color32 pixel)
        {
            int pixelKey = pixel.Argb & this.paletteMask;
            LookupNode[] bucket;
            if (!this.lookupNodes.TryGetValue(pixelKey, out bucket))
            {
                bucket = this.Palette;
            }

            if (bucket.Length == 1)
            {
                return bucket[0].PaletteIndex;
            }

            int bestDistance = int.MaxValue;
            byte bestMatch = 0;
            foreach (LookupNode lookup in bucket)
            {
                Color32 lookupPixel = lookup.Color32;

                int deltaAlpha = pixel.A - lookupPixel.A;
                int distance = deltaAlpha * deltaAlpha;

                int deltaRed = pixel.R - lookupPixel.R;
                distance += deltaRed * deltaRed;

                int deltaGreen = pixel.G - lookupPixel.G;
                distance += deltaGreen * deltaGreen;

                int deltaBlue = pixel.B - lookupPixel.B;
                distance += deltaBlue * deltaBlue;

                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                bestMatch = lookup.PaletteIndex;
            }

            if ((bucket == this.Palette) && (pixelKey != 0))
            {
                this.lookupNodes[pixelKey] = new[] { bucket[bestMatch] };
            }

            return bestMatch;
        }

        /// <summary>
        /// Computes the bit mask.
        /// </summary>
        /// <param name="max">
        /// The maximum byte value.
        /// </param>
        /// <param name="bits">
        /// The number of bits.
        /// </param>
        /// <returns>
        /// The <see cref="byte"/>.
        /// </returns>
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

        /// <summary>
        /// Gets the mask value from the palette.
        /// </summary>
        /// <param name="palette">
        /// The palette.
        /// </param>
        /// <returns>
        /// The <see cref="int"/> representing the component value of the mask.
        /// </returns>
        private static int GetMask(Color32[] palette)
        {
            IEnumerable<byte> alphas = palette.Select(p => p.A).ToArray();
            byte maxAlpha = alphas.Max();
            int uniqueAlphas = alphas.Distinct().Count();

            IEnumerable<byte> reds = palette.Select(p => p.R).ToArray();
            byte maxRed = reds.Max();
            int uniqueReds = reds.Distinct().Count();

            IEnumerable<byte> greens = palette.Select(p => p.G).ToArray();
            byte maxGreen = greens.Max();
            int uniqueGreens = greens.Distinct().Count();

            IEnumerable<byte> blues = palette.Select(p => p.B).ToArray();
            byte maxBlue = blues.Max();
            int uniqueBlues = blues.Distinct().Count();

            double totalUniques = uniqueAlphas + uniqueReds + uniqueGreens + uniqueBlues;

            double availableBits = 1.0 + Math.Log(uniqueAlphas * uniqueReds * uniqueGreens * uniqueBlues);

            byte alphaMask = ComputeBitMask(maxAlpha, Convert.ToInt32(Math.Round(uniqueAlphas / totalUniques * availableBits)));
            byte redMask = ComputeBitMask(maxRed, Convert.ToInt32(Math.Round(uniqueReds / totalUniques * availableBits)));
            byte greenMask = ComputeBitMask(maxGreen, Convert.ToInt32(Math.Round(uniqueGreens / totalUniques * availableBits)));
            byte blueMask = ComputeBitMask(maxBlue, Convert.ToInt32(Math.Round(uniqueBlues / totalUniques * availableBits)));

            Color32 maskedPixel = new Color32(alphaMask, redMask, greenMask, blueMask);
            return maskedPixel.Argb;
        }

        /// <summary>
        /// Gets the highest set bit index.
        /// </summary>
        /// <param name="value">
        /// The value.
        /// </param>
        /// <returns>
        /// The <see cref="byte"/>.
        /// </returns>
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

        /// <summary>
        /// The build lookup.
        /// </summary>
        /// <param name="palette">
        /// The palette.
        /// </param>
        private void BuildLookup(Color32[] palette)
        {
            int mask = GetMask(palette);
            Dictionary<int, List<LookupNode>> tempLookup = new Dictionary<int, List<LookupNode>>();
            foreach (LookupNode lookup in this.Palette)
            {
                int pixelKey = lookup.Color32.Argb & mask;

                List<LookupNode> bucket;
                if (!tempLookup.TryGetValue(pixelKey, out bucket))
                {
                    bucket = new List<LookupNode>();
                    tempLookup[pixelKey] = bucket;
                }

                bucket.Add(lookup);
            }

            this.lookupNodes = new Dictionary<int, LookupNode[]>(tempLookup.Count);
            foreach (int key in tempLookup.Keys)
            {
                this.lookupNodes[key] = tempLookup[key].ToArray();
            }

            this.paletteMask = mask;
        }

        /// <summary>
        /// Represents a single node containing the index and pixel.
        /// </summary>
        private struct LookupNode
        {
            /// <summary>
            /// The palette index.
            /// </summary>
            public byte PaletteIndex;

            /// <summary>
            /// The pixel.
            /// </summary>
            public Color32 Color32;
        }
    }
}