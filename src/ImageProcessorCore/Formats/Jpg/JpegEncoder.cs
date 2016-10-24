// <copyright file="JpegEncoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    using System;
    using System.IO;

    /// <summary>
    /// Encoder for writing the data image to a stream in jpeg format.
    /// </summary>
    public class JpegEncoder : IImageEncoder
    {
        /// <summary>
        /// The quality used to encode the image.
        /// </summary>
        private int quality = 75;

        /// <summary>
        /// The subsamples scheme used to encode the image.
        /// </summary>
        private JpegSubsample subsample = JpegSubsample.Ratio420;

        /// <summary>
        /// Whether subsampling has been specifically set.
        /// </summary>
        private bool subsampleSet;

        /// <summary>
        /// Gets or sets the quality, that will be used to encode the image. Quality
        /// index must be between 0 and 100 (compression from max to min).
        /// </summary>
        /// <remarks>
        /// If the quality is less than or equal to 80, the subsampling ratio will switch to <see cref="JpegSubsample.Ratio420"/>
        /// </remarks>
        /// <value>The quality of the jpg image from 0 to 100.</value>
        public int Quality
        {
            get { return this.quality; }
            set { this.quality = value.Clamp(1, 100); }
        }

        /// <summary>
        /// Gets or sets the subsample ration, that will be used to encode the image.
        /// </summary>
        /// <value>The subsample ratio of the jpg image.</value>
        public JpegSubsample Subsample
        {
            get { return this.subsample; }
            set
            {
                this.subsample = value;
                this.subsampleSet = true;
            }
        }

        /// <inheritdoc/>
        public string MimeType => "image/jpeg";

        /// <inheritdoc/>
        public string Extension => "jpg";

        /// <inheritdoc/>
        public bool IsSupportedFileExtension(string extension)
        {
            Guard.NotNullOrEmpty(extension, "extension");

            if (extension.StartsWith("."))
            {
                extension = extension.Substring(1);
            }

            return extension.Equals(this.Extension, StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals("jpeg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals("jfif", StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public void Encode<TColor, TPacked>(Image<TColor, TPacked> image, Stream stream)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            JpegEncoderCore encode = new JpegEncoderCore();
            if (this.subsampleSet)
            {
                encode.Encode(image, stream, this.Quality, this.Subsample);
            }
            else
            {
                encode.Encode(image, stream, this.Quality, this.Quality >= 80 ? JpegSubsample.Ratio444 : JpegSubsample.Ratio420);
            }
        }
    }
}
