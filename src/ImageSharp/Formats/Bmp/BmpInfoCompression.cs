// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Bmp;

/// <summary>
/// Defines the compression of the pixel data in the info header.
/// </summary>
/// <remarks>
/// The values 0 to 6 are the values of the Windows headers. The OS/2 2.x header uses the values 3 and 4 for other
/// compressions: 3 is Huffman 1D and 4 is RLE24. The enum gives these two compressions the values 101 and 100, so that
/// each enum value has one meaning. These values are not in any file. <see cref="BmpInfoHeader.ParseOs2Version2"/>
/// and <see cref="BmpInfoHeader.WriteOs2Version2Header"/> change the OS/2 values to the enum values, and back.
/// </remarks>
internal enum BmpInfoCompression : int
{
    /// <summary>
    /// BI_RGB: the pixel data is not compressed. Each row is padded to a multiple of 4 bytes. A pixel of 1, 2, 4 or
    /// 8 bits is an index into the color table. A pixel of 16, 24 or 32 bits is a color.
    /// </summary>
    Rgb = 0,

    /// <summary>
    /// BI_RLE8: run length encoding of 8-bit indices. Each command starts with two bytes. A first byte that is not 0
    /// is a run: the second byte is the index, and the first byte is the pixel count. A first byte of 0 starts an end
    /// of line, an end of bitmap, a delta, or an absolute run.
    /// </summary>
    Rle8 = 1,

    /// <summary>
    /// BI_RLE4: run length encoding of 4-bit indices. The commands are the BI_RLE8 commands, but a run alternates
    /// between the two indices in the high and low 4 bits of its second byte.
    /// </summary>
    Rle4 = 2,

    /// <summary>
    /// BI_BITFIELDS: the pixel data is not compressed. Red, green and blue masks give the position of each color
    /// channel in a 16-bit or 32-bit pixel.
    /// </summary>
    BitFields = 3,

    /// <summary>
    /// BI_JPEG: the pixel data is a JPEG image. The decoder does not support it.
    /// </summary>
    Jpeg = 4,

    /// <summary>
    /// BI_PNG: the pixel data is a PNG image. The decoder does not support it.
    /// </summary>
    Png = 5,

    /// <summary>
    /// BI_ALPHABITFIELDS, from Windows CE: as <see cref="BitFields"/>, with an alpha mask after the red, green and
    /// blue masks.
    /// </summary>
    AlphaBitFields = 6,

    /// <summary>
    /// OS/2 2.x RLE24 (value 4 in the file): run length encoding of 24-bit colors. The commands are the BI_RLE8
    /// commands, but each run value and each absolute pixel has three bytes: blue, green and red.
    /// </summary>
    Rle24 = 100,

    /// <summary>
    /// OS/2 2.x Huffman 1D (value 3 in the file): a 1-bit bitmap in the one-dimensional coding scheme of ITU-T T.4. The
    /// runs use the Modified Huffman codes, and an EOL code comes before the first row and after each row. A white run
    /// is 0 bits, so it selects palette index 0.
    /// </summary>
    Huffman1D = 101,
}
