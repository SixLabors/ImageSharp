// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;

namespace SixLabors.ImageSharp.Processing.Dithering.Processors
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
        /// Initializes a new instance of the <see cref="PaletteDitherProcessorBase{TPixel}"/> class.
        /// </summary>
        /// <param name="palette">The palette to select substitute colors from.</param>
        protected PaletteDitherProcessorBase(TPixel[] palette)
        {
            Guard.NotNull(palette, nameof(palette));
            this.Palette = palette;
        }

        /// <summary>
        /// Gets the palette to select substitute colors from.
        /// </summary>
        public TPixel[] Palette { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected PixelPair<TPixel> GetClosestPixelPair(ref TPixel pixel, TPixel[] colorPalette)
        {
            // Check if the color is in the lookup table
            if (this.cache.TryGetValue(pixel, out PixelPair<TPixel> value))
            {
                return value;
            }

            return this.GetClosestPixelPairSlow(ref pixel, colorPalette);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private PixelPair<TPixel> GetClosestPixelPairSlow(ref TPixel pixel, TPixel[] colorPalette)
        {
            // Not found - loop through the palette and find the nearest match.
            float leastDistance = float.MaxValue;
            float secondLeastDistance = float.MaxValue;
            var vector = pixel.ToVector4();

            TPixel closest = default;
            TPixel secondClosest = default;
            for (int index = 0; index < colorPalette.Length; index++)
            {
                ref TPixel candidate = ref colorPalette[index];
                float distance = Vector4.DistanceSquared(vector, candidate.ToVector4());

                if (distance < leastDistance)
                {
                    leastDistance = distance;
                    secondClosest = closest;
                    closest = candidate;
                }
                else if (distance < secondLeastDistance)
                {
                    secondLeastDistance = distance;
                    secondClosest = candidate;
                }
            }

            // Pop it into the cache for next time
            var pair = new PixelPair<TPixel>(closest, secondClosest);
            this.cache.Add(pixel, pair);

            return pair;
        }
    }
}