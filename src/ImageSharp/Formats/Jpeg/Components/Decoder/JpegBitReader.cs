// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;

/// <summary>
/// Used to buffer and track the bits read from the Huffman entropy encoded data.
/// </summary>
/// <remarks>
/// The reader reads entropy-coded bytes ahead from the stream into its own byte buffer, so a refill can load six
/// bytes at once. The stream position is then ahead of the next unread byte. Call <see cref="EndScan"/> at the end
/// of a scan and <see cref="RewindToMarker"/> instead of setting the stream position, so the stream is at the same
/// position as without the read-ahead buffer.
/// </remarks>
internal unsafe struct JpegBitReader
{
    /// <summary>
    /// The length of the read-ahead byte buffer that the scan decoders give to the reader.
    /// </summary>
    public const int EntropyBufferSize = 4096;

    private readonly BufferedReadStream stream;

    // Entropy-coded bytes read ahead from the stream. Bytes from entropyIndex to entropyCount are not read yet.
    // The stream position is after the last of them. The scan decoder keeps the memory pinned.
    private readonly byte* entropyBuffer;

    private readonly int entropyBufferLength;

    private int entropyIndex;

    private int entropyCount;

    // The entropy encoded code buffer.
    private ulong data;

    // The number of valid bits left to read in the buffer.
    private int remainingBits;

    // Whether there is no more good data to pull from the stream for the current mcu.
    private bool badData;

    // How many times have we hit the eof.
    private int eofHitCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="JpegBitReader"/> struct.
    /// </summary>
    /// <param name="stream">The input stream, at the first entropy-coded byte of the scan.</param>
    /// <param name="entropyBuffer">
    /// The read-ahead byte buffer, can be dirty. The scan decoder owns the memory and keeps it pinned
    /// until the end of the scan.
    /// </param>
    /// <param name="entropyBufferLength">The length of <paramref name="entropyBuffer"/> in bytes.</param>
    public JpegBitReader(BufferedReadStream stream, byte* entropyBuffer, int entropyBufferLength)
    {
        this.stream = stream;
        this.entropyBuffer = entropyBuffer;
        this.entropyBufferLength = entropyBufferLength;
        this.entropyIndex = 0;
        this.entropyCount = 0;
        this.data = 0ul;
        this.remainingBits = 0;
        this.Marker = JpegConstants.Markers.XFF;
        this.MarkerPosition = 0;
        this.badData = false;
        this.NoData = false;
        this.eofHitCount = 0;
    }

    /// <summary>
    /// Gets the current, if any, marker in the input stream.
    /// </summary>
    public byte Marker { get; private set; }

    /// <summary>
    /// Gets the opening position of an identified marker.
    /// </summary>
    public long MarkerPosition { get; private set; }

    /// <summary>
    /// Gets a value indicating whether to continue reading the input stream.
    /// </summary>
    public bool NoData { get; private set; }

    /// <summary>
    /// Gets the stream position of the next unread entropy-coded byte.
    /// </summary>
    private readonly long Position => this.stream.Position - (this.entropyCount - this.entropyIndex);

    /// <summary>
    /// Moves the stream to the start of the detected marker and drops the read-ahead bytes.
    /// Later reads continue from the marker.
    /// </summary>
    public void RewindToMarker()
    {
        this.stream.Position = this.MarkerPosition;
        this.entropyIndex = 0;
        this.entropyCount = 0;
    }

