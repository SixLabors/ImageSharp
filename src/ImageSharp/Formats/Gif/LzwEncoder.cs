// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// Encodes and compresses the image data using dynamic Lempel-Ziv compression.
    /// </summary>
    /// <remarks>
    /// Adapted from Jef Poskanzer's Java port by way of J. M. G. Elliott. K Weiner 12/00
    /// <para>
    /// GIFCOMPR.C       - GIF Image compression routines
    /// </para>
    /// <para>
    /// Lempel-Ziv compression based on 'compress'.  GIF modifications by
    /// David Rowley (mgardi@watdcsu.waterloo.edu)
    /// </para>
    /// GIF Image compression - modified 'compress'
    /// <para>
    /// Based on: compress.c - File compression ala IEEE Computer, June 1984.
    /// By Authors:  Spencer W. Thomas      (decvax!harpo!utah-cs!utah-gr!thomas)
    ///              Jim McKie              (decvax!mcvax!jim)
    ///              Steve Davies           (decvax!vax135!petsd!peora!srd)
    ///              Ken Turkowski          (decvax!decwrl!turtlevax!ken)
    ///              James A. Woods         (decvax!ihnp4!ames!jaw)
    ///              Joe Orost              (decvax!vax135!petsd!joe)
    /// </para>
    /// </remarks>
    internal sealed class LzwEncoder : IDisposable
    {
        /// <summary>
        /// 80% occupancy
        /// </summary>
        private const int HashSize = 5003;

        /// <summary>
        /// The amount to shift each code.
        /// </summary>
        private const int HashShift = 4;

        /// <summary>
        /// Mask used when shifting pixel values
        /// </summary>
        private static readonly int[] Masks =
        {
            0b0,
            0b1,
            0b11,
            0b111,
            0b1111,
            0b11111,
            0b111111,
            0b1111111,
            0b11111111,
            0b111111111,
            0b1111111111,
            0b11111111111,
            0b111111111111,
            0b1111111111111,
            0b11111111111111,
            0b111111111111111,
            0b1111111111111111
        };

        /// <summary>
        /// The maximum number of bits/code.
        /// </summary>
        private const int MaxBits = 12;

        /// <summary>
        /// Should NEVER generate this code.
        /// </summary>
        private const int MaxMaxCode = 1 << MaxBits;

        /// <summary>
        /// The initial code size.
        /// </summary>
        private readonly int initialCodeSize;

        /// <summary>
        /// The hash table.
        /// </summary>
        private readonly IMemoryOwner<int> hashTable;

        /// <summary>
        /// The code table.
        /// </summary>
        private readonly IMemoryOwner<int> codeTable;

        /// <summary>
        /// Define the storage for the packet accumulator.
        /// </summary>
        private readonly byte[] accumulators = new byte[256];

        /// <summary>
        /// Number of bits/code
        /// </summary>
        private int bitCount;

        /// <summary>
        /// maximum code, given bitCount
        /// </summary>
        private int maxCode;

        /// <summary>
        /// First unused entry
        /// </summary>
        private int freeEntry;

        /// <summary>
        /// Block compression parameters -- after all codes are used up,
        /// and compression rate changes, start over.
        /// </summary>
        private bool clearFlag;

        /// <summary>
        /// Algorithm:  use open addressing double hashing (no chaining) on the
        /// prefix code / next character combination.  We do a variant of Knuth's
        /// algorithm D (vol. 3, sec. 6.4) along with G. Knott's relatively-prime
        /// secondary probe.  Here, the modular division first probe is gives way
        /// to a faster exclusive-or manipulation.  Also do block compression with
        /// an adaptive reset, whereby the code table is cleared when the compression
        /// ratio decreases, but after the table fills.  The variable-length output
        /// codes are re-sized at this point, and a special CLEAR code is generated
        /// for the decompressor.  Late addition:  construct the table according to
        /// file size for noticeable speed improvement on small files.  Please direct
        /// questions about this implementation to ames!jaw.
        /// </summary>
        private int globalInitialBits;

        /// <summary>
        /// The clear code.
        /// </summary>
        private int clearCode;

        /// <summary>
        /// The end-of-file code.
        /// </summary>
        private int eofCode;

        /// <summary>
        /// Output the given code.
        /// Inputs:
        ///      code:   A bitCount-bit integer.  If == -1, then EOF.  This assumes
        ///              that bitCount =&lt; wordsize - 1.
        /// Outputs:
        ///      Outputs code to the file.
        /// Assumptions:
        ///      Chars are 8 bits long.
        /// Algorithm:
        ///      Maintain a BITS character long buffer (so that 8 codes will
        /// fit in it exactly).  Use the VAX insv instruction to insert each
        /// code in turn.  When the buffer fills up empty it and start over.
        /// </summary>
        private int currentAccumulator;

        /// <summary>
        /// The current bits.
        /// </summary>
        private int currentBits;

        /// <summary>
        /// Number of characters so far in this 'packet'
        /// </summary>
        private int accumulatorCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="LzwEncoder"/> class.
        /// </summary>
        /// <param name="memoryAllocator">The <see cref="MemoryAllocator"/> to use for buffer allocations.</param>
        /// <param name="colorDepth">The color depth in bits.</param>
        public LzwEncoder(MemoryAllocator memoryAllocator, int colorDepth)
        {
            this.initialCodeSize = Math.Max(2, colorDepth);
            this.hashTable = memoryAllocator.Allocate<int>(HashSize, AllocationOptions.Clean);
            this.codeTable = memoryAllocator.Allocate<int>(HashSize, AllocationOptions.Clean);
        }

        /// <summary>
        /// Encodes and compresses the indexed pixels to the stream.
        /// </summary>
        /// <param name="indexedPixels">The 2D buffer of indexed pixels.</param>
        /// <param name="stream">The stream to write to.</param>
        public void Encode(Buffer2D<byte> indexedPixels, Stream stream)
        {
            // Write "initial code size" byte
            stream.WriteByte((byte)this.initialCodeSize);

            // Compress and write the pixel data
            this.Compress(indexedPixels, this.initialCodeSize + 1, stream);

            // Write block terminator
            stream.WriteByte(GifConstants.Terminator);
        }

        /// <summary>
        /// Gets the maximum code value.
        /// </summary>
        /// <param name="bitCount">The number of bits</param>
        /// <returns>See <see cref="int"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetMaxcode(int bitCount) => (1 << bitCount) - 1;

        /// <summary>
        /// Add a character to the end of the current packet, and if it is 254 characters,
        /// flush the packet to disk.
        /// </summary>
        /// <param name="c">The character to add.</param>
        /// <param name="accumulatorsRef">The reference to the storage for packet accumulators</param>
        /// <param name="stream">The stream to write to.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddCharacter(byte c, ref byte accumulatorsRef, Stream stream)
        {
            Unsafe.Add(ref accumulatorsRef, this.accumulatorCount++) = c;
            if (this.accumulatorCount >= 254)
            {
                this.FlushPacket(stream);
            }
        }

        /// <summary>
        /// Table clear for block compress.
        /// </summary>
        /// <param name="stream">The output stream.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ClearBlock(Stream stream)
        {
            this.ResetCodeTable();
            this.freeEntry = this.clearCode + 2;
            this.clearFlag = true;

            this.Output(this.clearCode, stream);
        }

        /// <summary>
        /// Reset the code table.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResetCodeTable() => this.hashTable.GetSpan().Fill(-1);

        /// <summary>
        /// Compress the packets to the stream.
        /// </summary>
        /// <param name="indexedPixels">The 2D buffer of indexed pixels.</param>
        /// <param name="initialBits">The initial bits.</param>
        /// <param name="stream">The stream to write to.</param>
        private void Compress(Buffer2D<byte> indexedPixels, int initialBits, Stream stream)
        {
            // Set up the globals: globalInitialBits - initial number of bits
            this.globalInitialBits = initialBits;

            // Set up the necessary values
            this.clearFlag = false;
            this.bitCount = this.globalInitialBits;
            this.maxCode = GetMaxcode(this.bitCount);
            this.clearCode = 1 << (initialBits - 1);
            this.eofCode = this.clearCode + 1;
            this.freeEntry = this.clearCode + 2;
            this.accumulatorCount = 0; // Clear packet

            this.ResetCodeTable(); // Clear hash table
            this.Output(this.clearCode, stream);

            ref int hashTableRef = ref MemoryMarshal.GetReference(this.hashTable.GetSpan());
            ref int codeTableRef = ref MemoryMarshal.GetReference(this.codeTable.GetSpan());

            int entry = indexedPixels[0, 0];

            for (int y = 0; y < indexedPixels.Height; y++)
            {
                ref byte rowSpanRef = ref MemoryMarshal.GetReference(indexedPixels.GetRowSpan(y));
                int offsetX = y == 0 ? 1 : 0;

                for (int x = offsetX; x < indexedPixels.Width; x++)
                {
                    int code = Unsafe.Add(ref rowSpanRef, x);
                    int freeCode = (code << MaxBits) + entry;
                    int hashIndex = (code << HashShift) ^ entry;

                    if (Unsafe.Add(ref hashTableRef, hashIndex) == freeCode)
                    {
                        entry = Unsafe.Add(ref codeTableRef, hashIndex);
                        continue;
                    }

                    // Non-empty slot
                    if (Unsafe.Add(ref hashTableRef, hashIndex) >= 0)
                    {
                        int disp = 1;
                        if (hashIndex != 0)
                        {
                            disp = HashSize - hashIndex;
                        }

                        do
                        {
                            if ((hashIndex -= disp) < 0)
                            {
                                hashIndex += HashSize;
                            }

                            if (Unsafe.Add(ref hashTableRef, hashIndex) == freeCode)
                            {
                                entry = Unsafe.Add(ref codeTableRef, hashIndex);
                                break;
                            }
                        }
                        while (Unsafe.Add(ref hashTableRef, hashIndex) >= 0);

                        if (Unsafe.Add(ref hashTableRef, hashIndex) == freeCode)
                        {
                            continue;
                        }
                    }

                    this.Output(entry, stream);
                    entry = code;
                    if (this.freeEntry < MaxMaxCode)
                    {
                        Unsafe.Add(ref codeTableRef, hashIndex) = this.freeEntry++; // code -> hashtable
                        Unsafe.Add(ref hashTableRef, hashIndex) = freeCode;
                    }
                    else
                    {
                        this.ClearBlock(stream);
                    }
                }
            }

            // Output the final code.
            this.Output(entry, stream);
            this.Output(this.eofCode, stream);
        }

        /// <summary>
        /// Flush the packet to disk and reset the accumulator.
        /// </summary>
        /// <param name="outStream">The output stream.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FlushPacket(Stream outStream)
        {
            outStream.WriteByte((byte)this.accumulatorCount);
            outStream.Write(this.accumulators, 0, this.accumulatorCount);
            this.accumulatorCount = 0;
        }

        /// <summary>
        /// Output the current code to the stream.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <param name="outs">The stream to write to.</param>
        private void Output(int code, Stream outs)
        {
            ref byte accumulatorsRef = ref MemoryMarshal.GetReference(this.accumulators.AsSpan());
            this.currentAccumulator &= Masks[this.currentBits];

            if (this.currentBits > 0)
            {
                this.currentAccumulator |= code << this.currentBits;
            }
            else
            {
                this.currentAccumulator = code;
            }

            this.currentBits += this.bitCount;

            while (this.currentBits >= 8)
            {
                this.AddCharacter((byte)(this.currentAccumulator & 0xFF), ref accumulatorsRef, outs);
                this.currentAccumulator >>= 8;
                this.currentBits -= 8;
            }

            // If the next entry is going to be too big for the code size,
            // then increase it, if possible.
            if (this.freeEntry > this.maxCode || this.clearFlag)
            {
                if (this.clearFlag)
                {
                    this.maxCode = GetMaxcode(this.bitCount = this.globalInitialBits);
                    this.clearFlag = false;
                }
                else
                {
                    ++this.bitCount;
                    this.maxCode = this.bitCount == MaxBits
                        ? MaxMaxCode
                        : GetMaxcode(this.bitCount);
                }
            }

            if (code == this.eofCode)
            {
                // At EOF, write the rest of the buffer.
                while (this.currentBits > 0)
                {
                    this.AddCharacter((byte)(this.currentAccumulator & 0xFF), ref accumulatorsRef, outs);
                    this.currentAccumulator >>= 8;
                    this.currentBits -= 8;
                }

                if (this.accumulatorCount > 0)
                {
                    this.FlushPacket(outs);
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.hashTable?.Dispose();
            this.codeTable?.Dispose();
        }
    }
}
