namespace ImageProcessor.Formats
{
    using System;
    using System.IO;

    internal class LzwEncoder
    {
        private const int EOF = -1;

        private readonly int imgW;

        private readonly int imgH;

        private readonly byte[] pixAry;

        private readonly int initCodeSize;

        // output
        //
        // Output the given code.
        // Inputs:
        //      code:   A n_bits-bit integer.  If == -1, then EOF.  This assumes
        //              that n_bits =< wordsize - 1.
        // Outputs:
        //      Outputs code to the file.
        // Assumptions:
        //      Chars are 8 bits long.
        // Algorithm:
        //      Maintain a BITS character long buffer (so that 8 codes will
        // fit in it exactly).  Use the VAX insv instruction to insert each
        // code in turn.  When the buffer fills up empty it and start over.
        private readonly int[] masks =
        {
            0x0000, // 0
            0x0001, // 1
            0x0003, // 3
            0x0007, // 7
            0x000F, // 15
            0x001F, // 31
            0x003F, // 63
            0x007F, // 127
            0x00FF, // 255
            0x01FF, // 511
            0x03FF, // 1023
            0x07FF, // 2047
            0x0FFF, // 4095
            0x1FFF, // 8191
            0x3FFF, // 16383
            0x7FFF, // 32767
            0xFFFF // 65535
        };

        // Define the storage for the packet accumulator
        private readonly byte[] accumulatorBytes = new byte[256];

        private int remaining;
        private int curPixel;

        // GIFCOMPR.C       - GIF Image compression routines
        //
        // Lempel-Ziv compression based on 'compress'.  GIF modifications by
        // David Rowley (mgardi@watdcsu.waterloo.edu)

        // General DEFINEs

        private const int Bits = 12;

        private const int HSIZE = 5003; // 80% occupancy

        // GIF Image compression - modified 'compress'
        //
        // Based on: compress.c - File compression ala IEEE Computer, June 1984.
        //
        // By Authors:  Spencer W. Thomas      (decvax!harpo!utah-cs!utah-gr!thomas)
        //              Jim McKie              (decvax!mcvax!jim)
        //              Steve Davies           (decvax!vax135!petsd!peora!srd)
        //              Ken Turkowski          (decvax!decwrl!turtlevax!ken)
        //              James A. Woods         (decvax!ihnp4!ames!jaw)
        //              Joe Orost              (decvax!vax135!petsd!joe)

        private int numberOfBits; // number of bits/code
        private int maxbits = Bits; // user settable max # bits/code
        private int maxcode; // maximum code, given n_bits
        private int maxmaxcode = 1 << Bits; // should NEVER generate this code

        private readonly int[] hashTable = new int[HSIZE];
        private readonly int[] codeTable = new int[HSIZE];

        private int hsize = HSIZE; // for dynamic table sizing

        private int freeEntry = 0; // first unused entry

        // block compression parameters -- after all codes are used up,
        // and compression rate changes, start over.
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

        private int ClearCode;
        private int EOFCode;

        private int currentAccumulator;

        private int currentBits;

        /// <summary>
        /// Number of characters so far in this 'packet'
        /// </summary>
        private int accumulatorCount;

        public LzwEncoder(int width, int height, byte[] pixels, int colorDepth)
        {
            this.imgW = width;
            this.imgH = height;
            this.pixAry = pixels;
            this.initCodeSize = Math.Max(2, colorDepth);
        }

        public void Encode(Stream stream)
        {
            stream.WriteByte((byte)this.initCodeSize); // write "initial code size" byte

            this.remaining = this.imgW * this.imgH; // reset navigation variables
            this.curPixel = 0;

            this.Compress(this.initCodeSize + 1, stream); // compress and write the pixel data

            stream.WriteByte(0); // write block terminator
        }

        // Add a character to the end of the current packet, and if it is 254
        // characters, flush the packet to disk.
        private void CharOut(byte character, Stream stream)
        {
            this.accumulatorBytes[this.accumulatorCount++] = character;
            if (this.accumulatorCount >= 254)
            {
                this.FlushChar(stream);
            }
        }

        // Clear out the hash table

        // table clear for block compress
        private void ClearBlock(Stream outs)
        {
            this.ClearHashTable(this.hsize);
            this.freeEntry = this.ClearCode + 2;
            this.clearFlag = true;

            this.Output(this.ClearCode, outs);
        }

        // reset code table
        private void ClearHashTable(int hsize)
        {
            for (int i = 0; i < hsize; ++i)
            {
                this.hashTable[i] = -1;
            }
        }

