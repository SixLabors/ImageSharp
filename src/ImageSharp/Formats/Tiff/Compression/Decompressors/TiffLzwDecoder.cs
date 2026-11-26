// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;

/*
   This implementation is based on a port of a java tiff decoder by Harald Kuhr: https://github.com/haraldk/TwelveMonkeys

   Original licence:

   BSD 3-Clause License

   * Copyright (c) 2015, Harald Kuhr
   * All rights reserved.
   *
   * Redistribution and use in source and binary forms, with or without
   * modification, are permitted provided that the following conditions are met:
   *
   * * Redistributions of source code must retain the above copyright notice, this
   * list of conditions and the following disclaimer.
   *
   * * Redistributions in binary form must reproduce the above copyright notice,
   *   this list of conditions and the following disclaimer in the documentation
   *   and/or other materials provided with the distribution.
   *
   ** Neither the name of the copyright holder nor the names of its
   * contributors may be used to endorse or promote products derived from
   *   this software without specific prior written permission.
   *
   * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
   * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
   * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
   * DISCLAIMED.IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
   * FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
   * DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
   * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
   * CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
   * OR TORT(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
   * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

/// <summary>
/// Decompresses and decodes data using the dynamic LZW algorithms, see TIFF spec Section 13.
/// </summary>
internal sealed class TiffLzwDecoder
{
    /// <summary>
    /// The stream to decode.
    /// </summary>
    private readonly Stream stream;

    /// <summary>
    /// As soon as we use entry 4094 of the table (maxTableSize - 2), the lzw compressor write out a (12-bit) ClearCode.
    /// At this point, the compressor reinitializes the string table and then writes out 9-bit codes again.
    /// </summary>
    private const int ClearCode = 256;

    /// <summary>
    /// End of Information.
    /// </summary>
    private const int EoiCode = 257;

    /// <summary>
    /// Minimum code length of 9 bits.
    /// </summary>
    private const int MinBits = 9;

    /// <summary>
    /// Maximum code length of 12 bits.
    /// </summary>
    private const int MaxBits = 12;

    /// <summary>
    /// Maximum table size of 4096.
    /// </summary>
    private const int TableSize = 1 << MaxBits;

    private readonly LzwString[] table;

    private int tableLength;
    private int bitsPerCode;
    private int oldCode = ClearCode;
    private int maxCode;
    private int bitMask;
    private int maxString;
    private bool eofReached;
    private int bytesLeft;
    private int nextData;
    private int nextBits;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffLzwDecoder" /> class
    /// and sets the stream, where the compressed data should be read from.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="byteCount">The byte count of the compressed data. The stream has these bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream" /> is null.</exception>
    public TiffLzwDecoder(Stream stream, int byteCount)
    {
        Guard.NotNull(stream, nameof(stream));

        this.stream = stream;
        this.bytesLeft = byteCount;

        // TODO: Investigate a manner by which we can avoid this allocation.
        this.table = new LzwString[TableSize];
        for (int i = 0; i < 256; i++)
        {
            this.table[i] = new LzwString((byte)i);
        }

        this.Init();
    }

    private void Init()
    {
        // Table length is 256 + 2, because of special clear code and end of information code.
        this.tableLength = 258;
        this.bitsPerCode = MinBits;
        this.bitMask = BitmaskFor(this.bitsPerCode);
        this.maxCode = this.MaxCode();
        this.maxString = 1;
    }

    /// <summary>
    /// Decodes and decompresses all pixel indices from the stream.
    /// </summary>
    /// <param name="pixels">The pixel array to decode to.</param>
    /// <returns>
    /// The count of the decoded bytes. If the decoder found a code that is not valid, or if the data ended early, the count is less than the length of
    /// <paramref name="pixels"/>. The decoder does not change the bytes after that count.
    /// </returns>
    public int DecodePixels(Span<byte> pixels)
    {
        // Adapted from the pseudo-code example found in the TIFF 6.0 Specification, 1992.
        // See Section 13: "LZW Compression"/"LZW Decoding", page 61+
        int code;
        int offset = 0;

        while ((code = this.GetNextCode()) != EoiCode)
        {
            if (code == ClearCode)
            {
                this.Init();

                // A Clear code can follow a Clear code. The decoder skips these codes, as libtiff does.
                do
                {
                    code = this.GetNextCode();
                }
                while (code == ClearCode);

                if (code == EoiCode)
                {
                    break;
                }

                // Init resets the table length but keeps the old entries. Thus a code after the clear code must be in the
                // new table, not only have an entry. Codes 256 and 257 have no entry.
                if (!this.IsInTable(code) || this.table[code] == null)
                {
                    return offset;
                }

                offset += this.table[code].WriteTo(pixels, offset);
            }
            else
            {
                if (this.table[this.oldCode] == null)
                {
                    return offset;
                }

                bool added;
                if (this.IsInTable(code))
                {
                    offset += this.table[code].WriteTo(pixels, offset);

                    added = this.AddStringToTable(this.table[this.oldCode].Concatenate(this.table[code].FirstChar));
                }
                else if (code == this.tableLength)
                {
                    // The code is the next table entry, which the decoder does not have yet. It is the old string and
                    // the first byte of the old string. See TIFF 6.0 specification, section 13, LZW Decoding, page 61:
                    // https://www.itu.int/itudoc/itu-t/com16/tiff-fx/docs/tiff6.pdf
                    LzwString outString = this.table[this.oldCode].Concatenate(this.table[this.oldCode].FirstChar);

                    offset += outString.WriteTo(pixels, offset);
                    added = this.AddStringToTable(outString);
                }
                else
                {
                    // A code after the next table entry is not valid data.
                    return offset;
                }

                // A full table and no clear code is not valid data. The pixels of this code stay.
                if (!added)
                {
                    return offset;
                }
            }

            this.oldCode = code;

            if (offset >= pixels.Length)
            {
                break;
            }
        }

        return offset;
    }

    /// <summary>
    /// Adds a string to the table.
    /// </summary>
    /// <param name="lzwString">The string.</param>
    /// <returns><see langword="true"/> if the table has room for the string. Else <see langword="false"/>.</returns>
    private bool AddStringToTable(LzwString lzwString)
    {
        // The table has TableSize entries, so a full table has no index for the next string.
        if (this.tableLength >= this.table.Length)
        {
            return false;
        }

        this.table[this.tableLength++] = lzwString;

        if (this.tableLength > this.maxCode)
        {
            this.bitsPerCode++;

            if (this.bitsPerCode > MaxBits)
            {
                // Continue reading MaxBits (12 bit) length codes.
                this.bitsPerCode = MaxBits;
            }

            this.bitMask = BitmaskFor(this.bitsPerCode);
            this.maxCode = this.MaxCode();
        }

        if (lzwString.Length > this.maxString)
        {
            this.maxString = lzwString.Length;
        }

        return true;
    }

    private int GetNextCode()
    {
        if (this.eofReached)
        {
            return EoiCode;
        }

        // The data ends at the byte count. libtiff ends the data in the same way, at the count of the bits that remain. The stream has these bytes. Thus each
        // read gives a byte.
        if (this.bytesLeft == 0)
        {
            this.eofReached = true;
            return EoiCode;
        }

        this.bytesLeft--;
        this.nextData = (this.nextData << 8) | this.stream.ReadByte();
        this.nextBits += 8;

        if (this.nextBits < this.bitsPerCode)
        {
            if (this.bytesLeft == 0)
            {
                this.eofReached = true;
                return EoiCode;
            }

            this.bytesLeft--;
            this.nextData = (this.nextData << 8) | this.stream.ReadByte();
            this.nextBits += 8;
        }

        int code = (this.nextData >> (this.nextBits - this.bitsPerCode)) & this.bitMask;
        this.nextBits -= this.bitsPerCode;

        return code;
    }

    private bool IsInTable(int code) => code < this.tableLength;

    private int MaxCode() => this.bitMask - 1;

    private static int BitmaskFor(int bits) => (1 << bits) - 1;
}
