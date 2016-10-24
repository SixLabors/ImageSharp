// <copyright file="Quantizer.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Quantizers
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Encapsulates methods to calculate the color palette of an image.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public abstract class Quantizer<TColor, TPacked> : IQuantizer<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// Flag used to indicate whether a single pass or two passes are needed for quantization.
        /// </summary>
        private readonly bool singlePass;

        /// <summary>
        /// Initializes a new instance of the <see cref="Quantizer{TColor, TPacked}"/> class.
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

        /// <inheritdoc/>
        public virtual QuantizedImage<TColor, TPacked> Quantize(ImageBase<TColor, TPacked> image, int maxColors)
        {
            Guard.NotNull(image, nameof(image));

            // Get the size of the source image
            int height = image.Height;
            int width = image.Width;
            byte[] quantizedPixels = new byte[width * height];
            List<TColor> palette;

            using (PixelAccessor<TColor, TPacked> pixels = image.Lock())
            {
                // Call the FirstPass function if not a single pass algorithm.
                // For something like an Octree quantizer, this will run through
                // all image pixels, build a data structure, and create a palette.
                if (!this.singlePass)
                {
                    this.FirstPass(pixels, width, height);
                }

                // Get the palette
                palette = this.GetPalette();

                this.SecondPass(pixels, quantizedPixels, width, height);
            }

            return new QuantizedImage<TColor, TPacked>(width, height, palette.ToArray(), quantizedPixels);
        }

        /// <summary>
        /// Execute the first pass through the pixels in the image
        /// </summary>
        /// <param name="source">The source data</param>
        /// <param name="width">The width in pixels of the image.</param>
        /// <param name="height">The height in pixels of the image.</param>
        protected virtual void FirstPass(PixelAccessor<TColor, TPacked> source, int width, int height)
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
        protected virtual void SecondPass(PixelAccessor<TColor, TPacked> source, byte[] output, int width, int height)
        {
            Parallel.For(
                0,
                source.Height,
                Bootstrapper.Instance.ParallelOptions,
                y =>
                    {
                        for (int x = 0; x < source.Width; x++)
                        {
                            TColor sourcePixel = source[x, y];
                            output[(y * source.Width) + x] = this.QuantizePixel(sourcePixel);
                        }
                    });
        }

        /// <summary>
        /// Override this to process the pixel in the first pass of the algorithm
        /// </summary>
        /// <param name="pixel">The pixel to quantize</param>
        /// <remarks>
        /// This function need only be overridden if your quantize algorithm needs two passes,
        /// such as an Octree quantizer.
        /// </remarks>
        protected virtual void InitialQuantizePixel(TColor pixel)
        {
        }

        /// <summary>
        /// Override this to process the pixel in the second pass of the algorithm
        /// </summary>
        /// <param name="pixel">The pixel to quantize</param>
        /// <returns>
        /// The quantized value
        /// </returns>
        protected abstract byte QuantizePixel(TColor pixel);

        /// <summary>
        /// Retrieve the palette for the quantized image
        /// </summary>
        /// <returns>
        /// The new color palette
        /// </returns>
        protected abstract List<TColor> GetPalette();
    }
}