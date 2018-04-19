// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Quantization.FrameQuantizers
{
    /// <summary>
    /// Encapsulates methods to create a quantized image based upon the given palette.
    /// <see href="http://msdn.microsoft.com/en-us/library/aa479306.aspx"/>
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal sealed class PaletteFrameQuantizer<TPixel> : FrameQuantizerBase<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// A lookup table for colors
        /// </summary>
        private readonly Dictionary<TPixel, byte> colorMap = new Dictionary<TPixel, byte>();

        /// <summary>
        /// List of all colors in the palette
        /// </summary>
        private readonly TPixel[] colors;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteFrameQuantizer{TPixel}"/> class.
        /// </summary>
        /// <param name="quantizer">The palette quantizer</param>
        public PaletteFrameQuantizer(PaletteQuantizer quantizer)
            : base(quantizer, true)
        {
            this.colors = quantizer.GetPalette<TPixel>();
        }

        /// <inheritdoc/>
        protected override void SecondPass(ImageFrame<TPixel> source, byte[] output, int width, int height)
        {
            // Load up the values for the first pixel. We can use these to speed up the second
            // pass of the algorithm by avoiding transforming rows of identical color.
            TPixel sourcePixel = source[0, 0];
            TPixel previousPixel = sourcePixel;
            byte pixelValue = this.QuantizePixel(sourcePixel);
            ref TPixel colorPaletteRef = ref MemoryMarshal.GetReference(this.GetPalette().AsSpan());
            TPixel transformedPixel = Unsafe.Add(ref colorPaletteRef, pixelValue);

            for (int y = 0; y < height; y++)
            {
                ref TPixel rowRef = ref MemoryMarshal.GetReference(source.GetPixelRowSpan(y));

                // And loop through each column
                for (int x = 0; x < width; x++)
                {
                    // Get the pixel.
                    sourcePixel = Unsafe.Add(ref rowRef, x);

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
                            transformedPixel = Unsafe.Add(ref colorPaletteRef, pixelValue);
                        }
                    }

                    if (this.Dither)
                    {
                        // Apply the dithering matrix. We have to reapply the value now as the original has changed.
                        this.Diffuser.Dither(source, sourcePixel, transformedPixel, x, y, 0, 0, width, height);
                    }

                    output[(y * source.Width) + x] = pixelValue;
                }
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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