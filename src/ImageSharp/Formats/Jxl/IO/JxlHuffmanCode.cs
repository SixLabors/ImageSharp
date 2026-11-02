// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.IO;

/// <summary>
/// A single Huffman code.
/// </summary>
internal struct JxlHuffmanCode(byte bits, ushort value)
{
    /// <summary>
    /// Number of bits for this symbol.
    /// </summary>
    public byte Bits = bits;

    /// <summary>
    /// Symbol value/offset.
    /// </summary>
    public ushort Value = value;
}
