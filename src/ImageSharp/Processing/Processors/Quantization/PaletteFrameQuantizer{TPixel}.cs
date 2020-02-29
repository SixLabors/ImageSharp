// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// Encapsulates methods to create a quantized image based upon the given palette.
    /// <see href="http://msdn.microsoft.com/en-us/library/aa479306.aspx"/>
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal struct PaletteFrameQuantizer<TPixel> : IFrameQuantizer<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private IMemoryOwner<TPixel> paletteOwner;
        private readonly EuclideanPixelMap<TPixel> pixelMap;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteFrameQuantizer{TPixel}"/> struct.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="options">The quantizer options defining quantization rules.</param>
        /// <param name="colors">A <see cref="ReadOnlyMemory{TPixel}"/> containing all colors in the palette.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public PaletteFrameQuantizer(Configuration configuration, QuantizerOptions options, ReadOnlySpan<Color> colors)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.NotNull(options, nameof(options));

            this.Configuration = configuration;
            this.Options = options;

            int maxLength = Math.Min(colors.Length, options.MaxColors);
            this.paletteOwner = configuration.MemoryAllocator.Allocate<TPixel>(maxLength);
            Color.ToPixel(configuration, colors, this.paletteOwner.GetSpan());

            this.pixelMap = new EuclideanPixelMap<TPixel>(this.paletteOwner.Memory);
            this.isDisposed = false;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteFrameQuantizer{TPixel}"/> struct.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="options">The quantizer options defining quantization rules.</param>
        /// <param name="palette">A <see cref="ReadOnlyMemory{TPixel}"/> containing all colors in the palette.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public PaletteFrameQuantizer(Configuration configuration, QuantizerOptions options, ReadOnlySpan<TPixel> palette)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.NotNull(options, nameof(options));

            this.Configuration = configuration;
            this.Options = options;

            int maxLength = Math.Min(palette.Length, options.MaxColors);
            this.paletteOwner = configuration.MemoryAllocator.Allocate<TPixel>(maxLength);
            palette.CopyTo(this.paletteOwner.GetSpan());

            this.pixelMap = new EuclideanPixelMap<TPixel>(this.paletteOwner.Memory);
            this.isDisposed = false;
        }

        /// <inheritdoc/>
        public Configuration Configuration { get; }

        /// <inheritdoc/>
        public QuantizerOptions Options { get; }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly QuantizedFrame<TPixel> QuantizeFrame(ImageFrame<TPixel> source, Rectangle bounds)
            => FrameQuantizerExtensions.QuantizeFrame(ref Unsafe.AsRef(this), source, bounds);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly ReadOnlySpan<TPixel> BuildPalette(ImageFrame<TPixel> source, Rectangle bounds)
            => this.paletteOwner.GetSpan();

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly byte GetQuantizedColor(TPixel color, ReadOnlySpan<TPixel> palette, out TPixel match)
            => (byte)this.pixelMap.GetClosestColor(color, out match);

        /// <inheritdoc/>
        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.isDisposed = true;
            this.paletteOwner.Dispose();
            this.paletteOwner = null;
        }
    }
}
