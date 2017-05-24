// <copyright file="Quantizer{TPixel}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Quantizers
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using ImageSharp.Dithering;
    using ImageSharp.PixelFormats;

    /// <summary>
    /// Encapsulates methods to calculate the color palette of an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public abstract class Quantizer<TPixel> : IQuantizer<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Flag used to indicate whether a single pass or two passes are needed for quantization.
        /// </summary>
        private readonly bool singlePass;

        /// <summary>
        /// Initializes a new instance of the <see cref="Quantizer{TPixel}"/> class.
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

        /// <inheritdoc />
        public bool Dither { get; set; } = true;

        /// <inheritdoc />
        public IErrorDiffuser DitherType { get; set; } = new SierraLite();

        /// <inheritdoc/>
        public virtual QuantizedImage<TPixel> Quantize(ImageBase<TPixel> image, int maxColors)
        {
            Guard.NotNull(image, nameof(image));

            // Get the size of the source image
            int height = image.Height;
            int width = image.Width;
            byte[] quantizedPixels = new byte[width * height];

            // Call the FirstPass function if not a single pass algorithm.
            // For something like an Octree quantizer, this will run through
            // all image pixels, build a data structure, and create a palette.
            if (!this.singlePass)
            {
                this.FirstPass(image, width, height);
            }

            // Collect the palette. Required before the second pass runs.
            TPixel[] colorPalette = this.GetPalette();

            if (this.Dither)
            {
                // We clone the image as we don't want to alter the original.
                using (var clone = new Image<TPixel>(image))
                {
                    this.SecondPass(clone, quantizedPixels, width, height);
                }
            }
            else
            {
                this.SecondPass(image, quantizedPixels, width, height);
            }

            return new QuantizedImage<TPixel>(width, height, colorPalette, quantizedPixels);
        }

        /// <summary>
        /// Execute the first pass through the pixels in the image
        /// </summary>
        /// <param name="source">The source data</param>
        /// <param name="width">The width in pixels of the image.</param>
        /// <param name="height">The height in pixels of the image.</param>
        protected virtual void FirstPass(ImageBase<TPixel> source, int width, int height)
        {
            // Loop through each row
            for (int y = 0; y < height; y++)
            {
                Span<TPixel> row = source.GetRowSpan(y);

                // And loop through each column
                for (int x = 0; x < width; x++)
                {
                    // Now I have the pixel, call the FirstPassQuantize function...
                    this.InitialQuantizePixel(row[x]);
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
        protected abstract void SecondPass(ImageBase<TPixel> source, byte[] output, int width, int height);

        /// <summary>
        /// Override this to process the pixel in the first pass of the algorithm
        /// </summary>
        /// <param name="pixel">The pixel to quantize</param>
        /// <remarks>
        /// This function need only be overridden if your quantize algorithm needs two passes,
        /// such as an Octree quantizer.
        /// </remarks>
        protected virtual void InitialQuantizePixel(TPixel pixel)
        {
        }

        /// <summary>
        /// Retrieve the palette for the quantized image. Can be called more than once so make sure calls are cached.
        /// </summary>
        /// <returns>
        /// <see cref="T:TPixel[]"/>
        /// </returns>
        protected abstract TPixel[] GetPalette();

        /// <summary>
        /// Returns the closest color from the palette to the given color by calculating the Euclidean distance.
        /// </summary>
        /// <param name="pixel">The color.</param>
        /// <param name="colorPalette">The color palette.</param>
        /// <param name="cache">The cache to store the result in.</param>
        /// <returns>The <see cref="byte"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected byte GetClosestPixel(TPixel pixel, TPixel[] colorPalette, Dictionary<TPixel, byte> cache)
        {
            // Check if the color is in the lookup table
            if (cache.ContainsKey(pixel))
            {
                return cache[pixel];
            }

            // Not found - loop through the palette and find the nearest match.
            byte colorIndex = 0;
            float leastDistance = int.MaxValue;
            var vector = pixel.ToVector4();

            for (int index = 0; index < colorPalette.Length; index++)
            {
                float distance = Vector4.Distance(vector, colorPalette[index].ToVector4());

                // Greater... Move on.
                if (!(distance < leastDistance))
                {
                    continue;
                }

                colorIndex = (byte)index;
                leastDistance = distance;

                // And if it's an exact match, exit the loop
                if (MathF.Abs(distance) < Constants.Epsilon)
                {
                    break;
                }
            }

            // Now I have the index, pop it into the cache for next time
            cache.Add(pixel, colorIndex);

            return colorIndex;
        }
    }
}