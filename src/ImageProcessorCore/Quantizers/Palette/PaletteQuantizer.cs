// <copyright file="PaletteQuantizer.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Quantizers
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Encapsulates methods to create a quantized image based upon the given palette.
    /// <see href="http://msdn.microsoft.com/en-us/library/aa479306.aspx"/>
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class PaletteQuantizer<TColor, TPacked> : Quantizer<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// A lookup table for colors
        /// </summary>
        private readonly ConcurrentDictionary<string, byte> colorMap = new ConcurrentDictionary<string, byte>();

        /// <summary>
        /// List of all colors in the palette
        /// </summary>
        private TColor[] colors;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteQuantizer{TColor, TPacked}"/> class.
        /// </summary>
        /// <param name="palette">
        /// The color palette. If none is given this will default to the web safe colors defined 
        /// in the CSS Color Module Level 4.
        /// </param>
        public PaletteQuantizer(TColor[] palette = null)
            : base(true)
        {
            if (palette == null)
            {
                Color[] constants = ColorConstants.WebSafeColors;
                List<TColor> safe = new List<TColor> { default(TColor) };
                foreach (Color c in constants)
                {
                    TColor packed = default(TColor);
                    packed.PackFromVector4(c.ToVector4());
                    safe.Add(packed);
                }

                this.colors = safe.ToArray();
            }
            else
            {
                this.colors = palette;
            }
        }

        /// <inheritdoc/>
        public override QuantizedImage<TColor, TPacked> Quantize(ImageBase<TColor, TPacked> image, int maxColors)
        {
            Array.Resize(ref this.colors, maxColors.Clamp(1, 256));
            return base.Quantize(image, maxColors);
        }

        /// <inheritdoc/>
        protected override byte QuantizePixel(TColor pixel)
        {
            byte colorIndex = 0;
            string colorHash = pixel.ToString();

            // Check if the color is in the lookup table
            if (this.colorMap.ContainsKey(colorHash))
            {
                colorIndex = this.colorMap[colorHash];
            }
            else
            {
                // Not found - loop through the palette and find the nearest match.
                // Firstly check the alpha value - if less than the threshold, lookup the transparent color
                Color color =new Color(pixel.ToVector4());
                if (!(color.A > this.Threshold))
                {
                    // Transparent. Lookup the first color with an alpha value of 0
                    for (int index = 0; index < this.colors.Length; index++)
                    {
                        if (new Color(this.colors[index].ToVector4()).A == 0)
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
                    int red = color.R;
                    int green = color.G;
                    int blue = color.B;

                    // Loop through the entire palette, looking for the closest color match
                    for (int index = 0; index < this.colors.Length; index++)
                    {
                        Color paletteColor = new Color(this.colors[index].ToVector4());
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
                this.colorMap.TryAdd(colorHash, colorIndex);
            }

            return colorIndex;
        }

        /// <inheritdoc/>
        protected override List<TColor> GetPalette()
        {
            return this.colors.ToList();
        }
    }
}
