// <copyright file="PngDecoderOptions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System.Text;

    /// <summary>
    /// Encapsulates the png decoder options.
    /// </summary>
    public sealed class PngDecoderOptions : DecoderOptions, IPngDecoderOptions
    {
        private static readonly Encoding DefaultEncoding = Encoding.GetEncoding("ASCII");

        /// <summary>
        /// Initializes a new instance of the <see cref="PngDecoderOptions"/> class.
        /// </summary>
        public PngDecoderOptions()
        {
            this.InitializeWithDefaults();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PngDecoderOptions"/> class.
        /// </summary>
        /// <param name="options">The options for the decoder.</param>
        private PngDecoderOptions(IDecoderOptions options)
            : base(options)
        {
            this.InitializeWithDefaults();
        }

        /// <summary>
        /// Gets or sets the encoding that should be used when reading text chunks.
        /// </summary>
        public Encoding TextEncoding { get; set; }

        /// <summary>
        /// Converts the options to a <see cref="IPngDecoderOptions"/> instance with a cast
        /// or by creating a new instance with the specfied options.
        /// </summary>
        /// <param name="options">The options for the decoder.</param>
        /// <returns>The options for the png decoder.</returns>
        internal static IPngDecoderOptions Create(IDecoderOptions options)
        {
            IPngDecoderOptions pngOptions = options as IPngDecoderOptions;
            if (pngOptions != null)
            {
                return pngOptions;
            }

            return new PngDecoderOptions(options);
        }

        private void InitializeWithDefaults()
        {
            this.TextEncoding = DefaultEncoding;
        }
    }
}
