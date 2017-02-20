// <copyright file="JpegEncoderOptions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// Encapsulates the options for the <see cref="JpegEncoder"/>.
    /// </summary>
    public sealed class JpegEncoderOptions : EncoderOptions, IJpegEncoderOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JpegEncoderOptions"/> class.
        /// </summary>
        public JpegEncoderOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JpegEncoderOptions"/> class.
        /// </summary>
        /// <param name="options">The options for the encoder.</param>
        private JpegEncoderOptions(IEncoderOptions options)
            : base(options)
        {
        }

        /// <summary>
        /// Gets or sets the quality, that will be used to encode the image. Quality
        /// index must be between 0 and 100 (compression from max to min).
        /// </summary>
        /// <remarks>
        /// If the quality is less than or equal to 80, the subsampling ratio will switch to <see cref="JpegSubsample.Ratio420"/>
        /// </remarks>
        /// <value>The quality of the jpg image from 0 to 100.</value>
        public int Quality { get; set; }

        /// <summary>
        /// Gets or sets the subsample ration, that will be used to encode the image.
        /// </summary>
        /// <value>The subsample ratio of the jpg image.</value>
        public JpegSubsample? Subsample { get; set; }

        /// <summary>
        /// Converts the options to a <see cref="IJpegEncoderOptions"/> instance with a
        /// cast or by creating a new instance with the specfied options.
        /// </summary>
        /// <param name="options">The options for the encoder.</param>
        /// <returns>The options for the <see cref="JpegEncoder"/>.</returns>
        internal static IJpegEncoderOptions Create(IEncoderOptions options)
        {
            IJpegEncoderOptions jpegOptions = options as IJpegEncoderOptions;
            if (jpegOptions != null)
            {
                return jpegOptions;
            }

            return new JpegEncoderOptions(options);
        }
    }
}
