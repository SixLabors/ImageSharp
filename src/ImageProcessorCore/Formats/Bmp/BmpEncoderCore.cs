// <copyright file="BmpEncoderCore.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    using System.IO;

    using IO;

    /// <summary>
    /// Image encoder for writing an image to a stream as a Windows bitmap.
    /// </summary>
    internal sealed class BmpEncoderCore
    {
        /// <summary>
        /// The number of bits per pixel.
        /// </summary>
        private BmpBitsPerPixel bmpBitsPerPixel;

        /// <summary>
        /// The amount to pad each row by.
        /// </summary>
        private int padding;

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="ImageBase{TColor, TPacked}"/>.
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>long, float.</example></typeparam>
        /// <param name="image">The <see cref="ImageBase{TColor, TPacked}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        /// <param name="bitsPerPixel">The <see cref="BmpBitsPerPixel"/></param>
        public void Encode<TColor, TPacked>(ImageBase<TColor, TPacked> image, Stream stream, BmpBitsPerPixel bitsPerPixel)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            Guard.NotNull(image, nameof(image));
            Guard.NotNull(stream, nameof(stream));

            this.bmpBitsPerPixel = bitsPerPixel;

            // Cast to int will get the bytes per pixel
            short bpp = (short)(8 * (int)bitsPerPixel);
            int bytesPerLine = 4 * (((image.Width * bpp) + 31) / 32);
            this.padding = bytesPerLine - (image.Width * (int)bitsPerPixel);

            // Do not use IDisposable pattern here as we want to preserve the stream.
            EndianBinaryWriter writer = new EndianBinaryWriter(EndianBitConverter.Little, stream);

            BmpInfoHeader infoHeader = new BmpInfoHeader
            {
                HeaderSize = BmpInfoHeader.Size,
                Height = image.Height,
                Width = image.Width,
                BitsPerPixel = bpp,
                Planes = 1,
                ImageSize = image.Height * bytesPerLine,
                ClrUsed = 0,
                ClrImportant = 0
            };

            BmpFileHeader fileHeader = new BmpFileHeader
            {
                Type = 19778, // BM
                Offset = 54,
                FileSize = 54 + infoHeader.ImageSize
            };

            WriteHeader(writer, fileHeader);
            this.WriteInfo(writer, infoHeader);
            this.WriteImage(writer, image);

            writer.Flush();
        }

        /// <summary>
        /// Writes the bitmap header data to the binary stream.
        /// </summary>
        /// <param name="writer">
        /// The <see cref="EndianBinaryWriter"/> containing the stream to write to.
        /// </param>
        /// <param name="fileHeader">
        /// The <see cref="BmpFileHeader"/> containing the header data.
        /// </param>
        private static void WriteHeader(EndianBinaryWriter writer, BmpFileHeader fileHeader)
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
        /// The <see cref="EndianBinaryWriter"/> containing the stream to write to.
        /// </param>
        /// <param name="infoHeader">
        /// The <see cref="BmpFileHeader"/> containing the detailed information about the image.
        /// </param>
        private void WriteInfo(EndianBinaryWriter writer, BmpInfoHeader infoHeader)
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

        /// <summary>
        /// Writes the pixel data to the binary stream.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="writer">The <see cref="EndianBinaryWriter"/> containing the stream to write to.</param>
        /// <param name="image">
        /// The <see cref="ImageBase{TColor, TPacked}"/> containing pixel data.
        /// </param>
        private void WriteImage<TColor, TPacked>(EndianBinaryWriter writer, ImageBase<TColor, TPacked> image)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            using (PixelAccessor<TColor, TPacked> pixels = image.Lock())
            {
                switch (this.bmpBitsPerPixel)
                {
                    case BmpBitsPerPixel.Pixel32:
                        this.Write32Bit<TColor, TPacked>(writer, pixels);
                        break;

                    case BmpBitsPerPixel.Pixel24:
                        this.Write24Bit<TColor, TPacked>(writer, pixels);
                        break;
                }
            }
        }

        /// <summary>
        /// Writes the 32bit color palette to the stream.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="writer">The <see cref="EndianBinaryWriter"/> containing the stream to write to.</param>
        /// <param name="pixels">The <see cref="PixelAccessor{TColor,TPacked}"/> containing pixel data.</param>
        private void Write32Bit<TColor, TPacked>(EndianBinaryWriter writer, PixelAccessor<TColor, TPacked> pixels)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            for (int y = pixels.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < pixels.Width; x++)
                {
                    // Convert back to b-> g-> r-> a order.
                    byte[] bytes = pixels[x, y].ToBytes();
                    writer.Write(new[] { bytes[2], bytes[1], bytes[0], bytes[3] });
                }

                // Pad
                for (int i = 0; i < this.padding; i++)
                {
                    writer.Write((byte)0);
                }
            }
        }

        /// <summary>
        /// Writes the 24bit color palette to the stream.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="writer">The <see cref="EndianBinaryWriter"/> containing the stream to write to.</param>
        /// <param name="pixels">The <see cref="PixelAccessor{TColor,TPacked}"/> containing pixel data.</param>
        private void Write24Bit<TColor, TPacked>(EndianBinaryWriter writer, PixelAccessor<TColor, TPacked> pixels)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            for (int y = pixels.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < pixels.Width; x++)
                {
                    // Convert back to b-> g-> r order.
                    byte[] bytes = pixels[x, y].ToBytes();
                    writer.Write(new[] { bytes[2], bytes[1], bytes[0] });
                }

                // Pad
                for (int i = 0; i < this.padding; i++)
                {
                    writer.Write((byte)0);
                }
            }
        }
    }
}
