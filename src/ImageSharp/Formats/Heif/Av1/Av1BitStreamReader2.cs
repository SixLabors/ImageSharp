// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal ref struct Av1BitStreamReader2
{
    public const int WordSize = 1 << WordSizeLog2;
    private const int WordSizeLog2 = 5;

    private readonly Span<byte> data;
    private int wordPosition = 0;
    private int bitOffset = 0;

    public Av1BitStreamReader2(Span<byte> data) => this.data = data;

    public readonly int BitPosition => ((this.wordPosition - 1) * WordSize) + this.bitOffset;

    /// <summary>
    /// Gets the number of bytes in the readers buffer.
    /// </summary>
    public readonly int Length => this.data.Length;

    public void Reset()
    {
        this.wordPosition = 0;
        this.bitOffset = 0;
    }

    public void Skip(int bitCount)
    {
        this.bitOffset += bitCount;
        while (this.bitOffset >= WordSize)
        {
            this.bitOffset -= WordSize;
            this.wordPosition++;
        }
    }

    public uint ReadLiteral(int bitCount)
    {
        DebugGuard.MustBeBetweenOrEqualTo(bitCount, 1, 32, nameof(bitCount));

        uint literal = 0;
        for (int bit = bitCount - 1; bit >= 0; bit--)
        {
            literal |= this.ReadBit() << bit;
        }

        return literal;
    }

    internal uint ReadBit()
    {
        int byteOffset = DivideBy8(this.bitOffset, false);
        byte shift = (byte)(7 - Mod8(this.bitOffset));
        this.bitOffset++;
        return (uint)((this.data[byteOffset] >> shift) & 0x01);
    }

    internal bool ReadBoolean() => this.ReadLiteral(1) > 0;

    public ulong ReadLittleEndianBytes128(out int length)
    {
        // See section 4.10.5 of the AV1-Specification
        DebugGuard.IsTrue((this.bitOffset & 0x07) == 0, $"Reading of Little Endian 128 value only allowed on byte alignment (offset {this.BitPosition}).");

        ulong value = 0;
        length = 0;
        for (int i = 0; i < 56; i += 7)
        {
            uint leb128Byte = this.ReadLiteral(8);
            value |= (leb128Byte & 0x7FUL) << i;
            length++;
            if ((leb128Byte & 0x80U) == 0)
            {
                break;
            }
        }

        return value;
    }

    public uint ReadUnsignedVariableLength()
    {
        // See section 4.10.3 of the AV1-Specification
        int leadingZerosCount = 0;
        while (leadingZerosCount < 32 && this.ReadLiteral(1) == 0U)
        {
            leadingZerosCount++;
        }

        if (leadingZerosCount == 32)
        {
            return uint.MaxValue;
        }

        uint basis = (1U << leadingZerosCount) - 1U;
        uint value = this.ReadLiteral(leadingZerosCount);
        return basis + value;
    }

    public uint ReadNonSymmetric(uint n)
    {
        // See section 4.10.7 of the AV1-Specification
        if (n <= 1)
        {
            return 0;
        }

        int w = (int)(Av1Math.FloorLog2(n) + 1);
        uint m = (uint)((1 << w) - n);
        uint v = this.ReadLiteral(w - 1);
        if (v < m)
        {
            return v;
        }

        return (v << 1) - m + this.ReadLiteral(1);
    }

    public int ReadSignedFromUnsigned(int n)
    {
        // See section 4.10.6 of the AV1-Specification
        int signedValue;
        uint value = this.ReadLiteral(n);
        uint signMask = 1U << (n - 1);
        if ((value & signMask) == signMask)
        {
            // Prevent overflow by casting to long;
            signedValue = (int)((long)value - (signMask << 1));
        }
        else
        {
            signedValue = (int)value;
        }

        return signedValue;
    }

    public uint ReadLittleEndian(int n)
    {
        // See section 4.10.4 of the AV1-Specification
        DebugGuard.IsTrue((this.bitOffset & (WordSize - 1)) == 0, "Reading of Little Endian value only allowed on byte alignment");

        uint t = 0;
        for (int i = 0; i < 8 * n; i += 8)
        {
            t += this.ReadLiteral(8) << i;
        }

        return t;
    }

    public Span<byte> GetSymbolReader(int tileDataSize)
    {
        DebugGuard.IsTrue((this.bitOffset & 0x7) == 0, "Symbol reading needs to start on byte boundary.");

        throw new NotImplementedException("GetSymbolReader is not implemented yet / needs to be reviewd again");

        // TODO: Pass exact byte iso Word start.
        // TODO: This needs to be reviewed again, due to the change in how ReadLiteral() works now!
        /*int spanLength = tileDataSize >> WordSizeInBytesLog2;
        Span<uint> span = this.data.Slice(this.bitOffset >> WordSizeLog2, spanLength);
        this.Skip(tileDataSize << Log2Of8);
        return MemoryMarshal.Cast<uint, byte>(span);*/
    }

    // Last 3 bits are the value of mod 8.
    internal static int Mod8(int n) => n & 0x07;

    internal static int DivideBy8(int n, bool ceil) => (n + (ceil ? 7 : 0)) >> 3;
}