    /// <summary>
    /// Ends the scan. Moves the stream to the start of a detected marker that is not a restart marker,
    /// or else to the next unread byte, and drops the read-ahead bytes.
    /// The segment parser then continues from the same position as without the read-ahead buffer.
    /// </summary>
    public void EndScan()
    {
        if (this.HasBadMarker())
        {
            this.RewindToMarker();
            return;
        }

        this.stream.Position = this.Position;
        this.entropyIndex = 0;
        this.entropyCount = 0;
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    public void CheckBits()
    {
        if (this.remainingBits < JpegConstants.Huffman.MinBits)
        {
            this.FillBuffer();
        }
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    public void Reset()
    {
        this.data = 0ul;
        this.remainingBits = 0;
        this.Marker = JpegConstants.Markers.XFF;
        this.MarkerPosition = 0;
        this.badData = false;
        this.NoData = false;
    }

    /// <summary>
    /// Whether a RST marker has been detected, I.E. One that is between RST0 and RST7
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    public readonly bool HasRestartMarker() => HasRestart(this.Marker);

    /// <summary>
    /// Whether a bad marker has been detected, I.E. One that is not between RST0 and RST7
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    public readonly bool HasBadMarker() => this.Marker != JpegConstants.Markers.XFF && !this.HasRestartMarker();

    /// <summary>
    /// Determines whether the detected marker is TEM or a reserved code before SOF0.
    /// </summary>
    /// <returns>True for TEM and reserved marker codes.</returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    public readonly bool HasReservedMarker() => this.Marker < JpegConstants.Markers.SOF0;

    [MethodImpl(InliningOptions.AlwaysInline)]
    public void FillBuffer()
    {
        // Attempt to load at least the minimum number of required bits into the buffer.
        // We fail to do so only if we hit a marker or reach the end of the input stream.
        this.remainingBits += JpegConstants.Huffman.FetchBits;
        this.data = (this.data << JpegConstants.Huffman.FetchBits) | this.GetBytes(this.data);
    }

    /// <summary>
    /// Decodes the Huffman-coded DC difference and AC coefficients of one baseline block.
    /// </summary>
    /// <remarks>
    /// The bit buffer is copied to locals for the whole block. The fields live in the scan decoder object,
    /// so the JIT cannot keep them in registers. Each peek and consume would otherwise be a memory
    /// read-modify-write on the critical dependency chain.
    /// The local buffer is left-justified: the next bit is bit 63. The lookup index is then one shift, and
    /// consuming bits is one shift. The refill points are the same as in <see cref="DecodeHuffman(ref HuffmanTable)"/>
    /// and <see cref="Receive(int)"/>: before each symbol, and before value bits that are past the lookup index.
    /// </remarks>
    /// <param name="block">The first coefficient of the block in natural order. The AC coefficients are written here.</param>
    /// <param name="dcTable">The DC Huffman table.</param>
    /// <param name="dcLookup">
    /// The first of <see cref="JpegConstants.Huffman.FastSize"/> combined lookup entries for <paramref name="dcTable"/>.
    /// </param>
    /// <param name="acTable">The AC Huffman table.</param>
    /// <param name="acLookup">
    /// The first of <see cref="JpegConstants.Huffman.FastSize"/> combined lookup entries for <paramref name="acTable"/>.
    /// </param>
    /// <returns>The DC difference. The caller adds the predictor and writes the DC coefficient.</returns>
    public int DecodeBlockBaseline(ref short block, ref HuffmanTable dcTable, ref int dcLookup, ref HuffmanTable acTable, ref int acLookup)
    {
        // The data field keeps all fetched bits, right-justified, because the end-of-data check in GetBytes
        // reads it. Refill updates the field, so only the bit count must be written back.
        int bits = this.remainingBits;
        ulong buffer = LeftJustify(this.data, bits);

        // DC. A complete entry gives the difference. Symbols above 15 are not valid DC sizes.
        // They and the other cases use the field-based decoder.
        if (bits < JpegConstants.Huffman.MinBits)
        {
            buffer |= this.Refill() << (JpegConstants.Huffman.MinBits - bits);
            bits += JpegConstants.Huffman.FetchBits;
        }

        // The lookup index is less than FastSize, so the table reads are in range.
        int dc;
        int entry = Unsafe.Add(ref dcLookup, (nint)(buffer >> (JpegConstants.Huffman.RegisterSize - JpegConstants.Huffman.FastBits)));
        if ((entry & (HuffmanTable.FastComplete | 0xF000)) == HuffmanTable.FastComplete)
        {
            // A 64-bit shift uses the low 6 bits of the count. Bits 0 to 4 of the entry are the bits to consume.
            buffer <<= entry;
            bits -= entry & 31;
            dc = entry >> 16;
        }
        else
        {
            this.remainingBits = bits;
            dc = this.DecodeDcSlow(ref dcTable);
            bits = this.remainingBits;
            buffer = LeftJustify(this.data, bits);
        }

        // AC. The loop starts with i < 64 and a run adds at most 15, so i stays below the 80 entries of
        // TransposingOrder. The extra entries map to 63, so corrupt runs cannot write outside the block.
        ref byte zigZag = ref MemoryMarshal.GetReference(ZigZag.TransposingOrder);
        for (int i = 1; i < 64;)
        {
            if (bits < JpegConstants.Huffman.MinBits)
            {
                buffer |= this.Refill() << (JpegConstants.Huffman.MinBits - bits);
                bits += JpegConstants.Huffman.FetchBits;
            }

            entry = Unsafe.Add(ref acLookup, (nint)(buffer >> (JpegConstants.Huffman.RegisterSize - JpegConstants.Huffman.FastBits)));

            // The symbol is the zero run in the high nibble and the value bit count in the low nibble.
            if ((entry & HuffmanTable.FastComplete) != 0)
            {
                buffer <<= entry;
                bits -= entry & 31;

                if ((entry & 0x0F00) != 0)
                {
                    i += (entry >> 12) & 15;
                    Unsafe.Add(ref block, Unsafe.Add(ref zigZag, (uint)i++)) = (short)(entry >> 16);
                    continue;
                }

                // A zero value bit count: run 0 is the end of the block, other runs skip 16 zeros (ZRL).
                if ((entry & 0xFF00) == 0)
                {
                    break;
                }

                i += 16;
                continue;
            }

            if (entry != 0)
            {
                // The code is in the table, but its value bits are past the lookup index.
                // Bits 0 to 4 of the entry are the code length.
                buffer <<= entry;
                bits -= entry & 31;
                int s = (entry >> 8) & 15;
                i += (entry >> 12) & 15;

                if (bits < JpegConstants.Huffman.MinBits)
                {
                    buffer |= this.Refill() << (JpegConstants.Huffman.MinBits - bits);
                    bits += JpegConstants.Huffman.FetchBits;
                }

                bits -= s;
                Unsafe.Add(ref block, Unsafe.Add(ref zigZag, (uint)i++)) = (short)Extend((int)(buffer >> (JpegConstants.Huffman.RegisterSize - s)), s);
                buffer <<= s;
                continue;
            }

            // The code is longer than the lookup index, or not valid.
            this.remainingBits = bits;
            i = this.DecodeAcSlow(ref acTable, ref block, i);
            bits = this.remainingBits;
            buffer = LeftJustify(this.data, bits);
        }

        this.remainingBits = bits;
        return dc;
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    public unsafe int DecodeHuffman(ref HuffmanTable h)
    {
        this.CheckBits();
        int index = this.PeekBits(JpegConstants.Huffman.LookupBits);
        int size = h.LookaheadSize[index];

        if (size < JpegConstants.Huffman.SlowBits)
        {
            this.remainingBits -= size;
            return h.LookaheadValue[index];
        }

        ulong x = this.data << (JpegConstants.Huffman.RegisterSize - this.remainingBits);
        while (x > h.MaxCode[size])
        {
            size++;
        }

        this.remainingBits -= size;

        return h.Values[(h.ValOffset[size] + (int)(x >> (JpegConstants.Huffman.RegisterSize - size))) & 0xFF];
    }

    /// <summary>
    /// Decodes a DC difference with the field state. Used when the combined lookup table has no complete entry.
    /// </summary>
    /// <param name="dcTable">The DC Huffman table.</param>
    /// <returns>The DC difference.</returns>
    [MethodImpl(InliningOptions.ColdPath)]
    private int DecodeDcSlow(ref HuffmanTable dcTable)
    {
        int t = this.DecodeHuffman(ref dcTable);
        return t != 0 ? this.Receive(t) : 0;
    }

    /// <summary>
    /// Decodes one AC symbol and its value with the field state. Used for codes longer than the lookup index.
    /// </summary>
    /// <param name="acTable">The AC Huffman table.</param>
    /// <param name="block">The first coefficient of the block in natural order.</param>
    /// <param name="i">The zigzag index of the next coefficient.</param>
    /// <returns>The zigzag index after the symbol. 64 at the end of the block.</returns>
    [MethodImpl(InliningOptions.ColdPath)]
    private int DecodeAcSlow(ref HuffmanTable acTable, ref short block, int i)
    {
        int s = this.DecodeHuffman(ref acTable);
        int r = s >> 4;
        s &= 15;

        if (s != 0)
        {
            i += r;
            s = this.Receive(s);
            Unsafe.Add(ref block, ZigZag.TransposingOrder[i++]) = (short)s;
            return i;
        }

        return r == 0 ? 64 : i + 16;
    }

    /// <summary>
    /// Adds <see cref="JpegConstants.Huffman.FetchBits"/> bits to the field buffer and returns them.
    /// The block decoder places the returned bits below its valid local bits and adds the count to its bit count.
    /// </summary>
    /// <returns>The fetched bits in the low <see cref="JpegConstants.Huffman.FetchBits"/> bits.</returns>
    [MethodImpl(InliningOptions.ColdPath)]
    private ulong Refill()
    {
        ulong bytes = this.GetBytes(this.data);
        this.data = (this.data << JpegConstants.Huffman.FetchBits) | bytes;
        return bytes;
    }

    /// <summary>
    /// Moves the unconsumed bits of a right-justified buffer to the top of a 64-bit value. The lower bits are zero.
    /// </summary>
    /// <param name="data">The right-justified buffer.</param>
    /// <param name="bits">The number of unconsumed bits. Corrupt data can make it zero or less.</param>
    /// <returns>The left-justified bits.</returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static ulong LeftJustify(ulong data, int bits)
        => bits > 0 ? data << (JpegConstants.Huffman.RegisterSize - bits) : 0;

    [MethodImpl(InliningOptions.ShortMethod)]
    public int Receive(int nbits)
    {
        this.CheckBits();
        return Extend(this.GetBits(nbits), nbits);
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static bool HasRestart(byte marker)
        => marker >= JpegConstants.Markers.RST0 && marker <= JpegConstants.Markers.RST7;

    [MethodImpl(InliningOptions.ShortMethod)]
    public int GetBits(int nbits) => (int)ExtractBits(this.data, this.remainingBits -= nbits, nbits);

    [MethodImpl(InliningOptions.ShortMethod)]
    public readonly int PeekBits(int nbits) => (int)ExtractBits(this.data, this.remainingBits - nbits, nbits);

    [MethodImpl(InliningOptions.AlwaysInline)]
    private static ulong ExtractBits(ulong value, int offset, int size) => (value >> offset) & (ulong)((1 << size) - 1);

    /// <summary>
    /// Converts JPEG value bits to a signed coefficient (ITU T.81, F.2.2.1).
    /// </summary>
    /// <param name="v">The value bits.</param>
    /// <param name="nbits">The number of value bits.</param>
    /// <returns>The signed coefficient.</returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    public static int Extend(int v, int nbits) => v - ((((v + v) >> nbits) - 1) & ((1 << nbits) - 1));

    /// <summary>
    /// Reads the next <see cref="JpegConstants.Huffman.FetchLoop"/> entropy-coded bytes. Removes stuffed zero bytes
    /// and stops at a marker.
    /// </summary>
    /// <param name="data">The bit buffer before the refill. The end-of-data check reads it.</param>
    /// <returns>The bytes in big-endian order.</returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    private ulong GetBytes(ulong data)
    {
        // Fast path: most entropy-coded bytes are not 0xFF. When the read-ahead buffer holds the next six bytes
        // (FetchLoop) and none of them is 0xFF, there is no stuffed byte and no marker, so one big-endian load
        // gives the same result as six ReadStream calls. The buffer holds only bytes before the stream end,
        // so no EOF is hit.
        int index = this.entropyIndex;
        if (!this.badData && this.entropyCount - index >= JpegConstants.Huffman.FetchLoop)
        {
            byte* source = this.entropyBuffer + index;
            ulong value = ((ulong)BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<uint>(source)) << 16)
                | BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<ushort>(source + 4));

            // A byte is 0xFF when its complement is zero. The SWAR zero-byte test (v - 0x01..) & ~v & 0x80..
            // is nonzero only when some byte of v is zero. The top 16 bits are outside the mask, and a borrow
            // reaches them only after a zero byte is already found.
            ulong inverted = ~value & 0xFFFF_FFFF_FFFFUL;
            if (((inverted - 0x0101_0101_0101UL) & ~inverted & 0x8080_8080_8080UL) == 0)
            {
                this.entropyIndex = index + JpegConstants.Huffman.FetchLoop;
                return value;
            }
        }

        return this.GetBytesSlow(data);
    }

    /// <summary>
    /// Reads the next <see cref="JpegConstants.Huffman.FetchLoop"/> entropy-coded bytes one at a time.
    /// Used near a stuffed byte, a marker, the end of the read-ahead buffer, or the end of the stream.
    /// </summary>
    /// <param name="data">The bit buffer before the refill. The end-of-data check reads it.</param>
    /// <returns>The bytes in big-endian order.</returns>
    [MethodImpl(InliningOptions.ColdPath)]
    private ulong GetBytesSlow(ulong data)
    {
        ulong temp = 0;
        for (int i = 0; i < JpegConstants.Huffman.FetchLoop; i++)
        {
            int b = this.ReadStream(data);

            // Found a marker.
            if (b == JpegConstants.Markers.XFF)
            {
                int c = this.ReadStream(data);
                while (c == JpegConstants.Markers.XFF)
                {
                    // Loop here to discard any padding FF bytes on terminating marker,
                    // so that we can save a valid marker value.
                    c = this.ReadStream(data);
                }

                // Found a marker
                // We accept multiple FF bytes followed by a 0 as meaning a single FF data byte.
                // even though it's considered 'invalid' according to the specs.
                if (c != 0)
                {
                    // It's a trick so we won't read past actual marker
                    this.badData = true;
                    this.Marker = (byte)c;
                    this.MarkerPosition = this.Position - 2;
                }
            }

            temp = (temp << 8) | (ulong)(long)b;
        }

        return temp;
    }

    /// <summary>
    /// Finds the next marker at a restart boundary and applies the selected image-data recovery policy.
    /// </summary>
    /// <param name="integrityHandling">The recovery policy for compressed image data.</param>
    /// <returns>True if a marker was found. False if the stream ended first.</returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    public bool FindNextMarker(SegmentIntegrityHandling integrityHandling)
    {
        do
        {
            // The bit reader can already hold a marker from its entropy-data lookahead.
            // Libjpeg treats codes before SOF0 as damaged image data and scans forward to another marker.
            // Only IgnoreImageData permits that recovery. Real segment markers stay with the segment parser.
            if (this.HasReservedMarker() && integrityHandling != SegmentIntegrityHandling.IgnoreImageData)
            {
                JpegThrowHelper.ThrowInvalidImageContentException("Unexpected marker before a JPEG restart marker.");
            }

            while (true)
            {
                int b = this.ReadEntropyByte();
                if (b == -1)
                {
                    return false;
                }

                if (b == JpegConstants.Markers.XFF)
                {
                    // Multiple FF bytes are marker padding. FF 00 is a stuffed data byte, not a marker.
                    while (b == JpegConstants.Markers.XFF)
                    {
                        b = this.ReadEntropyByte();
                        if (b == -1)
                        {
                            return false;
                        }
                    }

                    if (b != 0)
                    {
                        this.Marker = (byte)b;
                        this.MarkerPosition = this.Position - 2;
                        break;
                    }
                }
            }
        }
        while (this.HasReservedMarker());

        return true;
    }

    [MethodImpl(InliningOptions.AlwaysInline)]
    private int ReadStream(ulong data)
    {
        int value = this.badData ? 0 : this.ReadEntropyByte();

        // We've encountered the end of the file stream which means there's no EOI marker or the marker has been read
        // during decoding of the SOS marker.
        // When reading individual bits 'badData' simply means we have hit a marker, When data is '0' and the stream is exhausted
        // we know we have hit the EOI and completed decoding the scan buffer.
        if (value == -1 || (this.badData && data == 0 && this.Position >= this.stream.Length))
        {
            // We've hit the end of the file stream more times than allowed which means there's no EOI marker
            // in the image or the SOS marker has the wrong dimensions set.
            if (this.eofHitCount > JpegConstants.Huffman.FetchLoop)
            {
                this.badData = true;
                this.NoData = true;
                value = 0;
            }

            this.eofHitCount++;
        }

        return value;
    }

    /// <summary>
    /// Reads the next entropy-coded byte from the read-ahead buffer.
    /// </summary>
    /// <returns>The byte, or -1 at the end of the stream.</returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    private int ReadEntropyByte()
    {
        int index = this.entropyIndex;
        if (index < this.entropyCount)
        {
            this.entropyIndex = index + 1;
            return this.entropyBuffer[index];
        }

        return this.FillEntropyBuffer();
    }

    /// <summary>
    /// Reads the next block of entropy-coded bytes into the empty read-ahead buffer and returns the first byte.
    /// </summary>
    /// <returns>The byte, or -1 at the end of the stream.</returns>
    [MethodImpl(InliningOptions.ColdPath)]
    private int FillEntropyBuffer()
    {
        // At the stream end ReadByte returns -1 and counts the EOF hit once, the same as before the read-ahead
        // buffer. Read would count one more EOF hit for its zero-byte result.
        if (this.stream.RemainingBytes <= 0)
        {
            return this.stream.ReadByte();
        }

        // Read returns at least one byte here, because bytes remain before the stream end.
        this.entropyCount = this.stream.Read(new Span<byte>(this.entropyBuffer, this.entropyBufferLength));
        this.entropyIndex = 1;
        return this.entropyBuffer[0];
    }
}
