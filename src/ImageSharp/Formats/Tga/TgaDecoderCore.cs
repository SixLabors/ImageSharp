// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tga
{
    /// <summary>
    /// Performs the tga decoding operation.
    /// </summary>
    internal sealed class TgaDecoderCore : IImageDecoderInternals
    {
        /// <summary>
        /// A scratch buffer to reduce allocations.
        /// </summary>
        private readonly byte[] scratchBuffer = new byte[4];

        /// <summary>
        /// The metadata.
        /// </summary>
        private ImageMetadata metadata;

        /// <summary>
        /// The tga specific metadata.
        /// </summary>
        private TgaMetadata tgaMetadata;

        /// <summary>
        /// The file header containing general information about the image.
        /// </summary>
        private TgaFileHeader fileHeader;

        /// <summary>
        /// Used for allocating memory during processing operations.
        /// </summary>
        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// The stream to decode from.
        /// </summary>
        private BufferedReadStream currentStream;

        /// <summary>
        /// The bitmap decoder options.
        /// </summary>
        private readonly ITgaDecoderOptions options;

        /// <summary>
        /// Indicates whether there is a alpha channel present.
        /// </summary>
        private bool hasAlpha;

        /// <summary>
        /// Initializes a new instance of the <see cref="TgaDecoderCore"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="options">The options.</param>
        public TgaDecoderCore(Configuration configuration, ITgaDecoderOptions options)
        {
            this.Configuration = configuration;
            this.memoryAllocator = configuration.MemoryAllocator;
            this.options = options;
        }

        /// <inheritdoc />
        public Configuration Configuration { get; }

        /// <summary>
        /// Gets the dimensions of the image.
        /// </summary>
        public Size Dimensions => new Size(this.fileHeader.Width, this.fileHeader.Height);

        /// <inheritdoc />
        public Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            try
            {
                TgaImageOrigin origin = this.ReadFileHeader(stream);
                this.currentStream.Skip(this.fileHeader.IdLength);

                // Parse the color map, if present.
                if (this.fileHeader.ColorMapType != 0 && this.fileHeader.ColorMapType != 1)
                {
                    TgaThrowHelper.ThrowNotSupportedException($"Unknown tga colormap type {this.fileHeader.ColorMapType} found");
                }

                if (this.fileHeader.Width == 0 || this.fileHeader.Height == 0)
                {
                    throw new UnknownImageFormatException("Width or height cannot be 0");
                }

                var image = Image.CreateUninitialized<TPixel>(this.Configuration, this.fileHeader.Width, this.fileHeader.Height, this.metadata);
                Buffer2D<TPixel> pixels = image.GetRootFramePixelBuffer();

                if (this.fileHeader.ColorMapType == 1)
                {
                    if (this.fileHeader.CMapLength <= 0)
                    {
                        TgaThrowHelper.ThrowInvalidImageContentException("Missing tga color map length");
                    }

                    if (this.fileHeader.CMapDepth <= 0)
                    {
                        TgaThrowHelper.ThrowInvalidImageContentException("Missing tga color map depth");
                    }

                    int colorMapPixelSizeInBytes = this.fileHeader.CMapDepth / 8;
                    int colorMapSizeInBytes = this.fileHeader.CMapLength * colorMapPixelSizeInBytes;
                    using (IManagedByteBuffer palette = this.memoryAllocator.AllocateManagedByteBuffer(colorMapSizeInBytes, AllocationOptions.Clean))
                    {
                        this.currentStream.Read(palette.Array, this.fileHeader.CMapStart, colorMapSizeInBytes);

                        if (this.fileHeader.ImageType == TgaImageType.RleColorMapped)
                        {
                            this.ReadPalettedRle(
                                this.fileHeader.Width,
                                this.fileHeader.Height,
                                pixels,
                                palette.Array,
                                colorMapPixelSizeInBytes,
                                origin);
                        }
                        else
                        {
                            this.ReadPaletted(
                                this.fileHeader.Width,
                                this.fileHeader.Height,
                                pixels,
                                palette.Array,
                                colorMapPixelSizeInBytes,
                                origin);
                        }
                    }

                    return image;
                }

                // Even if the image type indicates it is not a paletted image, it can still contain a palette. Skip those bytes.
                if (this.fileHeader.CMapLength > 0)
                {
                    int colorMapPixelSizeInBytes = this.fileHeader.CMapDepth / 8;
                    this.currentStream.Skip(this.fileHeader.CMapLength * colorMapPixelSizeInBytes);
                }

                switch (this.fileHeader.PixelDepth)
                {
                    case 8:
                        if (this.fileHeader.ImageType.IsRunLengthEncoded())
                        {
                            this.ReadRle(this.fileHeader.Width, this.fileHeader.Height, pixels, 1, origin);
                        }
                        else
                        {
                            this.ReadMonoChrome(this.fileHeader.Width, this.fileHeader.Height, pixels, origin);
                        }

                        break;

                    case 15:
                    case 16:
                        if (this.fileHeader.ImageType.IsRunLengthEncoded())
                        {
                            this.ReadRle(this.fileHeader.Width, this.fileHeader.Height, pixels, 2, origin);
                        }
                        else
                        {
                            this.ReadBgra16(this.fileHeader.Width, this.fileHeader.Height, pixels, origin);
                        }

                        break;

                    case 24:
                        if (this.fileHeader.ImageType.IsRunLengthEncoded())
                        {
                            this.ReadRle(this.fileHeader.Width, this.fileHeader.Height, pixels, 3, origin);
                        }
                        else
                        {
                            this.ReadBgr24(this.fileHeader.Width, this.fileHeader.Height, pixels, origin);
                        }

                        break;

                    case 32:
                        if (this.fileHeader.ImageType.IsRunLengthEncoded())
                        {
                            this.ReadRle(this.fileHeader.Width, this.fileHeader.Height, pixels, 4, origin);
                        }
                        else
                        {
                            this.ReadBgra32(this.fileHeader.Width, this.fileHeader.Height, pixels, origin);
                        }

                        break;

                    default:
                        TgaThrowHelper.ThrowNotSupportedException("ImageSharp does not support this kind of tga files.");
                        break;
                }

                return image;
            }
            catch (IndexOutOfRangeException e)
            {
                throw new ImageFormatException("TGA image does not have a valid format.", e);
            }
        }

        /// <summary>
        /// Reads a uncompressed TGA image with a palette.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
        /// <param name="palette">The color palette.</param>
        /// <param name="colorMapPixelSizeInBytes">Color map size of one entry in bytes.</param>
        /// <param name="origin">The image origin.</param>
        private void ReadPaletted<TPixel>(int width, int height, Buffer2D<TPixel> pixels, byte[] palette, int colorMapPixelSizeInBytes, TgaImageOrigin origin)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            TPixel color = default;
            bool invertX = InvertX(origin);

            for (int y = 0; y < height; y++)
            {
                int newY = InvertY(y, height, origin);
                Span<TPixel> pixelRow = pixels.GetRowSpan(newY);

                switch (colorMapPixelSizeInBytes)
                {
                    case 2:
                        if (invertX)
                        {
                            for (int x = width - 1; x >= 0; x--)
                            {
                                this.ReadPalettedBgra16Pixel(palette, colorMapPixelSizeInBytes, x, color, pixelRow);
                            }
                        }
                        else
                        {
                            for (int x = 0; x < width; x++)
                            {
                                this.ReadPalettedBgra16Pixel(palette, colorMapPixelSizeInBytes, x, color, pixelRow);
                            }
                        }

                        break;

                    case 3:
                        if (invertX)
                        {
                            for (int x = width - 1; x >= 0; x--)
                            {
                                this.ReadPalettedBgr24Pixel(palette, colorMapPixelSizeInBytes, x, color, pixelRow);
                            }
                        }
                        else
                        {
                            for (int x = 0; x < width; x++)
                            {
                                this.ReadPalettedBgr24Pixel(palette, colorMapPixelSizeInBytes, x, color, pixelRow);
                            }
                        }

                        break;

                    case 4:
                        if (invertX)
                        {
                            for (int x = width - 1; x >= 0; x--)
                            {
                                this.ReadPalettedBgra32Pixel(palette, colorMapPixelSizeInBytes, x, color, pixelRow);
                            }
                        }
                        else
                        {
                            for (int x = 0; x < width; x++)
                            {
                                this.ReadPalettedBgra32Pixel(palette, colorMapPixelSizeInBytes, x, color, pixelRow);
                            }
                        }

                        break;
                }
            }
        }

        /// <summary>
        /// Reads a run length encoded TGA image with a palette.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
        /// <param name="palette">The color palette.</param>
        /// <param name="colorMapPixelSizeInBytes">Color map size of one entry in bytes.</param>
        /// <param name="origin">The image origin.</param>
        private void ReadPalettedRle<TPixel>(int width, int height, Buffer2D<TPixel> pixels, byte[] palette, int colorMapPixelSizeInBytes, TgaImageOrigin origin)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int bytesPerPixel = 1;
            using (IMemoryOwner<byte> buffer = this.memoryAllocator.Allocate<byte>(width * height * bytesPerPixel, AllocationOptions.Clean))
            {
                TPixel color = default;
                Span<byte> bufferSpan = buffer.GetSpan();
                this.UncompressRle(width, height, bufferSpan, bytesPerPixel: 1);

                for (int y = 0; y < height; y++)
                {
                    int newY = InvertY(y, height, origin);
                    Span<TPixel> pixelRow = pixels.GetRowSpan(newY);
                    int rowStartIdx = y * width * bytesPerPixel;
                    for (int x = 0; x < width; x++)
                    {
                        int idx = rowStartIdx + x;
                        switch (colorMapPixelSizeInBytes)
                        {
                            case 1:
                                color.FromL8(Unsafe.As<byte, L8>(ref palette[bufferSpan[idx] * colorMapPixelSizeInBytes]));
                                break;
                            case 2:
                                this.ReadPalettedBgra16Pixel(palette, bufferSpan[idx], colorMapPixelSizeInBytes, ref color);
                                break;
                            case 3:
                                color.FromBgr24(Unsafe.As<byte, Bgr24>(ref palette[bufferSpan[idx] * colorMapPixelSizeInBytes]));
                                break;
                            case 4:
                                color.FromBgra32(Unsafe.As<byte, Bgra32>(ref palette[bufferSpan[idx] * colorMapPixelSizeInBytes]));
                                break;
                        }

                        int newX = InvertX(x, width, origin);
                        pixelRow[newX] = color;
                    }
                }
            }
        }

        /// <summary>
        /// Reads a uncompressed monochrome TGA image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
        /// <param name="origin">the image origin.</param>
        private void ReadMonoChrome<TPixel>(int width, int height, Buffer2D<TPixel> pixels, TgaImageOrigin origin)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            bool invertX = InvertX(origin);
            if (invertX)
            {
                TPixel color = default;
                for (int y = 0; y < height; y++)
                {
                    int newY = InvertY(y, height, origin);
                    Span<TPixel> pixelSpan = pixels.GetRowSpan(newY);
                    for (int x = width - 1; x >= 0; x--)
                    {
                        this.ReadL8Pixel(color, x, pixelSpan);
                    }
                }

                return;
            }

            using (IManagedByteBuffer row = this.memoryAllocator.AllocatePaddedPixelRowBuffer(width, 1, 0))
            {
                bool invertY = InvertY(origin);
                if (invertY)
                {
                    for (int y = height - 1; y >= 0; y--)
                    {
                        this.ReadL8Row(width, pixels, row, y);
                    }
                }
                else
                {
                    for (int y = 0; y < height; y++)
                    {
                        this.ReadL8Row(width, pixels, row, y);
                    }
                }
            }
        }

        /// <summary>
        /// Reads a uncompressed TGA image where each pixels has 16 bit.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
        /// <param name="origin">The image origin.</param>
        private void ReadBgra16<TPixel>(int width, int height, Buffer2D<TPixel> pixels, TgaImageOrigin origin)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            TPixel color = default;
            bool invertX = InvertX(origin);
            using (IManagedByteBuffer row = this.memoryAllocator.AllocatePaddedPixelRowBuffer(width, 2, 0))
            {
                for (int y = 0; y < height; y++)
                {
                    int newY = InvertY(y, height, origin);
                    Span<TPixel> pixelSpan = pixels.GetRowSpan(newY);

                    if (invertX)
                    {
                        for (int x = width - 1; x >= 0; x--)
                        {
                            this.currentStream.Read(this.scratchBuffer, 0, 2);
                            if (!this.hasAlpha)
                            {
                                this.scratchBuffer[1] |= 1 << 7;
                            }

                            if (this.fileHeader.ImageType == TgaImageType.BlackAndWhite)
                            {
                                color.FromLa16(Unsafe.As<byte, La16>(ref this.scratchBuffer[0]));
                            }
                            else
                            {
                                color.FromBgra5551(Unsafe.As<byte, Bgra5551>(ref this.scratchBuffer[0]));
                            }

                            pixelSpan[x] = color;
                        }
                    }
                    else
                    {
                        this.currentStream.Read(row);
                        Span<byte> rowSpan = row.GetSpan();

                        if (!this.hasAlpha)
                        {
                            // We need to set the alpha component value to fully opaque.
                            for (int x = 1; x < rowSpan.Length; x += 2)
                            {
                                rowSpan[x] |= 1 << 7;
                            }
                        }

                        if (this.fileHeader.ImageType == TgaImageType.BlackAndWhite)
                        {
                            PixelOperations<TPixel>.Instance.FromLa16Bytes(this.Configuration, rowSpan, pixelSpan, width);
                        }
                        else
                        {
                            PixelOperations<TPixel>.Instance.FromBgra5551Bytes(this.Configuration, rowSpan, pixelSpan, width);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Reads a uncompressed TGA image where each pixels has 24 bit.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
        /// <param name="origin">The image origin.</param>
        private void ReadBgr24<TPixel>(int width, int height, Buffer2D<TPixel> pixels, TgaImageOrigin origin)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            bool invertX = InvertX(origin);
            if (invertX)
            {
                TPixel color = default;
                for (int y = 0; y < height; y++)
                {
                    int newY = InvertY(y, height, origin);
                    Span<TPixel> pixelSpan = pixels.GetRowSpan(newY);
                    for (int x = width - 1; x >= 0; x--)
                    {
                        this.ReadBgr24Pixel(color, x, pixelSpan);
                    }
                }

                return;
            }

            using (IManagedByteBuffer row = this.memoryAllocator.AllocatePaddedPixelRowBuffer(width, 3, 0))
            {
                bool invertY = InvertY(origin);

                if (invertY)
                {
                    for (int y = height - 1; y >= 0; y--)
                    {
                        this.ReadBgr24Row(width, pixels, row, y);
                    }
                }
                else
                {
                    for (int y = 0; y < height; y++)
                    {
                        this.ReadBgr24Row(width, pixels, row, y);
                    }
                }
            }
        }

        /// <summary>
        /// Reads a uncompressed TGA image where each pixels has 32 bit.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
        /// <param name="origin">The image origin.</param>
        private void ReadBgra32<TPixel>(int width, int height, Buffer2D<TPixel> pixels, TgaImageOrigin origin)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            TPixel color = default;
            bool invertX = InvertX(origin);
            if (this.tgaMetadata.AlphaChannelBits == 8 && !invertX)
            {
                using (IManagedByteBuffer row = this.memoryAllocator.AllocatePaddedPixelRowBuffer(width, 4, 0))
                {
                    if (InvertY(origin))
                    {
                        for (int y = height - 1; y >= 0; y--)
                        {
                            this.ReadBgra32Row(width, pixels, row, y);
                        }
                    }
                    else
                    {
                        for (int y = 0; y < height; y++)
                        {
                            this.ReadBgra32Row(width, pixels, row, y);
                        }
                    }
                }

                return;
            }

            for (int y = 0; y < height; y++)
            {
                int newY = InvertY(y, height, origin);
                Span<TPixel> pixelRow = pixels.GetRowSpan(newY);
                if (invertX)
                {
                    for (int x = width - 1; x >= 0; x--)
                    {
                        this.ReadBgra32Pixel(x, color, pixelRow);
                    }
                }
                else
                {
                    for (int x = 0; x < width; x++)
                    {
                        this.ReadBgra32Pixel(x, color, pixelRow);
                    }
                }
            }
        }

        /// <summary>
        /// Reads a run length encoded TGA image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
        /// <param name="bytesPerPixel">The bytes per pixel.</param>
        /// <param name="origin">The image origin.</param>
        private void ReadRle<TPixel>(int width, int height, Buffer2D<TPixel> pixels, int bytesPerPixel, TgaImageOrigin origin)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            TPixel color = default;
            var alphaBits = this.tgaMetadata.AlphaChannelBits;
            using (IMemoryOwner<byte> buffer = this.memoryAllocator.Allocate<byte>(width * height * bytesPerPixel, AllocationOptions.Clean))
            {
                Span<byte> bufferSpan = buffer.GetSpan();
                this.UncompressRle(width, height, bufferSpan, bytesPerPixel);
                for (int y = 0; y < height; y++)
                {
                    int newY = InvertY(y, height, origin);
                    Span<TPixel> pixelRow = pixels.GetRowSpan(newY);
                    int rowStartIdx = y * width * bytesPerPixel;
                    for (int x = 0; x < width; x++)
                    {
                        int idx = rowStartIdx + (x * bytesPerPixel);
                        switch (bytesPerPixel)
                        {
                            case 1:
                                color.FromL8(Unsafe.As<byte, L8>(ref bufferSpan[idx]));
                                break;
                            case 2:
                                if (!this.hasAlpha)
                                {
                                    // Set alpha value to 1, to treat it as opaque for Bgra5551.
                                    bufferSpan[idx + 1] = (byte)(bufferSpan[idx + 1] | 128);
                                }

                                if (this.fileHeader.ImageType == TgaImageType.RleBlackAndWhite)
                                {
                                    color.FromLa16(Unsafe.As<byte, La16>(ref bufferSpan[idx]));
                                }
                                else
                                {
                                    color.FromBgra5551(Unsafe.As<byte, Bgra5551>(ref bufferSpan[idx]));
                                }

                                break;
                            case 3:
                                color.FromBgr24(Unsafe.As<byte, Bgr24>(ref bufferSpan[idx]));
                                break;
                            case 4:
                                if (this.hasAlpha)
                                {
                                    color.FromBgra32(Unsafe.As<byte, Bgra32>(ref bufferSpan[idx]));
                                }
                                else
                                {
                                    var alpha = alphaBits == 0 ? byte.MaxValue : bufferSpan[idx + 3];
                                    color.FromBgra32(new Bgra32(bufferSpan[idx + 2], bufferSpan[idx + 1], bufferSpan[idx], (byte)alpha));
                                }

                                break;
                        }

                        int newX = InvertX(x, width, origin);
                        pixelRow[newX] = color;
                    }
                }
            }
        }

        /// <inheritdoc />
        public IImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
        {
            this.ReadFileHeader(stream);
            return new ImageInfo(
                new PixelTypeInfo(this.fileHeader.PixelDepth),
                this.fileHeader.Width,
                this.fileHeader.Height,
                this.metadata);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadL8Row<TPixel>(int width, Buffer2D<TPixel> pixels, IManagedByteBuffer row, int y)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            this.currentStream.Read(row);
            Span<TPixel> pixelSpan = pixels.GetRowSpan(y);
            PixelOperations<TPixel>.Instance.FromL8Bytes(this.Configuration, row.GetSpan(), pixelSpan, width);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadL8Pixel<TPixel>(TPixel color, int x, Span<TPixel> pixelSpan)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var pixelValue = (byte)this.currentStream.ReadByte();
            color.FromL8(Unsafe.As<byte, L8>(ref pixelValue));
            pixelSpan[x] = color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadBgr24Pixel<TPixel>(TPixel color, int x, Span<TPixel> pixelSpan)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            this.currentStream.Read(this.scratchBuffer, 0, 3);
            color.FromBgr24(Unsafe.As<byte, Bgr24>(ref this.scratchBuffer[0]));
            pixelSpan[x] = color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadBgr24Row<TPixel>(int width, Buffer2D<TPixel> pixels, IManagedByteBuffer row, int y)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            this.currentStream.Read(row);
            Span<TPixel> pixelSpan = pixels.GetRowSpan(y);
            PixelOperations<TPixel>.Instance.FromBgr24Bytes(this.Configuration, row.GetSpan(), pixelSpan, width);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadBgra32Pixel<TPixel>(int x, TPixel color, Span<TPixel> pixelRow)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            this.currentStream.Read(this.scratchBuffer, 0, 4);
            var alpha = this.tgaMetadata.AlphaChannelBits == 0 ? byte.MaxValue : this.scratchBuffer[3];
            color.FromBgra32(new Bgra32(this.scratchBuffer[2], this.scratchBuffer[1], this.scratchBuffer[0], alpha));
            pixelRow[x] = color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadBgra32Row<TPixel>(int width, Buffer2D<TPixel> pixels, IManagedByteBuffer row, int y)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            this.currentStream.Read(row);
            Span<TPixel> pixelSpan = pixels.GetRowSpan(y);
            PixelOperations<TPixel>.Instance.FromBgra32Bytes(this.Configuration, row.GetSpan(), pixelSpan, width);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadPalettedBgra16Pixel<TPixel>(byte[] palette, int colorMapPixelSizeInBytes, int x, TPixel color, Span<TPixel> pixelRow)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int colorIndex = this.currentStream.ReadByte();
            this.ReadPalettedBgra16Pixel(palette, colorIndex, colorMapPixelSizeInBytes, ref color);
            pixelRow[x] = color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadPalettedBgra16Pixel<TPixel>(byte[] palette, int index, int colorMapPixelSizeInBytes, ref TPixel color)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Bgra5551 bgra = default;
            bgra.FromBgra5551(Unsafe.As<byte, Bgra5551>(ref palette[index * colorMapPixelSizeInBytes]));

            if (!this.hasAlpha)
            {
                // Set alpha value to 1, to treat it as opaque.
                bgra.PackedValue = (ushort)(bgra.PackedValue | 0x8000);
            }

            color.FromBgra5551(bgra);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadPalettedBgr24Pixel<TPixel>(byte[] palette, int colorMapPixelSizeInBytes, int x, TPixel color, Span<TPixel> pixelRow)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int colorIndex = this.currentStream.ReadByte();
            color.FromBgr24(Unsafe.As<byte, Bgr24>(ref palette[colorIndex * colorMapPixelSizeInBytes]));
            pixelRow[x] = color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadPalettedBgra32Pixel<TPixel>(byte[] palette, int colorMapPixelSizeInBytes, int x, TPixel color, Span<TPixel> pixelRow)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int colorIndex = this.currentStream.ReadByte();
            color.FromBgra32(Unsafe.As<byte, Bgra32>(ref palette[colorIndex * colorMapPixelSizeInBytes]));
            pixelRow[x] = color;
        }

        /// <summary>
        /// Produce uncompressed tga data from a run length encoded stream.
        /// </summary>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="buffer">Buffer for uncompressed data.</param>
        /// <param name="bytesPerPixel">The bytes used per pixel.</param>
        private void UncompressRle(int width, int height, Span<byte> buffer, int bytesPerPixel)
        {
            int uncompressedPixels = 0;
            var pixel = new byte[bytesPerPixel];
            int totalPixels = width * height;
            while (uncompressedPixels < totalPixels)
            {
                byte runLengthByte = (byte)this.currentStream.ReadByte();

                // The high bit of a run length packet is set to 1.
                int highBit = runLengthByte >> 7;
                if (highBit == 1)
                {
                    int runLength = runLengthByte & 127;
                    this.currentStream.Read(pixel, 0, bytesPerPixel);
                    int bufferIdx = uncompressedPixels * bytesPerPixel;
                    for (int i = 0; i < runLength + 1; i++, uncompressedPixels++)
                    {
                        pixel.AsSpan().CopyTo(buffer.Slice(bufferIdx));
                        bufferIdx += bytesPerPixel;
                    }
                }
                else
                {
                    // Non-run-length encoded packet.
                    int runLength = runLengthByte;
                    int bufferIdx = uncompressedPixels * bytesPerPixel;
                    for (int i = 0; i < runLength + 1; i++, uncompressedPixels++)
                    {
                        this.currentStream.Read(pixel, 0, bytesPerPixel);
                        pixel.AsSpan().CopyTo(buffer.Slice(bufferIdx));
                        bufferIdx += bytesPerPixel;
                    }
                }
            }
        }

        /// <summary>
        /// Returns the y- value based on the given height.
        /// </summary>
        /// <param name="y">The y- value representing the current row.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="origin">The image origin.</param>
        /// <returns>The <see cref="int"/> representing the inverted value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int InvertY(int y, int height, TgaImageOrigin origin)
        {
            if (InvertY(origin))
            {
                return height - y - 1;
            }

            return y;
        }

        /// <summary>
        /// Indicates whether the y coordinates needs to be inverted, to keep a top left origin.
        /// </summary>
        /// <param name="origin">The image origin.</param>
        /// <returns>True, if y coordinate needs to be inverted.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool InvertY(TgaImageOrigin origin)
        {
            switch (origin)
            {
                case TgaImageOrigin.BottomLeft:
                case TgaImageOrigin.BottomRight:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns the x- value based on the given width.
        /// </summary>
        /// <param name="x">The x- value representing the current column.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="origin">The image origin.</param>
        /// <returns>The <see cref="int"/> representing the inverted value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int InvertX(int x, int width, TgaImageOrigin origin)
        {
            if (InvertX(origin))
            {
                return width - x - 1;
            }

            return x;
        }

        /// <summary>
        /// Indicates whether the x coordinates needs to be inverted, to keep a top left origin.
        /// </summary>
        /// <param name="origin">The image origin.</param>
        /// <returns>True, if x coordinate needs to be inverted.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool InvertX(TgaImageOrigin origin)
        {
            switch (origin)
            {
                case TgaImageOrigin.TopRight:
                case TgaImageOrigin.BottomRight:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Reads the tga file header from the stream.
        /// </summary>
        /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
        /// <returns>The image origin.</returns>
        private TgaImageOrigin ReadFileHeader(BufferedReadStream stream)
        {
            this.currentStream = stream;

            Span<byte> buffer = stackalloc byte[TgaFileHeader.Size];

            this.currentStream.Read(buffer, 0, TgaFileHeader.Size);
            this.fileHeader = TgaFileHeader.Parse(buffer);
            this.metadata = new ImageMetadata();
            this.tgaMetadata = this.metadata.GetTgaMetadata();
            this.tgaMetadata.BitsPerPixel = (TgaBitsPerPixel)this.fileHeader.PixelDepth;

            var alphaBits = this.fileHeader.ImageDescriptor & 0xf;
            if (alphaBits != 0 && alphaBits != 1 && alphaBits != 8)
            {
                TgaThrowHelper.ThrowInvalidImageContentException("Invalid alpha channel bits");
            }

            this.tgaMetadata.AlphaChannelBits = (byte)alphaBits;
            this.hasAlpha = alphaBits > 0;

            // Bits 4 and 5 describe the image origin.
            var origin = (TgaImageOrigin)((this.fileHeader.ImageDescriptor & 0x30) >> 4);
            return origin;
        }
    }
}
