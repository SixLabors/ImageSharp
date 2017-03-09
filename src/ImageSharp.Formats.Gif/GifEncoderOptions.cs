// <copyright file="GifEncoderOptions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System.Text;

    using Quantizers;

    /// <summary>
    /// Encapsulates the options for the <see cref="GifEncoder"/>.
    /// </summary>
    public sealed class GifEncoderOptions : EncoderOptions, IGifEncoderOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GifEncoderOptions"/> class.
        /// </summary>
        public GifEncoderOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GifEncoderOptions"/> class.
        /// </summary>
        /// <param name="options">The options for the encoder.</param>
        private GifEncoderOptions(IEncoderOptions options)
            : base(options)
        {
        }

        /// <summary>
        /// Gets or sets the encoding that should be used when writing comments.
        /// </summary>
        public Encoding TextEncoding { get; set; } = GifConstants.DefaultEncoding;

        /// <summary>
        /// Gets or sets the quality of output for images.
        /// </summary>
        /// <remarks>For gifs the value ranges from 1 to 256.</remarks>
        public int Quality { get; set; }

        /// <summary>
        /// Gets or sets the transparency threshold.
        /// </summary>
        public byte Threshold { get; set; } = 128;

        /// <summary>
        /// Gets or sets the quantizer for reducing the color count.
        /// </summary>
        public IQuantizer Quantizer { get; set; }

        /// <summary>
        /// Converts the options to a <see cref="IGifEncoderOptions"/> instance with a
        /// cast or by creating a new instance with the specfied options.
        /// </summary>
        /// <param name="options">The options for the encoder.</param>
        /// <returns>The options for the <see cref="GifEncoder"/>.</returns>
        internal static IGifEncoderOptions Create(IEncoderOptions options)
        {
            return options as IGifEncoderOptions ?? new GifEncoderOptions(options);
        }
    }
}
