namespace ImageSharp.Formats.Jpeg.Port.Components
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Represents a HUffman Table
    /// </summary>
    internal sealed class HuffmanTable
    {
        private short[] huffcode = new short[257];
        private short[] huffsize = new short[257];
        private short[] valOffset = new short[18];
        private long[] maxcode = new long[18];

        private byte[] huffval;
        private byte[] bits;

        /// <summary>
        /// Initializes a new instance of the <see cref="HuffmanTable"/> class.
        /// </summary>
        /// <param name="lengths">The code lengths</param>
        /// <param name="values">The huffman values</param>
        public HuffmanTable(byte[] lengths, byte[] values)
        {
            this.huffval = new byte[values.Length];
            Buffer.BlockCopy(values, 0, this.huffval, 0, values.Length);
            this.bits = new byte[lengths.Length];
            Buffer.BlockCopy(lengths, 0, this.bits, 0, lengths.Length);

            this.GenerateSizeTable();
            this.GenerateCodeTable();
            this.GenerateDecoderTables();
        }

        /// <summary>
        /// Gets the Huffman value code at the given index
        /// </summary>
        /// <param name="i">The index</param>
        /// <returns>The <see cref="int"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short GetHuffVal(int i)
        {
            return this.huffval[i];
        }

        /// <summary>
        /// Gets the max code at the given index
        /// </summary>
        /// <param name="i">The index</param>
        /// <returns>The <see cref="int"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetMaxCode(int i)
        {
            return this.maxcode[i];
        }

        /// <summary>
        /// Gets the index to the locatation of the huffman value
        /// </summary>
        /// <param name="i">The index</param>
        /// <returns>The <see cref="int"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetValPtr(int i)
        {
            return this.valOffset[i];
        }

        /// <summary>
        /// Figure C.1: make table of Huffman code length for each symbol
        /// </summary>
        private void GenerateSizeTable()
        {
            short index = 0;
            for (short l = 1; l <= 16; l++)
            {
                byte i = this.bits[l];
                for (short j = 0; j < i; j++)
                {
                    this.huffsize[index] = l;
                    index++;
                }
            }

            this.huffsize[index] = 0;
        }

        /// <summary>
        /// Figure C.2: generate the codes themselves
        /// </summary>
        private void GenerateCodeTable()
        {
            short k = 0;
            short si = this.huffsize[0];
            short code = 0;
            for (short i = 0; i < this.huffsize.Length; i++)
            {
                while (this.huffsize[k] == si)
                {
                    this.huffcode[k] = code;
                    code++;
                    k++;
                }

                code <<= 1;
                si++;
            }
        }

        /// <summary>
        /// Figure F.15: generate decoding tables for bit-sequential decoding
        /// </summary>
        private void GenerateDecoderTables()
        {
            short bitcount = 0;
            for (int i = 1; i <= 16; i++)
            {
                if (this.bits[i] != 0)
                {
                    // valoffset[l] = huffval[] index of 1st symbol of code length i,
                    // minus the minimum code of length i
                    this.valOffset[i] = (short)(bitcount - this.huffcode[bitcount]);
                    bitcount += this.bits[i];
                    this.maxcode[i] = this.huffcode[bitcount - 1]; // maximum code of length i
                }
                else
                {
                    this.maxcode[i] = -1; // -1 if no codes of this length
                }
            }

            this.valOffset[17] = 0;
            this.maxcode[17] = 0xFFFFFL;
        }
    }
}