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
    /// <see href="http://lad.dsc.ufcg.edu.br/multimidia/jpegmarker.pdf"/>
    /// <see href="https://digitalexploration.wordpress.com/2009/07/29/jpeg-huffman-tables/"/>
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
            ushort max = JpegConstants.MaxLength;

            if (imageWidth > max || imageHeight > max)
            {
                throw new ImageFormatException($"Image dimensions exceed maximum allowable bounds of {max}px.");
            }

            using (EndianBinaryWriter writer = new EndianBinaryWriter(new BigEndianBitConverter(), stream))
            {
                this.WriteApplicationHeader(image, writer);
                this.WriteDct(writer);
                this.WriteSof(image, writer);
                this.WriteDht(writer);
                this.WriteSos(writer);
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
            dqt[1] = JpegConstants.Markers.DQT;
            dqt[2] = 0x00;
            dqt[3] = 0x84; // Length

            FDCT fdct = new FDCT(this.quality);

            int offset = 4;
            for (int i = 0; i < 2; i++)
            {
                dqt[offset++] = (byte)i;

                // TODO: Perf. Split and avoid allocation.
                int[] tempArray = fdct.Quantum[i];

                for (int j = 0; j < 64; j++)
                {
                    dqt[offset++] = (byte)tempArray[ZigZag.ZigZagMap[j]];
                }
            }

            writer.Write(dqt);
        }

        /// <summary>
        /// Writes the start of frame for basline jpeg.
        /// </summary>
        /// <param name="image">The image to encode from.</param>
        /// <param name="writer">The writer to write to the stream.</param>
        private void WriteSof(ImageBase image, EndianBinaryWriter writer)
        {
            byte[] sof = new byte[19];
            sof[0] = JpegConstants.Markers.XFF;
            sof[1] = JpegConstants.Markers.SOF0;
            sof[2] = 0x00;
            sof[3] = 17; // Length (high byte, low byte), 8 + components * 3 - YCbCr just now.
            sof[4] = 8; // Data Precision. 8 for now, 12 and 16 bit jpegs not supported
            sof[5] = (byte)((image.Height >> 8) & 0xFF);
            sof[6] = (byte)(image.Height & 0xFF); // (2 bytes, Hi-Lo), must be > 0 if DNL not supported
            sof[7] = (byte)((image.Width >> 8) & 0xFF);
            sof[8] = (byte)(image.Width & 0xFF); // (2 bytes, Hi-Lo), must be > 0 if DNL not supported
            sof[9] = 3; // Number of components (1 byte), usually 1 = grey scaled, 3 = color YCbCr or YIQ, 4 = color CMYK)

            byte[] componentIds = {
                JpegConstants.Components.Y,
                JpegConstants.Components.Cb,
                JpegConstants.Components.Cr
            };

            // TODO: One day make this settable.
            byte[] horizontalFactors = JpegConstants.ChromaFourFourFourHorizontal;
            byte[] verticalFactors = JpegConstants.ChromaFourFourFourVertical;

            byte[] quantizationTableNumber = { 0, 1, 1 };

            int offset = 10;
            for (int i = 0; i < sof[9]; i++)
            {
                sof[offset++] = componentIds[i];
                sof[offset++] = (byte)((horizontalFactors[i] << 4) + verticalFactors[i]);
                sof[offset++] = quantizationTableNumber[i];
            }

            writer.Write(sof);
        }

        /// <summary>
        /// Writes the define huffman tables section.
        /// </summary>
        /// <param name="writer">The writer to write to the stream.</param>
        private void WriteDht(EndianBinaryWriter writer)
        {
            // Marker
            writer.Write(new[] { JpegConstants.Markers.XFF, JpegConstants.Markers.DHT });
            writer.Write((short)0x01A2); // Table definition length.

            JpegHuffmanTable dcLuminanceTable = JpegHuffmanTable.StandardDcLuminance;
            JpegHuffmanTable dcChrominanceTable = JpegHuffmanTable.StandardDcChrominance;
            JpegHuffmanTable acLuminanceTable = JpegHuffmanTable.StandardAcLuminance;
            JpegHuffmanTable acChrominanceTable = JpegHuffmanTable.StandardAcChrominance;

            // For each Huffman Table
            // DC Luminance
            byte[] dcLuminance = new byte[29];

            // DC table identifier. 
            // The 4 high bits determine the class: 0=DC table, 1=Ac table. 
            // The 4 low bits specify the table identifier (0, 1, 2, or 3).
            dcLuminance[0] = 0x00; // TODO: Move to constants
            int offset = 1;

            foreach (short i in dcLuminanceTable.Codes)
            {
                dcLuminance[offset++] = (byte)i;
            }

            foreach (short i in dcLuminanceTable.Values)
            {
                dcLuminance[offset++] = (byte)i;
            }

            writer.Write(dcLuminance);

            // AC Luminance
            byte[] acLuminance = new byte[179];
            acLuminance[0] = 0x10; // AC table identifier. 
            offset = 1;

            foreach (short i in acLuminanceTable.Codes)
            {
                acLuminance[offset++] = (byte)i;
            }

            foreach (short i in acLuminanceTable.Values)
            {
                acLuminance[offset++] = (byte)i;
            }

            writer.Write(acLuminance);

            // DC Chrominance
            byte[] dcChrominance = new byte[29];
            dcChrominance[0] = 0x01; // TODO: Move to constants
            offset = 1;

            foreach (short i in dcChrominanceTable.Codes)
            {
                dcChrominance[offset++] = (byte)i;
            }

            foreach (short i in dcChrominanceTable.Values)
            {
                dcChrominance[offset++] = (byte)i;
            }

            writer.Write(dcChrominance);

            // AC Chrominance
            byte[] acChrominance = new byte[179];
            acChrominance[0] = 0x11; // TODO: Move to constants
            offset = 1;

            foreach (short i in acChrominanceTable.Codes)
            {
                acChrominance[offset++] = (byte)i;
            }

            foreach (short i in acChrominanceTable.Values)
            {
                acChrominance[offset++] = (byte)i;
            }

            writer.Write(acChrominance);
        }

        /// <summary>
        /// Writes the Scan header structure
        /// </summary>
        /// <param name="writer">The writer to write to the stream.</param>
        private void WriteSos(EndianBinaryWriter writer)
        {
            // Marker
            writer.Write(new[] { JpegConstants.Markers.XFF, JpegConstants.Markers.SOS });

            // Length (high byte, low byte), must be 6 + 2 * (number of components in scan)
            writer.Write((short)0xc); // 12

            byte[] sos = {
                3, // Number of components in a scan, usually 1 or 3
                1, // Component Id Y
                0, // DC/AC Huffman table 
                2, // Component Id Cb
                0x11, // DC/AC Huffman table 
                3, // Component Id Cr
                0x11, // DC/AC Huffman table 
                0, // Ss - Start of spectral selection.
                0x3f, // Se - Ens of spectral selection.
                0 // Ah + Ah (Successive approximation bit position high + low)
            };

            writer.Write(sos);
        }
    }
}
