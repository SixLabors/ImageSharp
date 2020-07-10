// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.
using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// This block of bytes tells the application detailed information
    /// about the image, which will be used to display the image on
    /// the screen.
    /// <see href="https://en.wikipedia.org/wiki/BMP_file_format"/>
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct BmpInfoHeader
    {
        /// <summary>
        /// Defines the size of the BITMAPCOREHEADER data structure in the bitmap file.
        /// </summary>
        public const int CoreSize = 12;

        /// <summary>
        /// Defines the size of the short variant of the OS22XBITMAPHEADER data structure in the bitmap file.
        /// </summary>
        public const int Os22ShortSize = 16;

        /// <summary>
        /// Defines the size of the BITMAPINFOHEADER (BMP Version 3) data structure in the bitmap file.
        /// </summary>
        public const int SizeV3 = 40;

        /// <summary>
        /// Special case of the BITMAPINFOHEADER V3 used by adobe where the color bitmasks are part of the info header instead of following it.
        /// </summary>
        public const int AdobeV3Size = 52;

        /// <summary>
        /// Special case of the BITMAPINFOHEADER V3 used by adobe where the color bitmasks (including the alpha channel) are part of the info header instead of following it.
        /// </summary>
        public const int AdobeV3WithAlphaSize = 56;

        /// <summary>
        /// Size of a IBM OS/2 2.x bitmap header.
        /// </summary>
        public const int Os2v2Size = 64;

        /// <summary>
        /// Defines the size of the BITMAPINFOHEADER (BMP Version 4) data structure in the bitmap file.
        /// </summary>
        public const int SizeV4 = 108;

        /// <summary>
        /// Defines the size of the BITMAPINFOHEADER (BMP Version 5) data structure in the bitmap file.
        /// </summary>
        public const int SizeV5 = 124;

        /// <summary>
        /// Defines the size of the biggest supported header data structure in the bitmap file.
        /// </summary>
        public const int MaxHeaderSize = SizeV5;

        /// <summary>
        /// Defines the size of the <see cref="HeaderSize"/> field.
        /// </summary>
        public const int HeaderSizeSize = 4;

        public BmpInfoHeader(
            int headerSize,
            int width,
            int height,
            short planes,
            short bitsPerPixel,
            BmpCompression compression = default,
            int imageSize = 0,
            int xPelsPerMeter = 0,
            int yPelsPerMeter = 0,
            int clrUsed = 0,
            int clrImportant = 0,
            int redMask = 0,
            int greenMask = 0,
            int blueMask = 0,
            int alphaMask = 0,
            int csType = 0,
            int redX = 0,
            int redY = 0,
            int redZ = 0,
            int greenX = 0,
            int greenY = 0,
            int greenZ = 0,
            int blueX = 0,
            int blueY = 0,
            int blueZ = 0,
            int gammeRed = 0,
            int gammeGreen = 0,
            int gammeBlue = 0)
        {
            this.HeaderSize = headerSize;
            this.Width = width;
            this.Height = height;
            this.Planes = planes;
            this.BitsPerPixel = bitsPerPixel;
            this.Compression = compression;
            this.ImageSize = imageSize;
            this.XPelsPerMeter = xPelsPerMeter;
            this.YPelsPerMeter = yPelsPerMeter;
            this.ClrUsed = clrUsed;
            this.ClrImportant = clrImportant;
            this.RedMask = redMask;
            this.GreenMask = greenMask;
            this.BlueMask = blueMask;
            this.AlphaMask = alphaMask;
            this.CsType = csType;
            this.RedX = redX;
            this.RedY = redY;
            this.RedZ = redZ;
            this.GreenX = greenX;
            this.GreenY = greenY;
            this.GreenZ = greenZ;
            this.BlueX = blueX;
            this.BlueY = blueY;
            this.BlueZ = blueZ;
            this.GammaRed = gammeRed;
            this.GammaGreen = gammeGreen;
            this.GammaBlue = gammeBlue;
        }

        /// <summary>
        /// Gets or sets the size of this header.
        /// </summary>
        public int HeaderSize { get; set; }

        /// <summary>
        /// Gets or sets the bitmap width in pixels (signed integer).
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Gets or sets the bitmap height in pixels (signed integer).
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Gets or sets the number of color planes being used. Must be set to 1.
        /// </summary>
        public short Planes { get; set; }

        /// <summary>
        /// Gets or sets the number of bits per pixel, which is the color depth of the image.
        /// Typical values are 1, 4, 8, 16, 24 and 32.
        /// </summary>
        public short BitsPerPixel { get; set; }

        /// <summary>
        /// Gets or sets the compression method being used.
        /// See the next table for a list of possible values.
        /// </summary>
        public BmpCompression Compression { get; set; }

        /// <summary>
        /// Gets or sets the image size. This is the size of the raw bitmap data (see below),
        /// and should not be confused with the file size.
        /// </summary>
        public int ImageSize { get; set; }

        /// <summary>
        /// Gets or sets the horizontal resolution of the image.
        /// (pixel per meter, signed integer)
        /// </summary>
        public int XPelsPerMeter { get; set; }

        /// <summary>
        /// Gets or sets the vertical resolution of the image.
        /// (pixel per meter, signed integer)
        /// </summary>
        public int YPelsPerMeter { get; set; }

        /// <summary>
        /// Gets or sets the number of colors in the color palette,
        /// or 0 to default to 2^n.
        /// </summary>
        public int ClrUsed { get; set; }

        /// <summary>
        /// Gets or sets the number of important colors used,
        /// or 0 when every color is important{ get; set; } generally ignored.
        /// </summary>
        public int ClrImportant { get; set; }

        /// <summary>
        /// Gets or sets red color mask. This is used with the BITFIELDS decoding.
        /// </summary>
        public int RedMask { get; set; }

        /// <summary>
        /// Gets or sets green color mask. This is used with the BITFIELDS decoding.
        /// </summary>
        public int GreenMask { get; set; }

        /// <summary>
        /// Gets or sets blue color mask. This is used with the BITFIELDS decoding.
        /// </summary>
        public int BlueMask { get; set; }

        /// <summary>
        /// Gets or sets alpha color mask. This is not used yet.
        /// </summary>
        public int AlphaMask { get; set; }

        /// <summary>
        /// Gets or sets the Color space type. Not used yet.
        /// </summary>
        public int CsType { get; set; }

        /// <summary>
        /// Gets or sets the X coordinate of red endpoint. Not used yet.
        /// </summary>
        public int RedX { get; set; }

        /// <summary>
        /// Gets or sets the Y coordinate of red endpoint. Not used yet.
        /// </summary>
        public int RedY { get; set; }

        /// <summary>
        /// Gets or sets the Z coordinate of red endpoint. Not used yet.
        /// </summary>
        public int RedZ { get; set; }

        /// <summary>
        /// Gets or sets the X coordinate of green endpoint. Not used yet.
        /// </summary>
        public int GreenX { get; set; }

        /// <summary>
        /// Gets or sets the Y coordinate of green endpoint. Not used yet.
        /// </summary>
        public int GreenY { get; set; }

        /// <summary>
        /// Gets or sets the Z coordinate of green endpoint. Not used yet.
        /// </summary>
        public int GreenZ { get; set; }

        /// <summary>
        /// Gets or sets the X coordinate of blue endpoint. Not used yet.
        /// </summary>
        public int BlueX { get; set; }

        /// <summary>
        /// Gets or sets the Y coordinate of blue endpoint. Not used yet.
        /// </summary>
        public int BlueY { get; set; }

        /// <summary>
        /// Gets or sets the Z coordinate of blue endpoint. Not used yet.
        /// </summary>
        public int BlueZ { get; set; }

        /// <summary>
        /// Gets or sets the Gamma red coordinate scale value. Not used yet.
        /// </summary>
        public int GammaRed { get; set; }

        /// <summary>
        /// Gets or sets the Gamma green coordinate scale value. Not used yet.
        /// </summary>
        public int GammaGreen { get; set; }

        /// <summary>
        /// Gets or sets the Gamma blue coordinate scale value. Not used yet.
        /// </summary>
        public int GammaBlue { get; set; }

        /// <summary>
        /// Parses the BITMAPCOREHEADER (BMP Version 2) consisting of the headerSize, width, height, planes, and bitsPerPixel fields (12 bytes).
        /// </summary>
        /// <param name="data">The data to parse.</param>
        /// <returns>The parsed header.</returns>
        /// <seealso href="https://msdn.microsoft.com/en-us/library/windows/desktop/dd183372.aspx"/>
        public static BmpInfoHeader ParseCore(ReadOnlySpan<byte> data)
        {
            return new BmpInfoHeader(
                headerSize: BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0, 4)),
                width: BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(4, 2)),
                height: BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(6, 2)),
                planes: BinaryPrimitives.ReadInt16LittleEndian(data.Slice(8, 2)),
                bitsPerPixel: BinaryPrimitives.ReadInt16LittleEndian(data.Slice(10, 2)));
        }

        /// <summary>
        /// Parses a short variant of the OS22XBITMAPHEADER. It is identical to the BITMAPCOREHEADER, except that the width and height
        /// are 4 bytes instead of 2, resulting in 16 bytes total.
        /// </summary>
        /// <param name="data">The data to parse.</param>
        /// <returns>The parsed header.</returns>
        /// <seealso href="https://www.fileformat.info/format/os2bmp/egff.htm"/>
        public static BmpInfoHeader ParseOs22Short(ReadOnlySpan<byte> data)
        {
            return new BmpInfoHeader(
                headerSize: BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0, 4)),
                width: BinaryPrimitives.ReadInt32LittleEndian(data.Slice(4, 4)),
                height: BinaryPrimitives.ReadInt32LittleEndian(data.Slice(8, 4)),
                planes: BinaryPrimitives.ReadInt16LittleEndian(data.Slice(12, 2)),
                bitsPerPixel: BinaryPrimitives.ReadInt16LittleEndian(data.Slice(14, 2)));
        }

        /// <summary>
        /// Parses the full BMP Version 3 BITMAPINFOHEADER header (40 bytes).
        /// </summary>
        /// <param name="data">The data to parse.</param>
        /// <returns>The parsed header.</returns>
        /// <seealso href="http://www.fileformat.info/format/bmp/egff.htm"/>
        public static BmpInfoHeader ParseV3(ReadOnlySpan<byte> data)
        {
            return new BmpInfoHeader(
                headerSize: BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0, 4)),
                width: BinaryPrimitives.ReadInt32LittleEndian(data.Slice(4, 4)),
                height: BinaryPrimitives.ReadInt32LittleEndian(data.Slice(8, 4)),
                planes: BinaryPrimitives.ReadInt16LittleEndian(data.Slice(12, 2)),
                bitsPerPixel: BinaryPrimitives.ReadInt16LittleEndian(data.Slice(14, 2)),
                compression: (BmpCompression)BinaryPrimitives.ReadInt32LittleEndian(data.Slice(16, 4)),
                imageSize: BinaryPrimitives.ReadInt32LittleEndian(data.Slice(20, 4)),
                xPelsPerMeter: BinaryPrimitives.ReadInt32LittleEndian(data.Slice(24, 4)),
                yPelsPerMeter: BinaryPrimitives.ReadInt32LittleEndian(data.Slice(28, 4)),
                clrUsed: BinaryPrimitives.ReadInt32LittleEndian(data.Slice(32, 4)),
                clrImportant: BinaryPrimitives.ReadInt32LittleEndian(data.Slice(36, 4)));
        }

        /// <summary>
        /// Special case of the BITMAPINFOHEADER V3 used by adobe where the color bitmasks are part of the info header instead of following it.
        /// 52 bytes without the alpha mask, 56 bytes with the alpha mask.
        /// </summary>
        /// <param name="data">The data to parse.</param>
        /// <param name="withAlpha">Indicates, if the alpha bitmask is present.</param>
        /// <returns>The parsed header.</returns>
        /// <seealso href="https://forums.adobe.com/message/3272950#3272950"/>
        public static BmpInfoHeader ParseAdobeV3(ReadOnlySpan<byte> data, bool withAlpha = true)
        {
            return new BmpInfoHeader(
                headerSize: BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0, 4)),
                width: BinaryPrimitives.ReadInt32LittleEndian(data.Slice(4, 4)),
                height: BinaryPrimitives.ReadInt32LittleEndian(data.Slice(8, 4)),
                planes: BinaryPrimitives.ReadInt16LittleEndian(data.Slice(12, 2)),
                bitsPerPixel: BinaryPrimitives.ReadInt16LittleEndian(data.Slice(14, 2)),
                compression: (BmpCompression)BinaryPrimitives.ReadInt32LittleEndian(data.Slice(16, 4)),
                imageSize: BinaryPrimitives.ReadInt32LittleEndian(data.Slice(20, 4)),
                xPelsPerMeter: BinaryPrimitives.ReadInt32LittleEndian(data.Slice(24, 4)),
                yPelsPerMeter: BinaryPrimitives.ReadInt32LittleEndian(data.Slice(28, 4)),
                clrUsed: BinaryPrimitives.ReadInt32LittleEndian(data.Slice(32, 4)),
                clrImportant: BinaryPrimitives.ReadInt32LittleEndian(data.Slice(36, 4)),
                redMask: BinaryPrimitives.ReadInt32LittleEndian(data.Slice(40, 4)),
                greenMask: BinaryPrimitives.ReadInt32LittleEndian(data.Slice(44, 4)),
                blueMask: BinaryPrimitives.ReadInt32LittleEndian(data.Slice(48, 4)),
                alphaMask: withAlpha ? BinaryPrimitives.ReadInt32LittleEndian(data.Slice(52, 4)) : 0);
        }

        /// <summary>
        /// Parses a OS/2 version 2 bitmap header (64 bytes). Only the first 40 bytes are parsed which are
        /// very similar to the Bitmap v3 header. The other 24 bytes are ignored, but they do not hold any
        /// useful information for decoding the image.
        /// </summary>
        /// <param name="data">The data to parse.</param>
        /// <returns>The parsed header.</returns>
        /// <seealso href="https://www.fileformat.info/format/os2bmp/egff.htm"/>
        public static BmpInfoHeader ParseOs2Version2(ReadOnlySpan<byte> data)
        {
            var infoHeader = new BmpInfoHeader(
                headerSize: BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0, 4)),
                width: BinaryPrimitives.ReadInt32LittleEndian(data.Slice(4, 4)),
                height: BinaryPrimitives.ReadInt32LittleEndian(data.Slice(8, 4)),
                planes: BinaryPrimitives.ReadInt16LittleEndian(data.Slice(12, 2)),
                bitsPerPixel: BinaryPrimitives.ReadInt16LittleEndian(data.Slice(14, 2)));

            int compression = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(16, 4));

            // The compression value in OS/2 bitmap has a different meaning than in windows bitmaps.
            // Map the OS/2 value to the windows values.
            switch (compression)
            {
                case 0:
                    infoHeader.Compression = BmpCompression.RGB;
                    break;
                case 1:
                    infoHeader.Compression = BmpCompression.RLE8;
                    break;
                case 2:
                    infoHeader.Compression = BmpCompression.RLE4;
                    break;
                case 4:
                    infoHeader.Compression = BmpCompression.RLE24;
                    break;
                default:
                    // Compression type 3 (1DHuffman) is not supported.
                    BmpThrowHelper.ThrowInvalidImageContentException("Compression type is not supported. ImageSharp only supports uncompressed, RLE4, RLE8 and RLE24.");
                    break;
            }

            infoHeader.ImageSize = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(20, 4));
            infoHeader.XPelsPerMeter = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(24, 4));
            infoHeader.YPelsPerMeter = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(28, 4));
            infoHeader.ClrUsed = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(32, 4));
            infoHeader.ClrImportant = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(36, 4));

            // The following 24 bytes of the header are omitted.
            return infoHeader;
        }

        /// <summary>
        /// Parses the full BMP Version 4 BITMAPINFOHEADER header (108 bytes).
        /// </summary>
        /// <param name="data">The data to parse.</param>
        /// <returns>The parsed header.</returns>
        /// <seealso href="http://www.fileformat.info/format/bmp/egff.htm"/>
        public static BmpInfoHeader ParseV4(ReadOnlySpan<byte> data)
        {
            if (data.Length < SizeV4)
            {
                throw new ArgumentException(nameof(data), $"Must be {SizeV4} bytes. Was {data.Length} bytes.");
            }

            return MemoryMarshal.Cast<byte, BmpInfoHeader>(data)[0];
        }

        /// <summary>
        /// Writes a bitmap version 3 (Microsoft Windows NT) header to a buffer (40 bytes).
        /// </summary>
        /// <param name="buffer">The buffer to write to.</param>
        public void WriteV3Header(Span<byte> buffer)
        {
            buffer.Clear();
            BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(0, 4), SizeV3);
            BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(4, 4), this.Width);
            BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(8, 4), this.Height);
            BinaryPrimitives.WriteInt16LittleEndian(buffer.Slice(12, 2), this.Planes);
            BinaryPrimitives.WriteInt16LittleEndian(buffer.Slice(14, 2), this.BitsPerPixel);
            BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(16, 4), (int)this.Compression);
            BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(20, 4), this.ImageSize);
            BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(24, 4), this.XPelsPerMeter);
            BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(28, 4), this.YPelsPerMeter);
            BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(32, 4), this.ClrUsed);
            BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(36, 4), this.ClrImportant);
        }

        /// <summary>
        /// Writes a complete Bitmap V4 header to a buffer.
        /// </summary>
        /// <param name="buffer">The buffer to write to.</param>
        public void WriteV4Header(Span<byte> buffer)
        {
            ref BmpInfoHeader dest = ref Unsafe.As<byte, BmpInfoHeader>(ref MemoryMarshal.GetReference(buffer));

            dest = this;
        }

        internal void VerifyDimensions()
        {
            const int MaximumBmpDimension = 65535;

            if (this.Width > MaximumBmpDimension || this.Height > MaximumBmpDimension)
            {
                throw new InvalidOperationException(
                    $"The input bmp '{this.Width}x{this.Height}' is "
                    + $"bigger then the max allowed size '{MaximumBmpDimension}x{MaximumBmpDimension}'");
            }
        }
    }
}
