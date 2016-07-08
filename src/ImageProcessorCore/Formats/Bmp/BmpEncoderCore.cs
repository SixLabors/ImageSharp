// <copyright file="BmpEncoderCore.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    using System;
    using System.IO;

    using IO;

    /// <summary>
    /// Image encoder for writing an image to a stream as a Windows bitmap.
    /// </summary>
    /// <remarks>The encoder can currently only write 24-bit rgb images to streams.</remarks>
    internal sealed class BmpEncoderCore
    {
        /// <summary>
        /// The number of bits per pixel.
        /// </summary>
        private BmpBitsPerPixel bmpBitsPerPixel;

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="ImageBase{TPackedVector}"/>.
        /// </summary>
        /// <typeparam name="TPackedVector">The type of pixels contained within the image.</typeparam>
        /// <param name="image">The <see cref="ImageBase{TPackedVector}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        /// <param name="bitsPerPixel">The <see cref="BmpBitsPerPixel"/></param>
        public void Encode<TPackedVector>(ImageBase<TPackedVector> image, Stream stream, BmpBitsPerPixel bitsPerPixel)
            where TPackedVector : IPackedVector, new()
        {
            Guard.NotNull(image, nameof(image));
            Guard.NotNull(stream, nameof(stream));

            this.bmpBitsPerPixel = bitsPerPixel;

            int rowWidth = image.Width;

            // TODO: Check this for varying file formats.
            int amount = (image.Width * (int)this.bmpBitsPerPixel) % 4;
            if (amount != 0)
            {
                rowWidth += 4 - amount;
            }

            // Do not use IDisposable pattern here as we want to preserve the stream. 
            EndianBinaryWriter writer = new EndianBinaryWriter(EndianBitConverter.Little, stream);

            int bpp = (int)this.bmpBitsPerPixel;

            BmpFileHeader fileHeader = new BmpFileHeader
            {
                Type = 19778, // BM
                Offset = 54,
                FileSize = 54 + (image.Height * rowWidth * bpp)
            };

            BmpInfoHeader infoHeader = new BmpInfoHeader
            {
                HeaderSize = 40,
                Height = image.Height,
                Width = image.Width,
                BitsPerPixel = (short)(8 * bpp),
                Planes = 1,
                ImageSize = image.Height * rowWidth * bpp,
                ClrUsed = 0,
                ClrImportant = 0
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
        /// <typeparam name="TPackedVector">The type of pixels contained within the image.</typeparam>
        /// <param name="writer">
        /// The <see cref="EndianBinaryWriter"/> containing the stream to write to.
        /// </param>
        /// <param name="image">
        /// The <see cref="ImageBase{TPackedVector}"/> containing pixel data.
        /// </param>
        private void WriteImage<TPackedVector>(EndianBinaryWriter writer, ImageBase<TPackedVector> image)
            where TPackedVector : IPackedVector, new()
        {
            // TODO: Add more compression formats.
            int amount = (image.Width * (int)this.bmpBitsPerPixel) % 4;
            if (amount != 0)
            {
                amount = 4 - amount;
            }

            using (IPixelAccessor<TPackedVector> pixels = image.Lock())
            {
                switch (this.bmpBitsPerPixel)
                {
                    case BmpBitsPerPixel.Pixel32:
                        this.Write32bit(writer, pixels, amount);
                        break;

                    case BmpBitsPerPixel.Pixel24:
                        this.Write24bit(writer, pixels, amount);
                        break;
                }
            }
        }

        /// <summary>
        /// Writes the 32bit color palette to the stream.
        /// </summary>
        /// <typeparam name="TPackedVector">The type of pixels contained within the image.</typeparam>
        /// <param name="writer">The <see cref="EndianBinaryWriter"/> containing the stream to write to.</param>
        /// <param name="pixels">The <see cref="IPixelAccessor"/> containing pixel data.</param>
        /// <param name="amount">The amount to pad each row by.</param>
        private void Write32bit<TPackedVector>(EndianBinaryWriter writer, IPixelAccessor<TPackedVector> pixels, int amount)
            where TPackedVector : IPackedVector, new()
        {
            for (int y = pixels.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < pixels.Width; x++)
                {
                    // Convert back to b-> g-> r-> a order.
                    byte[] bytes = pixels[x, y].ToBytes();
                    writer.Write(bytes);
                }

                // Pad
                for (int i = 0; i < amount; i++)
                {
                    writer.Write((byte)0);
                }
            }
        }

        /// <summary>
        /// Writes the 24bit color palette to the stream.
        /// </summary>
        /// <param name="writer">The <see cref="EndianBinaryWriter"/> containing the stream to write to.</param>
        /// <param name="pixels">The <see cref="IPixelAccessor"/> containing pixel data.</param>
        /// <param name="amount">The amount to pad each row by.</param>
        private void Write24bit<TPackedVector>(EndianBinaryWriter writer, IPixelAccessor<TPackedVector> pixels, int amount)
            where TPackedVector : IPackedVector, new()
        {
            for (int y = pixels.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < pixels.Width; x++)
                {
                    // Convert back to b-> g-> r-> a order.
                    byte[] bytes = pixels[x, y].ToBytes();
                    writer.Write(new[] { bytes[0], bytes[1], bytes[2] });
                }

                // Pad
                for (int i = 0; i < amount; i++)
                {
                    writer.Write((byte)0);
                }
            }
        }
    }
}
