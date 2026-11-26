// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Icon;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = Size)]
internal struct IconDirEntry
{
    /// <summary>
    /// The serialized directory-entry size in bytes.
    /// </summary>
    public const int Size = (4 * sizeof(byte)) + (2 * sizeof(ushort)) + (2 * sizeof(uint));

    /// <summary>
    /// The largest number of entries in the color table of an icon bitmap. ICONDIRENTRY stores the color count in one
    /// byte, and only bitmaps of 8 bits or fewer have a color table:
    /// https://learn.microsoft.com/en-us/previous-versions/ms997538(v=msdn.10)
    /// </summary>
    public const int MaxColorCount = 256;

    /// <summary>
    /// Specifies image width in pixels. Can be any number between 0 and 255. Value 0 means image width is 256 pixels.
    /// </summary>
    public byte Width;

    /// <summary>
    /// Specifies image height in pixels. Can be any number between 0 and 255. Value 0 means image height is 256 pixels.
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
    /// Specifies the size of the image's data in bytes.
    /// </summary>
    public uint BytesInRes;

    /// <summary>
    /// Specifies the offset of BMP or PNG data from the beginning of the ICO/CUR file.
    /// </summary>
    public uint ImageOffset;

    /// <summary>
    /// Makes sure that a color table fits an icon bitmap. The ICO, CUR and ANI metadata use this check for each color
    /// table that a caller sets.
    /// </summary>
    /// <param name="colorTable">The color table, or <see langword="null"/>.</param>
    /// <param name="paramName">The name of the parameter that holds the color table.</param>
    /// <exception cref="ArgumentOutOfRangeException">The color table has more than <see cref="MaxColorCount"/> entries.</exception>
    public static void GuardColorTable(ReadOnlyMemory<Color>? colorTable, string paramName)
    {
        if (colorTable.HasValue)
        {
            Guard.MustBeLessThanOrEqualTo(colorTable.Value.Length, MaxColorCount, paramName);
        }
    }

    /// <summary>
    /// Converts a pixel dimension to the one-byte value of <see cref="Width"/> or <see cref="Height"/>.
    /// </summary>
    /// <param name="dimension">The pixel dimension.</param>
    /// <returns>The encoded dimension. Zero means 256 pixels or more.</returns>
    public static byte ClampEncodingDimension(float? dimension)
        => dimension switch
        {
            // ICONDIRENTRY stores each dimension in one byte, and 0 means 256 pixels or more:
            // https://learn.microsoft.com/en-us/previous-versions/ms997538(v=msdn.10)
            > 255 => 0,
            >= 1 => (byte)dimension,
            _ => 0
        };

    /// <summary>
    /// Scales an encoded dimension after an image transform.
    /// </summary>
    /// <param name="value">The encoded source dimension, or <see langword="null"/> if the metadata does not set it.</param>
    /// <param name="destination">The full destination dimension.</param>
    /// <param name="ratio">The destination-to-source scale ratio.</param>
    /// <returns>The encoded destination dimension.</returns>
    public static byte ScaleEncodingDimension(byte? value, int destination, float ratio)
    {
        if (value is null)
        {
            return ClampEncodingDimension(destination);
        }

        // A stored zero means 256 pixels, so the scale expands it before it applies the transform ratio.
        int source = value.Value is 0 ? 256 : value.Value;
        return ClampEncodingDimension(MathF.Ceiling(source * ratio));
    }

    /// <summary>
    /// Parses an icon directory entry from its byte representation.
    /// </summary>
    /// <param name="data">The icon directory entry data.</param>
    /// <returns>The parsed icon directory entry.</returns>
    public static IconDirEntry Parse(ReadOnlySpan<byte> data)
        => MemoryMarshal.Cast<byte, IconDirEntry>(data)[0];

    /// <summary>
    /// Writes the icon directory entry to the destination stream.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    public readonly void WriteTo(Stream stream)
        => stream.Write(MemoryMarshal.Cast<IconDirEntry, byte>([this]));
}
