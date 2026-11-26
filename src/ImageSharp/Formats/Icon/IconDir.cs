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
    /// The minimum length in bytes of the start of an ICO or CUR file: the directory header and the first directory entry.
    /// </summary>
    public const int MinHeaderSize = Size + IconDirEntry.Size;

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
    /// Parses the directory and its first entry at the start of an ICO or CUR file. Then it checks the values that the ICO and CUR format detectors share.
    /// </summary>
    /// <param name="header">The start of the file.</param>
    /// <param name="type">The expected file type.</param>
    /// <param name="entry">The first directory entry, or the default value if the method returns <see langword="false"/>.</param>
    /// <returns>
    /// <see langword="true"/> if the header holds a directory of <paramref name="type"/> with at least one entry, and the first payload starts after the
    /// complete directory. Otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryParseFirstEntry(ReadOnlySpan<byte> header, IconFileType type, out IconDirEntry entry)
    {
        entry = default;
        if (header.Length < MinHeaderSize)
        {
            return false;
        }

        IconDir dir = Parse(header);
        if (dir.Reserved is not 0 || dir.Type != type || dir.Count is 0)
        {
            return false;
        }

        entry = IconDirEntry.Parse(header[Size..]);

        // The first payload must start after the complete directory, also if the caller gave only the detection prefix.
        return entry.Reserved is 0 && entry.BytesInRes is not 0 && entry.ImageOffset >= Size + (dir.Count * IconDirEntry.Size);
    }

    /// <summary>
    /// Writes the icon directory header to the destination stream.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    public readonly void WriteTo(Stream stream)
        => stream.Write(MemoryMarshal.Cast<IconDir, byte>([this]));
}
