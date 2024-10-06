// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal ref struct Av1BitStreamReader
{
    private readonly Span<byte> data;

    public Av1BitStreamReader(Span<byte> data) => this.data = data;

    public int BitPosition { get; private set; } = 0;

    /// <summary>
    /// Gets the number of bytes in the readers buffer.
    /// </summary>
    public readonly int Length => this.data.Length;

    public void Reset() => this.BitPosition = 0;

    public void Skip(int bitCount) => this.BitPosition += bitCount;

    public uint ReadLiteral(int bitCount)
    {
        DebugGuard.MustBeBetweenOrEqualTo(bitCount, 0, 32, nameof(bitCount));

        uint literal = 0;
        for (int bit = bitCount - 1; bit >= 0; bit--)
        {
            literal |= this.ReadBit() << bit;
        }

        return literal;
    }

    internal uint ReadBit()
    {
        int byteOffset = Av1Math.DivideBy8Floor(this.BitPosition);
        byte shift = (byte)(7 - Av1Math.Modulus8(this.BitPosition));
        this.BitPosition++;
        return (uint)((this.data[byteOffset] >> shift) & 0x01);
    }

    internal bool ReadBoolean() => this.ReadLiteral(1) > 0;

    public ulong ReadLittleEndianBytes128(out int length)
    {
        // See section 4.10.5 of the AV1-Specification
        DebugGuard.IsTrue((this.BitPosition & 0x07) == 0, $"Reading of Little Endian 128 value only allowed on byte alignment (offset {this.BitPosition}).");

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
        while (leadingZerosCount < 32)
        {
            uint bit = this.ReadLiteral(1);
            if (bit == 1)
            {
                break;
            }

            leadingZerosCount++;
        }

        if (leadingZerosCount == 32)
        {
            return uint.MaxValue;
        }

        if (leadingZerosCount != 0)
        {
            uint basis = (1U << leadingZerosCount) - 1U;
            uint value = this.ReadLiteral(leadingZerosCount);
            return basis + value;
        }

        return 0;
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
        DebugGuard.IsTrue(Av1Math.Modulus8(this.BitPosition) == 0, "Reading of Little Endian value only allowed on byte alignment");

        uint t = 0;
        for (int i = 0; i < 8 * n; i += 8)
        {
            t += this.ReadLiteral(8) << i;
        }

        return t;
    }

    public Span<byte> GetSymbolReader(int tileDataSize)
    {
        DebugGuard.IsTrue(Av1Math.Modulus8(this.BitPosition) == 0, "Symbol reading needs to start on byte boundary.");
        int bytesRead = Av1Math.DivideBy8Floor(this.BitPosition);
        Span<byte> span = this.data.Slice(bytesRead, tileDataSize);
        this.Skip(tileDataSize << 3);
        return span;
    }
}
