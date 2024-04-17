// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;

/// <summary>
/// Used to buffer and track the bits read from the Huffman entropy encoded data.
/// </summary>
internal struct JpegBitReader
{
    private readonly BufferedReadStream stream;

    // The entropy encoded code buffer.
    private ulong data;

    // The number of valid bits left to read in the buffer.
    private int remainingBits;

    // Whether there is no more good data to pull from the stream for the current mcu.
    private bool badData;

    // How many times have we hit the eof.
    private int eofHitCount;

    public JpegBitReader(BufferedReadStream stream)
    {
        this.stream = stream;
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
    public bool HasRestartMarker() => HasRestart(this.Marker);

    /// <summary>
    /// Whether a bad marker has been detected, I.E. One that is not between RST0 and RST7
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    public bool HasBadMarker() => this.Marker != JpegConstants.Markers.XFF && !this.HasRestartMarker();

    [MethodImpl(InliningOptions.AlwaysInline)]
    public void FillBuffer()
    {
        // Attempt to load at least the minimum number of required bits into the buffer.
        // We fail to do so only if we hit a marker or reach the end of the input stream.
        this.remainingBits += JpegConstants.Huffman.FetchBits;
        this.data = (this.data << JpegConstants.Huffman.FetchBits) | this.GetBytes();
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
    public int PeekBits(int nbits) => (int)ExtractBits(this.data, this.remainingBits - nbits, nbits);

    [MethodImpl(InliningOptions.AlwaysInline)]
    private static ulong ExtractBits(ulong value, int offset, int size) => (value >> offset) & (ulong)((1 << size) - 1);

    [MethodImpl(InliningOptions.ShortMethod)]
    private static int Extend(int v, int nbits) => v - ((((v + v) >> nbits) - 1) & ((1 << nbits) - 1));

    [MethodImpl(InliningOptions.ShortMethod)]
    private ulong GetBytes()
    {
        ulong temp = 0;
        for (int i = 0; i < JpegConstants.Huffman.FetchLoop; i++)
        {
            int b = this.ReadStream();

            // Found a marker.
            if (b == JpegConstants.Markers.XFF)
            {
                int c = this.ReadStream();
                while (c == JpegConstants.Markers.XFF)
                {
                    // Loop here to discard any padding FF bytes on terminating marker,
                    // so that we can save a valid marker value.
                    c = this.ReadStream();
                }

                // Found a marker
                // We accept multiple FF bytes followed by a 0 as meaning a single FF data byte.
                // even though it's considered 'invalid' according to the specs.
                if (c != 0)
                {
                    // It's a trick so we won't read past actual marker
                    this.badData = true;
                    this.Marker = (byte)c;
                    this.MarkerPosition = this.stream.Position - 2;
                }
            }

            temp = (temp << 8) | (ulong)(long)b;
        }

        return temp;
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    public bool FindNextMarker()
    {
        while (true)
        {
            int b = this.stream.ReadByte();
            if (b == -1)
            {
                return false;
            }

            // Found a marker.
            if (b == JpegConstants.Markers.XFF)
            {
                while (b == JpegConstants.Markers.XFF)
                {
                    // Loop here to discard any padding FF bytes on terminating marker.
                    b = this.stream.ReadByte();
                    if (b == -1)
                    {
                        return false;
                    }
                }

                // Found a valid marker. Exit loop
                if (b != 0)
                {
                    this.Marker = (byte)b;
                    this.MarkerPosition = this.stream.Position - 2;
                    return true;
                }
            }
        }
    }

    [MethodImpl(InliningOptions.AlwaysInline)]
    private int ReadStream()
    {
        int value = this.badData ? 0 : this.stream.ReadByte();

        // We've encountered the end of the file stream which means there's no EOI marker or the marker has been read
        // during decoding of the SOS marker.
        // When reading individual bits 'badData' simply means we have hit a marker, When data is '0' and the stream is exhausted
        // we know we have hit the EOI and completed decoding the scan buffer.
        if (value == -1 || (this.badData && this.data == 0 && this.stream.Position >= this.stream.Length))
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
}
