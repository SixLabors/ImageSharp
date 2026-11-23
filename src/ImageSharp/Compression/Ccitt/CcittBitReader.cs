// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Compression.Ccitt;

/// <summary>
/// Reads the code bits of CCITT data from a stream.
/// </summary>
/// <remarks>
/// The reader keeps the next code bits in a 64-bit window. The next bit is the most significant bit of the window. A
/// fill adds whole bytes until the window holds more than 56 bits. Then a decode step can look at 32 bits or more
/// without a check. After the end of the data, a fill adds zero bytes. The reader counts the data bits that are not
/// consumed, so the decoders can tell the zero bytes after the end from data.
/// </remarks>
internal struct CcittBitReader
{
    /// <summary>
    /// The result of <see cref="ReadRun"/> when no run code starts at the next bits: an EOL code, fill bits, or the end
    /// of the data.
    /// </summary>
    public const int NoCode = -1;

    /// <summary>
    /// The result of <see cref="ReadRun"/> when the next bits are not a valid run: bits that are no code, or a run that
    /// is longer than the rest of the row.
    /// </summary>
    public const int InvalidCode = -2;

    private readonly BufferedReadStream stream;

    private readonly bool reverseBits;

    /// <summary>
    /// The next code bits, the next bit in the most significant place. The bits after the last valid bit are 0.
    /// </summary>
    private ulong window;

    /// <summary>
    /// The number of valid bits in <see cref="window"/>: data bits, and zero bits after the end of the data.
    /// </summary>
    private int windowBits;

    /// <summary>
    /// The number of bytes that the reader did not read from the stream yet.
    /// </summary>
    private int bytesToRead;

    /// <summary>
    /// The number of data bits that are not consumed. It is 0 or less after the end of the data.
    /// </summary>
    private long dataBits;

    /// <summary>
    /// Initializes a new instance of the <see cref="CcittBitReader"/> struct.
    /// </summary>
    /// <param name="stream">The compressed input stream.</param>
    /// <param name="bitOrder">The order of the code bits in each byte.</param>
    /// <param name="byteCount">The number of bytes to read from the stream.</param>
    public CcittBitReader(BufferedReadStream stream, CcittBitOrder bitOrder, int byteCount)
    {
        this.stream = stream;
        this.reverseBits = bitOrder == CcittBitOrder.LeastSignificantBitFirst;
        this.bytesToRead = byteCount;
        this.dataBits = (long)byteCount * 8;
        this.Fill();
    }

    /// <summary>
    /// Gets a value indicating whether data bits remain.
    /// </summary>
    public readonly bool HasData => this.dataBits > 0;

    /// <summary>
    /// Reads one run of white or black pixels: make-up codes, then a terminating code.
    /// </summary>
    /// <param name="isWhite">Whether the run is white.</param>
    /// <param name="maxRun">The largest valid run: the pixels that remain in the row.</param>
    /// <returns>The run length, <see cref="NoCode"/> or <see cref="InvalidCode"/>.</returns>
    public int ReadRun(bool isWhite, int maxRun)
    {
        ref ushort table = ref MemoryMarshal.GetArrayDataReference(isWhite ? CcittCodes.WhiteRunTable : CcittCodes.BlackRunTable);
        int shift = 64 - (isWhite ? CcittCodes.WhiteLookupBits : CcittCodes.BlackLookupBits);
        int run = 0;

        while (true)
        {
            this.EnsureBits();

            // The index has the next 12 (white) or 13 (black) bits, so it is always inside the table.
            int entry = Unsafe.Add(ref table, (nint)(this.window >> shift));
            int length = entry & 0xF;
            if (length == 0 || length > this.dataBits)
            {
                // A code that ends after the last data bit is not complete, so the data ends here. The reader drops the
                // last data bits, so each later read sees the end of the data too.
                if (length > this.dataBits || this.dataBits <= 0)
                {
                    this.End();
                    return NoCode;
                }

                // No code starts with 8 zero bits. Such bits are fill bits or an EOL code, and they end the row. Other
                // bits that are no code are not valid data.
                return BitOperations.LeadingZeroCount(this.window) >= 8 ? NoCode : InvalidCode;
            }

            this.Consume(length);
            int codeRun = entry >> 4;
            run += codeRun;
            if (run > maxRun)
            {
                return InvalidCode;
            }

            // A terminating code (run 0 to 63) ends the run. A make-up code (run 64 or more) needs more codes.
            if (codeRun < 64)
            {
                return run;
            }
        }
    }

    /// <summary>
    /// Skips fill bits and EOL codes. An EOL code is 11 zero bits, then a 1 bit. Fill bits are zero bits before an EOL
    /// code, so the reader accepts 11 or more zero bits. See ITU-T Recommendation T.4 (07/2003), sections 4.1.2 and
    /// 4.1.3, page 4: https://www.itu.int/rec/T-REC-T.4
    /// </summary>
    /// <returns><see langword="true"/> if a code follows; <see langword="false"/> if the data ends.</returns>
    public bool SkipEolCodes()
    {
        int zeros = 0;
        while (true)
        {
            if (this.dataBits <= 0)
            {
                return false;
            }

            this.EnsureBits();
            if (this.window == 0)
            {
                // All bits of the window are fill bits.
                zeros += this.windowBits;
                this.ConsumeWindow();
                continue;
            }

            // The window is not 0, so its first 1 bit is a valid bit, and leadingZeros is less than windowBits.
            int leadingZeros = BitOperations.LeadingZeroCount(this.window);
            if (zeros == 0 && leadingZeros < 8)
            {
                // A run code starts at the next bit.
                return true;
            }

            zeros += leadingZeros;
            this.ConsumeToOne(leadingZeros);

            // 8 to 10 zero bits, then a 1 bit, is no code. The next EOL code starts a valid row again.
            if (zeros < 11 && !this.SkipToEol())
            {
                return false;
            }

            zeros = 0;
        }
    }

