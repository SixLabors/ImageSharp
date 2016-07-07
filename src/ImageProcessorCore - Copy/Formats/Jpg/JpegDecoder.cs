// <copyright file="JpegDecoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

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

            JpegDecoderCore decoder = new JpegDecoderCore();
            decoder.Decode(stream, image, false);

            // TODO: When nComp is 3 we set the ImageBase pixels internally, Eventually we should 
            // do the same here
            if (decoder.nComp == 1)
            {
                int pixelWidth = decoder.width;
                int pixelHeight = decoder.height;

                float[] pixels = new float[pixelWidth * pixelHeight * 4];

                Parallel.For(
                    0,
                    pixelHeight,
                    y =>
                    {
                        var yoff = decoder.img1.get_row_offset(y);
                        for (int x = 0; x < pixelWidth; x++)
                        {
                            int offset = ((y * pixelWidth) + x) * 4;

                            pixels[offset + 0] = decoder.img1.pixels[yoff + x] / 255f;
                            pixels[offset + 1] = decoder.img1.pixels[yoff + x] / 255f;
                            pixels[offset + 2] = decoder.img1.pixels[yoff + x] / 255f;
                            pixels[offset + 3] = 1;
                        }
                    });

                image.SetPixels(pixelWidth, pixelHeight, pixels);
            }
            else if (decoder.nComp != 3)
            {
                throw new NotSupportedException("JpegDecoder only supports RGB and Grayscale color spaces.");
            }
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
                header[7] == 0x78 && // X
                header[8] == 0x69 && // I
                header[9] == 0x66 && // F
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
