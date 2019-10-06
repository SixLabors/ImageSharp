// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Formats.Tga
{
    internal sealed class TgaDecoderCore
    {
        /// <summary>
        /// The metadata.
        /// </summary>
        private ImageMetadata metadata;

        /// <summary>
        /// The file header containing general information about the image.
        /// </summary>
        private TgaFileHeader fileHeader;

        /// <summary>
        /// The global configuration.
        /// </summary>
        private readonly Configuration configuration;

        /// <summary>
        /// Used for allocating memory during processing operations.
        /// </summary>
        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// The stream to decode from.
        /// </summary>
        private Stream currentStream;

        /// <summary>
        /// The bitmap decoder options.
        /// </summary>
        private readonly ITgaDecoderOptions options;

        public TgaDecoderCore(Configuration configuration, ITgaDecoderOptions options)
        {
            this.configuration = configuration;
            this.memoryAllocator = configuration.MemoryAllocator;
            this.options = options;
        }

        /// <summary>
        /// Decodes the image from the specified stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="stream">The stream, where the image should be decoded from. Cannot be null.</param>
        /// <exception cref="System.ArgumentNullException">
        ///    <para><paramref name="stream"/> is null.</para>
        /// </exception>
        /// <returns>The decoded image.</returns>
        public Image<TPixel> Decode<TPixel>(Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            try
            {
                this.ReadFileHeader(stream);

                var image = new Image<TPixel>(this.configuration, this.fileHeader.Width, this.fileHeader.Height, this.metadata);
                Buffer2D<TPixel> pixels = image.GetRootFramePixelBuffer();

                switch (this.fileHeader.PixelDepth)
                {
                    case 8:
                        this.ReadMonoChrome(pixels);
                        break;

                    case 16:
                        this.ReadBgra16(pixels);
                        break;

                    case 24:
                        this.ReadBgr24(pixels);
                        break;

                    case 32:
                        this.ReadBgra32(pixels);
                        break;

                    default:
                        TgaThrowHelper.ThrowNotSupportedException("Does not support this kind of tga files.");
                        break;
                }

                return image;
            }
            catch (IndexOutOfRangeException e)
            {
                throw new ImageFormatException("TGA image does not have a valid format.", e);
            }
        }

        private void ReadMonoChrome<TPixel>(Buffer2D<TPixel> pixels)
            where TPixel : struct, IPixel<TPixel>
        {
            using (IManagedByteBuffer row = this.memoryAllocator.AllocatePaddedPixelRowBuffer(this.fileHeader.Width, 1, 0))
            {
                for (int y = 0; y < this.fileHeader.Height; y++)
                {
                    this.currentStream.Read(row);
                    Span<TPixel> pixelSpan = pixels.GetRowSpan(y);
                    PixelOperations<TPixel>.Instance.FromGray8Bytes(
                        this.configuration,
                        row.GetSpan(),
                        pixelSpan,
                        this.fileHeader.Width);
                }
            }
        }

        private void ReadBgra16<TPixel>(Buffer2D<TPixel> pixels)
            where TPixel : struct, IPixel<TPixel>
        {
            using (IManagedByteBuffer row = this.memoryAllocator.AllocatePaddedPixelRowBuffer(this.fileHeader.Width, 2, 0))
            {
                for (int y = 0; y < this.fileHeader.Height; y++)
                {
                    this.currentStream.Read(row);
                    Span<TPixel> pixelSpan = pixels.GetRowSpan(y);
                    PixelOperations<TPixel>.Instance.FromBgra5551Bytes(
                        this.configuration,
                        row.GetSpan(),
                        pixelSpan,
                        this.fileHeader.Width);
                }
            }
        }

        private void ReadBgr24<TPixel>(Buffer2D<TPixel> pixels)
            where TPixel : struct, IPixel<TPixel>
        {
            using (IManagedByteBuffer row = this.memoryAllocator.AllocatePaddedPixelRowBuffer(this.fileHeader.Width, 3, 0))
            {
                for (int y = 0; y < this.fileHeader.Height; y++)
                {
                    this.currentStream.Read(row);
                    Span<TPixel> pixelSpan = pixels.GetRowSpan(y);
                    PixelOperations<TPixel>.Instance.FromBgr24Bytes(
                        this.configuration,
                        row.GetSpan(),
                        pixelSpan,
                        this.fileHeader.Width);
                }
            }
        }

        private void ReadBgra32<TPixel>(Buffer2D<TPixel> pixels)
            where TPixel : struct, IPixel<TPixel>
        {
            using (IManagedByteBuffer row = this.memoryAllocator.AllocatePaddedPixelRowBuffer(this.fileHeader.Width, 4, 0))
            {
                for (int y = 0; y < this.fileHeader.Height; y++)
                {
                    this.currentStream.Read(row);
                    Span<TPixel> pixelSpan = pixels.GetRowSpan(y);
                    PixelOperations<TPixel>.Instance.FromBgra32Bytes(
                        this.configuration,
                        row.GetSpan(),
                        pixelSpan,
                        this.fileHeader.Width);
                }
            }
        }

        /// <summary>
        /// Reads the raw image information from the specified stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        public IImageInfo Identify(Stream stream)
        {
            this.ReadFileHeader(stream);
            return new ImageInfo(
                new PixelTypeInfo(this.fileHeader.PixelDepth),
                this.fileHeader.Width,
                this.fileHeader.Height,
                this.metadata);
        }

        /// <summary>
        /// Reads the tga file header from the stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        private void ReadFileHeader(Stream stream)
        {
            this.currentStream = stream;

#if NETCOREAPP2_1
            Span<byte> buffer = stackalloc byte[TgaFileHeader.Size];
#else
            var buffer = new byte[TgaFileHeader.Size];
#endif
            this.currentStream.Read(buffer, 0, TgaFileHeader.Size);
            this.fileHeader = TgaFileHeader.Parse(buffer);
        }
    }
}
