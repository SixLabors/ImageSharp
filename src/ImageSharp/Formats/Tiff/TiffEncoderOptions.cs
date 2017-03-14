// <copyright file="TiffEncoderOptions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// Encapsulates the options for the <see cref="TiffEncoder"/>.
    /// </summary>
    public sealed class TiffEncoderOptions : EncoderOptions, ITiffEncoderOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TiffEncoderOptions"/> class.
        /// </summary>
        public TiffEncoderOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffEncoderOptions"/> class.
        /// </summary>
        /// <param name="options">The options for the encoder.</param>
        private TiffEncoderOptions(IEncoderOptions options)
            : base(options)
        {
        }

        /// <summary>
        /// Converts the options to a <see cref="ITiffEncoderOptions"/> instance with a
        /// cast or by creating a new instance with the specfied options.
        /// </summary>
        /// <param name="options">The options for the encoder.</param>
        /// <returns>The options for the <see cref="TiffEncoder"/>.</returns>
        internal static ITiffEncoderOptions Create(IEncoderOptions options)
        {
            return options as ITiffEncoderOptions ?? new TiffEncoderOptions(options);
        }
    }
}
