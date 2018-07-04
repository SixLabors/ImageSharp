// Copyright (c) Six Labors and contributors.
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
        /// Defines the size of the BITMAPINFOHEADER data structure in the bitmap file.
        /// </summary>
        public const int Size = 40;

        /// <summary>
        /// Defines the size of the BITMAPCOREHEADER data structure in the bitmap file.
        /// </summary>
        public const int CoreSize = 12;

        /// <summary>
        /// Defines the size of the biggest supported header data structure in the bitmap file.
        /// </summary>
        public const int MaxHeaderSize = Size;

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
            int clrImportant = 0)
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
        }

        /// <summary>
        /// Gets or sets the size of this header
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
        /// Parses the full BITMAPINFOHEADER header (40 bytes).
        /// </summary>
        /// <param name="data">The data to parse.</param>
        /// <returns>Parsed header</returns>
        /// <seealso href="https://msdn.microsoft.com/en-us/library/windows/desktop/dd183376.aspx"/>
        public static BmpInfoHeader Parse(ReadOnlySpan<byte> data)
        {
            if (data.Length != Size)
            {
                throw new ArgumentException(nameof(data), $"Must be {Size} bytes. Was {data.Length} bytes.");
            }

            return MemoryMarshal.Cast<byte, BmpInfoHeader>(data)[0];
        }

        /// <summary>
        /// Parses the BITMAPCOREHEADER consisting of the headerSize, width, height, planes, and bitsPerPixel fields (12 bytes).
        /// </summary>
        /// <param name="data">The data to parse,</param>
        /// <returns>Parsed header</returns>
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

        public unsafe void WriteTo(Span<byte> buffer)
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