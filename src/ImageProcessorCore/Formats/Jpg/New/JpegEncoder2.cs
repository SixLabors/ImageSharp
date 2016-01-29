// <copyright file="JpegEncoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    using System;
    using System.IO;

    using ImageProcessorCore.IO;

    /// <summary>
    /// Encoder for writing the data image to a stream in jpeg format.
    /// </summary>
    public class JpegEncoder2 : IImageEncoder
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

            if (imageWidth > 65535 || imageHeight > 65535)
            {
                throw new ImageFormatException("Image dimensions exceed maximum allowable bounds of 65535px.");
            }

            using (EndianBinaryWriter writer = new EndianBinaryWriter(new BigEndianBitConverter(), stream))
            {
                this.WriteApplicationHeader(image, writer);
                this.WriteDct(writer);
            }
        }

        /// <summary>
        /// Writes the application header containing the Jfif identifier plus extra data.
        /// </summary>
        /// <param name="image">The image to encode from.</param>
        /// <param name="writer">The writer to write to the stream.</param>
        private void WriteApplicationHeader(ImageBase image, EndianBinaryWriter writer)
        {
            double densityX = ((Image)image).HorizontalResolution;
            double densityY = ((Image)image).VerticalResolution;

            // Write the start of image marker. Markers are always prefixed with with 0xff.
            writer.Write(new[] { JpegConstants.Markers.XFF, JpegConstants.Markers.SOI });

            // Write the jfif headers
            byte[] jfif = {
                    0xff,
                    JpegConstants.Markers.APP0, // Application Marker
                    0x00,
                    0x10,
                    0x4a, // J
                    0x46, // F
                    0x49, // I
                    0x46, // F
                    0x00, // = "JFIF",'\0'
                    0x01, // versionhi
                    0x01, // versionlo
                    0x01, // xyunits as dpi
            };

            byte[] thumbnail = {
                    0x00, // thumbnwidth
                    0x00  // thumbnheight
            };

            writer.Write(jfif);
            writer.Write((short)densityX);
            writer.Write((short)densityY);
            writer.Write(thumbnail);
        }

        /// <summary>
        /// Writes the descrete cosine transform tables.
        /// </summary>
        /// <param name="writer">The writer to write to the stream.</param>
        private void WriteDct(EndianBinaryWriter writer)
        {
            byte[] dqt = new byte[134];

            // Write the define quantization table marker. Markers are always prefixed with with 0xff.
            dqt[0] = JpegConstants.Markers.XFF;
            dqt[0] = JpegConstants.Markers.DQT;
            dqt[0] = 0x00;
            dqt[0] = 0x84; // Length

            FDCT fdct = new FDCT(this.quality);

            int offset = 4;
            for (int i = 0; i < 2; i++)
            {
                 dqt[offset++] = (byte)i;

                // TODO: Avoid allocation.
                int[] tempArray = fdct.Quantum[i];

                for (int j = 0; j < 64; j++)
                {
                    dqt[offset++] = (byte)tempArray[ZigZag.ZigZagMap[j]];
                }
            }

            writer.Write(dqt);
        }
    }
}
