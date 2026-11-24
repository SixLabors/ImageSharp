// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Compression.Ccitt;

/// <summary>
/// Writes the code bits of CCITT data to a stream. The first code bit of each byte is its most significant bit.
/// </summary>
/// <remarks>
/// The writer keeps the pending code bits in a 64-bit window, the first bit in the most significant place. When the
/// window holds 32 bits or more, the writer moves its high 32 bits to a byte buffer. A code has 32 bits or less, so the
/// window never overflows. The writer writes the byte buffer to the stream when it is full and on <see cref="Flush"/>.
/// </remarks>
internal sealed class CcittBitWriter : IDisposable
{
    /// <summary>
    /// The size of the byte buffer. Each stream write moves one buffer, so a larger buffer gives fewer writes. The writer
    /// works with any size that is a multiple of 4.
    /// </summary>
    private const int BufferSize = 4096;

    private readonly Stream stream;

    private readonly IMemoryOwner<byte> buffer;

    /// <summary>
    /// The pending code bits, the first bit in the most significant place. The bits after the pending bits are 0.
    /// </summary>
    private ulong window;

    /// <summary>
    /// The number of pending code bits in <see cref="window"/>, 0 to 31 between the writes.
    /// </summary>
    private int windowBits;

    /// <summary>
    /// The number of bytes in <see cref="buffer"/>.
    /// </summary>
    private int position;

    /// <summary>
    /// Initializes a new instance of the <see cref="CcittBitWriter"/> class.
    /// </summary>
    /// <param name="stream">The output stream.</param>
    /// <param name="allocator">The memory allocator for the byte buffer.</param>
    public CcittBitWriter(Stream stream, MemoryAllocator allocator)
    {
        this.stream = stream;
        this.buffer = allocator.Allocate<byte>(BufferSize);
    }

    /// <summary>
    /// Writes a code.
    /// </summary>
    /// <param name="code">The code bits, the first bit in the most significant place of the code length.</param>
    /// <param name="length">The code length, 1 to 32.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteCode(uint code, int length)
    {
        // windowBits is 31 or less, so the code fits in the 64-bit window.
        this.window |= (ulong)code << (64 - this.windowBits - length);
        this.windowBits += length;
        if (this.windowBits >= 32)
        {
            this.MoveWord();
        }
    }

    /// <summary>
    /// Writes one run of white or black pixels: make-up codes, then a terminating code. See ITU-T Recommendation T.4
    /// (07/2003), section 4.1.1 and the note of Table 3b, pages 3-8.
    /// </summary>
    /// <param name="run">The run length.</param>
    /// <param name="isWhite">Whether the run is white.</param>
    public void WriteRun(int run, bool isWhite)
    {
        // A make-up code gives a multiple of 64 up to 2560. A longer run uses one 2560 code for each 2560 pixels.
        if (run >= 64)
        {
            ReadOnlySpan<uint> makeup = isWhite ? CcittCodes.WhiteMakeupCodes : CcittCodes.BlackMakeupCodes;
            while (run >= 64)
            {
                int makeupRun = Math.Min(run & ~63, CcittCodes.MaxMakeupRun);
                uint entry = makeup[(makeupRun >> 6) - 1];
                this.WriteCode(entry >> 4, (int)(entry & 0xF));
                run -= makeupRun;
            }
        }

        // A terminating code gives the rest of the run, 0 to 63.
        uint terminating = isWhite ? CcittCodes.WhiteTerminatingCodes[run] : CcittCodes.BlackTerminatingCodes[run];
        this.WriteCode(terminating >> 4, (int)(terminating & 0xF));
    }

    /// <summary>
    /// Writes an EOL code.
    /// </summary>
    public void WriteEol() => this.WriteCode(CcittCodes.EolCode, CcittCodes.EolLength);

    /// <summary>
    /// Moves to the next byte boundary. The bits before the boundary are 0.
    /// </summary>
    public void PadToByte()
    {
        this.windowBits = (this.windowBits + 7) & ~7;

        // The padding can fill the window to 32 bits. Move the word, so that windowBits stays 31 or less.
        if (this.windowBits >= 32)
        {
            this.MoveWord();
        }
    }

    /// <summary>
    /// Pads the code bits to a byte boundary and writes all bytes to the stream.
    /// </summary>
    public void Flush()
    {
        this.PadToByte();

        // The window holds 0 to 3 whole bytes, the first byte in the most significant place.
        Span<byte> bytes = this.buffer.GetSpan();
        while (this.windowBits > 0)
        {
            bytes[this.position++] = (byte)(this.window >> 56);
            this.window <<= 8;
            this.windowBits -= 8;
        }

        this.stream.Write(bytes[..this.position]);
        this.position = 0;
    }

    /// <inheritdoc/>
    public void Dispose() => this.buffer.Dispose();

    /// <summary>
    /// Moves the high 32 bits of the window to the byte buffer.
    /// </summary>
    private void MoveWord()
    {
        // BufferSize is a multiple of 4, and the buffer is written when it is full, so 4 bytes always fit.
        Span<byte> bytes = this.buffer.GetSpan();
        BinaryPrimitives.WriteUInt32BigEndian(bytes[this.position..], (uint)(this.window >> 32));
        this.position += 4;
        this.window <<= 32;
        this.windowBits -= 32;

        if (this.position == BufferSize)
        {
            this.stream.Write(bytes);
            this.position = 0;
        }
    }
}
