// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;

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
                        if (this.fileHeader.ImageType.IsRunLengthEncoded())
                        {
                            this.ReadRle24(pixels, this.fileHeader.Width, this.fileHeader.Height);
                        }
                        else
                        {
                            this.ReadBgr24(pixels);
                        }

                        break;

                    case 32:
                        if (this.fileHeader.ImageType.IsRunLengthEncoded())
                        {
                            this.ReadRle32(pixels, this.fileHeader.Width, this.fileHeader.Height);
                        }
                        else
                        {
                            this.ReadBgra32(pixels);
                        }

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
                    Span<TPixel> pixelSpan = pixels.GetRowSpan(this.fileHeader.Height - y - 1);
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
            using (IMemoryOwner<Bgra5551> bgraRow = this.memoryAllocator.Allocate<Bgra5551>(this.fileHeader.Width))
            {
                Span<Bgra5551> bgraRowSpan = bgraRow.GetSpan();
                long currentPosition = this.currentStream.Position;
                for (int y = 0; y < this.fileHeader.Height; y++)
                {
                    this.currentStream.Read(row);
                    Span<TPixel> pixelSpan = pixels.GetRowSpan(this.fileHeader.Height - y - 1);
                    PixelOperations<TPixel>.Instance.FromBgra5551Bytes(
                        this.configuration,
                        row.GetSpan(),
                        pixelSpan,
                        this.fileHeader.Width);
                }

                // We need to set each alpha component value to fully opaque.
                // Reset our stream for a second pass.
                this.currentStream.Position = currentPosition;
                for (int y = 0; y < this.fileHeader.Height; y++)
                {
                    this.currentStream.Read(row);
                    PixelOperations<Bgra5551>.Instance.FromBgra5551Bytes(
                        this.configuration,
                        row.GetSpan(),
                        bgraRowSpan,
                        this.fileHeader.Width);
                    Span<TPixel> pixelSpan = pixels.GetRowSpan(this.fileHeader.Height - y - 1);

                    for (int x = 0; x < this.fileHeader.Width; x++)
                    {
                        Bgra5551 bgra = bgraRowSpan[x];
                        bgra.PackedValue = (ushort)(bgra.PackedValue | (1 << 15));
                        ref TPixel pixel = ref pixelSpan[x];
                        pixel.FromBgra5551(bgra);
                    }
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
                    Span<TPixel> pixelSpan = pixels.GetRowSpan(this.fileHeader.Height - y - 1);
                    PixelOperations<TPixel>.Instance.FromBgr24Bytes(
                        this.configuration,
                        row.GetSpan(),
                        pixelSpan,
                        this.fileHeader.Width);
                }
            }
        }

        private void ReadRle24<TPixel>(Buffer2D<TPixel> pixels, int width, int height)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel color = default;
            using (IMemoryOwner<byte> buffer = this.memoryAllocator.Allocate<byte>(width * height * 3, AllocationOptions.Clean))
            {
                Span<byte> bufferSpan = buffer.GetSpan();
                this.UncompressRle24(width, height, bufferSpan);
                for (int y = 0; y < height; y++)
                {
                    Span<TPixel> pixelRow = pixels.GetRowSpan(this.fileHeader.Height - y - 1);
                    for (int x = 0; x < width; x++)
                    {
                        int idx = (y * width * 3) + (x * 3);
                        color.FromBgr24(Unsafe.As<byte, Bgr24>(ref bufferSpan[idx]));
                        pixelRow[x] = color;
                    }
                }
            }
        }

        private void UncompressRle24(int width, int height, Span<byte> buffer)
        {
            int uncompressedPixels = 0;
            var pixel = new byte[3];
            int totalPixels = width * height;
            while (uncompressedPixels < totalPixels)
            {
                byte runLengthByte = (byte)this.currentStream.ReadByte();

                // The high bit of a run length packet is set to 1.
                int highBit = runLengthByte >> 7;
                if (highBit == 1)
                {
                    int runLength = runLengthByte & 127;
                    this.currentStream.Read(pixel, 0, 3);
                    int bufferIdx = uncompressedPixels * 3;
                    for (int i = 0; i < runLength + 1; i++, uncompressedPixels++)
                    {
                        pixel.AsSpan().CopyTo(buffer.Slice(bufferIdx));
                        bufferIdx += 3;
                    }
                }
                else
                {
                    // Non-run-length encoded packet.
                    int runLength = runLengthByte;
                    int bufferIdx = uncompressedPixels * 3;
                    for (int i = 0; i < runLength + 1; i++, uncompressedPixels++)
                    {
                        this.currentStream.Read(pixel, 0, 3);
                        pixel.AsSpan().CopyTo(buffer.Slice(bufferIdx));
                        bufferIdx += 3;
                    }
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
                    Span<TPixel> pixelSpan = pixels.GetRowSpan(this.fileHeader.Height - y - 1);
                    PixelOperations<TPixel>.Instance.FromBgra32Bytes(
                        this.configuration,
                        row.GetSpan(),
                        pixelSpan,
                        this.fileHeader.Width);
                }
            }
        }

        private void ReadRle32<TPixel>(Buffer2D<TPixel> pixels, int width, int height)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel color = default;
            using (IMemoryOwner<byte> buffer = this.memoryAllocator.Allocate<byte>(width * height * 4, AllocationOptions.Clean))
            {
                Span<byte> bufferSpan = buffer.GetSpan();
                this.UncompressRle32(width, height, bufferSpan);
                for (int y = 0; y < height; y++)
                {
                    Span<TPixel> pixelRow = pixels.GetRowSpan(this.fileHeader.Height - y - 1);
                    for (int x = 0; x < width; x++)
                    {
                        int idx = (y * width * 4) + (x * 4);
                        color.FromBgra32(Unsafe.As<byte, Bgra32>(ref bufferSpan[idx]));
                        pixelRow[x] = color;
                    }
                }
            }
        }

        private void UncompressRle32(int width, int height, Span<byte> buffer)
        {
            int uncompressedPixels = 0;
            var pixel = new byte[4];
            int totalPixels = width * height;
            while (uncompressedPixels < totalPixels)
            {
                byte runLengthByte = (byte)this.currentStream.ReadByte();

                // The high bit of a run length packet is set to 1.
                int highBit = runLengthByte >> 7;
                if (highBit == 1)
                {
                    int runLength = runLengthByte & 127;
                    this.currentStream.Read(pixel, 0, 4);
                    int bufferIdx = uncompressedPixels * 4;
                    for (int i = 0; i < runLength + 1; i++, uncompressedPixels++)
                    {
                        pixel.AsSpan().CopyTo(buffer.Slice(bufferIdx));
                        bufferIdx += 4;
                    }
                }
                else
                {
                    // Non-run-length encoded packet.
                    int runLength = runLengthByte;
                    int bufferIdx = uncompressedPixels * 4;
                    for (int i = 0; i < runLength + 1; i++, uncompressedPixels++)
                    {
                        this.currentStream.Read(pixel, 0, 4);
                        pixel.AsSpan().CopyTo(buffer.Slice(bufferIdx));
                        bufferIdx += 4;
                    }
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

            // TODO: no meta data yet.
            this.metadata = new ImageMetadata();
        }
    }
}
