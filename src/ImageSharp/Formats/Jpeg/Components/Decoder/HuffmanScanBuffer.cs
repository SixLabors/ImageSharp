// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Used to buffer and track the bits read from the Huffman entropy encoded data.
    /// </summary>
    internal struct HuffmanScanBuffer
    {
        private readonly DoubleBufferedStreamReader stream;

        // The entropy encoded code buffer.
        private ulong data;

        // The number of valid bits left to read in the buffer.
        private int remain;

        // Whether there is more data to pull from the stream for the current mcu.
        private bool noMore;

        public HuffmanScanBuffer(DoubleBufferedStreamReader stream)
        {
            this.stream = stream;
            this.data = 0ul;
            this.remain = 0;
            this.Marker = JpegConstants.Markers.XFF;
            this.MarkerPosition = 0;
            this.BadMarker = false;
            this.noMore = false;
            this.Eof = false;
        }

        /// <summary>
        /// Gets or sets the current, if any, marker in the input stream.
        /// </summary>
        public byte Marker { get; set; }

        /// <summary>
        /// Gets or sets the opening position of an identified marker.
        /// </summary>
        public long MarkerPosition { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether we have a bad marker, I.E. One that is not between RST0 and RST7
        /// </summary>
        public bool BadMarker { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether we have prematurely reached the end of the file.
        /// </summary>
        public bool Eof { get; set; }

        [MethodImpl(InliningOptions.ShortMethod)]
        public void CheckBits()
        {
            if (this.remain < 16)
            {
                this.FillBuffer();
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public void Reset()
        {
            this.data = 0ul;
            this.remain = 0;
            this.Marker = JpegConstants.Markers.XFF;
            this.MarkerPosition = 0;
            this.BadMarker = false;
            this.noMore = false;
            this.Eof = false;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public bool HasRestart()
        {
            byte m = this.Marker;
            return m >= JpegConstants.Markers.RST0 && m <= JpegConstants.Markers.RST7;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public void FillBuffer()
        {
            // Attempt to load at least the minimum number of required bits into the buffer.
            // We fail to do so only if we hit a marker or reach the end of the input stream.
            this.remain += 48;
            this.data = (this.data << 48) | this.GetBytes();
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public unsafe int DecodeHuffman(ref HuffmanTable h)
        {
            this.CheckBits();
            int v = this.PeekBits(JpegConstants.Huffman.LookupBits);
            int symbol = h.LookaheadValue[v];
            int size = h.LookaheadSize[v];

            if (size == JpegConstants.Huffman.SlowBits)
            {
                ulong x = this.data << (JpegConstants.Huffman.RegisterSize - this.remain);
                while (x > h.MaxCode[size])
                {
                    size++;
                }

                v = (int)(x >> (JpegConstants.Huffman.RegisterSize - size));
                symbol = h.Values[h.ValOffset[size] + v];
            }

            this.remain -= size;

            return symbol;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public int Receive(int nbits)
        {
            this.CheckBits();
            return Extend(this.GetBits(nbits), nbits);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public int GetBits(int nbits) => (int)ExtractBits(this.data, this.remain -= nbits, nbits);

        [MethodImpl(InliningOptions.ShortMethod)]
        public int PeekBits(int nbits) => (int)ExtractBits(this.data, this.remain - nbits, nbits);

        [MethodImpl(InliningOptions.ShortMethod)]
        private static ulong ExtractBits(ulong value, int offset, int size) => (value >> offset) & (ulong)((1 << size) - 1);

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int Extend(int v, int nbits) => v - ((((v + v) >> nbits) - 1) & ((1 << nbits) - 1));

        [MethodImpl(InliningOptions.ShortMethod)]
        private ulong GetBytes()
        {
            ulong temp = 0;
            for (int i = 0; i < 6; i++)
            {
                int b = this.noMore ? 0 : this.stream.ReadByte();

                if (b == -1)
                {
                    // We've encountered the end of the file stream which means there's no EOI marker in the image
                    // or the SOS marker has the wrong dimensions set.
                    this.Eof = true;
                    b = 0;
                }

                // Found a marker.
                if (b == JpegConstants.Markers.XFF)
                {
                    this.MarkerPosition = this.stream.Position - 1;
                    int c = this.stream.ReadByte();
                    while (c == JpegConstants.Markers.XFF)
                    {
                        c = this.stream.ReadByte();

                        if (c == -1)
                        {
                            this.Eof = true;
                            c = 0;
                            break;
                        }
                    }

                    if (c != 0)
                    {
                        this.Marker = (byte)c;
                        this.noMore = true;
                        if (!this.HasRestart())
                        {
                            this.BadMarker = true;
                        }
                    }
                }

                temp = (temp << 8) | (ulong)(long)b;
            }

            return temp;
        }
    }
}