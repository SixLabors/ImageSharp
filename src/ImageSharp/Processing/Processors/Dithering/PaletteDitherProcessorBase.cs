// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// The base class for dither and diffusion processors that consume a palette.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal abstract class PaletteDitherProcessorBase<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly Dictionary<TPixel, PixelPair<TPixel>> cache = new Dictionary<TPixel, PixelPair<TPixel>>();

        /// <summary>
        /// The vector representation of the image palette.
        /// </summary>
        private readonly Vector4[] paletteVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteDitherProcessorBase{TPixel}"/> class.
        /// </summary>
        /// <param name="palette">The palette to select substitute colors from.</param>
        protected PaletteDitherProcessorBase(TPixel[] palette)
        {
            this.Palette = palette ?? throw new ArgumentNullException(nameof(palette));
            this.paletteVector = new Vector4[this.Palette.Length];
            PixelOperations<TPixel>.Instance.ToScaledVector4(this.Palette, this.paletteVector, this.Palette.Length);
        }

        /// <summary>
        /// Gets the palette to select substitute colors from.
        /// </summary>
        public TPixel[] Palette { get; }

        /// <summary>
        /// Returns the two closest colors from the palette calcluated via Euclidean distance in the Rgba space.
        /// </summary>
        /// <param name="pixel">The source color to match.</param>
        /// <returns>The <see cref="PixelPair{TPixel}"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected PixelPair<TPixel> GetClosestPixelPair(ref TPixel pixel)
        {
            // Check if the color is in the lookup table
            if (this.cache.TryGetValue(pixel, out PixelPair<TPixel> value))
            {
                return value;
            }

            return this.GetClosestPixelPairSlow(ref pixel);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private PixelPair<TPixel> GetClosestPixelPairSlow(ref TPixel pixel)
        {
            // Not found - loop through the palette and find the nearest match.
            float leastDistance = float.MaxValue;
            float secondLeastDistance = float.MaxValue;
            var vector = pixel.ToVector4();

            TPixel closest = default;
            TPixel secondClosest = default;
            for (int index = 0; index < this.paletteVector.Length; index++)
            {
                ref Vector4 candidate = ref this.paletteVector[index];
                float distance = Vector4.DistanceSquared(vector, candidate);

                if (distance < leastDistance)
                {
                    leastDistance = distance;
                    secondClosest = closest;
                    closest = this.Palette[index];
                }
                else if (distance < secondLeastDistance)
                {
                    secondLeastDistance = distance;
                    secondClosest = this.Palette[index];
                }
            }

            // Pop it into the cache for next time
            var pair = new PixelPair<TPixel>(closest, secondClosest);
            this.cache.Add(pixel, pair);

            return pair;
        }
    }
}