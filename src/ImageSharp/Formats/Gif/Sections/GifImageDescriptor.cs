// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// Each image in the Data Stream is composed of an Image Descriptor,
    /// an optional Local Color Table, and the image data.
    /// Each image must fit within the boundaries of the
    /// Logical Screen, as defined in the Logical Screen Descriptor.
    /// </summary>
    internal readonly struct GifImageDescriptor
    {
        public const int Size = 10;

        public GifImageDescriptor(
            ushort left,
            ushort top,
            ushort width,
            ushort height,
            bool localColorTableFlag,
            int localColorTableSize,
            bool interlaceFlag = false,
            bool sortFlag = false)
        {
            this.Left = left;
            this.Top = top;
            this.Width = width;
            this.Height = height;
            this.LocalColorTableFlag = localColorTableFlag;
            this.LocalColorTableSize = localColorTableSize;
            this.InterlaceFlag = interlaceFlag;
            this.SortFlag = sortFlag;
        }

        /// <summary>
        /// Gets the column number, in pixels, of the left edge of the image,
        /// with respect to the left edge of the Logical Screen.
        /// Leftmost column of the Logical Screen is 0.
        /// </summary>
        public ushort Left { get; }

        /// <summary>
        /// Gets the row number, in pixels, of the top edge of the image with
        /// respect to the top edge of the Logical Screen.
        /// Top row of the Logical Screen is 0.
        /// </summary>
        public ushort Top { get; }

        /// <summary>
        /// Gets the width of the image in pixels.
        /// </summary>
        public ushort Width { get; }

        /// <summary>
        /// Gets the height of the image in pixels.
        /// </summary>
        public ushort Height { get; }

        /// <summary>
        /// Gets a value indicating whether the presence of a Local Color Table immediately
        /// follows this Image Descriptor.
        /// </summary>
        public bool LocalColorTableFlag { get; }

        /// <summary>
        /// Gets the local color table size.
        /// If the Local Color Table Flag is set to 1, the value in this field
        /// is used to calculate the number of bytes contained in the Local Color Table.
        /// </summary>
        public int LocalColorTableSize { get; }

        /// <summary>
        /// Gets a value indicating whether the image is to be interlaced.
        /// An image is interlaced in a four-pass interlace pattern.
        /// </summary>
        public bool InterlaceFlag { get; }

        /// <summary>
        /// Gets a value indicating whether the Global Color Table is sorted.
        /// </summary>
        public bool SortFlag { get; }

        public byte PackFields()
        {
            var field = default(PackedField);

            field.SetBit(0, this.LocalColorTableFlag);          // 0: Local color table flag = 1 (LCT used)
            field.SetBit(1, this.InterlaceFlag);                // 1: Interlace flag 0
            field.SetBit(2, this.SortFlag);                     // 2: Sort flag 0
            field.SetBits(5, 3, this.LocalColorTableSize - 1);  // 3-4: Reserved, 5-7 : LCT size. 2^(N+1)

            return field.Byte;
        }

        public void WriteTo(Span<byte> buffer)
        {
            buffer[0] = GifConstants.ImageDescriptorLabel;                              // Image Separator (0x2C)
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(1, 2), this.Left);    // Image Left Position
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(3, 2), this.Top);     // Image Top Position
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(5, 2), this.Width);   // Image Width
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(7, 2), this.Height);  // Image Height
            buffer[9] = this.PackFields();                                              // Packed Fields
        }

        public static GifImageDescriptor Parse(ReadOnlySpan<byte> buffer)
        {
            byte packed = buffer[8];

            return new GifImageDescriptor(
                 left: BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(0, 2)),
                 top: BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(2, 2)),
                 width: BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(4, 2)),
                 height: BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(6, 2)),
                 localColorTableFlag: ((packed & 0x80) >> 7) == 1,
                 localColorTableSize: 2 << (packed & 0x07),
                 interlaceFlag: ((packed & 0x40) >> 6) == 1);
        }
    }
}