// <copyright file="PaletteQuantizer.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Quantizers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Encapsulates methods to create a quantized image based upon the given palette.
    /// <see href="http://msdn.microsoft.com/en-us/library/aa479306.aspx"/>
    /// </summary>
    public class PaletteQuantizer : Quantizer
    {
        /// <summary>
        /// A lookup table for colors
        /// </summary>
        private readonly Dictionary<int, byte> colorMap = new Dictionary<int, byte>();

        /// <summary>
        /// List of all colors in the palette
        /// </summary>
        private Bgra32[] colors;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteQuantizer"/> class.
        /// </summary>
        /// <param name="palette">
        /// The color palette. If none is given this will default to the web safe colors defined 
        /// in the CSS Color Module Level 4.
        /// </param>
        public PaletteQuantizer(Color[] palette = null)
            : base(true)
        {
            if (palette == null)
            {
                List<Bgra32> safe = ColorConstants.WebSafeColors.Select(c => (Bgra32)c).ToList();
                safe.Insert(0, Bgra32.Empty);
                this.colors = safe.ToArray();
            }
            else
            {
                this.colors = palette.Select(c => (Bgra32)c).ToArray();
            }
        }

        /// <inheritdoc/>
        public override QuantizedImage Quantize(ImageBase image, int maxColors)
        {
            Array.Resize(ref this.colors, maxColors.Clamp(1, 256));
            return base.Quantize(image, maxColors);
        }

        /// <inheritdoc/>
        protected override byte QuantizePixel(Bgra32 pixel)
        {
            byte colorIndex = 0;
            int colorHash = pixel.Bgra;

            // Check if the color is in the lookup table
            if (this.colorMap.ContainsKey(colorHash))
            {
                colorIndex = this.colorMap[colorHash];
            }
            else
            {
                // Not found - loop through the palette and find the nearest match.
                // Firstly check the alpha value - if less than the threshold, lookup the transparent color
                if (!(pixel.A > this.Threshold))
                {
                    // Transparent. Lookup the first color with an alpha value of 0
                    for (int index = 0; index < this.colors.Length; index++)
                    {
                        if (this.colors[index].A == 0)
                        {
                            colorIndex = (byte)index;
                            this.TransparentIndex = colorIndex;
                            break;
                        }
                    }
                }
                else
                {
                    // Not transparent...
                    int leastDistance = int.MaxValue;
                    int red = pixel.R;
                    int green = pixel.G;
                    int blue = pixel.B;

                    // Loop through the entire palette, looking for the closest color match
                    for (int index = 0; index < this.colors.Length; index++)
                    {
                        Bgra32 paletteColor = this.colors[index];

                        int redDistance = paletteColor.R - red;
                        int greenDistance = paletteColor.G - green;
                        int blueDistance = paletteColor.B - blue;

                        int distance = (redDistance * redDistance) +
                                       (greenDistance * greenDistance) +
                                       (blueDistance * blueDistance);

                        if (distance < leastDistance)
                        {
                            colorIndex = (byte)index;
                            leastDistance = distance;

                            // And if it's an exact match, exit the loop
                            if (distance == 0)
                            {
                                break;
                            }
                        }
                    }
                }

                // Now I have the color, pop it into the cache for next time
                this.colorMap[colorHash] = colorIndex;
            }

            return colorIndex;
        }

        /// <inheritdoc/>
        protected override List<Bgra32> GetPalette()
        {
            return this.colors.ToList();
        }
    }
}
