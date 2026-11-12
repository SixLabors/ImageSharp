// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;

/// <summary>
/// Represents a Huffman coding table containing basic coding data plus tables for accelerated computation.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct HuffmanTable
{
    /// <summary>
    /// Memory workspace buffer size used in <see cref="HuffmanTable"/> ctor.
    /// </summary>
    public const int WorkspaceByteSize = 256 * sizeof(uint);

    /// <summary>
    /// Derived from the DHT marker. Contains the symbols, in order of incremental code length.
    /// </summary>
    public InlineArray256<byte> Values;

    /// <summary>
    /// Contains the largest code of length k (0 if none). MaxCode[17] is a sentinel to
    /// ensure <see cref="JpegBitReader.DecodeHuffman"/> terminates.
    /// </summary>
    public InlineArray18<ulong> MaxCode;

    /// <summary>
    /// Values[] offset for codes of length k  ValOffset[k] = Values[] index of 1st symbol of code length
    /// k, less the smallest code of length k; so given a code of length k, the corresponding symbol is
    /// Values[code + ValOffset[k]].
    /// </summary>
    public InlineArray19<int> ValOffset;

    /// <summary>
    /// Contains the length of bits for the given k value.
    /// </summary>
    public InlineArray256<byte> LookaheadSize;

    /// <summary>
    /// Lookahead table: indexed by the next <see cref="JpegConstants.Huffman.LookupBits"/> bits of
    /// the input data stream.  If the next Huffman code is no more
    /// than <see cref="JpegConstants.Huffman.LookupBits"/> bits long, we can obtain its length and
    /// the corresponding symbol directly from this tables.
    ///
    /// The lower 8 bits of each table entry contain the number of
    /// bits in the corresponding Huffman code, or <see cref="JpegConstants.Huffman.LookupBits"/> + 1
    /// if too long.  The next 8 bits of each entry contain the symbol.
    /// </summary>
    public InlineArray256<byte> LookaheadValue;

    /// <summary>
    /// The flag of a combined lookup entry that holds the decoded value.
    /// </summary>
    public const int FastComplete = 1 << 6;

    /// <summary>
    /// Initializes a new instance of the <see cref="HuffmanTable"/> struct.
    /// </summary>
    /// <param name="codeLengths">The code lengths.</param>
    /// <param name="values">The huffman values.</param>
    /// <param name="workspace">The provided spare workspace memory, can be dirty.</param>
    /// <param name="fastLookup">
    /// The combined lookup table to fill, with <see cref="JpegConstants.Huffman.FastSize"/> entries. It can contain old data.
    /// The next <see cref="JpegConstants.Huffman.FastBits"/> bits of the stream are the index into the table.
    /// The table is outside the struct because it is 8 KB. The scan decoder owns it in pooled memory.
    /// <list type="bullet">
    /// <item>Bits 0 to 4: the bits to consume. Bit 5 is always zero, so a 64-bit shift by the entry consumes these bits.</item>
    /// <item>Bit 6: <see cref="FastComplete"/>. The bit is set when the symbol has no value bits, or when the code and its
    /// value bits fit in the index. Then bits 0 to 4 count the code and the value bits.
    /// The bit is clear when the value bits are past the index. Then bits 0 to 4 count only the code.</item>
    /// <item>Bits 8 to 15: the symbol.</item>
    /// <item>Bits 16 to 31: the signed value of a complete entry.</item>
    /// </list>
    /// A zero entry means that the code is longer than <see cref="JpegConstants.Huffman.FastBits"/> bits, or that the code is not valid.
    /// The table decodes the value from the low nibble of the symbol. This is the AC run/size layout (ITU T.81, F.1.2.2).
    /// It is also the DC size layout for symbols up to 15.
    /// </param>
    public HuffmanTable(ReadOnlySpan<byte> codeLengths, ReadOnlySpan<byte> values, Span<uint> workspace, Span<int> fastLookup)
    {
        Unsafe.CopyBlockUnaligned(ref this.Values[0], ref MemoryMarshal.GetReference(values), (uint)values.Length);

        // Generate codes
        uint code = 0;
        int si = 1;
        int p = 0;
        for (int i = 1; i <= 16; i++)
        {
            int count = codeLengths[i];
            for (int j = 0; j < count; j++)
            {
                workspace[p++] = code;
                code++;
            }

            // 'code' is now 1 more than the last code used for codelength 'si'
            // in the valid worst possible case 'code' would have the least
            // significant bit set to 1, e.g. 1111(0) +1 => 1111(1)
            // but it must still fit in 'si' bits since no huffman code can be equal to all 1s
            // if last code is all ones, e.g. 1111(1), then incrementing it by 1 would yield
            // a new code which occupies one extra bit, e.g. 1111(1) +1 => (1)1111(0)
            if (code >= (1 << si))
            {
                JpegThrowHelper.ThrowInvalidImageContentException("Bad huffman table.");
            }

            code <<= 1;
            si++;
        }

        // Figure F.15: generate decoding tables for bit-sequential decoding
        p = 0;
        for (int j = 1; j <= 16; j++)
        {
            if (codeLengths[j] != 0)
            {
                this.ValOffset[j] = p - (int)workspace[p];
                p += codeLengths[j];
                this.MaxCode[j] = workspace[p - 1]; // Maximum code of length l
                this.MaxCode[j] <<= JpegConstants.Huffman.RegisterSize - j; // Left justify
                this.MaxCode[j] |= (1ul << (JpegConstants.Huffman.RegisterSize - j)) - 1;
            }
            else
            {
                this.MaxCode[j] = 0;
            }
        }

        this.ValOffset[18] = 0;
        this.MaxCode[17] = ulong.MaxValue; // Ensures huff decode terminates

        // Compute lookahead tables to speed up decoding.
        // First we set all the table entries to JpegConstants.Huffman.SlowBits, indicating "too long";
        // then we iterate through the Huffman codes that are short enough and
        // fill in all the entries that correspond to bit sequences starting
        // with that code.
        ref byte lookupSizeRef = ref this.LookaheadSize[0];
        Unsafe.InitBlockUnaligned(ref lookupSizeRef, JpegConstants.Huffman.SlowBits, JpegConstants.Huffman.LookupSize);

        p = 0;
        for (int length = 1; length <= JpegConstants.Huffman.LookupBits; length++)
        {
            int jShift = JpegConstants.Huffman.LookupBits - length;
            for (int i = 1; i <= codeLengths[length]; i++, p++)
            {
                // length = current code's length, p = its index in huffCode[] & Values[].
                // Generate left-justified code followed by all possible bit sequences
                int lookBits = (int)(workspace[p] << jShift);
                for (int ctr = 1 << (JpegConstants.Huffman.LookupBits - length); ctr > 0; ctr--)
                {
                    this.LookaheadSize[lookBits] = (byte)length;
                    this.LookaheadValue[lookBits] = this.Values[p];
                    lookBits++;
                }
            }
        }

        // The method builds the combined table in the same way. Each code of at most FastBits bits fills all entries that it prefixes.
        // The low nibble of the symbol is the count of value bits that follow the code.
        // When the code and the value bits fit in the index, the index holds the value bits. Thus, one table read also decodes the value.
        // An entry that no code of at most FastBits bits prefixes stays zero. A zero entry selects the slow path.
        fastLookup.Clear();
        p = 0;
        for (int length = 1; length <= JpegConstants.Huffman.FastBits; length++)
        {
            int shift = JpegConstants.Huffman.FastBits - length;
            for (int i = 1; i <= codeLengths[length]; i++, p++)
            {
                int symbol = this.Values[p];
                int magnitude = symbol & 15;
                int first = (int)(workspace[p] << shift);
                for (int j = 0; j < 1 << shift; j++)
                {
                    int entry;
                    if (magnitude == 0)
                    {
                        entry = length | FastComplete | (symbol << 8);
                    }
                    else if (length + magnitude <= JpegConstants.Huffman.FastBits)
                    {
                        // The value bits follow the code in the low bits of the index.
                        int bits = (j >> (shift - magnitude)) & ((1 << magnitude) - 1);
                        entry = (length + magnitude) | FastComplete | (symbol << 8) | (JpegBitReader.Extend(bits, magnitude) << 16);
                    }
                    else
                    {
                        entry = length | (symbol << 8);
                    }

                    fastLookup[first + j] = entry;
                }
            }
        }
    }
}
