using System;
using System.Collections.Generic;
using System.Drawing;

namespace nQuant
{
    using System.Drawing.Imaging;

    public class WuQuantizer : WuQuantizerBase, IWuQuantizer
    {
        private IEnumerable<byte> indexedPixels(ImageBuffer image, List<Pixel> lookups, int alphaThreshold, PaletteBuffer paletteBuffer)
        {
            var alphas = paletteBuffer.Alphas;
            var reds = paletteBuffer.Reds;
            var greens = paletteBuffer.Greens;
            var blues = paletteBuffer.Blues;
            var sums = paletteBuffer.Sums;

            PaletteLookup lookup = new PaletteLookup(lookups);

            foreach (Pixel pixel in image.Pixels)
            {
                byte bestMatch = 255;

                if (pixel.Alpha >= alphaThreshold)
                {
                    bestMatch = lookup.GetPaletteIndex(pixel);

                    alphas[bestMatch] += pixel.Alpha;
                    reds[bestMatch] += pixel.Red;
                    greens[bestMatch] += pixel.Green;
                    blues[bestMatch] += pixel.Blue;
                    sums[bestMatch]++;
                }

                yield return bestMatch;
            }
        }


        internal override Image GetQuantizedImage(ImageBuffer image, int colorCount, List<Pixel> lookups, int alphaThreshold)
        {
            var result = new Bitmap(image.Image.Width, image.Image.Height, PixelFormat.Format8bppIndexed);
            result.SetResolution(image.Image.HorizontalResolution, image.Image.VerticalResolution);
            var resultBuffer = new ImageBuffer(result);
            PaletteBuffer paletteBuffer = new PaletteBuffer(colorCount);
            resultBuffer.UpdatePixelIndexes(indexedPixels(image, lookups, alphaThreshold, paletteBuffer));
            result.Palette = paletteBuffer.BuildPalette(result.Palette);
            return result;
        }
    }
}
