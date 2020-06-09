// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// Allows the quantization of images pixels using Octrees.
    /// <see href="http://msdn.microsoft.com/en-us/library/aa479306.aspx"/>
    /// </summary>
    public class OctreeQuantizer : IQuantizer
    {
        private static readonly QuantizerOptions DefaultOptions = new QuantizerOptions();

        /// <summary>
        /// Initializes a new instance of the <see cref="OctreeQuantizer"/> class
        /// using the default <see cref="QuantizerOptions"/>.
        /// </summary>
        public OctreeQuantizer()
            : this(DefaultOptions)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OctreeQuantizer"/> class.
        /// </summary>
        /// <param name="options">The quantizer options defining quantization rules.</param>
        public OctreeQuantizer(QuantizerOptions options)
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
            => new OctreeQuantizer<TPixel>(configuration, options);
    }
}
