// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Icon;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = Size)]
internal struct IconDir
{
    public const int Size = 3 * sizeof(ushort);
    public ushort Reserved;
    public IconFileType Type;
    public ushort Count;

    public IconDir(IconFileType type)
        : this(type, 0)
    {
    }

    public IconDir(IconFileType type, ushort count)
        : this(0, type, count)
    {
    }

    public IconDir(ushort reserved, IconFileType type, ushort count)
    {
        this.Reserved = reserved;
        this.Type = type;
        this.Count = count;
    }

    public static IconDir Parse(in ReadOnlySpan<byte> data)
        => MemoryMarshal.Cast<byte, IconDir>(data)[0];
}
