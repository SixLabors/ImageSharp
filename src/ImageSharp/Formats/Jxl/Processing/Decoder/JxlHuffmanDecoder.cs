// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Jxl.IO;
using SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

/// <summary>
/// Decodes Huffman codes.
/// </summary>
internal sealed class JxlHuffmanDecoder
{
    /// <summary>
    /// Number of bits that a huffman table uses.
    /// </summary>
    private const int HuffmanTableBits = 8;

    private const int GoalSize = 1 << HuffmanTableBits;

    public const int CodeLengthCodes = 18;

    public const int DefaultCodeLength = 8;

    public const int CodeLengthRepeatCode = 16;

    /// <summary>
    /// Static Huffman codes for code length code lengths.
    /// </summary>
    private static readonly JxlHuffmanCode[] CodeLengthCodeLengthsCodes =
    [
        new(2, 0), new(2, 4), new(2, 3), new(3, 2), new(2, 0), new(2, 4), new(2, 3), new(4, 1),
        new(2, 0), new(2, 4), new(2, 3), new(3, 2), new(2, 0), new(2, 4), new(2, 3), new(4, 5),
    ];

    private static ReadOnlySpan<byte> CodeLengthCodeOrder =>
    [
        1, 2, 3, 4, 0, 5, 17, 6, 16, 7, 8, 9, 10, 11, 12, 13, 14, 15,
    ];

    /// <summary>
    /// Gets or sets the list of huffman codes.
    /// </summary>
    public JxlHuffmanCode[] Table { get; set; } = [];

    public static bool ReadHuffmanCodeLengths(Span<byte> codeLengthCodeLengths, int numSymbols, Span<byte> codeLengths, JxlBitReader br)
    {
        int symbol = 0;
        int prevCodeLen = DefaultCodeLength;
        int repeat = 0;
        int repeatCodeLen = 0;
        int space = 32768;

        Span<JxlHuffmanCode> table = stackalloc JxlHuffmanCode[32];
        Span<ushort> counts = stackalloc ushort[16];
        table.Clear();
        counts.Clear();

        for (int i = 0; i < CodeLengthCodes; i++)
        {
            counts[codeLengthCodeLengths[i]]++;
        }

        if (JxlHuffman.BuildHuffmanTable(table, 5, codeLengthCodeLengths, counts) == 0)
        {
            return false;
        }

        while (symbol < numSymbols && space > 0)
        {
            JxlHuffmanCode code = table[(int)br.PeekBits32(5u)];
            br.SkipBits32(code.Bits);
            byte codeLength = (byte)code.Value;     // It is indeed converted from ushort to byte

            if (codeLength < CodeLengthRepeatCode)
            {
                repeat = 0;
                codeLengths[symbol++] = codeLength;
                if (codeLength != 0)
                {
                    prevCodeLen = codeLength;
                    space -= 32768 >> codeLength;
                }
            }
            else
            {
                int extraBits = codeLength - 14;
                byte newLength = 0;
                if (codeLength == CodeLengthRepeatCode)
                {
                    newLength = (byte)prevCodeLen;
                }

                if (repeatCodeLen != newLength)
                {
                    repeat = 0;
                    repeatCodeLen = newLength;
                }

                int oldRepeat = repeat;

                if (repeat > 0)
                {
                    repeat -= 2;
                    repeat <<= extraBits;
                }

                repeat += (int)br.ReadBits32((uint)extraBits) + 3;
                int repeatDelta = repeat - oldRepeat;

                if (symbol + repeatDelta > numSymbols)
                {
                    return false;
                }

                codeLengths.Slice(symbol, repeatDelta).Fill((byte)repeatCodeLen);
                symbol += repeatDelta;
                if (repeatCodeLen != 0)
                {
                    space -= repeatDelta << (15 - repeatCodeLen);
                }
            }
        }

        if (space != 0)
        {
            return false;
        }

        codeLengths[symbol..].Clear();
        return true;
    }

