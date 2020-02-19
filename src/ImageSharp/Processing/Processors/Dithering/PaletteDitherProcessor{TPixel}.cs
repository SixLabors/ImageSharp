// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// Allows the consumption a palette to dither an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal sealed class PaletteDitherProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly int paletteLength;
        private readonly IDither dither;
        private readonly float ditherScale;
        private readonly ReadOnlyMemory<Color> sourcePalette;
        private IMemoryOwner<TPixel> palette;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteDitherProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="definition">The <see cref="PaletteDitherProcessor"/> defining the processor parameters.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public PaletteDitherProcessor(Configuration configuration, PaletteDitherProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            this.paletteLength = definition.Palette.Span.Length;
            this.dither = definition.Dither;
            this.ditherScale = definition.DitherScale;
            this.sourcePalette = definition.Palette;
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            var interest = Rectangle.Intersect(this.SourceRectangle, source.Bounds());

            this.dither.ApplyPaletteDither(
                this.Configuration,
                this.palette.Memory,
                source,
                interest,
                this.ditherScale);
        }

        /// <inheritdoc/>
        protected override void BeforeFrameApply(ImageFrame<TPixel> source)
        {
            // Lazy init palettes:
            if (this.palette is null)
            {
                this.palette = this.Configuration.MemoryAllocator.Allocate<TPixel>(this.paletteLength);
                ReadOnlySpan<Color> sourcePalette = this.sourcePalette.Span;
                Color.ToPixel(this.Configuration, sourcePalette, this.palette.Memory.Span);
            }

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
            }

            this.palette = null;

            this.isDisposed = true;
            base.Dispose(disposing);
        }
    }
}
