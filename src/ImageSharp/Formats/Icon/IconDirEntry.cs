// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Icon;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = Size)]
internal struct IconDirEntry
{
    public const int Size = (4 * sizeof(byte)) + (2 * sizeof(ushort)) + (2 * sizeof(uint));

    public byte Width;

    public byte Height;

    public byte ColorCount;

    public byte Reserved;

    public ushort Planes;

    public ushort BitCount;

    public uint BytesInRes;

    public uint ImageOffset;

    public static IconDirEntry Parse(in ReadOnlySpan<byte> data)
        => MemoryMarshal.Cast<byte, IconDirEntry>(data)[0];
}
