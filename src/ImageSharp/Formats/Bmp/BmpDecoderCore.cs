// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.MetaData;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Performs the bmp decoding operation.
    /// </summary>
    /// <remarks>
    /// A useful decoding source example can be found at <see href="https://dxr.mozilla.org/mozilla-central/source/image/decoders/nsBMPDecoder.cpp"/>
    /// </remarks>
    internal sealed class BmpDecoderCore
    {
        /// <summary>
        /// The mask for the red part of the color for 16 bit rgb bitmaps.
        /// </summary>
        private const int Rgb16RMask = 0x7C00;

        /// <summary>
        /// The mask for the green part of the color for 16 bit rgb bitmaps.
        /// </summary>
        private const int Rgb16GMask = 0x3E0;

        /// <summary>
        /// The mask for the blue part of the color for 16 bit rgb bitmaps.
        /// </summary>
        private const int Rgb16BMask = 0x1F;

        /// <summary>
        /// RLE8 flag value that indicates following byte has special meaning.
        /// </summary>
        private const int RleCommand = 0x00;

        /// <summary>
        /// RLE8 flag value marking end of a scan line.
        /// </summary>
        private const int RleEndOfLine = 0x00;

        /// <summary>
        /// RLE8 flag value marking end of bitmap data.
        /// </summary>
        private const int RleEndOfBitmap = 0x01;

        /// <summary>
        /// RLE8 flag value marking the start of [x,y] offset instruction.
        /// </summary>
        private const int RleDelta = 0x02;

        /// <summary>
        /// The stream to decode from.
        /// </summary>
        private Stream stream;

        /// <summary>
        /// The metadata
        /// </summary>
        private ImageMetaData metaData;

        /// <summary>
        /// The file header containing general information.
        /// TODO: Why is this not used? We advance the stream but do not use the values parsed.
        /// </summary>
        private BmpFileHeader fileHeader;

        /// <summary>
        /// The info header containing detailed information about the bitmap.
        /// </summary>
        private BmpInfoHeader infoHeader;

        private readonly Configuration configuration;

        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// Initializes a new instance of the <see cref="BmpDecoderCore"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="options">The options</param>
        public BmpDecoderCore(Configuration configuration, IBmpDecoderOptions options)
        {
            this.configuration = configuration;
            this.memoryAllocator = configuration.MemoryAllocator;
        }

        /// <summary>
        /// Decodes the image from the specified this._stream and sets
        /// the data to image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="stream">The stream, where the image should be
        /// decoded from. Cannot be null (Nothing in Visual Basic).</param>
        /// <exception cref="System.ArgumentNullException">
        ///    <para><paramref name="stream"/> is null.</para>
        /// </exception>
        /// <returns>The decoded image.</returns>
        public Image<TPixel> Decode<TPixel>(Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            try
            {
                this.ReadImageHeaders(stream, out bool inverted, out byte[] palette);

                var image = new Image<TPixel>(this.configuration, this.infoHeader.Width, this.infoHeader.Height, this.metaData);

                Buffer2D<TPixel> pixels = image.GetRootFramePixelBuffer();

                switch (this.infoHeader.Compression)
                {
                    case BmpCompression.RGB:
                        if (this.infoHeader.BitsPerPixel == 32)
                        {
                            this.ReadRgb32(pixels, this.infoHeader.Width, this.infoHeader.Height, inverted);
                        }
                        else if (this.infoHeader.BitsPerPixel == 24)
                        {
                            this.ReadRgb24(pixels, this.infoHeader.Width, this.infoHeader.Height, inverted);
                        }
                        else if (this.infoHeader.BitsPerPixel == 16)
                        {
                            this.ReadRgb16(pixels, this.infoHeader.Width, this.infoHeader.Height, inverted);
                        }
                        else if (this.infoHeader.BitsPerPixel <= 8)
                        {
                            this.ReadRgbPalette(
                                pixels,
                                palette,
                                this.infoHeader.Width,
                                this.infoHeader.Height,
                                this.infoHeader.BitsPerPixel,
                                inverted);
                        }

                        break;
                    case BmpCompression.RLE8:
                        this.ReadRle8(pixels, palette, this.infoHeader.Width, this.infoHeader.Height, inverted);

                        break;
                    default:
                        throw new NotSupportedException("Does not support this kind of bitmap files.");
                }

                return image;
            }
            catch (IndexOutOfRangeException e)
            {
                throw new ImageFormatException("Bitmap does not have a valid format.", e);
            }
        }

        /// <summary>
        /// Reads the raw image information from the specified stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        public IImageInfo Identify(Stream stream)
        {
            this.ReadImageHeaders(stream, out _, out _);
            return new ImageInfo(new PixelTypeInfo(this.infoHeader.BitsPerPixel), this.infoHeader.Width, this.infoHeader.Height, this.metaData);
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
        /// Calculates the amount of bytes to pad a row.
        /// </summary>
        /// <param name="width">The image width.</param>
        /// <param name="componentCount">The pixel component count.</param>
        /// <returns>
        /// The <see cref="int"/>.
        /// </returns>
        private static int CalculatePadding(int width, int componentCount)
        {
            int padding = (width * componentCount) % 4;

            if (padding != 0)
            {
                padding = 4 - padding;
            }

            return padding;
        }

        /// <summary>
        /// Performs final shifting from a 5bit value to an 8bit one.
        /// </summary>
        /// <param name="value">The masked and shifted value</param>
        /// <returns>The <see cref="byte"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte GetBytesFrom5BitValue(int value) => (byte)((value << 3) | (value >> 2));

        /// <summary>
        /// Looks up color values and builds the image from de-compressed RLE8 data.
        /// Compresssed RLE8 stream is uncompressed by <see cref="UncompressRle8(int, Span{byte})"/>
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
        /// <param name="colors">The <see cref="T:byte[]"/> containing the colors.</param>
        /// <param name="width">The width of the bitmap.</param>
        /// <param name="height">The height of the bitmap.</param>
        /// <param name="inverted">Whether the bitmap is inverted.</param>
        private void ReadRle8<TPixel>(Buffer2D<TPixel> pixels, byte[] colors, int width, int height, bool inverted)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel color = default;
            var rgba = new Rgba32(0, 0, 0, 255);

            using (Buffer2D<byte> buffer = this.memoryAllocator.Allocate2D<byte>(width, height, AllocationOptions.Clean))
            {
                this.UncompressRle8(width, buffer.GetSpan());

                for (int y = 0; y < height; y++)
                {
                    int newY = Invert(y, height, inverted);
                    Span<byte> bufferRow = buffer.GetRowSpan(y);
                    Span<TPixel> pixelRow = pixels.GetRowSpan(newY);

                    for (int x = 0; x < width; x++)
                    {
                        rgba.Bgr = Unsafe.As<byte, Bgr24>(ref colors[bufferRow[x] * 4]);
                        color.PackFromRgba32(rgba);
                        pixelRow[x] = color;
                    }
                }
            }
        }

        /// <summary>
        /// Produce uncompressed bitmap data from RLE8 stream
        /// </summary>
        /// <remarks>
        /// RLE8 is a 2-byte run-length encoding
        /// <br/>If first byte is 0, the second byte may have special meaning
        /// <br/>Otherwise, first byte is the length of the run and second byte is the color for the run
        /// </remarks>
        /// <param name="w">The width of the bitmap.</param>
        /// <param name="buffer">Buffer for uncompressed data.</param>
        private void UncompressRle8(int w, Span<byte> buffer)
        {
#if NETCOREAPP2_1
            Span<byte> cmd = stackalloc byte[2];
#else
            byte[] cmd = new byte[2];
#endif
            int count = 0;

            while (count < buffer.Length)
            {
                if (this.stream.Read(cmd, 0, cmd.Length) != 2)
                {
                    throw new Exception("Failed to read 2 bytes from stream");
                }

                if (cmd[0] == RleCommand)
                {
                    switch (cmd[1])
                    {
                        case RleEndOfBitmap:
                            return;

                        case RleEndOfLine:
                            int extra = count % w;
                            if (extra > 0)
                            {
                                count += w - extra;
                            }

                            break;

                        case RleDelta:
                            int dx = this.stream.ReadByte();
                            int dy = this.stream.ReadByte();
                            count += (w * dy) + dx;

                            break;

                        default:
                            // If the second byte > 2, we are in 'absolute mode'
                            // Take this number of bytes from the stream as uncompressed data
                            int length = cmd[1];

                            byte[] run = new byte[length];

                            this.stream.Read(run, 0, run.Length);

                            run.AsSpan().CopyTo(buffer.Slice(count));

                            count += run.Length;

                            // Absolute mode data is aligned to two-byte word-boundary
                            int padding = length & 1;

                            this.stream.Skip(padding);

                            break;
                    }
                }
                else
                {
                    for (int i = 0; i < cmd[0]; i++)
                    {
                        buffer[count++] = cmd[1];
                    }
                }
            }
        }

        /// <summary>
        /// Reads the color palette from the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
        /// <param name="colors">The <see cref="T:byte[]"/> containing the colors.</param>
        /// <param name="width">The width of the bitmap.</param>
        /// <param name="height">The height of the bitmap.</param>
        /// <param name="bits">The number of bits per pixel.</param>
        /// <param name="inverted">Whether the bitmap is inverted.</param>
        private void ReadRgbPalette<TPixel>(Buffer2D<TPixel> pixels, byte[] colors, int width, int height, int bits, bool inverted)
            where TPixel : struct, IPixel<TPixel>
        {
            // Pixels per byte (bits per pixel)
            int ppb = 8 / bits;

            int arrayWidth = (width + ppb - 1) / ppb;

            // Bit mask
            int mask = 0xFF >> (8 - bits);

            // Rows are aligned on 4 byte boundaries
            int padding = arrayWidth % 4;
            if (padding != 0)
            {
                padding = 4 - padding;
            }

            using (IManagedByteBuffer row = this.memoryAllocator.AllocateManagedByteBuffer(arrayWidth + padding, AllocationOptions.Clean))
            {
                TPixel color = default;
                var rgba = new Rgba32(0, 0, 0, 255);

                Span<byte> rowSpan = row.GetSpan();

                for (int y = 0; y < height; y++)
                {
                    int newY = Invert(y, height, inverted);
                    this.stream.Read(row.Array, 0, row.Length());
                    int offset = 0;
                    Span<TPixel> pixelRow = pixels.GetRowSpan(newY);

                    // TODO: Could use PixelOperations here!
                    for (int x = 0; x < arrayWidth; x++)
                    {
                        int colOffset = x * ppb;
                        for (int shift = 0, newX = colOffset; shift < ppb && newX < width; shift++, newX++)
                        {
                            int colorIndex = ((rowSpan[offset] >> (8 - bits - (shift * bits))) & mask) * 4;

                            // Stored in b-> g-> r order.
                            rgba.Bgr = Unsafe.As<byte, Bgr24>(ref colors[colorIndex]);
                            color.PackFromRgba32(rgba);
                            pixelRow[newX] = color;
                        }

                        offset++;
                    }
                }
            }
        }

        /// <summary>
        /// Reads the 16 bit color palette from the stream
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
        /// <param name="width">The width of the bitmap.</param>
        /// <param name="height">The height of the bitmap.</param>
        /// <param name="inverted">Whether the bitmap is inverted.</param>
        private void ReadRgb16<TPixel>(Buffer2D<TPixel> pixels, int width, int height, bool inverted)
            where TPixel : struct, IPixel<TPixel>
        {
            int padding = CalculatePadding(width, 2);
            int stride = (width * 2) + padding;
            TPixel color = default;
            var rgba = new Rgba32(0, 0, 0, 255);

            using (IManagedByteBuffer buffer = this.memoryAllocator.AllocateManagedByteBuffer(stride))
            {
                for (int y = 0; y < height; y++)
                {
                    this.stream.Read(buffer.Array, 0, stride);
                    int newY = Invert(y, height, inverted);
                    Span<TPixel> pixelRow = pixels.GetRowSpan(newY);

                    int offset = 0;
                    for (int x = 0; x < width; x++)
                    {
                        short temp = BitConverter.ToInt16(buffer.Array, offset);

                        rgba.R = GetBytesFrom5BitValue((temp & Rgb16RMask) >> 10);
                        rgba.G = GetBytesFrom5BitValue((temp & Rgb16GMask) >> 5);
                        rgba.B = GetBytesFrom5BitValue(temp & Rgb16BMask);

                        color.PackFromRgba32(rgba);
                        pixelRow[x] = color;
                        offset += 2;
                    }
                }
            }
        }

        /// <summary>
        /// Reads the 24 bit color palette from the stream
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
        /// <param name="width">The width of the bitmap.</param>
        /// <param name="height">The height of the bitmap.</param>
        /// <param name="inverted">Whether the bitmap is inverted.</param>
        private void ReadRgb24<TPixel>(Buffer2D<TPixel> pixels, int width, int height, bool inverted)
            where TPixel : struct, IPixel<TPixel>
        {
            int padding = CalculatePadding(width, 3);

            using (IManagedByteBuffer row = this.memoryAllocator.AllocatePaddedPixelRowBuffer(width, 3, padding))
            {
                for (int y = 0; y < height; y++)
                {
                    this.stream.Read(row);
                    int newY = Invert(y, height, inverted);
                    Span<TPixel> pixelSpan = pixels.GetRowSpan(newY);
                    PixelOperations<TPixel>.Instance.PackFromBgr24Bytes(row.GetSpan(), pixelSpan, width);
                }
            }
        }

        /// <summary>
        /// Reads the 32 bit color palette from the stream
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
        /// <param name="width">The width of the bitmap.</param>
        /// <param name="height">The height of the bitmap.</param>
        /// <param name="inverted">Whether the bitmap is inverted.</param>
        private void ReadRgb32<TPixel>(Buffer2D<TPixel> pixels, int width, int height, bool inverted)
            where TPixel : struct, IPixel<TPixel>
        {
            int padding = CalculatePadding(width, 4);

            using (IManagedByteBuffer row = this.memoryAllocator.AllocatePaddedPixelRowBuffer(width, 4, padding))
            {
                for (int y = 0; y < height; y++)
                {
                    this.stream.Read(row);
                    int newY = Invert(y, height, inverted);
                    Span<TPixel> pixelSpan = pixels.GetRowSpan(newY);
                    PixelOperations<TPixel>.Instance.PackFromBgra32Bytes(row.GetSpan(), pixelSpan, width);
                }
            }
        }

        /// <summary>
        /// Reads the <see cref="BmpInfoHeader"/> from the stream.
        /// </summary>
        private void ReadInfoHeader()
        {
#if NETCOREAPP2_1
            Span<byte> buffer = stackalloc byte[BmpInfoHeader.MaxHeaderSize];
#else
            byte[] buffer = new byte[BmpInfoHeader.MaxHeaderSize];
#endif
            this.stream.Read(buffer, 0, BmpInfoHeader.HeaderSizeSize); // read the header size

            int headerSize = BinaryPrimitives.ReadInt32LittleEndian(buffer);
            if (headerSize < BmpInfoHeader.CoreSize)
            {
                throw new NotSupportedException($"ImageSharp does not support this BMP file. HeaderSize: {headerSize}.");
            }

            int skipAmount = 0;
            if (headerSize > BmpInfoHeader.MaxHeaderSize)
            {
                skipAmount = headerSize - BmpInfoHeader.MaxHeaderSize;
                headerSize = BmpInfoHeader.MaxHeaderSize;
            }

            // read the rest of the header
            this.stream.Read(buffer, BmpInfoHeader.HeaderSizeSize, headerSize - BmpInfoHeader.HeaderSizeSize);

            if (headerSize == BmpInfoHeader.CoreSize)
            {
                // 12 bytes
                this.infoHeader = BmpInfoHeader.ParseCore(buffer);
            }
            else if (headerSize >= BmpInfoHeader.Size)
            {
                // >= 40 bytes
                this.infoHeader = BmpInfoHeader.Parse(buffer);
            }
            else
            {
                throw new NotSupportedException($"ImageSharp does not support this BMP file. HeaderSize: {headerSize}.");
            }

            // Resolution is stored in PPM.
            var meta = new ImageMetaData
            {
                ResolutionUnits = PixelResolutionUnit.PixelsPerMeter
            };
            if (this.infoHeader.XPelsPerMeter > 0 && this.infoHeader.YPelsPerMeter > 0)
            {
                meta.HorizontalResolution = this.infoHeader.XPelsPerMeter;
                meta.VerticalResolution = this.infoHeader.YPelsPerMeter;
            }
            else
            {
                // Convert default metadata values to PPM.
                meta.HorizontalResolution = Math.Round(UnitConverter.InchToMeter(ImageMetaData.DefaultHorizontalResolution));
                meta.VerticalResolution = Math.Round(UnitConverter.InchToMeter(ImageMetaData.DefaultVerticalResolution));
            }

            this.metaData = meta;

            short bitsPerPixel = this.infoHeader.BitsPerPixel;
            var bmpMetaData = this.metaData.GetFormatMetaData(BmpFormat.Instance);

            // We can only encode at these bit rates so far.
            if (bitsPerPixel.Equals((short)BmpBitsPerPixel.Pixel24)
                || bitsPerPixel.Equals((short)BmpBitsPerPixel.Pixel32))
            {
                bmpMetaData.BitsPerPixel = (BmpBitsPerPixel)bitsPerPixel;
            }

            // skip the remaining header because we can't read those parts
            this.stream.Skip(skipAmount);
        }

        /// <summary>
        /// Reads the <see cref="BmpFileHeader"/> from the stream.
        /// </summary>
        private void ReadFileHeader()
        {
#if NETCOREAPP2_1
            Span<byte> buffer = stackalloc byte[BmpFileHeader.Size];
#else
            byte[] buffer = new byte[BmpFileHeader.Size];
#endif
            this.stream.Read(buffer, 0, BmpFileHeader.Size);

            this.fileHeader = BmpFileHeader.Parse(buffer);
        }

        /// <summary>
        /// Reads the <see cref="BmpFileHeader"/> and <see cref="BmpInfoHeader"/> from the stream and sets the corresponding fields.
        /// </summary>
        private void ReadImageHeaders(Stream stream, out bool inverted, out byte[] palette)
        {
            this.stream = stream;

            this.ReadFileHeader();
            this.ReadInfoHeader();

            // see http://www.drdobbs.com/architecture-and-design/the-bmp-file-format-part-1/184409517
            // If the height is negative, then this is a Windows bitmap whose origin
            // is the upper-left corner and not the lower-left. The inverted flag
            // indicates a lower-left origin.Our code will be outputting an
            // upper-left origin pixel array.
            inverted = false;
            if (this.infoHeader.Height < 0)
            {
                inverted = true;
                this.infoHeader.Height = -this.infoHeader.Height;
            }

            int colorMapSize = -1;

            if (this.infoHeader.ClrUsed == 0)
            {
                if (this.infoHeader.BitsPerPixel == 1
                    || this.infoHeader.BitsPerPixel == 4
                    || this.infoHeader.BitsPerPixel == 8)
                {
                    colorMapSize = ImageMaths.GetColorCountForBitDepth(this.infoHeader.BitsPerPixel) * 4;
                }
            }
            else
            {
                colorMapSize = this.infoHeader.ClrUsed * 4;
            }

            palette = null;

            if (colorMapSize > 0)
            {
                // 256 * 4
                if (colorMapSize > 1024)
                {
                    throw new ImageFormatException($"Invalid bmp colormap size '{colorMapSize}'");
                }

                palette = new byte[colorMapSize];

                this.stream.Read(palette, 0, colorMapSize);
            }

            this.infoHeader.VerifyDimensions();
        }
    }
}