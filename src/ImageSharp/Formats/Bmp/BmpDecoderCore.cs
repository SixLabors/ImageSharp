// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Performs the bmp decoding operation.
    /// </summary>
    internal sealed class BmpDecoderCore
    {
        /// <summary>
        /// For Windows Mobile version 5.0 and later, you can OR any of the values BI_RGB,
        /// BI_BITFIELDS and BI_ALPHABITFIELDS with <c>SourcePreRotateMask</c> to specify that the source
        /// DIB section has the same rotation angle as the destination.
        /// Otherwise, the image can be rotated 90 degrees anti-clockwise (Landscape/Portrait).
        /// https://msdn.microsoft.com/en-us/library/aa452495.aspx
        /// </summary>
        private const uint SourcePreRotateMask = 0x8000;

        /// <summary>
        /// The mask for the red part of the color for 16-bit RGB bitmaps.
        /// </summary>
        private const int Rgb16RMask = 0x00007C00;

        /// <summary>
        /// The mask for the green part of the color for 16-bit RGB bitmaps.
        /// </summary>
        private const int Rgb16GMask = 0x000003E0;

        /// <summary>
        /// The mask for the blue part of the color for 16 bit RGB bitmaps.
        /// </summary>
        private const int Rgb16BMask = 0x0000001F;

        /// <summary>
        /// The stream to decode from.
        /// </summary>
        private Stream currentStream;

        /// <summary>
        /// The file header containing general information.
        /// TODO: Why is this not used? We advance the stream but do not use the values parsed.
        /// </summary>
        private BmpFileHeader fileHeader;

        /// <summary>
        /// The info header containing detailed information about the bitmap.
        /// </summary>
        private BmpInfoHeader infoHeader;

        private Configuration configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="BmpDecoderCore"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="options">The options</param>
        public BmpDecoderCore(Configuration configuration, IBmpDecoderOptions options)
        {
            this.configuration = configuration;
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
            this.currentStream = stream;

            try
            {
                this.ReadFileHeader();
                this.ReadInfoHeader();

                int colorMapSize = -1;
                if (this.infoHeader.ClrUsed == 0)
                {
                    if (this.infoHeader.BitsPerPixel == ((int)BmpBitsPerPixel.MonoChrome) ||
                        this.infoHeader.BitsPerPixel == ((int)BmpBitsPerPixel.Palette4) ||
                        this.infoHeader.BitsPerPixel == ((int)BmpBitsPerPixel.Palette16) ||
                        this.infoHeader.BitsPerPixel == ((int)BmpBitsPerPixel.Palette256))
                    {
                        colorMapSize = (int)Math.Pow(2, this.infoHeader.BitsPerPixel) * 4;
                    }
                }
                else
                {
                    colorMapSize = this.infoHeader.ClrUsed * 4;
                }

                byte[] palette = null;

                if (colorMapSize > 0)
                {
                    // 256 * 4
                    if (colorMapSize > 1024)
                    {
                        throw new ImageFormatException($"Invalid bmp colormap size '{colorMapSize}'");
                    }

                    palette = new byte[colorMapSize];

                    this.currentStream.Read(palette, 0, colorMapSize);
                }

                if (this.infoHeader.Width > int.MaxValue || this.infoHeader.Height > int.MaxValue)
                {
                    throw new ArgumentOutOfRangeException(
                        $"The input bitmap '{this.infoHeader.Width}x{this.infoHeader.Height}' is "
                        + $"bigger then the max allowed size '{int.MaxValue}x{int.MaxValue}'");
                }

                var image = new Image<TPixel>(this.configuration, this.infoHeader.Width, this.infoHeader.Height);
                using (PixelAccessor<TPixel> pixels = image.Lock())
                {
                    switch (this.infoHeader.Compression)
                    {
                        case BmpCompression.RGB:
                            if (this.infoHeader.BitsPerPixel == 32)
                            {
                                this.ReadRgb32(pixels, this.infoHeader.Width, this.infoHeader.Height, this.infoHeader.IsTopDown);
                            }
                            else if (this.infoHeader.BitsPerPixel == 24)
                            {
                                this.ReadRgb24(pixels, this.infoHeader.Width, this.infoHeader.Height, this.infoHeader.IsTopDown);
                            }
                            else if (this.infoHeader.BitsPerPixel == 16)
                            {
                                this.ReadRgb16(pixels, this.infoHeader.Width, this.infoHeader.Height, this.infoHeader.IsTopDown);
                            }
                            else if (this.infoHeader.BitsPerPixel <= 8)
                            {
                                this.ReadRgbPalette(pixels, palette, this.infoHeader.Width, this.infoHeader.Height, this.infoHeader.BitsPerPixel, this.infoHeader.IsTopDown);
                            }

                            break;
                        default:
                            throw new NotSupportedException("Does not support this kind of bitmap files.");
                    }
                }

                return image;
            }
            catch (IndexOutOfRangeException e)
            {
                throw new ImageFormatException("Bitmap does not have a valid format.", e);
            }
        }

        /// <summary>
        /// Returns the y- value based on the given height.
        /// </summary>
        /// <param name="y">The y- value representing the current row.</param>
        /// <param name="height">The height of the bitmap.</param>
        /// <param name="inverted">Whether the bitmap is inverted.</param>
        /// <returns>The <see cref="int"/> representing the inverted value.</returns>
        private static int Invert(int y, int height, bool inverted)
        {
            int row;

            if (!inverted)
            {
                row = height - y - 1;
            }
            else
            {
                row = y;
            }

            return row;
        }

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
        /// Reads the color palette from the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The <see cref="PixelAccessor{TPixel}"/> to assign the palette to.</param>
        /// <param name="colors">The <see cref="T:byte[]"/> containing the colors.</param>
        /// <param name="width">The width of the bitmap.</param>
        /// <param name="height">The height of the bitmap.</param>
        /// <param name="bits">The number of bits per pixel.</param>
        /// <param name="inverted">Whether the bitmap is inverted.</param>
        private void ReadRgbPalette<TPixel>(PixelAccessor<TPixel> pixels, byte[] colors, int width, int height, int bits, bool inverted)
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

            byte[] row = new byte[arrayWidth + padding];
            var color = default(TPixel);

            var rgba = default(Rgba32);

            for (int y = 0; y < height; y++)
            {
                int newY = Invert(y, height, inverted);
                this.currentStream.Read(row, 0, row.Length);
                int offset = 0;
                Span<TPixel> pixelRow = pixels.GetRowSpan(y);

                // TODO: Could use PixelOperations here!
                for (int x = 0; x < arrayWidth; x++)
                {
                    int colOffset = x * ppb;

                    for (int shift = 0; shift < ppb && (x + shift) < width; shift++)
                    {
                        int colorIndex = ((row[offset] >> (8 - bits - (shift * bits))) & mask) * 4;
                        int newX = colOffset + shift;

                        // Stored in b-> g-> r order.
                        rgba.Bgr = Unsafe.As<byte, Bgr24>(ref colors[colorIndex]);
                        color.PackFromRgba32(rgba);
                        pixelRow[newX] = color;
                    }

                    offset++;
                }
            }
        }

        /// <summary>
        /// Reads the 16 bit color palette from the stream
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The <see cref="PixelAccessor{TPixel}"/> to assign the palette to.</param>
        /// <param name="width">The width of the bitmap.</param>
        /// <param name="height">The height of the bitmap.</param>
        /// <param name="inverted">Whether the bitmap is inverted.</param>
        private void ReadRgb16<TPixel>(PixelAccessor<TPixel> pixels, int width, int height, bool inverted)
            where TPixel : struct, IPixel<TPixel>
        {
            // We divide here as we will store the colors in our floating point format.
            const int ScaleR = 8; // 256/32
            const int ScaleG = 4; // 256/64
            const int ComponentCount = 2;

            var color = default(TPixel);
            var rgba = new Rgba32(0, 0, 0, 255);

            using (var row = new PixelArea<TPixel>(width, ComponentOrder.Xyz))
            {
                for (int y = 0; y < height; y++)
                {
                    row.Read(this.currentStream);

                    int newY = Invert(y, height, inverted);

                    Span<TPixel> pixelRow = pixels.GetRowSpan(newY);

                    int offset = 0;
                    for (int x = 0; x < width; x++)
                    {
                        short temp = BitConverter.ToInt16(row.Bytes, offset);

                        rgba.R = (byte)(((temp & Rgb16RMask) >> 11) * ScaleR);
                        rgba.G = (byte)(((temp & Rgb16GMask) >> 5) * ScaleG);
                        rgba.B = (byte)((temp & Rgb16BMask) * ScaleR);

                        color.PackFromRgba32(rgba);
                        pixelRow[x] = color;
                        offset += ComponentCount;
                    }
                }
            }
        }

        /// <summary>
        /// Reads the 24 bit color palette from the stream
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The <see cref="PixelAccessor{TPixel}"/> to assign the palette to.</param>
        /// <param name="width">The width of the bitmap.</param>
        /// <param name="height">The height of the bitmap.</param>
        /// <param name="inverted">Whether the bitmap is inverted.</param>
        private void ReadRgb24<TPixel>(PixelAccessor<TPixel> pixels, int width, int height, bool inverted)
            where TPixel : struct, IPixel<TPixel>
        {
            int padding = CalculatePadding(width, 3);
            using (var row = new PixelArea<TPixel>(width, ComponentOrder.Zyx, padding))
            {
                for (int y = 0; y < height; y++)
                {
                    row.Read(this.currentStream);

                    int newY = Invert(y, height, inverted);
                    pixels.CopyFrom(row, newY);
                }
            }
        }

        /// <summary>
        /// Reads the 32 bit color palette from the stream
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The <see cref="PixelAccessor{TPixel}"/> to assign the palette to.</param>
        /// <param name="width">The width of the bitmap.</param>
        /// <param name="height">The height of the bitmap.</param>
        /// <param name="inverted">Whether the bitmap is inverted.</param>
        private void ReadRgb32<TPixel>(PixelAccessor<TPixel> pixels, int width, int height, bool inverted)
            where TPixel : struct, IPixel<TPixel>
        {
            int padding = CalculatePadding(width, 4);
            using (var row = new PixelArea<TPixel>(width, ComponentOrder.Zyxw, padding))
            {
                for (int y = 0; y < height; y++)
                {
                    row.Read(this.currentStream);

                    int newY = Invert(y, height, inverted);
                    pixels.CopyFrom(row, newY);
                }
            }
        }

        /// <summary>
        /// Reads the <see cref="BmpInfoHeader"/> from the stream.
        /// </summary>
        private void ReadInfoHeader()
        {
            byte[] data = new byte[(int)BmpNativeStructuresSizes.BITMAPV5HEADER];

            // read header size
            this.currentStream.Read(data, 0, sizeof(uint));
            int headerSize = BitConverter.ToInt32(data, 0);

            // read the rest of the header
            int skipAmmount = 0;
            if (headerSize > (int)BmpNativeStructuresSizes.BITMAPV5HEADER)
            {
                // FIXME: If the header size is bigger than BITMAPV5HEADER structure, this is a bug
                skipAmmount = headerSize - (int)BmpNativeStructuresSizes.BITMAPV5HEADER;
                headerSize = (int)BmpNativeStructuresSizes.BITMAPV5HEADER;
            }

            this.currentStream.Read(data, sizeof(uint), headerSize - sizeof(uint));

            switch (headerSize)
            {
                // Windows DIB Info Header v2 and OS/2 DIB Info Header v1
                case (int)BmpNativeStructuresSizes.BITMAPCOREHEADER:
                    this.infoHeader = this.ParseBitmapCoreHeader(data);
                    break;

                // OS/2 DIB Info Header v2 minimum size
                case (int)BmpNativeStructuresSizes.OS22XBITMAPHEADER_MIN:
                    throw new NotSupportedException($"This kind of bitmap files (header size $headerSize) is not supported.");
                    break;

                // OS/2 DIB Info Header v2 maximum size
                case (int)BmpNativeStructuresSizes.OS22XBITMAPHEADER:
                    throw new NotSupportedException($"This kind of bitmap files (header size $headerSize) is not supported.");
                    break;

                // Windows DIB Info Header v4
                case (int)BmpNativeStructuresSizes.BITMAPV4HEADER:
                    this.infoHeader = this.ParseBitmapInfoHeader(data);
                    break;

                // Windows DIB Info Header v5
                case (int)BmpNativeStructuresSizes.BITMAPV5HEADER:
                    this.infoHeader = this.ParseBitmapInfoHeader(data);
                    break;

                default:
                    if ((headerSize == (int)BmpNativeStructuresSizes.BITMAPINFOHEADER) ||
                        (headerSize == (int)BmpNativeStructuresSizes.BITMAPINFOHEADER_NT) ||
                        (headerSize == (int)BmpNativeStructuresSizes.BITMAPINFOHEADER_CE))
                    {
                        // Windows DIB Info Header v3
                        this.infoHeader = this.ParseBitmapInfoHeader(data);
                        break;
                    }
                    else if ((headerSize > (int)BmpNativeStructuresSizes.OS22XBITMAPHEADER_MIN) &&
                        (headerSize < (int)BmpNativeStructuresSizes.OS22XBITMAPHEADER_MAX))
                    {
                        // OS/2 DIB Info Header v2 variable size
                        throw new NotSupportedException($"This kind of bitmap files (header size $headerSize) is not supported.");
                        break;
                    }
                    else
                    {
                        // UPS! Unknow DIB header
                        throw new NotSupportedException($"This kind of bitmap files (header size $headerSize) is not supported.");
                    }
            }

            // Check if the DIB is top-down (negative height => origin is the upper-left corner)
            // or bottom-up (positeve height => origin is the lower-left corner)
            if (this.infoHeader.Height < 0)
            {
                this.infoHeader.IsTopDown = true;
                this.infoHeader.Height = -this.infoHeader.Height;
            }

            // Check if the DIB is pre-rotated
            if ((SourcePreRotateMask & (uint)this.infoHeader.Compression) == SourcePreRotateMask)
            {
                this.infoHeader.IsSourcePreRotate = true;
                this.infoHeader.Compression = (BmpCompression)((uint)this.infoHeader.Compression & (~SourcePreRotateMask));
            }

            // skip the remaining header because we can't read those parts
            this.currentStream.Skip(skipAmmount);
        }

        /// <summary>
        /// Parses the <see cref="BmpInfoHeader"/> from the stream, assuming it uses the WinCoreHeader format.
        /// <seealso href="https://msdn.microsoft.com/en-us/library/windows/desktop/dd183372.aspx">See this MSDN link for more information.</seealso>
        /// </summary>
        /// <param name="data">Header bytes read from the stream</param>
        /// <returns>Parsed header</returns>
        private BmpInfoHeader ParseBitmapCoreHeader(byte[] data)
        {
            return new BmpInfoHeader
            {
                HeaderSize = BitConverter.ToInt32(data, 0),
                Width = BitConverter.ToUInt16(data, 4),
                Height = BitConverter.ToUInt16(data, 6),
                Planes = BitConverter.ToInt16(data, 8),
                BitsPerPixel = BitConverter.ToInt16(data, 10),

                // the rest is not present in the core header
                ImageSize = 0,
                XPelsPerMeter = 0,
                YPelsPerMeter = 0,
                ClrUsed = 0,
                ClrImportant = 0,
                Compression = BmpCompression.RGB
            };
        }

        /// <summary>
        /// Parses the <see cref="BmpInfoHeader"/> from the stream, assuming it uses the <c>BITMAPINFOHEADER</c> format.
        /// <seealso href="https://msdn.microsoft.com/en-us/library/windows/desktop/dd183376.aspx">See this MSDN link for more information.</seealso>
        /// </summary>
        /// <param name="data">Header bytes read from the stream</param>
        /// <returns>Parsed header</returns>
        private BmpInfoHeader ParseBitmapInfoHeader(byte[] data)
        {
            return new BmpInfoHeader
            {
                HeaderSize = BitConverter.ToInt32(data, 0),
                Width = BitConverter.ToInt32(data, 4),
                Height = BitConverter.ToInt32(data, 8),
                Planes = BitConverter.ToInt16(data, 12),
                BitsPerPixel = BitConverter.ToInt16(data, 14),
                ImageSize = BitConverter.ToInt32(data, 20),
                XPelsPerMeter = BitConverter.ToInt32(data, 24),
                YPelsPerMeter = BitConverter.ToInt32(data, 28),
                ClrUsed = BitConverter.ToInt32(data, 32),
                ClrImportant = BitConverter.ToInt32(data, 36),
                Compression = (BmpCompression)BitConverter.ToInt32(data, 16)
            };
        }

        /// <summary>
        /// Reads the <see cref="BmpFileHeader"/> from the stream.
        /// </summary>
        private void ReadFileHeader()
        {
            byte[] data = new byte[BmpFileHeader.Size];

            this.currentStream.Read(data, 0, BmpFileHeader.Size);

            this.fileHeader = new BmpFileHeader
            {
                Type = BitConverter.ToUInt16(data, 0),
                FileSize = BitConverter.ToUInt32(data, 2),
                Reserved1 = BitConverter.ToUInt16(data, 6),
                Reserved2 = BitConverter.ToUInt16(data, 8),
                Offset = BitConverter.ToUInt32(data, 10)
            };
        }
    }
}
