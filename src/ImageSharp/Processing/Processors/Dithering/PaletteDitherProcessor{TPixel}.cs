// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
        private IMemoryOwner<TPixel> palette;
        private IMemoryOwner<Vector4> paletteVector;
        private bool palleteVectorMapped;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteDitherProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="definition">The <see cref="PaletteDitherProcessor"/> defining the processor parameters.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        protected PaletteDitherProcessor(Configuration configuration, PaletteDitherProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            this.Definition = definition;
            this.palette = this.Configuration.MemoryAllocator.Allocate<TPixel>(definition.Palette.Length);
            this.paletteVector = this.Configuration.MemoryAllocator.Allocate<Vector4>(definition.Palette.Length);
        }

        protected PaletteDitherProcessor Definition { get; }

        /// <inheritdoc/>
        protected override void BeforeFrameApply(ImageFrame<TPixel> source)
        {
            // Lazy init palettes:
            if (!this.palleteVectorMapped)
            {
                ReadOnlySpan<Color> sourcePalette = this.Definition.Palette.Span;
                Color.ToPixel(this.Configuration, sourcePalette, this.palette.Memory.Span);

                PixelOperations<TPixel>.Instance.ToVector4(
                    this.Configuration,
                    this.palette.Memory.Span,
                    this.paletteVector.Memory.Span,
                    PixelConversionModifiers.Scale);
            }

            this.palleteVectorMapped = true;

            base.BeforeFrameApply(source);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (this.isDisposed)
            {
                return;
            }

            if (disposing)
            {
                this.palette?.Dispose();
                this.paletteVector?.Dispose();
            }

            this.palette = null;
            this.paletteVector = null;

            this.isDisposed = true;
            base.Dispose(disposing);
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
            Span<TPixel> paletteSpan = this.palette.Memory.Span;
            ref TPixel paletteSpanBase = ref MemoryMarshal.GetReference(paletteSpan);
            Span<Vector4> paletteVectorSpan = this.paletteVector.Memory.Span;
            ref Vector4 paletteVectorSpanBase = ref MemoryMarshal.GetReference(paletteVectorSpan);

            for (int index = 0; index < paletteVectorSpan.Length; index++)
            {
                ref Vector4 candidate = ref Unsafe.Add(ref paletteVectorSpanBase, index);
                float distance = Vector4.DistanceSquared(vector, candidate);

                if (distance < leastDistance)
                {
                    leastDistance = distance;
                    secondClosest = closest;
                    closest = Unsafe.Add(ref paletteSpanBase, index);
                }
                else if (distance < secondLeastDistance)
                {
                    secondLeastDistance = distance;
                    secondClosest = Unsafe.Add(ref paletteSpanBase, index);
                }
            }

            // Pop it into the cache for next time
            var pair = new PixelPair<TPixel>(closest, secondClosest);
            this.cache.Add(pixel, pair);

            return pair;
        }
    }
}
