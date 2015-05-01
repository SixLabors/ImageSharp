// ===============================================================================
// JpegEncoder.cs
// .NET Image Tools
// ===============================================================================
// Copyright (c) .NET Image Tools Development Group. 
// All rights reserved.
// ===============================================================================


namespace ImageProcessor.Formats
{
    using System;
    using System.IO;

    using BitMiracle.LibJpeg;

    /// <summary>
    /// Encoder for writing the data image to a stream in jpg format.
    /// </summary>
    public class JpegEncoder : IImageEncoder
    {
        #region Properties

        private int _quality = 100;
        /// <summary>
        /// Gets or sets the quality, that will be used to encode the image. Quality 
        /// index must be between 0 and 100 (compression from max to min). 
        /// </summary>
        /// <value>The quality of the jpg image from 0 to 100.</value>
        public int Quality
        {
            get { return _quality; }
            set { _quality = value; }
        }

        #endregion

        #region IImageEncoder Members

        /// <summary>
        /// Gets the default file extension for this encoder.
        /// </summary>
        /// <value>The default file extension for this encoder.</value>
        public string Extension
        {
            get { return "JPG"; }
        }

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

            if (extension.StartsWith(".")) extension = extension.Substring(1);
            return extension.Equals("JPG", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals("JPEG", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals("JFIF", StringComparison.OrdinalIgnoreCase);
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
            Guard.NotNull(image, "image");
            Guard.NotNull(stream, "stream");

            int pixelWidth = image.Width;
            int pixelHeight = image.Height;

            byte[] sourcePixels = image.Pixels;

            SampleRow[] rows = new SampleRow[pixelHeight];

            for (int y = 0; y < pixelHeight; y++)
            {
                byte[] samples = new byte[pixelWidth * 3];

                for (int x = 0; x < pixelWidth; x++)
                {
                    int start = x * 3;
                    int source = ((y * pixelWidth) + x) * 4;

                    samples[start] = sourcePixels[source + 2];
                    samples[start + 1] = sourcePixels[source + 1];
                    samples[start + 2] = sourcePixels[source];
                }

                rows[y] = new SampleRow(samples, pixelWidth, 8, 3);
            }

            JpegImage jpg = new JpegImage(rows, Colorspace.RGB);
            jpg.WriteJpeg(stream, new CompressionParameters { Quality = this.Quality });
        }

        #endregion
    }
}
