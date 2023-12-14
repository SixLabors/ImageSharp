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
        => this.Type = type;

    public IconDir(IconFileType type, ushort count)
    {
        this.Reserved = 0;
        this.Type = type;
        this.Count = count;
    }
}
