// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// The Logical Screen Descriptor contains the parameters
    /// necessary to define the area of the display device
    /// within which the images will be rendered
    /// </summary>
    internal readonly struct GifLogicalScreenDescriptor
    {
        /// <summary>
        /// The size of the written structure.
        /// </summary>
        public const int Size = 7;

        public GifLogicalScreenDescriptor(
            ushort width,
            ushort height,
            int bitsPerPixel,
            byte backgroundColorIndex,
            byte pixelAspectRatio,
            bool globalColorTableFlag,
            int globalColorTableSize)
        {
            this.Width = width;
            this.Height = height;
            this.BitsPerPixel = bitsPerPixel;
            this.BackgroundColorIndex = backgroundColorIndex;
            this.PixelAspectRatio = pixelAspectRatio;
            this.GlobalColorTableFlag = globalColorTableFlag;
            this.GlobalColorTableSize = globalColorTableSize;
        }

        /// <summary>
        /// Gets the width, in pixels, of the Logical Screen where the images will
        /// be rendered in the displaying device.
        /// </summary>
        public ushort Width { get; }

        /// <summary>
        /// Gets the height, in pixels, of the Logical Screen where the images will be
        /// rendered in the displaying device.
        /// </summary>
        public ushort Height { get; }

        /// <summary>
        /// Gets the color depth, in number of bits per pixel.
        /// </summary>
        public int BitsPerPixel { get; }

        /// <summary>
        /// Gets the index at the Global Color Table for the Background Color.
        /// The Background Color is the color used for those
        /// pixels on the screen that are not covered by an image.
        /// </summary>
        public byte BackgroundColorIndex { get; }

        /// <summary>
        /// Gets the pixel aspect ratio. Default to 0.
        /// </summary>
        public byte PixelAspectRatio { get; }

        /// <summary>
        /// Gets a value indicating whether a flag denoting the presence of a Global Color Table
        /// should be set.
        /// If the flag is set, the Global Color Table will immediately
        /// follow the Logical Screen Descriptor.
        /// </summary>
        public bool GlobalColorTableFlag { get; }

        /// <summary>
        /// Gets the global color table size.
        /// If the Global Color Table Flag is set to 1,
        /// the value in this field is used to calculate the number of
        /// bytes contained in the Global Color Table.
        /// </summary>
        public int GlobalColorTableSize { get; }

        public byte PackFields()
        {
            PackedField field = default;

            field.SetBit(0, this.GlobalColorTableFlag);     // 0   : Global Color Table Flag     | 1 bit
            field.SetBits(1, 3, this.GlobalColorTableSize); // 1-3 : Color Resolution            | 3 bits
            field.SetBit(4, false);                         // 4   : Sort Flag                   | 1 bits
            field.SetBits(5, 3, this.GlobalColorTableSize); // 5-7 : Size of Global Color Table  | 3 bits

            return field.Byte;
        }

        public void WriteTo(Span<byte> buffer)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(0, 2), this.Width);  // Logical Screen Width
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(2, 2), this.Height); // Logical Screen Height
            buffer[4] = this.PackFields();                                             // Packed Fields
            buffer[5] = this.BackgroundColorIndex;                                     // Background Color Index
            buffer[6] = this.PixelAspectRatio;                                         // Pixel Aspect Ratio
        }

        public static GifLogicalScreenDescriptor Parse(ReadOnlySpan<byte> buffer)
        {
            byte packed = buffer[4];

            var result = new GifLogicalScreenDescriptor(
                width: BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(0, 2)),
                height: BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(2, 2)),
                bitsPerPixel: (buffer[4] & 0x07) + 1,  // The lowest 3 bits represent the bit depth minus 1
                backgroundColorIndex: buffer[5],
                pixelAspectRatio: buffer[6],
                globalColorTableFlag: ((packed & 0x80) >> 7) == 1,
                globalColorTableSize: 2 << (packed & 0x07));

            if (result.GlobalColorTableSize > 255 * 4)
            {
                throw new ImageFormatException($"Invalid gif colormap size '{result.GlobalColorTableSize}'");
            }

            return result;
        }
    }
}