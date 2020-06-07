// Copyright (c) Six Labors.
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
        public IQuantizer<TPixel> CreatePixelSpecificQuantizer<TPixel>(Configuration configuration)
            where TPixel : unmanaged, IPixel<TPixel>
            => this.CreatePixelSpecificQuantizer<TPixel>(configuration, this.Options);

        /// <inheritdoc />
        public IQuantizer<TPixel> CreatePixelSpecificQuantizer<TPixel>(Configuration configuration, QuantizerOptions options)
            where TPixel : unmanaged, IPixel<TPixel>
            => new WuQuantizer<TPixel>(configuration, options);
    }
}
