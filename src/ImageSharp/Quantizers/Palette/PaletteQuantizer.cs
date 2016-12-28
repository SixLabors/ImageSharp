// <copyright file="PaletteQuantizer.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Quantizers
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>
    /// Encapsulates methods to create a quantized image based upon the given palette.
    /// <see href="http://msdn.microsoft.com/en-us/library/aa479306.aspx"/>
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public class PaletteQuantizer<TColor> : Quantizer<TColor>
    where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// The pixel buffer, used to reduce allocations.
        /// </summary>
        private readonly byte[] pixelBuffer = new byte[4];

        /// <summary>
        /// A lookup table for colors
        /// </summary>
        private readonly Dictionary<TColor, byte> colorMap = new Dictionary<TColor, byte>();

        /// <summary>
        /// List of all colors in the palette
        /// </summary>
        private TColor[] colors;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteQuantizer{TColor}"/> class.
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
                TColor[] safe = new TColor[constants.Length + 1];

                for (int i = 0; i < constants.Length; i++)
                {
                    constants[i].ToXyzwBytes(this.pixelBuffer, 0);
                    TColor packed = default(TColor);
                    packed.PackFromBytes(this.pixelBuffer[0], this.pixelBuffer[1], this.pixelBuffer[2], this.pixelBuffer[3]);
                    safe[i] = packed;
                }

                this.colors = safe;
            }
            else
            {
                this.colors = palette;
            }
        }

        /// <inheritdoc/>
        public override QuantizedImage<TColor> Quantize(ImageBase<TColor> image, int maxColors)
        {
            Array.Resize(ref this.colors, maxColors.Clamp(1, 255));
            return base.Quantize(image, maxColors);
        }

        /// <inheritdoc/>
        protected override byte QuantizePixel(TColor pixel)
        {
            byte colorIndex = 0;
            TColor colorHash = pixel;

            // Check if the color is in the lookup table
            if (this.colorMap.ContainsKey(colorHash))
            {
                colorIndex = this.colorMap[colorHash];
            }
            else
            {
                // Not found - loop through the palette and find the nearest match.
                float leastDistance = int.MaxValue;
                Vector4 vector = pixel.ToVector4();

                for (int index = 0; index < this.colors.Length; index++)
                {
                    float distance = Vector4.Distance(vector, this.colors[index].ToVector4());

                    if (distance < leastDistance)
                    {
                        colorIndex = (byte)index;
                        leastDistance = distance;

                        // And if it's an exact match, exit the loop
                        if (Math.Abs(distance) < .0001F)
                        {
                            break;
                        }
                    }
                }

                // Now I have the color, pop it into the cache for next time
                this.colorMap.Add(colorHash, colorIndex);
            }

            return colorIndex;
        }

        /// <inheritdoc/>
        protected override TColor[] GetPalette()
        {
            return this.colors;
        }
    }
}