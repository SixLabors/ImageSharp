// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Icon;

/// <summary>
/// Represents an ICO or CUR file directory header.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = Size)]
internal struct IconDir
{
    /// <summary>
    /// The serialized directory-header size in bytes.
    /// </summary>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="IconDir"/> struct.
    /// </summary>
    /// <param name="type">The icon file type.</param>
    public IconDir(IconFileType type)
        : this(type, 0)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IconDir"/> struct.
    /// </summary>
    /// <param name="type">The icon file type.</param>
    /// <param name="count">The number of directory entries.</param>
    public IconDir(IconFileType type, ushort count)
    {
        this.Reserved = 0;
        this.Type = type;
        this.Count = count;
    }

    /// <summary>
    /// Parses an icon directory header from its byte representation.
    /// </summary>
    /// <param name="data">The icon directory header data.</param>
    /// <returns>The parsed icon directory header.</returns>
    public static IconDir Parse(ReadOnlySpan<byte> data)
        => MemoryMarshal.Cast<byte, IconDir>(data)[0];

    /// <summary>
    /// Writes the icon directory header to the destination stream.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    public readonly void WriteTo(Stream stream)
        => stream.Write(MemoryMarshal.Cast<IconDir, byte>([this]));
}
