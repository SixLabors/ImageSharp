// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// The base class for dither and diffusion processors that consume a palette.
    /// </summary>
    internal abstract class PaletteDitherProcessorBase<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly Dictionary<TPixel, PixelPair<TPixel>> cache = new Dictionary<TPixel, PixelPair<TPixel>>();

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteDitherProcessorBase{TPixel}"/> class.
        /// </summary>
        /// <param name="palette">The palette to select substitute colors from.</param>
        public PaletteDitherProcessorBase(TPixel[] palette)
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
            if (this.cache.ContainsKey(pixel))
            {
                return this.cache[pixel];
            }

            // Not found - loop through the palette and find the nearest match.
            float leastDistance = int.MaxValue;
            float secondLeastDistance = int.MaxValue;
            var vector = pixel.ToVector4();

            var closest = default(TPixel);
            var secondClosest = default(TPixel);
            for (int index = 0; index < colorPalette.Length; index++)
            {
                TPixel temp = colorPalette[index];
                var tempVector = temp.ToVector4();
                float distance = Vector4.Distance(vector, tempVector);

                if (distance < leastDistance)
                {
                    leastDistance = distance;
                    secondClosest = closest;
                    closest = temp;
                }
                else if (distance < secondLeastDistance)
                {
                    secondLeastDistance = distance;
                    secondClosest = temp;
                }
            }

            // Pop it into the cache for next time
            var pair = new PixelPair<TPixel>(closest, secondClosest);
            this.cache.Add(pixel, pair);

            return pair;
        }
    }
}