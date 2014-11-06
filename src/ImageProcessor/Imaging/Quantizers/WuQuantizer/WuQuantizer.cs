// --------------------------------------------------------------------------------------------------------------------
// <copyright file="WuQuantizer.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Quantizers.WuQuantizer
{
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;

    /// <summary>
    /// Encapsulates methods to calculate the color palette of an image using 
    /// a Wu color quantizer <see href="http://www.ece.mcmaster.ca/~xwu/cq.c"/>.
    /// Adapted from <see href="https://github.com/drewnoakes"/>
    /// </summary>
    public class WuQuantizer : WuQuantizerBase, IWuQuantizer
    {
        /// <summary>
        /// The get quantized image.
        /// </summary>
        /// <param name="image">
        /// The image.
        /// </param>
        /// <param name="colorCount">
        /// The color count.
        /// </param>
        /// <param name="lookups">
        /// The lookups.
        /// </param>
        /// <param name="alphaThreshold">
        /// The alpha threshold.
        /// </param>
        /// <returns>
        /// The <see cref="Image"/>.
        /// </returns>
        internal override Image GetQuantizedImage(ImageBuffer image, int colorCount, Pixel[] lookups, int alphaThreshold)
        {
            Bitmap result = new Bitmap(image.Image.Width, image.Image.Height, PixelFormat.Format8bppIndexed);
            result.SetResolution(image.Image.HorizontalResolution, image.Image.VerticalResolution);
            ImageBuffer resultBuffer = new ImageBuffer(result);
            PaletteColorHistory[] paletteHistogram = new PaletteColorHistory[colorCount + 1];
            resultBuffer.UpdatePixelIndexes(IndexedPixels(image, lookups, alphaThreshold, paletteHistogram));
            result.Palette = BuildPalette(result.Palette, paletteHistogram);
            return result;
        }

        /// <summary>
        /// The build palette.
        /// </summary>
        /// <param name="palette">
        /// The palette.
        /// </param>
        /// <param name="paletteHistogram">
        /// The palette histogram.
        /// </param>
        /// <returns>
        /// The <see cref="ColorPalette"/>.
        /// </returns>
        private static ColorPalette BuildPalette(ColorPalette palette, PaletteColorHistory[] paletteHistogram)
        {
            for (int paletteColorIndex = 0; paletteColorIndex < paletteHistogram.Length; paletteColorIndex++)
            {
                palette.Entries[paletteColorIndex] = paletteHistogram[paletteColorIndex].ToNormalizedColor();
            }

            return palette;
        }

        /// <summary>
        /// The indexed pixels.
        /// </summary>
        /// <param name="image">
        /// The image.
        /// </param>
        /// <param name="lookups">
        /// The lookups.
        /// </param>
        /// <param name="alphaThreshold">
        /// The alpha threshold.
        /// </param>
        /// <param name="paletteHistogram">
        /// The palette histogram.
        /// </param>
        /// <returns>
        /// The <see cref="IEnumerable"/>.
        /// </returns>
        private static IEnumerable<byte[]> IndexedPixels(ImageBuffer image, Pixel[] lookups, int alphaThreshold, PaletteColorHistory[] paletteHistogram)
        {
            byte[] lineIndexes = new byte[image.Image.Width];
            PaletteLookup lookup = new PaletteLookup(lookups);

            foreach (Pixel[] pixelLine in image.PixelLines)
            {
                for (int pixelIndex = 0; pixelIndex < pixelLine.Length; pixelIndex++)
                {
                    Pixel pixel = pixelLine[pixelIndex];
                    byte bestMatch = 0;
                    if (pixel.Alpha > alphaThreshold)
                    {
                        bestMatch = lookup.GetPaletteIndex(pixel);
                        paletteHistogram[bestMatch].AddPixel(pixel);
                    }

                    lineIndexes[pixelIndex] = bestMatch;
                }

                yield return lineIndexes;
            }
        }
    }
}