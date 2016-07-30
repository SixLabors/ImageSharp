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
    /// <typeparam name="T">The pixel format.</typeparam>
    /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
    public class PaletteQuantizer<T, TP> : Quantizer<T, TP>
        where T : IPackedVector<TP>
        where TP : struct
    {
        /// <summary>
        /// A lookup table for colors
        /// </summary>
        private readonly ConcurrentDictionary<string, byte> colorMap = new ConcurrentDictionary<string, byte>();

        /// <summary>
        /// List of all colors in the palette
        /// </summary>
        private T[] colors;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteQuantizer{T,TP}"/> class.
        /// </summary>
        /// <param name="palette">
        /// The color palette. If none is given this will default to the web safe colors defined 
        /// in the CSS Color Module Level 4.
        /// </param>
        public PaletteQuantizer(T[] palette = null)
            : base(true)
        {
            if (palette == null)
            {
                Color[] constants = ColorConstants.WebSafeColors;
                List<T> safe = new List<T> { default(T) };
                foreach (Color c in constants)
                {
                    T packed = default(T);
                    packed.PackVector(c.ToVector4());
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
        public override QuantizedImage<T, TP> Quantize(ImageBase<T, TP> image, int maxColors)
        {
            Array.Resize(ref this.colors, maxColors.Clamp(1, 256));
            return base.Quantize(image, maxColors);
        }

        /// <inheritdoc/>
        protected override byte QuantizePixel(T pixel)
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
                byte[] bytes = pixel.ToBytes();
                if (!(bytes[3] > this.Threshold))
                {
                    // Transparent. Lookup the first color with an alpha value of 0
                    for (int index = 0; index < this.colors.Length; index++)
                    {
                        if (this.colors[index].ToBytes()[3] == 0)
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
                    int red = bytes[0];
                    int green = bytes[1];
                    int blue = bytes[2];

                    // Loop through the entire palette, looking for the closest color match
                    for (int index = 0; index < this.colors.Length; index++)
                    {
                        byte[] paletteColor = this.colors[index].ToBytes();
                        int redDistance = paletteColor[0] - red;
                        int greenDistance = paletteColor[1] - green;
                        int blueDistance = paletteColor[2] - blue;

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
        protected override List<T> GetPalette()
        {
            return this.colors.ToList();
        }
    }
}
