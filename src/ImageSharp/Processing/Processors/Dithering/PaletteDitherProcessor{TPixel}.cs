// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// The base class for dither and diffusion processors that consume a palette.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal abstract class PaletteDitherProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly Dictionary<TPixel, PixelPair<TPixel>> cache = new Dictionary<TPixel, PixelPair<TPixel>>();

        private TPixel[] palette;

        /// <summary>
        /// The vector representation of the image palette.
        /// </summary>
        private Vector4[] paletteVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteDitherProcessor{TPixel}"/> class.
        /// </summary>
        protected PaletteDitherProcessor(PaletteDitherProcessor definition)
        {
            this.Definition = definition;
        }

        protected PaletteDitherProcessor Definition { get; }

        protected override void BeforeFrameApply(
            ImageFrame<TPixel> source,
            Rectangle sourceRectangle,
            Configuration configuration)
        {
            base.BeforeFrameApply(source, sourceRectangle, configuration);

            // Lazy init palette:
            if (this.palette is null)
            {
                ReadOnlySpan<Color> sourcePalette = this.Definition.Palette.Span;
                this.palette = new TPixel[sourcePalette.Length];
                Color.ToPixel<TPixel>(configuration, sourcePalette, this.palette);
            }

            // Lazy init paletteVector:
            if (this.paletteVector is null)
            {
                this.paletteVector = new Vector4[this.palette.Length];
                PixelOperations<TPixel>.Instance.ToVector4(
                    configuration,
                    (ReadOnlySpan<TPixel>)this.palette,
                    (Span<Vector4>)this.paletteVector,
                    PixelConversionModifiers.Scale);
            }
        }

        /// <summary>
        /// Returns the two closest colors from the palette calculated via Euclidean distance in the Rgba space.
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
                    closest = this.palette[index];
                }
                else if (distance < secondLeastDistance)
                {
                    secondLeastDistance = distance;
                    secondClosest = this.palette[index];
                }
            }

            // Pop it into the cache for next time
            var pair = new PixelPair<TPixel>(closest, secondClosest);
            this.cache.Add(pixel, pair);

            return pair;
        }
    }
}