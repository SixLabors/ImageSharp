// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// The Logical Screen Descriptor contains the parameters
    /// necessary to define the area of the display device
    /// within which the images will be rendered
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal readonly struct GifLogicalScreenDescriptor
    {
        public const int Size = 7;

        public GifLogicalScreenDescriptor(
            ushort width,
            ushort height,
            byte packed,
            byte backgroundColorIndex,
            byte pixelAspectRatio = 0)
        {
            this.Width = width;
            this.Height = height;
            this.Packed = packed;
            this.BackgroundColorIndex = backgroundColorIndex;
            this.PixelAspectRatio = pixelAspectRatio;
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
        /// Gets the packed value consisting of:
        /// globalColorTableFlag, colorResolution, sortFlag, and sizeOfGlobalColorTable.
        /// </summary>
        public byte Packed { get; }

        /// <summary>
        /// Gets the index at the Global Color Table for the Background Color.
        /// The Background Color is the color used for those
        /// pixels on the screen that are not covered by an image.
        /// </summary>
        public byte BackgroundColorIndex { get; }

        /// <summary>
        /// Gets the pixel aspect ratio.
        /// </summary>
        public byte PixelAspectRatio { get; }

        /// <summary>
        /// Gets a value indicating whether a flag denoting the presence of a Global Color Table
        /// should be set.
        /// If the flag is set, the Global Color Table will included after
        /// the Logical Screen Descriptor.
        /// </summary>
        public bool GlobalColorTableFlag => ((this.Packed & 0x80) >> 7) == 1;

        /// <summary>
        /// Gets the global color table size.
        /// If the Global Color Table Flag is set,
        /// the value in this field is used to calculate the number of
        /// bytes contained in the Global Color Table.
        /// </summary>
        public int GlobalColorTableSize => 2 << (this.Packed & 0x07);

        /// <summary>
        /// Gets the color depth, in number of bits per pixel.
        /// The lowest 3 packed bits represent the bit depth minus 1.
        /// </summary>
        public int BitsPerPixel => (this.Packed & 0x07) + 1;

        public void WriteTo(Span<byte> buffer)
        {
            ref GifLogicalScreenDescriptor dest = ref Unsafe.As<byte, GifLogicalScreenDescriptor>(ref MemoryMarshal.GetReference(buffer));

            dest = this;
        }

        public static GifLogicalScreenDescriptor Parse(ReadOnlySpan<byte> buffer)
        {
            GifLogicalScreenDescriptor result = MemoryMarshal.Cast<byte, GifLogicalScreenDescriptor>(buffer)[0];

            if (result.GlobalColorTableSize > 255 * 4)
            {
                throw new ImageFormatException($"Invalid gif colormap size '{result.GlobalColorTableSize}'");
            }

            return result;
        }

        public static byte GetPackedValue(bool globalColorTableFlag, int colorResolution, bool sortFlag, int globalColorTableSize)
        {
            /*
            Global Color Table Flag    | 1 Bit
            Color Resolution           | 3 Bits
            Sort Flag                  | 1 Bit
            Size of Global Color Table | 3 Bits
            */

            byte value = 0;

            if (globalColorTableFlag)
            {
                value |= 1 << 7;
            }

            value |= (byte)(colorResolution << 4);

            if (sortFlag)
            {
                value |= 1 << 3;
            }

            value |= (byte)globalColorTableSize;

            return value;
        }
    }
}