// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// Encapsulates methods to create a quantized image based upon the given palette.
    /// <see href="http://msdn.microsoft.com/en-us/library/aa479306.aspx"/>
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class PaletteQuantizer<TPixel> : IQuantizer<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private EuclideanPixelMap<TPixel> pixelMap;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteQuantizer{TPixel}"/> struct.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="options">The quantizer options defining quantization rules.</param>
        /// <param name="palette">The palette to use.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public PaletteQuantizer(Configuration configuration, QuantizerOptions options, ReadOnlyMemory<TPixel> palette)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.NotNull(options, nameof(options));

            this.Configuration = configuration;
            this.Options = options;
            this.pixelMap = new EuclideanPixelMap<TPixel>(configuration, palette);
        }

        ~PaletteQuantizer() => throw new Exception("Very bad");

        /// <inheritdoc/>
        public Configuration Configuration { get; }

        /// <inheritdoc/>
        public QuantizerOptions Options { get; }

        /// <inheritdoc/>
        public ReadOnlyMemory<TPixel> Palette => this.pixelMap.Palette;

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public IndexedImageFrame<TPixel> QuantizeFrame(ImageFrame<TPixel> source, Rectangle bounds)
            => QuantizerUtilities.QuantizeFrame(this, source, bounds);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void AddPaletteColors(Buffer2DRegion<TPixel> pixelRegion)
        {
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public byte GetQuantizedColor(TPixel color, out TPixel match)
            => (byte)this.pixelMap.GetClosestColor(color, out match);

        /// <inheritdoc/>
        public void Dispose()
        {
            this.pixelMap?.Dispose();
            this.pixelMap = null;
            GC.SuppressFinalize(this);
        }
    }
}
