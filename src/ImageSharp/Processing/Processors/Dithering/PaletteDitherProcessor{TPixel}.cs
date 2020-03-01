// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// Allows the consumption a palette to dither an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal sealed class PaletteDitherProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly DitherProcessor ditherProcessor;
        private readonly IDither dither;
        private IMemoryOwner<TPixel> paletteMemory;
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
            this.dither = definition.Dither;

            ReadOnlySpan<Color> sourcePalette = definition.Palette.Span;
            this.paletteMemory = this.Configuration.MemoryAllocator.Allocate<TPixel>(sourcePalette.Length);
            Color.ToPixel(this.Configuration, sourcePalette, this.paletteMemory.Memory.Span);

            this.ditherProcessor = new DitherProcessor(
                this.Configuration,
                this.paletteMemory.Memory,
                definition.DitherScale);
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            var interest = Rectangle.Intersect(this.SourceRectangle, source.Bounds());
            this.dither.ApplyPaletteDither(in this.ditherProcessor, source, interest);
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
                this.paletteMemory?.Dispose();
            }

            this.paletteMemory = null;

            this.isDisposed = true;
            base.Dispose(disposing);
        }

        /// <summary>
        /// Used to allow inlining of calls to
        /// <see cref="IPaletteDitherImageProcessor{TPixel}.GetPaletteColor(TPixel, ReadOnlySpan{TPixel})"/>.
        /// </summary>
        private readonly struct DitherProcessor : IPaletteDitherImageProcessor<TPixel>
        {
            private readonly EuclideanPixelMap<TPixel> pixelMap;

            [MethodImpl(InliningOptions.ShortMethod)]
            public DitherProcessor(
                Configuration configuration,
                ReadOnlyMemory<TPixel> palette,
                float ditherScale)
            {
                this.Configuration = configuration;
                this.pixelMap = new EuclideanPixelMap<TPixel>(configuration, palette);
                this.Palette = palette;
                this.DitherScale = ditherScale;
            }

            public Configuration Configuration { get; }

            public ReadOnlyMemory<TPixel> Palette { get; }

            public float DitherScale { get; }

            [MethodImpl(InliningOptions.ShortMethod)]
            public TPixel GetPaletteColor(TPixel color, ReadOnlySpan<TPixel> palette)
            {
                this.pixelMap.GetClosestColor(color, out TPixel match);
                return match;
            }
        }
    }
}
