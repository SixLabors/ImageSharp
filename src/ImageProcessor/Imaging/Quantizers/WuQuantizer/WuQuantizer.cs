// --------------------------------------------------------------------------------------------------------------------
// <copyright file="WuQuantizer.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods to calculate the color palette of an image using
//   a Wu color quantizer <see href="http://www.ece.mcmaster.ca/~xwu/cq.c" />.
//   Adapted from <see href="https://github.com/drewnoakes" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Quantizers.WuQuantizer
{
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;

    using ImageProcessor.Imaging.Colors;

    /// <summary>
    /// Encapsulates methods to calculate the color palette of an image using 
    /// a Wu color quantizer <see href="http://www.ece.mcmaster.ca/~xwu/cq.c"/>.
    /// Adapted from <see href="https://github.com/drewnoakes"/>
    /// </summary>
    public class WuQuantizer : WuQuantizerBase
    {
        /// <summary>
        /// Quantizes the image contained within the <see cref="ImageBuffer"/> returning the result.
        /// </summary>
        /// <param name="imageBuffer">
        /// The <see cref="ImageBuffer"/> for storing and manipulating pixel information..
        /// </param>
        /// <param name="colorCount">
        /// The maximum number of colors apply to the image.
        /// </param>
        /// <param name="lookups">
        /// The array of <see cref="Color32"/> containing indexed versions of the images colors.
        /// </param>
        /// <param name="alphaThreshold">
        /// All colors with an alpha value less than this will be considered fully transparent.
        /// </param>
        /// <returns>
        /// The quantized <see cref="Bitmap"/>.
        /// </returns>
        internal override Bitmap GetQuantizedImage(ImageBuffer imageBuffer, int colorCount, Color32[] lookups, int alphaThreshold)
        {
            Bitmap result = new Bitmap(imageBuffer.Image.Width, imageBuffer.Image.Height, PixelFormat.Format8bppIndexed);
            result.SetResolution(imageBuffer.Image.HorizontalResolution, imageBuffer.Image.VerticalResolution);
            ImageBuffer resultBuffer = new ImageBuffer(result);
            PaletteColorHistory[] paletteHistogram = new PaletteColorHistory[colorCount + 1];
            resultBuffer.UpdatePixelIndexes(IndexedPixels(imageBuffer, lookups, alphaThreshold, paletteHistogram));
            result.Palette = BuildPalette(result.Palette, paletteHistogram);
            return result;
        }

        /// <summary>
        /// Builds a color palette from the given <see cref="PaletteColorHistory"/>.
        /// </summary>
        /// <param name="palette">
        /// The <see cref="ColorPalette"/> to fill.
        /// </param>
        /// <param name="paletteHistory">
        /// The <see cref="PaletteColorHistory"/> containing the sum of all pixel data.
        /// </param>
        /// <returns>
        /// The <see cref="ColorPalette"/>.
        /// </returns>
        private static ColorPalette BuildPalette(ColorPalette palette, PaletteColorHistory[] paletteHistory)
        {
            int length = paletteHistory.Length;
            for (int i = 0; i < length; i++)
            {
                palette.Entries[i] = paletteHistory[i].ToNormalizedColor();
            }

            return palette;
        }

        /// <summary>
        /// Gets an enumerable array of bytes representing each row of the image.
        /// </summary>
        /// <param name="image">
        /// The <see cref="ImageBuffer"/> for storing and manipulating pixel information.
        /// </param>
        /// <param name="lookups">
        /// The array of <see cref="Color32"/> containing indexed versions of the images colors.
        /// </param>
        /// <param name="alphaThreshold">
        /// The alpha threshold.
        /// </param>
        /// <param name="paletteHistogram">
        /// The palette histogram.
        /// </param>
        /// <returns>
        /// The enumerable list of <see cref="byte"/> representing each pixel.
        /// </returns>
        private static IEnumerable<byte[]> IndexedPixels(ImageBuffer image, Color32[] lookups, int alphaThreshold, PaletteColorHistory[] paletteHistogram)
        {
            byte[] lineIndexes = new byte[image.Image.Width];
            PaletteLookup lookup = new PaletteLookup(lookups);

            // Determine the correct fallback color.
            byte fallback = lookups.Length < AlphaMax ? AlphaMin : AlphaMax;
            foreach (Color32[] pixelLine in image.PixelLines)
            {
                int length = pixelLine.Length;
                for (int i = 0; i < length; i++)
                {
                    Color32 pixel = pixelLine[i];
                    byte bestMatch = fallback;
                    if (pixel.A > alphaThreshold)
                    {
                        bestMatch = lookup.GetPaletteIndex(pixel);
                        paletteHistogram[bestMatch].AddPixel(pixel);
                    }

                    lineIndexes[i] = bestMatch;
                }

                yield return lineIndexes;
            }
        }
    }
}