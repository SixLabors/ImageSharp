// <copyright file="BmpEncoderOptions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// Encapsulates the options for the <see cref="BmpEncoder"/>.
    /// </summary>
    public sealed class BmpEncoderOptions : EncoderOptions, IBmpEncoderOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BmpEncoderOptions"/> class.
        /// </summary>
        public BmpEncoderOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BmpEncoderOptions"/> class.
        /// </summary>
        /// <param name="options">The options for the encoder.</param>
        private BmpEncoderOptions(IEncoderOptions options)
            : base(options)
        {
        }

        /// <summary>
        /// Gets or sets the number of bits per pixel.
        /// </summary>
        public BmpBitsPerPixel BitsPerPixel { get; set; } = BmpBitsPerPixel.Pixel24;

        /// <summary>
        /// Converts the options to a <see cref="IBmpEncoderOptions"/> instance with a cast
        /// or by creating a new instance with the specfied options.
        /// </summary>
        /// <param name="options">The options for the encoder.</param>
        /// <returns>The options for the <see cref="BmpEncoder"/>.</returns>
        internal static IBmpEncoderOptions Create(IEncoderOptions options)
        {
            return options as IBmpEncoderOptions ?? new BmpEncoderOptions(options);
        }
    }
}
