// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Icon;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = Size)]
internal struct IconDirEntry
{
    public const int Size = (4 * sizeof(byte)) + (2 * sizeof(ushort)) + (2 * sizeof(uint));

    /// <summary>
    /// Specifies image width in pixels. Can be any number between 0 and 255. Value 0 means image width is 256 pixels.
    /// </summary>
    public byte Width;

    /// <summary>
    /// Specifies image height in pixels. Can be any number between 0 and 255. Value 0 means image height is 256 pixels.[
    /// </summary>
    public byte Height;

    /// <summary>
    /// Specifies number of colors in the color palette. Should be 0 if the image does not use a color palette.
    /// </summary>
    public byte ColorCount;

    /// <summary>
    /// Reserved. Should be 0.
    /// </summary>
    public byte Reserved;

    /// <summary>
    /// In ICO format: Specifies color planes. Should be 0 or 1.<br/>
    /// In CUR format: Specifies the horizontal coordinates of the hotspot in number of pixels from the left.
    /// </summary>
    public ushort Planes;

    /// <summary>
    /// In ICO format: Specifies bits per pixel.<br/>
    /// In CUR format: Specifies the vertical coordinates of the hotspot in number of pixels from the top.
    /// </summary>
    public ushort BitCount;

    /// <summary>
    /// Specifies the size of the image's data in bytes
    /// </summary>
    public uint BytesInRes;

    /// <summary>
    /// Specifies the offset of BMP or PNG data from the beginning of the ICO/CUR file.
    /// </summary>
    public uint ImageOffset;

    public static ref IconDirEntry Parse(in ReadOnlySpan<byte> data)
        => ref Unsafe.As<byte, IconDirEntry>(ref MemoryMarshal.GetReference(data));

    public readonly void WriteTo(in Stream stream)
        => stream.Write(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in this, 1)));
}
