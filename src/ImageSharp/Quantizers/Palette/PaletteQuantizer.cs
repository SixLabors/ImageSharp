// <copyright file="PaletteQuantizer.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Quantizers
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Encapsulates methods to create a quantized image based upon the given palette.
    /// <see href="http://msdn.microsoft.com/en-us/library/aa479306.aspx"/>
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class PaletteQuantizer<TColor, TPacked> : Quantizer<TColor, TPacked>
        where TColor : struct, IPackedPixel<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// The pixel buffer, used to reduce allocations.
        /// </summary>
        private readonly byte[] pixelBuffer = new byte[4];

        /// <summary>
        /// A lookup table for colors
        /// </summary>
        private readonly Dictionary<int, byte> colorMap = new Dictionary<int, byte>();

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
                TColor[] safe = new TColor[constants.Length + 1];

                for (int i = 0; i < constants.Length; i++)
                {
                    constants[i].ToBytes(this.pixelBuffer, 0, ComponentOrder.XYZW);
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
        public override QuantizedImage<TColor, TPacked> Quantize(ImageBase<TColor, TPacked> image, int maxColors)
        {
            Array.Resize(ref this.colors, maxColors.Clamp(1, 255));
            return base.Quantize(image, maxColors);
        }

        /// <inheritdoc/>
        protected override byte QuantizePixel(TColor pixel)
        {
            byte colorIndex = 0;
            int colorHash = pixel.GetHashCode();

            // Check if the color is in the lookup table
            if (this.colorMap.ContainsKey(colorHash))
            {
                colorIndex = this.colorMap[colorHash];
            }
            else
            {
                // Not found - loop through the palette and find the nearest match.
                pixel.ToBytes(this.pixelBuffer, 0, ComponentOrder.XYZW);
                int leastDistance = int.MaxValue;
                int red = this.pixelBuffer[0];
                int green = this.pixelBuffer[1];
                int blue = this.pixelBuffer[2];
                int alpha = this.pixelBuffer[3];

                for (int index = 0; index < this.colors.Length; index++)
                {
                    this.colors[index].ToBytes(this.pixelBuffer, 0, ComponentOrder.XYZW);
                    int redDistance = this.pixelBuffer[0] - red;
                    int greenDistance = this.pixelBuffer[1] - green;
                    int blueDistance = this.pixelBuffer[2] - blue;
                    int alphaDistance = this.pixelBuffer[3] - alpha;

                    int distance = (redDistance * redDistance) +
                                   (greenDistance * greenDistance) +
                                   (blueDistance * blueDistance) +
                                   (alphaDistance * alphaDistance);

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