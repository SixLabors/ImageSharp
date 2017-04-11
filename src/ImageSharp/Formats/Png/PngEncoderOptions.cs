// <copyright file="PngEncoderOptions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using Quantizers;

    /// <summary>
    /// Encapsulates the options for the <see cref="PngEncoder"/>.
    /// </summary>
    public sealed class PngEncoderOptions : EncoderOptions, IPngEncoderOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PngEncoderOptions"/> class.
        /// </summary>
        public PngEncoderOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PngEncoderOptions"/> class.
        /// </summary>
        /// <param name="options">The options for the encoder.</param>
        private PngEncoderOptions(IEncoderOptions options)
            : base(options)
        {
        }

        /// <summary>
        /// Gets or sets the quality of output for images.
        /// </summary>
        public int Quality { get; set; }

        /// <summary>
        /// Gets or sets the png color type
        /// </summary>
        public PngColorType PngColorType { get; set; } = PngColorType.RgbWithAlpha;

        /// <summary>
        /// Gets or sets the compression level 1-9.
        /// <remarks>Defaults to 6.</remarks>
        /// </summary>
        public int CompressionLevel { get; set; } = 6;

        /// <summary>
        /// Gets or sets the gamma value, that will be written
        /// the the stream, when the <see cref="WriteGamma"/> property
        /// is set to true. The default value is 2.2F.
        /// </summary>
        /// <value>The gamma value of the image.</value>
        public float Gamma { get; set; } = 2.2F;

        /// <summary>
        /// Gets or sets quantizer for reducing the color count.
        /// </summary>
        public IQuantizer Quantizer { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance should write
        /// gamma information to the stream. The default value is false.
        /// </summary>
        public bool WriteGamma { get; set; }

        /// <summary>
        /// Converts the options to a <see cref="IPngEncoderOptions"/> instance with a
        /// cast or by creating a new instance with the specfied options.
        /// </summary>
        /// <param name="options">The options for the encoder.</param>
        /// <returns>The options for the <see cref="PngEncoder"/>.</returns>
        internal static IPngEncoderOptions Create(IEncoderOptions options)
        {
            return options as IPngEncoderOptions ?? new PngEncoderOptions(options);
        }
    }
}
