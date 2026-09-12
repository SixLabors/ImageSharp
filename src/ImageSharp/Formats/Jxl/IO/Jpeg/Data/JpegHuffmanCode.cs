// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg.Data;

// We may need to get a ref to fields, so don't
// make these properties.
internal struct JpegHuffmanCode()
{
    /// <summary>
    /// Bit length histogram
    /// </summary>
    public InlineArray17<int> Counts;

    /// <summary>
    /// Symbol values stored by increasing bit lengths.
    /// </summary>
    public InlineArray17<int> Values;

    /// <summary>
    /// The index of the code in the current set of Huffman codes.
    /// For AC component Huffman codes, 0x10 is added to the index.
    /// </summary>
    public int SlotId;

    /// <summary>
    /// True if the code is last within its marker segment.
    /// </summary>
    public bool IsLast = true;
}
