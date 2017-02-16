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
    public sealed class PaletteQuantizer<TColor> : Quantizer<TColor>
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
            return this.GetClosestColor(pixel, this.colors, this.colorMap);
        }

        /// <inheritdoc/>
        protected override TColor[] GetPalette()
        {
            return this.colors;
        }
    }
}