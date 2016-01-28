// <copyright file="JpegEncoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    using BitMiracle.LibJpeg;

    /// <summary>
    /// Encoder for writing the data image to a stream in jpeg format.
    /// </summary>
    public class JpegEncoder : IImageEncoder
    {
        /// <summary>
        /// The quality.
        /// </summary>
        private int quality = 100;

        /// <summary>
        /// Gets or sets the quality, that will be used to encode the image. Quality
        /// index must be between 0 and 100 (compression from max to min).
        /// </summary>
        /// <value>The quality of the jpg image from 0 to 100.</value>
        public int Quality
        {
            get { return this.quality; }
            set { this.quality = value.Clamp(1, 100); }
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
        public void Encode(ImageBase image, Stream stream)
        {
            Guard.NotNull(image, nameof(image));
            Guard.NotNull(stream, nameof(stream));

            int imageWidth = image.Width;
            int imageHeight = image.Height;

            SampleRow[] rows = new SampleRow[imageHeight];

            Parallel.For(
                0,
                imageHeight,
                y =>
                    {
                        byte[] samples = new byte[imageWidth * 3];

                        for (int x = 0; x < imageWidth; x++)
                        {
                            Bgra32 color = Color.ToNonPremultiplied(image[x, y]);

                            int start = x * 3;
                            samples[start] = color.R;
                            samples[start + 1] = color.G;
                            samples[start + 2] = color.B;
                        }

                        rows[y] = new SampleRow(samples, imageWidth, 8, 3);
                    });

            using (JpegImage jpg = new JpegImage(rows, Colorspace.RGB))
            {
                jpg.WriteJpeg(stream, new CompressionParameters { Quality = this.Quality });
            }
        }
    }
}
