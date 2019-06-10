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

        // Whether there is no more good data to pull from the stream for the current mcu.
        private bool badData;

        public HuffmanScanBuffer(DoubleBufferedStreamReader stream)
        {
            this.stream = stream;
            this.data = 0ul;
            this.remain = 0;
            this.Marker = JpegConstants.Markers.XFF;
            this.MarkerPosition = 0;
            this.BadMarker = false;
            this.badData = false;
            this.NoData = false;
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
        /// Gets a value indicating whether a bad marker has been detected, I.E. One that is not between RST0 and RST7
        /// </summary>
        public bool BadMarker { get; private set; }

        /// <summary>
        /// Gets a value indicating whether to continue reading the input stream.
        /// </summary>
        public bool NoData { get; private set; }

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
            this.badData = false;
            this.NoData = false;
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
                symbol = h.Values[(h.ValOffset[size] + v) & 0xFF];
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
                int b = this.ReadStream();

                // Found a marker.
                if (b == JpegConstants.Markers.XFF)
                {
                    int c = this.ReadStream();
                    while (c == JpegConstants.Markers.XFF)
                    {
                        // Loop here to discard any padding FF's on terminating marker,
                        // so that we can save a valid marker value.
                        c = this.ReadStream();
                    }

                    // We accept multiple FF's followed by a 0 as meaning a single FF data byte.
                    // This data pattern is not valid according to the standard.
                    if (c != 0)
                    {
                        this.Marker = (byte)c;
                        this.badData = true;
                        if (!this.HasRestart())
                        {
                            this.MarkerPosition = this.stream.Position - 2;
                            this.BadMarker = true;
                        }
                    }
                }

                temp = (temp << 8) | (ulong)(long)b;
            }

            return temp;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private int ReadStream()
        {
            int value = this.badData ? 0 : this.stream.ReadByte();
            if (value == -1)
            {
                // We've encountered the end of the file stream which means there's no EOI marker
                // in the image or the SOS marker has the wrong dimensions set.
                this.badData = true;
                this.NoData = true;
                value = 0;
            }

            return value;
        }
    }
}
