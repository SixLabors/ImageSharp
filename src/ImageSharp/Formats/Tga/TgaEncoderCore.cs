// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Formats.Tga
{
    /// <summary>
    /// Image encoder for writing an image to a stream as a truevision targa image.
    /// </summary>
    internal sealed class TgaEncoderCore
    {
        /// <summary>
        /// Used for allocating memory during processing operations.
        /// </summary>
        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// The global configuration.
        /// </summary>
        private Configuration configuration;

        /// <summary>
        /// The color depth, in number of bits per pixel.
        /// </summary>
        private TgaBitsPerPixel? bitsPerPixel;

        /// <summary>
        /// Initializes a new instance of the <see cref="TgaEncoderCore"/> class.
        /// </summary>
        /// <param name="options">The encoder options.</param>
        /// <param name="memoryAllocator">The memory manager.</param>
        public TgaEncoderCore(ITgaEncoderOptions options, MemoryAllocator memoryAllocator)
        {
            this.memoryAllocator = memoryAllocator;
            this.bitsPerPixel = options.BitsPerPixel;
        }

        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(image, nameof(image));
            Guard.NotNull(stream, nameof(stream));

            this.configuration = image.GetConfiguration();
            ImageMetadata metadata = image.Metadata;
            TgaMetadata tgaMetadata = metadata.GetFormatMetadata(TgaFormat.Instance);
            this.bitsPerPixel = this.bitsPerPixel ?? tgaMetadata.BitsPerPixel;

            var fileHeader = new TgaFileHeader(
                idLength: 0,
                colorMapType: 0,
                imageType: TgaImageType.TrueColor,
                cMapStart: 0,
                cMapLength: 0,
                cMapDepth: 0,
                xOffset: 0,
                yOffset: 0,
                width: (short)image.Width,
                height: (short)image.Height,
                pixelDepth: (byte)this.bitsPerPixel.Value,
                imageDescriptor: 0);

#if NETCOREAPP2_1
            Span<byte> buffer = stackalloc byte[TgaFileHeader.Size];
#else
            var buffer = new byte[TgaFileHeader.Size];
#endif
            fileHeader.WriteTo(buffer);

            stream.Write(buffer, 0, TgaFileHeader.Size);

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
            Buffer2D<TPixel> pixels = image.PixelBuffer;
            switch (this.bitsPerPixel)
            {
                case TgaBitsPerPixel.Pixel24:
                    this.Write24Bit(stream, pixels);
                    break;
            }
        }

        private IManagedByteBuffer AllocateRow(int width, int bytesPerPixel) => this.memoryAllocator.AllocatePaddedPixelRowBuffer(width, bytesPerPixel, 0);

        /// <summary>
        /// Writes the 24bit pixels uncompressed to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> to write to.</param>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> containing pixel data.</param>
        private void Write24Bit<TPixel>(Stream stream, Buffer2D<TPixel> pixels)
            where TPixel : struct, IPixel<TPixel>
        {
            using (IManagedByteBuffer row = this.AllocateRow(pixels.Width, 3))
            {
                for (int y = pixels.Height - 1; y >= 0; y--)
                {
                    Span<TPixel> pixelSpan = pixels.GetRowSpan(y);
                    PixelOperations<TPixel>.Instance.ToBgr24Bytes(
                        this.configuration,
                        pixelSpan,
                        row.GetSpan(),
                        pixelSpan.Length);
                    stream.Write(row.Array, 0, row.Length());
                }
            }
        }
    }
}
