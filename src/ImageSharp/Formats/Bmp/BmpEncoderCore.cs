// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Image encoder for writing an image to a stream as a Windows bitmap.
    /// </summary>
    internal sealed class BmpEncoderCore
    {
        /// <summary>
        /// The amount to pad each row by.
        /// </summary>
        private int padding;

        /// <summary>
        /// Gets or sets the number of bits per pixel.
        /// </summary>
        private BmpBitsPerPixel bitsPerPixel;

        /// <summary>
        /// Initializes a new instance of the <see cref="BmpEncoderCore"/> class.
        /// </summary>
        /// <param name="options">The encoder options</param>
        public BmpEncoderCore(IBmpEncoderOptions options)
        {
            this.bitsPerPixel = options.BitsPerPixel;
        }

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="ImageFrame{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="ImageFrame{TPixel}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(image, nameof(image));
            Guard.NotNull(stream, nameof(stream));

            // Cast to int will get the bytes per pixel
            short bpp = (short)(8 * (int)this.bitsPerPixel);
            int bytesPerLine = 4 * (((image.Width * bpp) + 31) / 32);
            this.padding = bytesPerLine - (image.Width * (int)this.bitsPerPixel);

            // Do not use IDisposable pattern here as we want to preserve the stream.
            var writer = new EndianBinaryWriter(Endianness.LittleEndian, stream);

            var infoHeader = new BmpInfoHeader
            {
                HeaderSize = sizeof(uint),
                Height = image.Height,
                Width = image.Width,
                BitsPerPixel = bpp,
                Planes = 1,
                ImageSize = image.Height * bytesPerLine,
                ClrUsed = 0,
                ClrImportant = 0
            };

            uint offset = (uint)(BmpFileHeader.Size + infoHeader.HeaderSize);
            var fileHeader = new BmpFileHeader
            {
                Type = 0x4D42, // BM
                FileSize = offset + (uint)infoHeader.ImageSize,
                Reserved1 = 0,
                Reserved2 = 0,
                Offset = offset
            };

            WriteHeader(writer, fileHeader);
            this.WriteInfo(writer, infoHeader);
            this.WriteImage(writer, image.Frames.RootFrame);

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
            writer.Write(fileHeader.Reserved1);
            writer.Write(fileHeader.Reserved2);
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
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="writer">The <see cref="EndianBinaryWriter"/> containing the stream to write to.</param>
        /// <param name="image">
        /// The <see cref="ImageFrame{TPixel}"/> containing pixel data.
        /// </param>
        private void WriteImage<TPixel>(EndianBinaryWriter writer, ImageFrame<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            using (PixelAccessor<TPixel> pixels = image.Lock())
            {
                switch (this.bitsPerPixel)
                {
                    case BmpBitsPerPixel.RGB32:
                        this.Write32Bit(writer, pixels);
                        break;

                    case BmpBitsPerPixel.RGB24:
                        this.Write24Bit(writer, pixels);
                        break;
                }
            }
        }

        /// <summary>
        /// Writes the 32bit color palette to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="writer">The <see cref="EndianBinaryWriter"/> containing the stream to write to.</param>
        /// <param name="pixels">The <see cref="PixelAccessor{TPixel}"/> containing pixel data.</param>
        private void Write32Bit<TPixel>(EndianBinaryWriter writer, PixelAccessor<TPixel> pixels)
            where TPixel : struct, IPixel<TPixel>
        {
            using (var row = new PixelArea<TPixel>(pixels.Width, ComponentOrder.Zyxw, this.padding))
            {
                for (int y = pixels.Height - 1; y >= 0; y--)
                {
                    pixels.CopyTo(row, y);
                    writer.Write(row.Bytes, 0, row.Length);
                }
            }
        }

        /// <summary>
        /// Writes the 24bit color palette to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="writer">The <see cref="EndianBinaryWriter"/> containing the stream to write to.</param>
        /// <param name="pixels">The <see cref="PixelAccessor{TPixel}"/> containing pixel data.</param>
        private void Write24Bit<TPixel>(EndianBinaryWriter writer, PixelAccessor<TPixel> pixels)
            where TPixel : struct, IPixel<TPixel>
        {
            using (var row = new PixelArea<TPixel>(pixels.Width, ComponentOrder.Zyx, this.padding))
            {
                for (int y = pixels.Height - 1; y >= 0; y--)
                {
                    pixels.CopyTo(row, y);
                    writer.Write(row.Bytes, 0, row.Length);
                }
            }
        }
    }
}
