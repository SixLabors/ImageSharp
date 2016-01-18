// <copyright file="JpegDecoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Formats
{
    using System;
    using System.IO;
    using System.Numerics;
    using System.Threading.Tasks;

    using BitMiracle.LibJpeg;

    /// <summary>
    /// Image decoder for generating an image out of a jpg stream.
    /// </summary>
    public class JpegDecoder : IImageDecoder
    {
        /// <summary>
        /// Gets the size of the header for this image type.
        /// </summary>
        /// <value>The size of the header.</value>
        public int HeaderSize => 11;

        /// <summary>
        /// Indicates if the image decoder supports the specified
        /// file extension.
        /// </summary>
        /// <param name="extension">The file extension.</param>
        /// <returns>
        /// <c>true</c>, if the decoder supports the specified
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

            return extension.Equals("JPG", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals("JPEG", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals("JFIF", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Indicates if the image decoder supports the specified
        /// file header.
        /// </summary>
        /// <param name="header">The file header.</param>
        /// <returns>
        /// <c>true</c>, if the decoder supports the specified
        /// file header; otherwise <c>false</c>.
        /// </returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="header"/>
        /// is null (Nothing in Visual Basic).</exception>
        public bool IsSupportedFileFormat(byte[] header)
        {
            Guard.NotNull(header, "header");

            bool isSupported = false;

            if (header.Length >= 11)
            {
                bool isJfif = IsJfif(header);
                bool isExif = IsExif(header);
                bool isJpeg = IsJpeg(header);

                isSupported = isJfif || isExif || isJpeg;
            }

            return isSupported;
        }

        /// <summary>
        /// Decodes the image from the specified stream and sets
        /// the data to image.
        /// </summary>
        /// <param name="image">The image, where the data should be set to.
        /// Cannot be null (Nothing in Visual Basic).</param>
        /// <param name="stream">The stream, where the image should be
        /// decoded from. Cannot be null (Nothing in Visual Basic).</param>
        /// <exception cref="System.ArgumentNullException">
        /// 	<para><paramref name="image"/> is null (Nothing in Visual Basic).</para>
        /// 	<para>- or -</para>
        /// 	<para><paramref name="stream"/> is null (Nothing in Visual Basic).</para>
        /// </exception>
        public void Decode(Image image, Stream stream)
        {
            Guard.NotNull(image, "image");
            Guard.NotNull(stream, "stream");
            JpegImage jpg = new JpegImage(stream);

            int pixelWidth = jpg.Width;
            int pixelHeight = jpg.Height;

            float[] pixels = new float[pixelWidth * pixelHeight * 4];

            if (jpg.Colorspace == Colorspace.RGB && jpg.BitsPerComponent == 8)
            {
                Parallel.For(
                    0,
                    pixelHeight,
                    y =>
                        {
                            SampleRow row = jpg.GetRow(y);

                            for (int x = 0; x < pixelWidth; x++)
                            {
                                Sample sample = row.GetAt(x);

                                int offset = ((y * pixelWidth) + x) * 4;

                                // Expand from sRGB to linear RGB
                                Color color =
                                    Color.Expand(new Color(new Vector3(sample[0], sample[1], sample[2]) / 255f));

                                pixels[offset + 0] = color.R;
                                pixels[offset + 1] = color.G;
                                pixels[offset + 2] = color.B;
                                pixels[offset + 3] = color.A;
                            }
                        });
            }
            else if (jpg.Colorspace == Colorspace.Grayscale && jpg.BitsPerComponent == 8)
            {
                Parallel.For(
                    0,
                    pixelHeight,
                    y =>
                    {
                        SampleRow row = jpg.GetRow(y);

                        for (int x = 0; x < pixelWidth; x++)
                        {
                            Sample sample = row.GetAt(x);

                            int offset = ((y * pixelWidth) + x) * 4;

                            // Expand from sRGB to linear RGB
                            Color color =
                                Color.Expand(new Color(new Vector3(sample[0], sample[0], sample[0]) / 255f));

                            pixels[offset + 0] = color.R;
                            pixels[offset + 1] = color.G;
                            pixels[offset + 2] = color.B;
                            pixels[offset + 3] = color.A;
                        }
                    });
            }
            else
            {
                throw new NotSupportedException("JpegDecoder only supports RGB and Grayscale color spaces.");
            }

            image.SetPixels(pixelWidth, pixelHeight, pixels);
        }

        /// <summary>
        /// Returns a value indicating whether the given bytes identify Jfif data.
        /// </summary>
        /// <param name="header">The bytes representing the file header.</param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool IsJfif(byte[] header)
        {
            bool isJfif =
                header[6] == 0x4A && // J
                header[7] == 0x46 && // F
                header[8] == 0x49 && // I
                header[9] == 0x46 && // F
                header[10] == 0x00;

            return isJfif;
        }

        /// <summary>
        /// Returns a value indicating whether the given bytes identify EXIF data.
        /// </summary>
        /// <param name="header">The bytes representing the file header.</param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool IsExif(byte[] header)
        {
            bool isExif =
                header[6] == 0x45 && // E
                header[7] == 0x78 && // x
                header[8] == 0x69 && // i
                header[9] == 0x66 && // f
                header[10] == 0x00;

            return isExif;
        }

        /// <summary>
        /// Returns a value indicating whether the given bytes identify Jpeg data.
        /// This is a last chance resort for jpegs that contain ICC information.
        /// </summary>
        /// <param name="header">The bytes representing the file header.</param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool IsJpeg(byte[] header)
        {
            bool isJpg =
                header[0] == 0xFF && // 255
                header[1] == 0xD8; // 216

            return isJpg;
        }
    }
}
