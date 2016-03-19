// <copyright file="Quantizer.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Quantizers
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Encapsulates methods to calculate the color palette of an image.
    /// </summary>
    public abstract class Quantizer : IQuantizer
    {
        /// <summary>
        /// Flag used to indicate whether a single pass or two passes are needed for quantization.
        /// </summary>
        private readonly bool singlePass;

        /// <summary>
        /// Initializes a new instance of the <see cref="Quantizer"/> class.
        /// </summary>
        /// <param name="singlePass">
        /// If true, the quantization only needs to loop through the source pixels once
        /// </param>
        /// <remarks>
        /// If you construct this class with a true value for singlePass, then the code will, when quantizing your image,
        /// only call the 'QuantizeImage' function. If two passes are required, the code will call 'InitialQuantizeImage'
        /// and then 'QuantizeImage'.
        /// </remarks>
        protected Quantizer(bool singlePass)
        {
            this.singlePass = singlePass;
        }

        /// <summary>
        /// Gets or sets the transparency index.
        /// </summary>
        public int TransparentIndex { get; protected set; } = -1;

        /// <inheritdoc/>
        public virtual QuantizedImage Quantize(ImageBase image, int maxColors)
        {
            Guard.NotNull(image, nameof(image));

            // Get the size of the source image
            int height = image.Height;
            int width = image.Width;

            // Call the FirstPass function if not a single pass algorithm.
            // For something like an Octree quantizer, this will run through
            // all image pixels, build a data structure, and create a palette.
            if (!this.singlePass)
            {
                this.FirstPass(image, width, height);
            }

            byte[] quantizedPixels = new byte[width * height];

            // Get the palette
            List<Bgra32> palette = this.GetPalette();

            this.SecondPass(image, quantizedPixels, width, height);

            return new QuantizedImage(width, height, palette.ToArray(), quantizedPixels, this.TransparentIndex);
        }

        /// <summary>
        /// Execute the first pass through the pixels in the image
        /// </summary>
        /// <param name="source">The source data</param>
        /// <param name="width">The width in pixels of the image.</param>
        /// <param name="height">The height in pixels of the image.</param>
        protected virtual void FirstPass(ImageBase source, int width, int height)
        {
            // Loop through each row
            for (int y = 0; y < height; y++)
            {
                // And loop through each column
                for (int x = 0; x < width; x++)
                {
                    // Now I have the pixel, call the FirstPassQuantize function...
                    this.InitialQuantizePixel(source[x, y]);
                }
            }
        }

        /// <summary>
        /// Execute a second pass through the bitmap
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="output">The output pixel array</param>
        /// <param name="width">The width in pixels of the image</param>
        /// <param name="height">The height in pixels of the image</param>
        protected virtual void SecondPass(ImageBase source, byte[] output, int width, int height)
        {
            int i = 0;

            // Convert the first pixel, so that I have values going into the loop.
            // Implicit cast here from Color.
            Bgra32 previousPixel = source[0, 0];
            byte pixelValue = this.QuantizePixel(previousPixel);

            output[0] = pixelValue;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Bgra32 sourcePixel = source[x, y];

                    // Check if this is the same as the last pixel. If so use that value
                    // rather than calculating it again. This is an inexpensive optimization.
                    if (sourcePixel != previousPixel)
                    {
                        // Quantize the pixel
                        pixelValue = this.QuantizePixel(sourcePixel);

                        // And setup the previous pointer
                        previousPixel = sourcePixel;
                    }

                    output[i++] = pixelValue;
                }
            }
        }

        /// <summary>
        /// Override this to process the pixel in the first pass of the algorithm
        /// </summary>
        /// <param name="pixel">The pixel to quantize</param>
        /// <remarks>
        /// This function need only be overridden if your quantize algorithm needs two passes,
        /// such as an Octree quantizer.
        /// </remarks>
        protected virtual void InitialQuantizePixel(Bgra32 pixel)
        {
        }

        /// <summary>
        /// Override this to process the pixel in the second pass of the algorithm
        /// </summary>
        /// <param name="pixel">The pixel to quantize</param>
        /// <returns>
        /// The quantized value
        /// </returns>
        protected abstract byte QuantizePixel(Bgra32 pixel);

        /// <summary>
        /// Retrieve the palette for the quantized image
        /// </summary>
        /// <returns>
        /// The new color palette
        /// </returns>
        protected abstract List<Bgra32> GetPalette();
    }
}