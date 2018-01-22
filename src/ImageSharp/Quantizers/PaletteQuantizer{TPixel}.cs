// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Quantizers.Base;

namespace SixLabors.ImageSharp.Quantizers
{
    /// <summary>
    /// Encapsulates methods to create a quantized image based upon the given palette.
    /// <see href="http://msdn.microsoft.com/en-us/library/aa479306.aspx"/>
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public sealed class PaletteQuantizer<TPixel> : QuantizerBase<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// A lookup table for colors
        /// </summary>
        private readonly Dictionary<TPixel, byte> colorMap = new Dictionary<TPixel, byte>();

        /// <summary>
        /// List of all colors in the palette
        /// </summary>
        private TPixel[] colors;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteQuantizer{TPixel}"/> class.
        /// </summary>
        /// <param name="palette">
        /// The color palette. If none is given this will default to the web safe colors defined
        /// in the CSS Color Module Level 4.
        /// </param>
        public PaletteQuantizer(TPixel[] palette = null)
            : base(true)
        {
            if (palette == null)
            {
                Rgba32[] constants = ColorConstants.WebSafeColors;
                TPixel[] safe = new TPixel[constants.Length + 1];

                Span<byte> constantsBytes = constants.AsSpan().NonPortableCast<Rgba32, byte>();

                PixelOperations<TPixel>.Instance.PackFromRgba32Bytes(constantsBytes, safe, constants.Length);
                this.colors = safe;
            }
            else
            {
                this.colors = palette;
            }
        }

        /// <inheritdoc/>
        public override QuantizedImage<TPixel> Quantize(ImageFrame<TPixel> image, int maxColors)
        {
            Array.Resize(ref this.colors, maxColors.Clamp(1, 255));
            this.colorMap.Clear();
            return base.Quantize(image, maxColors);
        }

        /// <inheritdoc/>
        protected override void SecondPass(ImageFrame<TPixel> source, byte[] output, int width, int height)
        {
            // Load up the values for the first pixel. We can use these to speed up the second
            // pass of the algorithm by avoiding transforming rows of identical color.
            TPixel sourcePixel = source[0, 0];
            TPixel previousPixel = sourcePixel;
            byte pixelValue = this.QuantizePixel(sourcePixel);
            TPixel[] colorPalette = this.GetPalette();
            TPixel transformedPixel = colorPalette[pixelValue];

            for (int y = 0; y < height; y++)
            {
                Span<TPixel> row = source.GetPixelRowSpan(y);

                // And loop through each column
                for (int x = 0; x < width; x++)
                {
                    // Get the pixel.
                    sourcePixel = row[x];

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
                        this.DitherType.Dither(source, sourcePixel, transformedPixel, x, y, 0, 0, width, height, false);
                    }

                    output[(y * source.Width) + x] = pixelValue;
                }
            }
        }

        /// <inheritdoc/>
        protected override TPixel[] GetPalette()
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
        private byte QuantizePixel(TPixel pixel)
        {
            return this.GetClosestPixel(pixel, this.GetPalette(), this.colorMap);
        }
    }
}