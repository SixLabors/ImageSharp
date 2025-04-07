// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Icon;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = Size)]
internal struct IconDir
{
    public const int Size = 3 * sizeof(ushort);

    /// <summary>
    /// Reserved. Must always be 0.
    /// </summary>
    public ushort Reserved;

    /// <summary>
    /// Specifies image type: 1 for icon (.ICO) image, 2 for cursor (.CUR) image. Other values are invalid.
    /// </summary>
    public IconFileType Type;

    /// <summary>
    /// Specifies number of images in the file.
    /// </summary>
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

    public static ref IconDir Parse(ReadOnlySpan<byte> data)
        => ref Unsafe.As<byte, IconDir>(ref MemoryMarshal.GetReference(data));

    public readonly void WriteTo(Stream stream)
        => stream.Write(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in this, 1)));
}
