// <copyright file="LzwEncoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    using System;
    using System.IO;

    /// <summary>
    /// Encodes and compresses the image data using dynamic Lempel-Ziv compression.
    /// </summary>
    /// <remarks>
    /// Adapted from Jef Poskanzer's Java port by way of J. M. G. Elliott. K Weiner 12/00
    /// <para>
    /// GIFCOMPR.C       - GIF Image compression routines
    ///
    /// Lempel-Ziv compression based on 'compress'.  GIF modifications by
    /// David Rowley (mgardi@watdcsu.waterloo.edu)
    /// </para>
    /// <para>
    /// GIF Image compression - modified 'compress'
    ///
    /// Based on: compress.c - File compression ala IEEE Computer, June 1984.
    ///
    /// By Authors:  Spencer W. Thomas      (decvax!harpo!utah-cs!utah-gr!thomas)
    ///              Jim McKie              (decvax!mcvax!jim)
    ///              Steve Davies           (decvax!vax135!petsd!peora!srd)
    ///              Ken Turkowski          (decvax!decwrl!turtlevax!ken)
    ///              James A. Woods         (decvax!ihnp4!ames!jaw)
    ///              Joe Orost              (decvax!vax135!petsd!joe)
    /// </para>
    /// </remarks>
    internal sealed class LzwEncoder
    {
        private const int Eof = -1;

        private const int Bits = 12;

        private const int HashSize = 5003; // 80% occupancy

        private readonly byte[] pixelArray;

        private readonly int initialCodeSize;

        private int curPixel;

        /// <summary>
        /// Number of bits/code
        /// </summary>
        private int bitCount;

        /// <summary>
        /// User settable max # bits/code
        /// </summary>
        private int maxbits = Bits;

        private int maxcode; // maximum code, given bitCount

        private int maxmaxcode = 1 << Bits; // should NEVER generate this code

        private readonly int[] hashTable = new int[HashSize];

        private readonly int[] codeTable = new int[HashSize];

        /// <summary>
        /// For dynamic table sizing
        /// </summary>
        private int hsize = HashSize;

        /// <summary>
        /// First unused entry
        /// </summary>
        private int freeEntry;

        /// <summary>
        /// Block compression parameters -- after all codes are used up, 
        /// and compression rate changes, start over.
        /// </summary>
        private bool clearFlag;

        // Algorithm:  use open addressing double hashing (no chaining) on the
        // prefix code / next character combination.  We do a variant of Knuth's
        // algorithm D (vol. 3, sec. 6.4) along with G. Knott's relatively-prime
        // secondary probe.  Here, the modular division first probe is gives way
        // to a faster exclusive-or manipulation.  Also do block compression with
        // an adaptive reset, whereby the code table is cleared when the compression
        // ratio decreases, but after the table fills.  The variable-length output
        // codes are re-sized at this point, and a special CLEAR code is generated
        // for the decompressor.  Late addition:  construct the table according to
        // file size for noticeable speed improvement on small files.  Please direct
        // questions about this implementation to ames!jaw.

        private int globalInitialBits;

        private int clearCode;

        private int eofCode;

        // output
        //
        // Output the given code.
        // Inputs:
        //      code:   A bitCount-bit integer.  If == -1, then EOF.  This assumes
        //              that bitCount =< wordsize - 1.
        // Outputs:
        //      Outputs code to the file.
        // Assumptions:
        //      Chars are 8 bits long.
        // Algorithm:
        //      Maintain a BITS character long buffer (so that 8 codes will
        // fit in it exactly).  Use the VAX insv instruction to insert each
        // code in turn.  When the buffer fills up empty it and start over.

        private int currentAccumulator;

        private int currentBits;

        private readonly int[] masks =
            {
                0x0000, 0x0001, 0x0003, 0x0007, 0x000F, 0x001F, 0x003F, 0x007F, 0x00FF,
                0x01FF, 0x03FF, 0x07FF, 0x0FFF, 0x1FFF, 0x3FFF, 0x7FFF, 0xFFFF
            };

        /// <summary>
        /// Number of characters so far in this 'packet'
        /// </summary>
        private int accumulatorCount;

        /// <summary>
        /// Define the storage for the packet accumulator.
        /// </summary>
        private readonly byte[] accumulators = new byte[256];

        /// <summary>
        /// Initializes a new instance of the <see cref="LzwEncoder"/> class.
        /// </summary>
        /// <param name="indexedPixels">The array of indexed pixels.</param>
        /// <param name="colorDepth">The color depth in bits.</param>
        public LzwEncoder(byte[] indexedPixels, int colorDepth)
        {
            this.pixelArray = indexedPixels;
            this.initialCodeSize = Math.Max(2, colorDepth);
        }

        /// <summary>
        /// Encodes and compresses the indexed pixels to the stream.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
        public void Encode(Stream stream)
        {
            // Write "initial code size" byte
            stream.WriteByte((byte)this.initialCodeSize);

            this.curPixel = 0;

            // Compress and write the pixel data
            this.Compress(this.initialCodeSize + 1, stream);

            // Write block terminator
            stream.WriteByte(GifConstants.Terminator);
        }

        /// <summary>
        /// Gets the maximum code value
        /// </summary>
        /// <param name="bitCount">The number of bits</param>
        /// <returns>See <see cref="int"/></returns>
        private static int GetMaxcode(int bitCount)
        {
            return (1 << bitCount) - 1;
        }

        /// <summary>
        /// Add a character to the end of the current packet, and if it is 254 characters, 
        /// flush the packet to disk.
        /// </summary>
        /// <param name="c">The character to add.</param>
        /// <param name="stream">The stream to write to.</param>
        private void AddCharacter(byte c, Stream stream)
        {
            this.accumulators[this.accumulatorCount++] = c;
            if (this.accumulatorCount >= 254)
            {
                this.FlushPacket(stream);
            }
        }

        /// <summary>
        ///  Table clear for block compress
        /// </summary>
        /// <param name="stream">The output stream.</param>
        private void ClearBlock(Stream stream)
        {
            this.ResetCodeTable(this.hsize);
            this.freeEntry = this.clearCode + 2;
            this.clearFlag = true;

            this.Output(this.clearCode, stream);
        }

        /// <summary>
        /// Reset the code table.
        /// </summary>
        /// <param name="size">The hash size.</param>
        private void ResetCodeTable(int size)
        {
            for (int i = 0; i < size; ++i)
            {
                this.hashTable[i] = -1;
            }
        }

        /// <summary>
        /// Compress the packets to the stream.
        /// </summary>
        /// <param name="intialBits">The inital bits.</param>
        /// <param name="stream">The stream to write to.</param>
        private void Compress(int intialBits, Stream stream)
        {
            int fcode;
            int c;
            int ent;
            int hsizeReg;
            int hshift;

            // Set up the globals:  globalInitialBits - initial number of bits
            this.globalInitialBits = intialBits;

            // Set up the necessary values
            this.clearFlag = false;
            this.bitCount = this.globalInitialBits;
            this.maxcode = GetMaxcode(this.bitCount);

            this.clearCode = 1 << (intialBits - 1);
            this.eofCode = this.clearCode + 1;
            this.freeEntry = this.clearCode + 2;

            this.accumulatorCount = 0; // clear packet

            ent = this.NextPixel();

            hshift = 0;
            for (fcode = this.hsize; fcode < 65536; fcode *= 2) { ++hshift; }
            hshift = 8 - hshift; // set hash code range bound

            hsizeReg = this.hsize;

            this.ResetCodeTable(hsizeReg); // clear hash table

            this.Output(this.clearCode, stream);

            while ((c = this.NextPixel()) != Eof)
            {
                fcode = (c << this.maxbits) + ent;
                int i = (c << hshift) ^ ent /* = 0 */;

                if (this.hashTable[i] == fcode)
                {
                    ent = this.codeTable[i];
                    continue;
                }

                // Non-empty slot
                if (this.hashTable[i] >= 0) 
                {
                    int disp = hsizeReg - i;
                    if (i == 0) disp = 1;
                    do
                    {
                        if ((i -= disp) < 0) { i += hsizeReg; }

                        if (this.hashTable[i] == fcode)
                        {
                            ent = this.codeTable[i];
                            break;
                        }
                    }
                    while (this.hashTable[i] >= 0);

                    if (this.hashTable[i] == fcode) { continue; }
                }

                this.Output(ent, stream);
                ent = c;
                if (this.freeEntry < this.maxmaxcode)
                {
                    this.codeTable[i] = this.freeEntry++; // code -> hashtable
                    this.hashTable[i] = fcode;
                }
                else this.ClearBlock(stream);
            }

            // Put out the final code.
            this.Output(ent, stream);

            this.Output(this.eofCode, stream);
        }

        // Flush the packet to disk, and reset the accumulator
        private void FlushPacket(Stream outs)
        {
            if (this.accumulatorCount > 0)
            {
                outs.WriteByte((byte)this.accumulatorCount);
                outs.Write(this.accumulators, 0, this.accumulatorCount);
                this.accumulatorCount = 0;
            }
        }

        /// <summary>
        /// Return the next pixel from the image
        /// </summary>
        /// <returns>
        /// The <see cref="int"/>
        /// </returns>
        private int NextPixel()
        {
            if (this.curPixel == this.pixelArray.Length)
            {
                return Eof;
            }

            if (this.curPixel == this.pixelArray.Length)
                return Eof;

            this.curPixel++;
            return this.pixelArray[this.curPixel - 1] & 0xff;
        }

        /// <summary>
        /// Output the current code to the stream.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <param name="outs">The stream to write to.</param>
        private void Output(int code, Stream outs)
        {
            this.currentAccumulator &= this.masks[this.currentBits];

            if (this.currentBits > 0) this.currentAccumulator |= (code << this.currentBits);
            else this.currentAccumulator = code;

            this.currentBits += this.bitCount;

            while (this.currentBits >= 8)
            {
                this.AddCharacter((byte)(this.currentAccumulator & 0xff), outs);
                this.currentAccumulator >>= 8;
                this.currentBits -= 8;
            }

            // If the next entry is going to be too big for the code size,
            // then increase it, if possible.
            if (this.freeEntry > this.maxcode || this.clearFlag)
            {
                if (this.clearFlag)
                {
                    this.maxcode = GetMaxcode(this.bitCount = this.globalInitialBits);
                    this.clearFlag = false;
                }
                else
                {
                    ++this.bitCount;
                    this.maxcode = this.bitCount == this.maxbits
                        ? this.maxmaxcode
                        : GetMaxcode(this.bitCount);
                }
            }

            if (code == this.eofCode)
            {
                // At EOF, write the rest of the buffer.
                while (this.currentBits > 0)
                {
                    this.AddCharacter((byte)(this.currentAccumulator & 0xff), outs);
                    this.currentAccumulator >>= 8;
                    this.currentBits -= 8;
                }

                this.FlushPacket(outs);
            }
        }
    }
}