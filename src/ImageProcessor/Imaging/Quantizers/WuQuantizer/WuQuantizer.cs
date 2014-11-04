using System;
using System.Collections.Generic;
using System.Drawing;

namespace nQuant
{
    public class WuQuantizer : WuQuantizerBase, IWuQuantizer
    {
         protected override QuantizedPalette GetQuantizedPalette(int colorCount, ColorData data, Box[] cubes, int alphaThreshold)
         {
            int pixelsCount = data.Pixels.Length;
            Lookup[] lookups = BuildLookups(cubes, data);

            var alphas = new int[colorCount + 1];
            var reds = new int[colorCount + 1];
            var greens = new int[colorCount + 1];
            var blues = new int[colorCount + 1];
            var sums = new int[colorCount + 1];
            var palette = new QuantizedPalette(pixelsCount);

            IList<Pixel> pixels = data.Pixels;

            Dictionary<int, byte> cachedMaches = new Dictionary<int, byte>();

            for (int pixelIndex = 0; pixelIndex < pixelsCount; pixelIndex++)
            {
                Pixel pixel = pixels[pixelIndex];

                if (pixel.Alpha > alphaThreshold)
                {
                    byte bestMatch;
                    int argb = pixel.Argb;

                    if (!cachedMaches.TryGetValue(argb, out bestMatch))
                    {
                        int bestDistance = int.MaxValue;

                        for (int lookupIndex = 0; lookupIndex < lookups.Length; lookupIndex++)
                        {
                            Lookup lookup = lookups[lookupIndex];
                            var deltaAlpha = pixel.Alpha - lookup.Alpha;
                            var deltaRed = pixel.Red - lookup.Red;
                            var deltaGreen = pixel.Green - lookup.Green;
                            var deltaBlue = pixel.Blue - lookup.Blue;

                            int distance = deltaAlpha * deltaAlpha + deltaRed * deltaRed + deltaGreen * deltaGreen + deltaBlue * deltaBlue;

                            if (distance >= bestDistance)
                                continue;

                            bestDistance = distance;
                            bestMatch = (byte)lookupIndex;
                        }

                        cachedMaches[argb] = bestMatch;
                    }

                    alphas[bestMatch] += pixel.Alpha;
                    reds[bestMatch] += pixel.Red;
                    greens[bestMatch] += pixel.Green;
                    blues[bestMatch] += pixel.Blue;
                    sums[bestMatch]++;

                    palette.PixelIndex[pixelIndex] = bestMatch;
                }
                else
                {
                    palette.PixelIndex[pixelIndex] = AlphaColor;
                }
            }

            for (var paletteIndex = 0; paletteIndex < colorCount; paletteIndex++)
            {
                if (sums[paletteIndex] > 0)
                {
                    alphas[paletteIndex] /= sums[paletteIndex];
                    reds[paletteIndex] /= sums[paletteIndex];
                    greens[paletteIndex] /= sums[paletteIndex];
                    blues[paletteIndex] /= sums[paletteIndex];
                }

                var color = Color.FromArgb(alphas[paletteIndex], reds[paletteIndex], greens[paletteIndex], blues[paletteIndex]);
                palette.Colors.Add(color);
            }

            palette.Colors.Add(Color.FromArgb(0, 0, 0, 0));

            return palette;
        }
    }
}
