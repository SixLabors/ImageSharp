// --------------------------------------------------------------------------------------------------------------------
// <copyright file="BmpEncoder.cs" company="James South">
//   Copyright © James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Image encoder for writing an image to a stream as a Windows bitmap.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Formats
{
    using System;
    using System.IO;

    /// <summary>
    /// Image encoder for writing an image to a stream as a Windows bitmap.
    /// </summary>
    /// <remarks>The encoder can currently only write 24-bit rgb images to streams.</remarks>
    public class BmpEncoder : IImageEncoder
    {
        /// <summary>
        /// Gets or sets the quality of output for images.
        /// </summary>
        /// <remarks>Png is a lossless format so this is not used in this encoder.</remarks>
        public int Quality { get; set; }

        /// <summary>
        /// Gets the default file extension for this encoder.
        /// </summary>
        public string Extension
        {
            get { return "BMP"; }
        }

        /// <summary>
        /// Returns a value indicating whether the <see cref="IImageEncoder"/> supports the specified
        /// file header.
        /// </summary>
        /// <param name="extension">The <see cref="string"/> containing the file extension.</param>
        /// <returns>
        /// True if the decoder supports the file extension; otherwise, false.
        /// </returns>
        public bool IsSupportedFileExtension(string extension)
        {
            Guard.NotNullOrEmpty(extension, "extension");

            extension = extension.StartsWith(".") ? extension.Substring(1) : extension;

            return extension.Equals("BMP", StringComparison.OrdinalIgnoreCase)
                || extension.Equals("DIP", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="ImageBase"/>.
        /// </summary>
        /// <param name="image">The <see cref="ImageBase"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        public void Encode(ImageBase image, Stream stream)
        {
            Guard.NotNull(image, "image");
            Guard.NotNull(stream, "stream");

            int rowWidth = image.Width;

            int amount = (image.Width * 3) % 4;
            if (amount != 0)
            {
                rowWidth += 4 - amount;
            }

            BinaryWriter writer = new BinaryWriter(stream);

            BmpFileHeader fileHeader = new BmpFileHeader
            {
                Type = 19778, // BM
                Offset = 54,
                FileSize = 54 + (image.Height * rowWidth * 3)
            };

            WriteHeader(writer, fileHeader);

            BmpInfoHeader infoHeader = new BmpInfoHeader
            {
                HeaderSize = 40,
                Height = image.Height,
                Width = image.Width,
                BitsPerPixel = 24,
                Planes = 1,
                Compression = BmpCompression.RGB,
                ImageSize = image.Height * rowWidth * 3,
                ClrUsed = 0,
                ClrImportant = 0
            };

            WriteInfo(writer, infoHeader);

            WriteImage(writer, image);

            writer.Flush();
        }

        /// <summary>
        /// Writes the pixel data to the binary stream.
        /// </summary>
        /// <param name="writer">
        /// The <see cref="BinaryWriter"/> containing the stream to write to.
        /// </param>
        /// <param name="image">
        /// The <see cref="ImageBase"/> containing pixel data.
        /// </param>
        private static void WriteImage(BinaryWriter writer, ImageBase image)
        {
            int amount = (image.Width * 3) % 4;
            if (amount != 0)
            {
                amount = 4 - amount;
            }

            byte[] data = image.Pixels;

            for (int y = image.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    int offset = ((y * image.Width) + x) * 4;

                    writer.Write(data[offset + 0]);
                    writer.Write(data[offset + 1]);
                    writer.Write(data[offset + 2]);
                }

                for (int i = 0; i < amount; i++)
                {
                    writer.Write((byte)0);
                }
            }
        }

        /// <summary>
        /// Writes the bitmap header data to the binary stream.
        /// </summary>
        /// <param name="writer">
        /// The <see cref="BinaryWriter"/> containing the stream to write to.
        /// </param>
        /// <param name="fileHeader">
        /// The <see cref="BmpFileHeader"/> containing the header data.
        /// </param>
        private static void WriteHeader(BinaryWriter writer, BmpFileHeader fileHeader)
        {
            writer.Write(fileHeader.Type);
            writer.Write(fileHeader.FileSize);
            writer.Write(fileHeader.Reserved);
            writer.Write(fileHeader.Offset);
        }

        /// <summary>
        /// Writes the bitmap information to the binary stream.
        /// </summary>
        /// <param name="writer">
        /// The <see cref="BinaryWriter"/> containing the stream to write to.
        /// </param>
        /// <param name="infoHeader">
        /// The <see cref="BmpFileHeader"/> containing the detailed information about the image.
        /// </param>
        private static void WriteInfo(BinaryWriter writer, BmpInfoHeader infoHeader)
        {
            writer.Write(infoHeader.HeaderSize);
            writer.Write(infoHeader.Width);
            writer.Write(infoHeader.Height);
            writer.Write(infoHeader.Planes);
            writer.Write(infoHeader.BitsPerPixel);
            writer.Write((int)infoHeader.Compression);
            writer.Write(infoHeader.ImageSize);
            writer.Write(infoHeader.XPelsPerMeter);
            writer.Write(infoHeader.YPelsPerMeter);
            writer.Write(infoHeader.ClrUsed);
            writer.Write(infoHeader.ClrImportant);
        }
    }
}