    /// <summary>
    /// Skips the bits up to the next EOL code, and the EOL code. An EOL code cannot occur in a valid row, so the decoder
    /// can continue at the next row after data that is not valid. See ITU-T Recommendation T.4 (07/2003), section 4.1.2,
    /// page 4.
    /// </summary>
    /// <returns><see langword="true"/> if the reader found an EOL code; <see langword="false"/> if the data ends.</returns>
    public bool SkipToEol()
    {
        int zeros = 0;
        while (this.dataBits > 0)
        {
            this.EnsureBits();
            if (this.window == 0)
            {
                zeros += this.windowBits;
                this.ConsumeWindow();
                continue;
            }

            // The zero bits before the next 1 bit continue the zero bits of the last window.
            int leadingZeros = BitOperations.LeadingZeroCount(this.window);
            zeros += leadingZeros;
            this.ConsumeToOne(leadingZeros);
            if (zeros >= 11)
            {
                return true;
            }

            zeros = 0;
        }

        return false;
    }

    /// <summary>
    /// Ends the data. The reader drops its window and the bytes that it did not read, so each later read sees zero bits
    /// after the end of the data.
    /// </summary>
    public void End()
    {
        this.window = 0;
        this.windowBits = 0;
        this.bytesToRead = 0;
        this.dataBits = 0;
    }

    /// <summary>
    /// Gets a value indicating whether a number of data bits remain.
    /// </summary>
    /// <param name="count">The bit count.</param>
    /// <returns><see langword="true"/> if <paramref name="count"/> data bits or more remain.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool HasBits(int count) => this.dataBits >= count;

    /// <summary>
    /// Moves to the next byte boundary of the data, when the next bit is not on a byte boundary.
    /// </summary>
    public void AlignToByte()
    {
        if (this.dataBits > 0)
        {
            // The data has a whole number of bytes, so the bits that remain in the current byte are dataBits mod 8.
            int skip = (int)(this.dataBits & 7);
            if (skip > 0)
            {
                this.EnsureBits();
                this.Consume(skip);
            }
        }
    }

    /// <summary>
    /// Gets the next bits. The caller must call <see cref="EnsureBits"/> first.
    /// </summary>
    /// <param name="count">The bit count, 1 to 32.</param>
    /// <returns>The next bits, the first bit in the most significant place.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly uint Peek(int count) => (uint)(this.window >> (64 - count));

    /// <summary>
    /// Gets the number of zero bits before the next 1 bit in the window. The caller must call <see cref="EnsureBits"/>
    /// first.
    /// </summary>
    /// <returns>The number of zero bits, 64 when the window is 0.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int LeadingZeros() => BitOperations.LeadingZeroCount(this.window);

    /// <summary>
    /// Makes sure that the window holds 32 bits or more.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureBits()
    {
        if (this.windowBits < 32)
        {
            this.Fill();
        }
    }

    /// <summary>
    /// Consumes bits from the window. The caller must call <see cref="EnsureBits"/> first.
    /// </summary>
    /// <param name="count">The bit count, 0 to 32. A shift of a 64-bit value uses only the low 6 bits of the count.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Consume(int count)
    {
        this.window <<= count;
        this.windowBits -= count;
        this.dataBits -= count;
    }

    /// <summary>
    /// Consumes the zero bits before the first 1 bit of the window, and the 1 bit.
    /// </summary>
    /// <param name="leadingZeros">The number of zero bits, 0 to 63. The window is not 0.</param>
    private void ConsumeToOne(int leadingZeros)
    {
        // The total count, leadingZeros + 1, can be 64, and a shift of a 64-bit value by 64 does not change the value.
        // Two shifts, of leadingZeros and of 1, consume the bits.
        this.window = (this.window << leadingZeros) << 1;
        this.windowBits -= leadingZeros + 1;
        this.dataBits -= leadingZeros + 1;
    }

    /// <summary>
    /// Consumes all bits of the window. A shift of 64 bits does not clear a 64-bit value, so this method sets the
    /// window to 0.
    /// </summary>
    private void ConsumeWindow()
    {
        this.dataBits -= this.windowBits;
        this.window = 0;
        this.windowBits = 0;
    }

    /// <summary>
    /// Adds bytes to the window until it holds more than 56 bits.
    /// </summary>
    private void Fill()
    {
        ref byte reversed = ref MemoryMarshal.GetReference(CcittCodes.ReversedBits);
        while (this.windowBits <= 56)
        {
            ulong value = 0;
            if (this.bytesToRead > 0)
            {
                int next = this.stream.ReadByte();
                if (next < 0)
                {
                    // The stream ends before the byte count. The data ends here, so the missing bytes are not data.
                    this.dataBits -= (long)this.bytesToRead * 8;
                    this.bytesToRead = 0;
                }
                else
                {
                    this.bytesToRead--;
                    value = this.reverseBits ? Unsafe.Add(ref reversed, (uint)next) : (uint)next;
                }
            }

            this.window |= value << (56 - this.windowBits);
            this.windowBits += 8;
        }
    }
}
