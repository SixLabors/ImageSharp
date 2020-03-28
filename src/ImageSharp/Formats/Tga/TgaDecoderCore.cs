// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tga
{
    /// <summary>
    /// Performs the tga decoding operation.
    /// </summary>
    internal sealed class TgaDecoderCore
    {
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
            this.configuration = configuration;
            this.memoryAllocator = configuration.MemoryAllocator;
            this.options = options;
        }

        /// <summary>
        /// Gets the dimensions of the image.
        /// </summary>
        public Size Dimensions => new Size(this.fileHeader.Width, this.fileHeader.Height);

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
            where TPixel : unmanaged, IPixel<TPixel>
        {
            try
            {
                bool inverted = this.ReadFileHeader(stream);
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

                var image = Image.CreateUninitialized<TPixel>(this.configuration, this.fileHeader.Width, this.fileHeader.Height, this.metadata);
                Buffer2D<TPixel> pixels = image.GetRootFramePixelBuffer();

                if (this.fileHeader.ColorMapType == 1)
                {
                    if (this.fileHeader.CMapLength <= 0)
                    {
                        TgaThrowHelper.ThrowImageFormatException("Missing tga color map length");
                    }

                    if (this.fileHeader.CMapDepth <= 0)
                    {
                        TgaThrowHelper.ThrowImageFormatException("Missing tga color map depth");
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
                                inverted);
                        }
                        else
                        {
                            this.ReadPaletted(
                                this.fileHeader.Width,
                                this.fileHeader.Height,
                                pixels,
                                palette.Array,
                                colorMapPixelSizeInBytes,
                                inverted);
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
                            this.ReadRle(this.fileHeader.Width, this.fileHeader.Height, pixels, 1, inverted);
                        }
                        else
                        {
                            this.ReadMonoChrome(this.fileHeader.Width, this.fileHeader.Height, pixels, inverted);
                        }

                        break;

                    case 15:
                    case 16:
                        if (this.fileHeader.ImageType.IsRunLengthEncoded())
                        {
                            this.ReadRle(this.fileHeader.Width, this.fileHeader.Height, pixels, 2, inverted);
                        }
                        else
                        {
                            this.ReadBgra16(this.fileHeader.Width, this.fileHeader.Height, pixels, inverted);
                        }

                        break;

                    case 24:
                        if (this.fileHeader.ImageType.IsRunLengthEncoded())
                        {
                            this.ReadRle(this.fileHeader.Width, this.fileHeader.Height, pixels, 3, inverted);
                        }
                        else
                        {
                            this.ReadBgr24(this.fileHeader.Width, this.fileHeader.Height, pixels, inverted);
                        }

                        break;

                    case 32:
                        if (this.fileHeader.ImageType.IsRunLengthEncoded())
                        {
                            this.ReadRle(this.fileHeader.Width, this.fileHeader.Height, pixels, 4, inverted);
                        }
                        else
                        {
                            this.ReadBgra32(this.fileHeader.Width, this.fileHeader.Height, pixels, inverted);
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
        /// <param name="inverted">Indicates, if the origin of the image is top left rather the bottom left (the default).</param>
        private void ReadPaletted<TPixel>(int width, int height, Buffer2D<TPixel> pixels, byte[] palette, int colorMapPixelSizeInBytes, bool inverted)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (IManagedByteBuffer row = this.memoryAllocator.AllocateManagedByteBuffer(width, AllocationOptions.Clean))
            {
                TPixel color = default;
                Span<byte> rowSpan = row.GetSpan();

                for (int y = 0; y < height; y++)
                {
                    this.currentStream.Read(row);
                    int newY = Invert(y, height, inverted);
                    Span<TPixel> pixelRow = pixels.GetRowSpan(newY);
                    switch (colorMapPixelSizeInBytes)
                    {
                        case 2:
                            for (int x = 0; x < width; x++)
                            {
                                int colorIndex = rowSpan[x];

                                Bgra5551 bgra = Unsafe.As<byte, Bgra5551>(ref palette[colorIndex * colorMapPixelSizeInBytes]);
                                if (!this.hasAlpha)
                                {
                                    // Set alpha value to 1, to treat it as opaque for Bgra5551.
                                    bgra.PackedValue = (ushort)(bgra.PackedValue | 0x8000);
                                }

                                color.FromBgra5551(bgra);
                                pixelRow[x] = color;
                            }

                            break;

                        case 3:
                            for (int x = 0; x < width; x++)
                            {
                                int colorIndex = rowSpan[x];
                                color.FromBgr24(Unsafe.As<byte, Bgr24>(ref palette[colorIndex * colorMapPixelSizeInBytes]));
                                pixelRow[x] = color;
                            }

                            break;

                        case 4:
                            for (int x = 0; x < width; x++)
                            {
                                int colorIndex = rowSpan[x];
                                color.FromBgra32(Unsafe.As<byte, Bgra32>(ref palette[colorIndex * colorMapPixelSizeInBytes]));
                                pixelRow[x] = color;
                            }

                            break;
                    }
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
        /// <param name="inverted">Indicates, if the origin of the image is top left rather the bottom left (the default).</param>
        private void ReadPalettedRle<TPixel>(int width, int height, Buffer2D<TPixel> pixels, byte[] palette, int colorMapPixelSizeInBytes, bool inverted)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int bytesPerPixel = 1;
            using (IMemoryOwner<byte> buffer = this.memoryAllocator.Allocate<byte>(width * height * bytesPerPixel, AllocationOptions.Clean))
            {
                TPixel color = default;
                var alphaBits = this.tgaMetadata.AlphaChannelBits;
                Span<byte> bufferSpan = buffer.GetSpan();
                this.UncompressRle(width, height, bufferSpan, bytesPerPixel: 1);

                for (int y = 0; y < height; y++)
                {
                    int newY = Invert(y, height, inverted);
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

                                Bgra5551 bgra = Unsafe.As<byte, Bgra5551>(ref palette[bufferSpan[idx] * colorMapPixelSizeInBytes]);
                                if (!this.hasAlpha)
                                {
                                    // Set alpha value to 1, to treat it as opaque for Bgra5551.
                                    bgra.PackedValue = (ushort)(bgra.PackedValue | 0x8000);
                                }

                                color.FromBgra5551(bgra);
                                break;
                            case 3:
                                color.FromBgr24(Unsafe.As<byte, Bgr24>(ref palette[bufferSpan[idx] * colorMapPixelSizeInBytes]));
                                break;
                            case 4:
                                if (this.hasAlpha)
                                {
                                    color.FromBgra32(Unsafe.As<byte, Bgra32>(ref palette[bufferSpan[idx] * colorMapPixelSizeInBytes]));
                                }
                                else
                                {
                                    var alpha = alphaBits == 0 ? byte.MaxValue : bufferSpan[idx + 3];
                                    color.FromBgra32(new Bgra32(bufferSpan[idx + 2], bufferSpan[idx + 1], bufferSpan[idx], (byte)alpha));
                                }

                                break;
                        }

                        pixelRow[x] = color;
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
        /// <param name="inverted">Indicates, if the origin of the image is top left rather the bottom left (the default).</param>
        private void ReadMonoChrome<TPixel>(int width, int height, Buffer2D<TPixel> pixels, bool inverted)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (IManagedByteBuffer row = this.memoryAllocator.AllocatePaddedPixelRowBuffer(width, 1, 0))
            {
                for (int y = 0; y < height; y++)
                {
                    this.currentStream.Read(row);
                    int newY = Invert(y, height, inverted);
                    Span<TPixel> pixelSpan = pixels.GetRowSpan(newY);
                    PixelOperations<TPixel>.Instance.FromL8Bytes(this.configuration, row.GetSpan(), pixelSpan, width);
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
        /// <param name="inverted">Indicates, if the origin of the image is top left rather the bottom left (the default).</param>
        private void ReadBgra16<TPixel>(int width, int height, Buffer2D<TPixel> pixels, bool inverted)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (IManagedByteBuffer row = this.memoryAllocator.AllocatePaddedPixelRowBuffer(width, 2, 0))
            {
                for (int y = 0; y < height; y++)
                {
                    this.currentStream.Read(row);
                    Span<byte> rowSpan = row.GetSpan();

                    if (!this.hasAlpha)
                    {
                        // We need to set the alpha component value to fully opaque.
                        for (int x = 1; x < rowSpan.Length; x += 2)
                        {
                            rowSpan[x] = (byte)(rowSpan[x] | (1 << 7));
                        }
                    }

                    int newY = Invert(y, height, inverted);
                    Span<TPixel> pixelSpan = pixels.GetRowSpan(newY);
                    PixelOperations<TPixel>.Instance.FromBgra5551Bytes(this.configuration, rowSpan, pixelSpan, width);
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
        /// <param name="inverted">Indicates, if the origin of the image is top left rather the bottom left (the default).</param>
        private void ReadBgr24<TPixel>(int width, int height, Buffer2D<TPixel> pixels, bool inverted)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (IManagedByteBuffer row = this.memoryAllocator.AllocatePaddedPixelRowBuffer(width, 3, 0))
            {
                for (int y = 0; y < height; y++)
                {
                    this.currentStream.Read(row);
                    int newY = Invert(y, height, inverted);
                    Span<TPixel> pixelSpan = pixels.GetRowSpan(newY);
                    PixelOperations<TPixel>.Instance.FromBgr24Bytes(this.configuration, row.GetSpan(), pixelSpan, width);
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
        /// <param name="inverted">Indicates, if the origin of the image is top left rather the bottom left (the default).</param>
        private void ReadBgra32<TPixel>(int width, int height, Buffer2D<TPixel> pixels, bool inverted)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (this.tgaMetadata.AlphaChannelBits == 8)
            {
                using (IManagedByteBuffer row = this.memoryAllocator.AllocatePaddedPixelRowBuffer(width, 4, 0))
                {
                    for (int y = 0; y < height; y++)
                    {
                        this.currentStream.Read(row);
                        int newY = Invert(y, height, inverted);
                        Span<TPixel> pixelSpan = pixels.GetRowSpan(newY);

                        PixelOperations<TPixel>.Instance.FromBgra32Bytes(this.configuration, row.GetSpan(), pixelSpan, width);
                    }
                }

                return;
            }

            TPixel color = default;
            var alphaBits = this.tgaMetadata.AlphaChannelBits;
            using (IManagedByteBuffer row = this.memoryAllocator.AllocatePaddedPixelRowBuffer(width, 4, 0))
            {
                for (int y = 0; y < height; y++)
                {
                    this.currentStream.Read(row);
                    int newY = Invert(y, height, inverted);
                    Span<TPixel> pixelRow = pixels.GetRowSpan(newY);
                    Span<byte> rowSpan = row.GetSpan();

                    for (int x = 0; x < width; x++)
                    {
                        int idx = x * 4;
                        var alpha = alphaBits == 0 ? byte.MaxValue : rowSpan[idx + 3];
                        color.FromBgra32(new Bgra32(rowSpan[idx + 2], rowSpan[idx + 1], rowSpan[idx], (byte)alpha));
                        pixelRow[x] = color;
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
        /// <param name="inverted">Indicates, if the origin of the image is top left rather the bottom left (the default).</param>
        private void ReadRle<TPixel>(int width, int height, Buffer2D<TPixel> pixels, int bytesPerPixel, bool inverted)
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
                    int newY = Invert(y, height, inverted);
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

                                color.FromBgra5551(Unsafe.As<byte, Bgra5551>(ref bufferSpan[idx]));
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

                        pixelRow[x] = color;
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
        /// <param name="height">The height of the bitmap.</param>
        /// <param name="inverted">Whether the bitmap is inverted.</param>
        /// <returns>The <see cref="int"/> representing the inverted value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Invert(int y, int height, bool inverted) => (!inverted) ? height - y - 1 : y;

        /// <summary>
        /// Reads the tga file header from the stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <returns>true, if the image origin is top left.</returns>
        private bool ReadFileHeader(Stream stream)
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
                TgaThrowHelper.ThrowImageFormatException("Invalid alpha channel bits");
            }

            this.tgaMetadata.AlphaChannelBits = (byte)alphaBits;
            this.hasAlpha = alphaBits > 0;

            // TODO: bits 4 and 5 describe the image origin. See spec page 9. bit 4 is currently ignored.
            // Theoretically the origin could also be top right and bottom right.
            // Bit at position 5 of the descriptor indicates, that the origin is top left instead of bottom left.
            if ((this.fileHeader.ImageDescriptor & (1 << 5)) != 0)
            {
                return true;
            }

            return false;
        }
    }
}
