// <copyright file="JpegEncoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Formats
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
        /// The jpeg quality.
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

        /// <summary>
        /// Gets the default file extension for this encoder.
        /// </summary>
        /// <value>The default file extension for this encoder.</value>
        public string Extension => "jpg";

        /// <summary>
        /// Indicates if the image encoder supports the specified
        /// file extension.
        /// </summary>
        /// <param name="extension">The file extension.</param>
        /// <returns>
        /// 	<c>true</c>, if the encoder supports the specified
        /// extensions; otherwise <c>false</c>.
        /// </returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="extension"/>
        /// is null (Nothing in Visual Basic).</exception>
        /// <exception cref="System.ArgumentException"><paramref name="extension"/> is a string
        /// of length zero or contains only blanks.</exception>
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

        /// <summary>
        /// Encodes the data of the specified image and writes the result to
        /// the specified stream.
        /// </summary>
        /// <param name="image">The image, where the data should be get from.
        /// Cannot be null (Nothing in Visual Basic).</param>
        /// <param name="stream">The stream, where the image data should be written to.
        /// Cannot be null (Nothing in Visual Basic).</param>
        /// <exception cref="System.ArgumentNullException">
        /// 	<para><paramref name="image"/> is null (Nothing in Visual Basic).</para>
        /// 	<para>- or -</para>
        /// 	<para><paramref name="stream"/> is null (Nothing in Visual Basic).</para>
        /// </exception>
        public void Encode(ImageBase image, Stream stream)
        {
            Guard.NotNull(image, nameof(image));
            Guard.NotNull(stream, nameof(stream));

            int pixelWidth = image.Width;
            int pixelHeight = image.Height;

            float[] sourcePixels = image.Pixels;

            SampleRow[] rows = new SampleRow[pixelHeight];

            Parallel.For(
                0,
                pixelHeight,
                y =>
                    {
                        byte[] samples = new byte[pixelWidth * 3];

                        for (int x = 0; x < pixelWidth; x++)
                        {
                            int start = x * 3;
                            int source = ((y * pixelWidth) + x) * 4;

                            // Convert to non-premultiplied color.
                            float r = sourcePixels[source];
                            float g = sourcePixels[source + 1];
                            float b = sourcePixels[source + 2];
                            float a = sourcePixels[source + 3];

                            Bgra32 color = Color.ToNonPremultiplied(new Color(r, g, b, a));

                            samples[start] = color.R;
                            samples[start + 1] = color.G;
                            samples[start + 2] = color.B;
                        }

                        rows[y] = new SampleRow(samples, pixelWidth, 8, 3);
                    });

            JpegImage jpg = new JpegImage(rows, Colorspace.RGB);
            jpg.WriteJpeg(stream, new CompressionParameters { Quality = this.Quality });
        }
    }
}
