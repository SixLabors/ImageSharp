// <copyright file="PaletteQuantizer.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Quantizers
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Encapsulates methods to create a quantized image based upon the given palette.
    /// <see href="http://msdn.microsoft.com/en-us/library/aa479306.aspx"/>
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public sealed class PaletteQuantizer<TColor> : Quantizer<TColor>
        where TColor : struct, IPixel<TColor>
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
                Rgba32[] constants = ColorConstants.WebSafeColors;
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
        protected override void SecondPass(PixelAccessor<TColor> source, byte[] output, int width, int height)
        {
            // Load up the values for the first pixel. We can use these to speed up the second
            // pass of the algorithm by avoiding transforming rows of identical color.
            TColor sourcePixel = source[0, 0];
            TColor previousPixel = sourcePixel;
            byte pixelValue = this.QuantizePixel(sourcePixel);
            TColor[] colorPalette = this.GetPalette();
            TColor transformedPixel = colorPalette[pixelValue];

            for (int y = 0; y < height; y++)
            {
                // And loop through each column
                for (int x = 0; x < width; x++)
                {
                    // Get the pixel.
                    sourcePixel = source[x, y];

                    // Check if this is the same as the last pixel. If so use that value
                    // rather than calculating it again. This is an inexpensive optimization.
                    if (!previousPixel.Equals(sourcePixel))
                    {
                        // Quantize the pixel
                        pixelValue = this.QuantizePixel(sourcePixel);

                        // And setup the previous pointer
                        previousPixel = sourcePixel;

                        if (this.Dither)
                        {
                            transformedPixel = colorPalette[pixelValue];
                        }
                    }

                    if (this.Dither)
                    {
                        // Apply the dithering matrix. We have to reapply the value now as the original has changed.
                        this.DitherType.Dither(source, sourcePixel, transformedPixel, x, y, width, height, false);
                    }

                    output[(y * source.Width) + x] = pixelValue;
                }
            }
        }

        /// <inheritdoc/>
        protected override TColor[] GetPalette()
        {
            return this.colors;
        }

        /// <summary>
        /// Process the pixel in the second pass of the algorithm
        /// </summary>
        /// <param name="pixel">The pixel to quantize</param>
        /// <returns>
        /// The quantized value
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte QuantizePixel(TColor pixel)
        {
            return this.GetClosestColor(pixel, this.GetPalette(), this.colorMap);
        }
    }
}