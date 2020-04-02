// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// Allows the quantization of images pixels using Xiaolin Wu's Color Quantizer <see href="http://www.ece.mcmaster.ca/~xwu/cq.c"/>
    /// </summary>
    public class WuQuantizer : IQuantizer
    {
        private static readonly QuantizerOptions DefaultOptions = new QuantizerOptions();

        /// <summary>
        /// Initializes a new instance of the <see cref="WuQuantizer"/> class
        /// using the default <see cref="QuantizerOptions"/>.
        /// </summary>
        public WuQuantizer()
            : this(DefaultOptions)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WuQuantizer"/> class.
        /// </summary>
        /// <param name="options">The quantizer options defining quantization rules.</param>
        public WuQuantizer(QuantizerOptions options)
        {
            Guard.NotNull(options, nameof(options));
            this.Options = options;
        }

        /// <inheritdoc />
        public QuantizerOptions Options { get; }

        /// <inheritdoc />
        public IFrameQuantizer<TPixel> CreateFrameQuantizer<TPixel>(Configuration configuration)
            where TPixel : unmanaged, IPixel<TPixel>
            => this.CreateFrameQuantizer<TPixel>(configuration, this.Options);

        /// <inheritdoc />
        public IFrameQuantizer<TPixel> CreateFrameQuantizer<TPixel>(Configuration configuration, QuantizerOptions options)
            where TPixel : unmanaged, IPixel<TPixel>
            => new WuFrameQuantizer<TPixel>(configuration, options);
    }
}
