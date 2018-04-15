// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.Memory;
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
        private readonly BmpBitsPerPixel bitsPerPixel;

        private readonly MemoryManager memoryManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="BmpEncoderCore"/> class.
        /// </summary>
        /// <param name="options">The encoder options</param>
        /// <param name="memoryManager">The memory manager</param>
        public BmpEncoderCore(IBmpEncoderOptions options, MemoryManager memoryManager)
        {
            this.memoryManager = memoryManager;
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

            var infoHeader = new BmpInfoHeader
            {
                HeaderSize = BmpInfoHeader.BitmapInfoHeaderSize,
                Height = image.Height,
                Width = image.Width,
                BitsPerPixel = bpp,
                Planes = 1,
                ImageSize = image.Height * bytesPerLine,
                ClrUsed = 0,
                ClrImportant = 0
            };

            var fileHeader = new BmpFileHeader(
                type: 19778, // BM
                offset: 54,
                reserved: 0,
                fileSize: 54 + infoHeader.ImageSize);

            byte[] buffer = new byte[40]; // TODO: stackalloc

            fileHeader.WriteTo(buffer);

            stream.Write(buffer, 0, BmpFileHeader.Size);

            infoHeader.WriteTo(buffer);

            stream.Write(buffer, 0, 40);

            this.WriteImage(stream, image.Frames.RootFrame);

            stream.Flush();
        }

        /// <summary>
        /// Writes the pixel data to the binary stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> to write to.</param>
        /// <param name="image">
        /// The <see cref="ImageFrame{TPixel}"/> containing pixel data.
        /// </param>
        private void WriteImage<TPixel>(Stream stream, ImageFrame<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            using (PixelAccessor<TPixel> pixels = image.Lock())
            {
                switch (this.bitsPerPixel)
                {
                    case BmpBitsPerPixel.Pixel32:
                        this.Write32Bit(stream, pixels);
                        break;

                    case BmpBitsPerPixel.Pixel24:
                        this.Write24Bit(stream, pixels);
                        break;
                }
            }
        }

        private IManagedByteBuffer AllocateRow(int width, int bytesPerPixel)
        {
            return this.memoryManager.AllocatePaddedPixelRowBuffer(width, bytesPerPixel, this.padding);
        }

        /// <summary>
        /// Writes the 32bit color palette to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> to write to.</param>
        /// <param name="pixels">The <see cref="PixelAccessor{TPixel}"/> containing pixel data.</param>
        private void Write32Bit<TPixel>(Stream stream, PixelAccessor<TPixel> pixels)
            where TPixel : struct, IPixel<TPixel>
        {
            using (IManagedByteBuffer row = this.AllocateRow(pixels.Width, 4))
            {
                for (int y = pixels.Height - 1; y >= 0; y--)
                {
                    Span<TPixel> pixelSpan = pixels.GetRowSpan(y);
                    PixelOperations<TPixel>.Instance.ToBgra32Bytes(pixelSpan, row.Span, pixelSpan.Length);
                    stream.Write(row.Array, 0, row.Length());
                }
            }
        }

        /// <summary>
        /// Writes the 24bit color palette to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> to write to.</param>
        /// <param name="pixels">The <see cref="PixelAccessor{TPixel}"/> containing pixel data.</param>
        private void Write24Bit<TPixel>(Stream stream, PixelAccessor<TPixel> pixels)
            where TPixel : struct, IPixel<TPixel>
        {
            using (IManagedByteBuffer row = this.AllocateRow(pixels.Width, 3))
            {
                for (int y = pixels.Height - 1; y >= 0; y--)
                {
                    Span<TPixel> pixelSpan = pixels.GetRowSpan(y);
                    PixelOperations<TPixel>.Instance.ToBgr24Bytes(pixelSpan, row.Span, pixelSpan.Length);
                    stream.Write(row.Array, 0, row.Length());
                }
            }
        }
    }
}
