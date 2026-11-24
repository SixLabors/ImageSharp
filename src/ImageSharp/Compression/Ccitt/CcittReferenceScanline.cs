// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Compression.Ccitt;

/// <summary>
/// Represents a reference scan line for CCITT 2D decoding.
/// </summary>
/// <remarks>
/// Each byte of the scan line is one pixel: 0 for white and 1 for black. Thus an XOR with 1 gives the other color.
/// </remarks>
internal readonly ref struct CcittReferenceScanline
{
    /// <summary>
    /// The byte of a white pixel.
    /// </summary>
    private const byte WhiteByte = 0;

    private readonly ReadOnlySpan<byte> scanLine;
    private readonly int width;

    /// <summary>
    /// Initializes a new instance of the <see cref="CcittReferenceScanline"/> struct.
    /// </summary>
    /// <param name="scanLine">The scan line.</param>
    public CcittReferenceScanline(ReadOnlySpan<byte> scanLine)
    {
        this.scanLine = scanLine;
        this.width = scanLine.Length;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CcittReferenceScanline"/> struct for the imaginary white line.
    /// </summary>
    /// <param name="width">The width of the scanline.</param>
    public CcittReferenceScanline(int width)
    {
        this.scanLine = default;
        this.width = width;
    }

    public bool IsEmpty => this.scanLine.IsEmpty;

    /// <summary>
    /// Finds b1: The first changing element on the reference line to the right of a0 and of opposite color to a0.
    /// </summary>
    /// <param name="a0">The reference or starting element om the coding line.</param>
    /// <param name="a0Byte">Fill byte.</param>
    /// <returns>Position of b1.</returns>
    public int FindB1(int a0, byte a0Byte)
    {
        if (this.IsEmpty)
        {
            return this.FindB1ForImaginaryWhiteLine(a0, a0Byte);
        }

        return this.FindB1ForNormalLine(a0, a0Byte);
    }

    /// <summary>
    /// Finds b2: The next changing element to the right of b1 on the reference line.
    /// </summary>
    /// <param name="b1">The first changing element on the reference line to the right of a0 and opposite of color to a0.</param>
    /// <returns>Position of b2.</returns>
    public int FindB2(int b1)
    {
        if (this.IsEmpty)
        {
            return this.FindB2ForImaginaryWhiteLine();
        }

        return this.FindB2ForNormalLine(b1);
    }

    private int FindB1ForImaginaryWhiteLine(int a0, byte a0Byte)
    {
        if (a0 < 0)
        {
            if (a0Byte != WhiteByte)
            {
                return 0;
            }
        }

        return this.width;
    }

    private int FindB1ForNormalLine(int a0, byte a0Byte)
    {
        int offset = 0;
        if (a0 < 0)
        {
            if (a0Byte != this.scanLine[0])
            {
                return 0;
            }
        }
        else
        {
            offset = a0;
        }

        ReadOnlySpan<byte> searchSpace = this.scanLine[offset..];
        byte searchByte = (byte)(a0Byte ^ 1);
        int index = searchSpace.IndexOf(searchByte);
        if (index < 0)
        {
            return this.scanLine.Length;
        }

        if (index != 0)
        {
            return offset + index;
        }

        searchByte = (byte)(searchSpace[0] ^ 1);
        index = searchSpace.IndexOf(searchByte);
        if (index < 0)
        {
            return this.scanLine.Length;
        }

        searchSpace = searchSpace[index..];
        offset += index;
        index = searchSpace.IndexOf((byte)(searchByte ^ 1));
        if (index < 0)
        {
            return this.scanLine.Length;
        }

        return index + offset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindB2ForImaginaryWhiteLine() => this.width;

    private int FindB2ForNormalLine(int b1)
    {
        if (b1 >= this.scanLine.Length)
        {
            return this.scanLine.Length;
        }

        byte searchByte = (byte)(this.scanLine[b1] ^ 1);
        int offset = b1 + 1;
        ReadOnlySpan<byte> searchSpace = this.scanLine[offset..];
        int index = searchSpace.IndexOf(searchByte);
        if (index == -1)
        {
            return this.scanLine.Length;
        }

        return offset + index;
    }
}