        private void Compress(int init_bits, Stream outs)
        {
            int fcode;
            int c;
            int ent;
            int disp;
            int hsize_reg;
            int hshift;

            // Set up the globals:  globalInitialBits - initial number of bits
            this.globalInitialBits = init_bits;

            // Set up the necessary values
            this.clearFlag = false;
            this.numberOfBits = this.globalInitialBits;
            this.maxcode = this.MAXCODE(this.numberOfBits);

            this.ClearCode = 1 << (init_bits - 1);
            this.EOFCode = this.ClearCode + 1;
            this.freeEntry = this.ClearCode + 2;

            this.accumulatorCount = 0; // clear packet

            ent = this.NextPixel();

            hshift = 0;
            for (fcode = this.hsize; fcode < 65536; fcode *= 2)
            {
                ++hshift;
            }

            hshift = 8 - hshift; // set hash code range bound

            hsize_reg = this.hsize;
            this.ClearHashTable(hsize_reg); // clear hash table

            this.Output(this.ClearCode, outs);

            // TODO: Refactor this. Goto is baaaaaaad!
            //  outer_loop:
            while ((c = this.NextPixel()) != EOF)
            {
                fcode = (c << this.maxbits) + ent;
                int i = c << hshift ^ ent;

                if (this.hashTable[i] == fcode)
                {
                    ent = this.codeTable[i];
                    continue;
                }

                // non-empty slot
                if (this.hashTable[i] >= 0)
                {
                    disp = hsize_reg - i; // secondary hash (after G. Knott)
                    if (i == 0)
                    {
                        disp = 1;
                    }

                    do
                    {
                        if ((i -= disp) < 0)
                        {
                            i += hsize_reg;
                        }

                        if (this.hashTable[i] == fcode)
                        {
                            ent = this.codeTable[i];
                            // goto outer_loop;
                            break;
                        }
                    }
                    while (this.hashTable[i] >= 0);
                }

                this.Output(ent, outs);
                ent = c;

                if (this.freeEntry < this.maxmaxcode)
                {
                    this.codeTable[i] = this.freeEntry++; // code -> hashtable
                    this.hashTable[i] = fcode;
                }
                else
                {
                    this.ClearBlock(outs);
                }
            }

            // Put out the final code.
            this.Output(ent, outs);
            this.Output(this.EOFCode, outs);
        }

        // Flush the packet to disk, and reset the accumulator
        private void FlushChar(Stream stream)
        {
            if (this.accumulatorCount > 0)
            {
                stream.WriteByte((byte)this.accumulatorCount);
                stream.Write(this.accumulatorBytes, 0, this.accumulatorCount);
                this.accumulatorCount = 0;
            }
        }

        private int MAXCODE(int bits)
        {
            return (1 << bits) - 1;
        }

        //----------------------------------------------------------------------------
        // Return the next pixel from the image
        //----------------------------------------------------------------------------
        private int NextPixel()
        {
            if (this.remaining == 0)
            {
                return EOF;
            }

            --this.remaining;

            byte pix = this.pixAry[this.curPixel++];

            return pix & 0xff;
        }

        private void Output(int code, Stream outs)
        {
            this.currentAccumulator &= this.masks[this.currentBits];

            if (this.currentBits > 0)
            {
                this.currentAccumulator |= code << this.currentBits;
            }
            else
            {
                this.currentAccumulator = code;
            }

            this.currentBits += this.numberOfBits;

            while (this.currentBits >= 8)
            {
                this.CharOut((byte)(this.currentAccumulator & 0xff), outs);
                this.currentAccumulator >>= 8;
                this.currentBits -= 8;
            }

            // If the next entry is going to be too big for the code size,
            // then increase it, if possible.
            if (this.freeEntry > this.maxcode || this.clearFlag)
            {
                if (this.clearFlag)
                {
                    this.maxcode = this.MAXCODE(this.numberOfBits = this.globalInitialBits);
                    this.clearFlag = false;
                }
                else
                {
                    ++this.numberOfBits;
                    this.maxcode = this.numberOfBits == this.maxbits
                        ? this.maxmaxcode
                        : this.MAXCODE(this.numberOfBits);
                }
            }

            if (code == this.EOFCode)
            {
                // At EOF, write the rest of the buffer.
                while (this.currentBits > 0)
                {
                    this.CharOut((byte)(this.currentAccumulator & 0xff), outs);
                    this.currentAccumulator >>= 8;
                    this.currentBits -= 8;
                }

                this.FlushChar(outs);
            }
        }
    }
}
