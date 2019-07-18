// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Dithering;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// Encapsulates methods to create a quantized image based upon the given palette.
    /// <see href="http://msdn.microsoft.com/en-us/library/aa479306.aspx"/>
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal sealed class PaletteFrameQuantizer<TPixel> : FrameQuantizer<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The reduced image palette.
        /// </summary>
        private readonly ReadOnlyMemory<TPixel> palette;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteFrameQuantizer{TPixel}"/> class.
        /// </summary>
        /// <param name="diffuser">The palette quantizer.</param>
        /// <param name="colors">An array of all colors in the palette.</param>
        public PaletteFrameQuantizer(IErrorDiffuser diffuser, ReadOnlyMemory<TPixel> colors)
            : base(diffuser, true) => this.palette = colors;

        /// <inheritdoc/>
        protected override void SecondPass(
            ImageFrame<TPixel> source,
            Span<byte> output,
            ReadOnlySpan<TPixel> palette,
            int width,
            int height)
        {
            // Load up the values for the first pixel. We can use these to speed up the second
            // pass of the algorithm by avoiding transforming rows of identical color.
            TPixel sourcePixel = source[0, 0];
            TPixel previousPixel = sourcePixel;
            byte pixelValue = this.QuantizePixel(ref sourcePixel);
            ref TPixel paletteRef = ref MemoryMarshal.GetReference(palette);
            TPixel transformedPixel = Unsafe.Add(ref paletteRef, pixelValue);

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
                        pixelValue = this.QuantizePixel(ref sourcePixel);

                        // And setup the previous pointer
                        previousPixel = sourcePixel;

                        if (this.Dither)
                        {
                            transformedPixel = Unsafe.Add(ref paletteRef, pixelValue);
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
        protected override ReadOnlyMemory<TPixel> GetPalette() => this.palette;

        /// <summary>
        /// Process the pixel in the second pass of the algorithm
        /// </summary>
        /// <param name="pixel">The pixel to quantize</param>
        /// <returns>
        /// The quantized value
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte QuantizePixel(ref TPixel pixel) => this.GetClosestPixel(ref pixel);
    }
}