// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Dithering;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// The base class for all <see cref="IFrameQuantizer{TPixel}"/> implementations
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public abstract class FrameQuantizer<TPixel> : IFrameQuantizer<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// A lookup table for colors
        /// </summary>
        private readonly Dictionary<TPixel, byte> distanceCache = new Dictionary<TPixel, byte>();

        /// <summary>
        /// Flag used to indicate whether a single pass or two passes are needed for quantization.
        /// </summary>
        private readonly bool singlePass;

        /// <summary>
        /// The vector representation of the image palette.
        /// </summary>
        private Vector4[] paletteVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="FrameQuantizer{TPixel}"/> class.
        /// </summary>
        /// <param name="quantizer">The quantizer</param>
        /// <param name="singlePass">
        /// If true, the quantization process only needs to loop through the source pixels once
        /// </param>
        /// <remarks>
        /// If you construct this class with a <value>true</value> for <paramref name="singlePass"/>, then the code will
        /// only call the <see cref="SecondPass(ImageFrame{TPixel}, Span{byte}, ReadOnlySpan{TPixel},  int, int)"/> method.
        /// If two passes are required, the code will also call <see cref="FirstPass(ImageFrame{TPixel}, int, int)"/>.
        /// </remarks>
        protected FrameQuantizer(IQuantizer quantizer, bool singlePass)
        {
            Guard.NotNull(quantizer, nameof(quantizer));

            this.Diffuser = quantizer.Diffuser;
            this.Dither = this.Diffuser != null;
            this.singlePass = singlePass;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FrameQuantizer{TPixel}"/> class.
        /// </summary>
        /// <param name="diffuser">The diffuser</param>
        /// <param name="singlePass">
        /// If true, the quantization process only needs to loop through the source pixels once
        /// </param>
        /// <remarks>
        /// If you construct this class with a <value>true</value> for <paramref name="singlePass"/>, then the code will
        /// only call the <see cref="SecondPass(ImageFrame{TPixel}, Span{byte}, ReadOnlySpan{TPixel},  int, int)"/> method.
        /// If two passes are required, the code will also call <see cref="FirstPass(ImageFrame{TPixel}, int, int)"/>.
        /// </remarks>
        protected FrameQuantizer(IErrorDiffuser diffuser, bool singlePass)
        {
            this.Diffuser = diffuser;
            this.Dither = this.Diffuser != null;
            this.singlePass = singlePass;
        }

        /// <inheritdoc />
        public IErrorDiffuser Diffuser { get; }

        /// <inheritdoc />
        public bool Dither { get; }

        /// <inheritdoc/>
        public virtual void Dispose()
        {
        }

        /// <inheritdoc/>
        public IQuantizedFrame<TPixel> QuantizeFrame(ImageFrame<TPixel> image)
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
            ReadOnlyMemory<TPixel> palette = this.GetPalette();
            this.paletteVector = new Vector4[palette.Length];
            PixelOperations<TPixel>.Instance.ToVector4(
                image.Configuration,
                palette.Span,
                (Span<Vector4>)this.paletteVector,
                PixelConversionModifiers.Scale);

            var quantizedFrame = new QuantizedFrame<TPixel>(image.MemoryAllocator, width, height, palette);

            Span<byte> pixelSpan = quantizedFrame.GetWritablePixelSpan();
            if (this.Dither)
            {
                // We clone the image as we don't want to alter the original via dithering.
                using (ImageFrame<TPixel> clone = image.Clone())
                {
                    this.SecondPass(clone, pixelSpan, palette.Span, width, height);
                }
            }
            else
            {
                this.SecondPass(image, pixelSpan, palette.Span, width, height);
            }

            return quantizedFrame;
        }

        /// <summary>
        /// Execute the first pass through the pixels in the image to create the palette.
        /// </summary>
        /// <param name="source">The source data.</param>
        /// <param name="width">The width in pixels of the image.</param>
        /// <param name="height">The height in pixels of the image.</param>
        protected virtual void FirstPass(ImageFrame<TPixel> source, int width, int height)
        {
        }

        /// <summary>
        /// Returns the closest color from the palette to the given color by calculating the
        /// Euclidean distance in the Rgba colorspace.
        /// </summary>
        /// <param name="pixel">The color.</param>
        /// <returns>The <see cref="int"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected byte GetClosestPixel(ref TPixel pixel)
        {
            // Check if the color is in the lookup table
            if (this.distanceCache.TryGetValue(pixel, out byte value))
            {
                return value;
            }

            return this.GetClosestPixelSlow(ref pixel);
        }

        /// <summary>
        /// Retrieve the palette for the quantized image.
        /// </summary>
        /// <returns>
        /// <see cref="T:TPixel[]"/>
        /// </returns>
        protected abstract ReadOnlyMemory<TPixel> GetPalette();

        /// <summary>
        /// Returns the index of the first instance of the transparent color in the palette.
        /// </summary>
        /// <returns>The <see cref="int"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected byte GetTransparentIndex()
        {
            // Transparent pixels are much more likely to be found at the end of a palette.
            int paletteVectorLengthMinus1 = this.paletteVector.Length - 1;

            int index = paletteVectorLengthMinus1;
            for (int i = paletteVectorLengthMinus1; i >= 0; i--)
            {
                ref Vector4 candidate = ref this.paletteVector[i];
                if (candidate.Equals(default))
                {
                    index = i;
                }
            }

            return (byte)index;
        }

        /// <summary>
        /// Execute a second pass through the image to assign the pixels to a palette entry.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="output">The output pixel array.</param>
        /// <param name="palette">The output color palette.</param>
        /// <param name="width">The width in pixels of the image.</param>
        /// <param name="height">The height in pixels of the image.</param>
        protected abstract void SecondPass(
            ImageFrame<TPixel> source,
            Span<byte> output,
            ReadOnlySpan<TPixel> palette,
            int width,
            int height);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private byte GetClosestPixelSlow(ref TPixel pixel)
        {
            // Loop through the palette and find the nearest match.
            int colorIndex = 0;
            float leastDistance = float.MaxValue;
            Vector4 vector = pixel.ToScaledVector4();
            float epsilon = Constants.EpsilonSquared;

            for (int index = 0; index < this.paletteVector.Length; index++)
            {
                ref Vector4 candidate = ref this.paletteVector[index];
                float distance = Vector4.DistanceSquared(vector, candidate);

                // Greater... Move on.
                if (!(distance < leastDistance))
                {
                    continue;
                }

                colorIndex = index;
                leastDistance = distance;

                // And if it's an exact match, exit the loop
                if (distance < epsilon)
                {
                    break;
                }
            }

            // Now I have the index, pop it into the cache for next time
            byte result = (byte)colorIndex;
            this.distanceCache.Add(pixel, result);
            return result;
        }
    }
}