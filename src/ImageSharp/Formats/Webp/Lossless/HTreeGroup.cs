// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp.Lossless;

/// <summary>
/// Huffman table group.
/// Includes special handling for the following cases:
///  - IsTrivialLiteral: one common literal base for RED/BLUE/ALPHA (not GREEN)
///  - IsTrivialCode: only 1 code (no bit is read from the bitstream)
///  - UsePackedTable: few enough literal symbols, so all the bit codes can fit into a small look-up table PackedTable[]
/// The common literal base, if applicable, is stored in 'LiteralArb'.
/// </summary>
internal struct HTreeGroup
{
    public HTreeGroup(uint packedTableSize)
    {
        this.HTrees = new List<HuffmanCode[]>(WebpConstants.HuffmanCodesPerMetaCode);
        this.PackedTable = new HuffmanCode[packedTableSize];
        this.IsTrivialCode = false;
        this.IsTrivialLiteral = false;
        this.LiteralArb = 0;
        this.UsePackedTable = false;
    }

    /// <summary>
    /// Gets the Huffman trees. This has a maximum of <see cref="WebpConstants.HuffmanCodesPerMetaCode" /> (5) entry's.
    /// </summary>
    public List<HuffmanCode[]> HTrees { get; }

    /// <summary>
    /// Gets or sets a value indicating whether huffman trees for Red, Blue and Alpha Symbols are trivial (have a single code).
    /// </summary>
    public bool IsTrivialLiteral { get; set; }

    /// <summary>
    /// Gets or sets a the literal argb value of the pixel.
    /// If IsTrivialLiteral is true, this is the ARGB value of the pixel, with Green channel being set to zero.
    /// </summary>
    public uint LiteralArb { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether there is only one code.
    /// </summary>
    public bool IsTrivialCode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use packed table below for short literal code.
    /// </summary>
    public bool UsePackedTable { get; set; }

    /// <summary>
    /// Gets or sets table mapping input bits to packed values, or escape case to literal code.
    /// </summary>
    public HuffmanCode[] PackedTable { get; set; }
}
