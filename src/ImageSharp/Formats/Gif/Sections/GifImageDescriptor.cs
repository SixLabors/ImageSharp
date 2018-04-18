// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// Each image in the Data Stream is composed of an Image Descriptor,
    /// an optional Local Color Table, and the image data.
    /// Each image must fit within the boundaries of the
    /// Logical Screen, as defined in the Logical Screen Descriptor.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal readonly struct GifImageDescriptor
    {
        public const int Size = 10;

        public GifImageDescriptor(
            ushort left,
            ushort top,
            ushort width,
            ushort height,
            byte packed)
        {
            this.Left = left;
            this.Top = top;
            this.Width = width;
            this.Height = height;
            this.Packed = packed;
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
        /// Gets the packed value of localColorTableFlag, interlaceFlag, sortFlag, and localColorTableSize.
        /// </summary>
        public byte Packed { get; }

        public bool LocalColorTableFlag => ((this.Packed & 0x80) >> 7) == 1;

        public int LocalColorTableSize => 2 << (this.Packed & 0x07);

        public bool InterlaceFlag => ((this.Packed & 0x40) >> 6) == 1;

        public void WriteTo(Span<byte> buffer)
        {
            buffer[0] = GifConstants.ImageDescriptorLabel;

            ref GifImageDescriptor dest = ref Unsafe.As<byte, GifImageDescriptor>(ref MemoryMarshal.GetReference(buffer.Slice(1)));

            dest = this;
        }

        public static GifImageDescriptor Parse(ReadOnlySpan<byte> buffer)
        {
            return MemoryMarshal.Cast<byte, GifImageDescriptor>(buffer)[0];
        }

        public static byte GetPackedValue(bool localColorTableFlag, bool interfaceFlag, bool sortFlag, int localColorTableSize)
        {
            var field = default(PackedField);

            field.SetBit(0, localColorTableFlag);          // 0: Local color table flag = 1 (LCT used)
            field.SetBit(1, interfaceFlag);                // 1: Interlace flag 0
            field.SetBit(2, sortFlag);                     // 2: Sort flag 0
            field.SetBits(5, 3, localColorTableSize - 1);  // 3-4: Reserved, 5-7 : LCT size. 2^(N+1)

            return field.Byte;
        }
    }
}