    /// <summary>
    /// Reads a simple Huffman code.
    /// </summary>
    /// <param name="alphabetSize">Alphabet size (256 at most)</param>
    /// <param name="br">Bit-stream reader</param>
    /// <param name="table">Output table (must have at most 8 items)</param>
    /// <returns>Status of the operation</returns>
    public static bool ReadSimpleCode(int alphabetSize, JxlBitReader br, Span<JxlHuffmanCode> table)
    {
        int maxBits = (alphabetSize > 1) ? JxlMath.FloorLog2Nonzero(alphabetSize - 1) + 1 : 0;
        uint symbolCount = br.ReadBits32(2u) + 1u;

        scoped Span<ushort> symbols = stackalloc ushort[4];
        symbols.Clear();    // Clearing is necessary. Not every value will be initialized.

        for (int i = 0; i < symbolCount; i++)
        {
            uint symbol = br.ReadBits32((uint)maxBits);
            if (symbol >= alphabetSize)
            {
                return false;
            }

            symbols[i] = (ushort)symbol;
        }

        for (int i = 0; i < symbolCount - 1; i++)
        {
            for (int j = i + 1; j < symbolCount; j++)
            {
                if (symbols[i] == symbols[j])
                {
                    return false;
                }
            }
        }

        if (symbolCount == 4)
        {
            symbolCount += br.ReadBits32(1u);
        }

        int tableSize = 1;
        switch (symbolCount)
        {
            case 1:
                table[0] = new(0, symbols[0]);
                break;

            case 2:
                if (symbols[0] > symbols[1])
                {
                    SwapSymbols(0, 1, symbols);
                }

                table[0] = new(1, symbols[0]);
                table[1] = new(1, symbols[1]);
                tableSize = 2;
                break;

            case 3:
                if (symbols[1] > symbols[2])
                {
                    SwapSymbols(1, 2, symbols);
                }

                table[0] = new(1, symbols[0]);
                table[2] = new(1, symbols[0]);
                table[1] = new(2, symbols[1]);
                table[3] = new(2, symbols[2]);
                tableSize = 4;
                break;

            case 4:
                for (int i = 0; i < 3; i++)
                {
                    for (int j = i + 1; j < 4; j++)
                    {
                        if (symbols[i] > symbols[j])
                        {
                            SwapSymbols(i, j, symbols);
                        }
                    }
                }

                table[0] = new(2, symbols[0]);
                table[2] = new(2, symbols[1]);
                table[1] = new(2, symbols[2]);
                table[3] = new(2, symbols[3]);
                tableSize = 4;
                break;

            case 5:
                if (symbols[2] > symbols[3])
                {
                    SwapSymbols(2, 3, symbols);
                }

                table[0] = new(1, symbols[0]);
                table[1] = new(2, symbols[1]);
                table[2] = new(1, symbols[0]);
                table[3] = new(3, symbols[2]);
                table[4] = new(1, symbols[0]);
                table[5] = new(2, symbols[1]);
                table[6] = new(1, symbols[0]);
                table[7] = new(3, symbols[3]);
                tableSize = 8;
                break;

            default:
                // This should be unreachable.
                return false;
        }

        while (tableSize != GoalSize)
        {
            table[tableSize..].CopyTo(table);
            tableSize <<= 1;
        }

        return true;
    }

    public bool ReadFromBitStream(int alphabetSize, JxlBitReader br)
    {
        if (alphabetSize > (1 << JxlAnsConstants.PrefixMaxBits))
        {
            return false;
        }

        uint simpleCodeOrSkip = br.ReadBits32(2u);
        if (simpleCodeOrSkip == 1u)
        {
            this.Table = new JxlHuffmanCode[GoalSize];
            return ReadSimpleCode(alphabetSize, br, this.Table);
        }

        // The alphabet size is at most 256
        Span<byte> codeLengths = stackalloc byte[alphabetSize];
        codeLengths.Clear();    // Zero-initialized in reference software

        Span<byte> codeLengthCodeLengths = stackalloc byte[CodeLengthCodes];
        codeLengthCodeLengths.Clear();  // Zero-initialized in reference software

        int space = 32;
        int numCodes = 0;

        for (uint i = simpleCodeOrSkip; i < CodeLengthCodes && space > 0; i++)
        {
            int codeLengthIndex = CodeLengthCodeOrder[(int)i];
            JxlHuffmanCode huff = CodeLengthCodeLengthsCodes[(int)br.PeekBits32(4u)];
            br.SkipBits32(huff.Bits);
            byte value = (byte)huff.Value;  // It's indeed converted from ushort to byte
            codeLengthCodeLengths[codeLengthIndex] = value;

            if (value != 0)
            {
                space -= 32 >> value;
                numCodes++;
            }
        }

        bool ok = (numCodes == 1 || space == 0) && ReadHuffmanCodeLengths(codeLengthCodeLengths, alphabetSize, codeLengths, br);

        if (!ok)
        {
            return false;
        }

        Span<ushort> counts = stackalloc ushort[16];
        counts.Clear();     // Zero-initialized

        this.Table = new JxlHuffmanCode[alphabetSize + 376];
        uint tableSize = JxlHuffman.BuildHuffmanTable(this.Table, HuffmanTableBits, codeLengths, counts);

        this.Table = this.Table[..(int)tableSize];

        return tableSize > 0;
    }

    public ushort ReadSymbol(JxlBitReader br)
    {
        Span<JxlHuffmanCode> table = this.Table.AsSpan()[(int)br.PeekBits32(HuffmanTableBits)..];
        int bitCount = table[0].Bits;
        if (bitCount > HuffmanTableBits)
        {
            br.SkipBits32(HuffmanTableBits);
            bitCount -= HuffmanTableBits;
            table = table[(int)(table[0].Value + br.PeekBits32((uint)bitCount))..];
        }

        br.SkipBits32(table[0].Bits);
        return table[0].Value;
    }

    private static void SwapSymbols(int i, int j, Span<ushort> symbols) => RuntimeUtility.Swap(ref symbols[i], ref symbols[j]);
}
