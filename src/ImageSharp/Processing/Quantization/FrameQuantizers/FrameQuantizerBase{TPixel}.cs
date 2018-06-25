// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Dithering.ErrorDiffusion;

namespace SixLabors.ImageSharp.Processing.Quantization.FrameQuantizers
{
    /// <summary>
    /// The base class for all <see cref="IFrameQuantizer{TPixel}"/> implementations
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public abstract class FrameQuantizerBase<TPixel> : IFrameQuantizer<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Flag used to indicate whether a single pass or two passes are needed for quantization.
        /// </summary>
        private readonly bool singlePass;

        /// <summary>
        /// Initializes a new instance of the <see cref="FrameQuantizerBase{TPixel}"/> class.
        /// </summary>
        /// <param name="quantizer">The quantizer</param>
        /// <param name="singlePass">
        /// If true, the quantization process only needs to loop through the source pixels once
        /// </param>
        /// <remarks>
        /// If you construct this class with a true value for singlePass, then the code will, when quantizing your image,
        /// only call the <see cref="FirstPass(ImageFrame{TPixel}, int, int)"/> methods.
        /// If two passes are required, the code will also call <see cref="SecondPass(ImageFrame{TPixel}, Span{byte}, int, int)"/>
        /// and then 'QuantizeImage'.
        /// </remarks>
        protected FrameQuantizerBase(IQuantizer quantizer, bool singlePass)
        {
            Guard.NotNull(quantizer, nameof(quantizer));

            this.Diffuser = quantizer.Diffuser;
            this.Dither = this.Diffuser != null;
            this.singlePass = singlePass;
        }

        /// <inheritdoc />
        public bool Dither { get; }

        /// <inheritdoc />
        public IErrorDiffuser Diffuser { get; }

        /// <inheritdoc/>
        public virtual QuantizedFrame<TPixel> QuantizeFrame(ImageFrame<TPixel> image)
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

            // Collect the palette. Required before the second pass runs.
            var quantizedFrame = new QuantizedFrame<TPixel>(image.MemoryAllocator, width, height, this.GetPalette());

            if (this.Dither)
            {
                // We clone the image as we don't want to alter the original.
                using (ImageFrame<TPixel> clone = image.Clone())
                {
                    this.SecondPass(clone, quantizedFrame.GetPixelSpan(), width, height);
                }
            }
            else
            {
                this.SecondPass(image, quantizedFrame.GetPixelSpan(), width, height);
            }

            return quantizedFrame;
        }

        /// <summary>
        /// Execute the first pass through the pixels in the image
        /// </summary>
        /// <param name="source">The source data</param>
        /// <param name="width">The width in pixels of the image.</param>
        /// <param name="height">The height in pixels of the image.</param>
        protected virtual void FirstPass(ImageFrame<TPixel> source, int width, int height)
        {
        }

        /// <summary>
        /// Execute a second pass through the image
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="output">The output pixel array</param>
        /// <param name="width">The width in pixels of the image</param>
        /// <param name="height">The height in pixels of the image</param>
        protected abstract void SecondPass(ImageFrame<TPixel> source, Span<byte> output, int width, int height);

        /// <summary>
        /// Retrieve the palette for the quantized image.
        /// <remarks>Can be called more than once so make sure calls are cached.</remarks>
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
            if (cache.TryGetValue(pixel, out byte value))
            {
                return value;
            }

            return this.GetClosestPixelSlow(pixel, colorPalette, cache);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private byte GetClosestPixelSlow(TPixel pixel, TPixel[] colorPalette, Dictionary<TPixel, byte> cache)
        {
            // Loop through the palette and find the nearest match.
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