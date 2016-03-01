

namespace ImageProcessorCore.Formats
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using ImageProcessorCore.IO;

    internal class HuffmanTable
    {
        public static int HUFFMAN_MAX_TABLES = 4;

        private short[] huffcode = new short[256];
        private short[] huffsize = new short[256];
        private short[] valptr = new short[16];
        private short[] mincode = { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 };
        private short[] maxcode = { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 };

        private short[] huffval;
        private short[] bits;

        int bufferPutBits;

        int bufferPutBuffer;

        internal int ImageHeight;
        internal int ImageWidth;
        internal int[,] DC_matrix0;
        internal int[,] AC_matrix0;
        internal int[,] DC_matrix1;
        internal int[,] AC_matrix1;
        internal int[][,] DC_matrix;
        internal int[][,] AC_matrix;
        internal int NumOfDCTables;
        internal int NumOfACTables;

        public List<short[]> bitsList;
        public List<short[]> val;


        public static byte JPEG_DC_TABLE = 0;
        public static byte JPEG_AC_TABLE = 1;

        private short lastk = 0;

        internal HuffmanTable(JpegHuffmanTable table)
        {
            if (table != null)
            {
                this.huffval = table.Values;
                this.bits = table.Codes;

                this.GenerateSizeTable();
                this.GenerateCodeTable();
                this.GenerateDecoderTables();
            }
            else
            {
                // Encode initialization
                this.bitsList = new List<short[]>
                {
                    JpegHuffmanTable.StandardDcLuminance.Codes,
                    JpegHuffmanTable.StandardAcLuminance.Codes,
                    JpegHuffmanTable.StandardDcChrominance.Codes,
                    JpegHuffmanTable.StandardAcChrominance.Codes
                };

                this.val = new List<short[]>
                {
                    JpegHuffmanTable.StandardDcLuminance.Values,
                    JpegHuffmanTable.StandardAcLuminance.Values,
                    JpegHuffmanTable.StandardDcChrominance.Values,
                    JpegHuffmanTable.StandardAcChrominance.Values
                };

                this.initHuf();
            }
        }

        /// <summary>See Figure C.1</summary>
        private void GenerateSizeTable()
        {
            short index = 0;
            for (short i = 0; i < this.bits.Length; i++)
            {
                for (short j = 0; j < this.bits[i]; j++)
                {
                    this.huffsize[index] = (short)(i + 1);
                    index++;
                }
            }

            this.lastk = index;
        }

        /// <summary>See Figure C.2</summary>
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

        /// <summary>See figure F.15</summary>
        private void GenerateDecoderTables()
        {
            short bitcount = 0;
            for (int i = 0; i < 16; i++)
            {
                if (this.bits[i] != 0)
                {
                    this.valptr[i] = bitcount;
                }

                for (int j = 0; j < this.bits[i]; j++)
                {
                    if (this.huffcode[j + bitcount] < this.mincode[i] || this.mincode[i] == -1)
                    {
                        this.mincode[i] = this.huffcode[j + bitcount];
                    }

                    if (this.huffcode[j + bitcount] > this.maxcode[i])
                    {
                        this.maxcode[i] = this.huffcode[j + bitcount];
                    }
                }

                if (this.mincode[i] != -1)
                {
                    this.valptr[i] = (short)(this.valptr[i] - this.mincode[i]);
                }

                bitcount += this.bits[i];
            }
        }

        /// <summary>Figure F.12</summary>
        public static int Extend(int diff, int t)
        {
            // here we use bitshift to implement 2^ ... 
            // NOTE: Math.Pow returns 0 for negative powers, which occassionally happen here!
            int Vt = 1 << t - 1;

            // WAS: int Vt = (int)Math.Pow(2, (t - 1));
            if (diff < Vt)
            {
                Vt = (-1 << t) + 1;
                diff = diff + Vt;
            }

            return diff;
        }

        /// <summary>
        /// Figure F.16 - Reads the huffman code bit-by-bit.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="zigzag">The zigzag.</param>
        /// <param name="prec">The prec.</param>
        /// <param name="DCcode">The D Ccode.</param>
        /// <param name="ACcode">The A Ccode.</param>
        /// <summary>
        /// HuffmanBlockEncoder run length encodes and Huffman encodes the quantized data.
        /// </summary>
        internal void HuffmanBlockEncoder(Stream writer, int[] zigzag, int prec, int DCcode, int ACcode)
        {
            int temp, temp2, nbits, k, r, i;

            this.NumOfDCTables = 2;
            this.NumOfACTables = 2;

            // The DC portion
            temp = temp2 = zigzag[0] - prec;
            if (temp < 0)
            {
                temp = -temp;
                temp2--;
            }

            nbits = 0;
            while (temp != 0)
            {
                nbits++;
                temp >>= 1;
            }

            // TODO: Why would this happen?
            //if (nbits > 11) nbits = 11;

            this.bufferIt(writer, this.DC_matrix[DCcode][nbits, 0], this.DC_matrix[DCcode][nbits, 1]);

            // The arguments in bufferIt are code and size.
            if (nbits != 0)
            {
                this.bufferIt(writer, temp2, nbits);
            }

            // The AC portion
            r = 0;

            for (k = 1; k < 64; k++)
            {
                if ((temp = zigzag[ZigZag.ZigZagMap[k]]) == 0)
                {
                    r++;
                }
                else
                {
                    while (r > 15)
                    {
                        this.bufferIt(writer, this.AC_matrix[ACcode][0xF0, 0], this.AC_matrix[ACcode][0xF0, 1]);

                        r -= 16;
                    }

                    temp2 = temp;
                    if (temp < 0)
                    {
                        temp = -temp;
                        temp2--;
                    }

                    nbits = 1;

                    while ((temp >>= 1) != 0)
                    {
                        nbits++;
                    }

                    i = (r << 4) + nbits;
                    this.bufferIt(writer, this.AC_matrix[ACcode][i, 0], this.AC_matrix[ACcode][i, 1]);
                    this.bufferIt(writer, temp2, nbits);

                    r = 0;
                }
            }

            if (r > 0)
            {
                this.bufferIt(writer, this.AC_matrix[ACcode][0, 0], this.AC_matrix[ACcode][0, 1]);
            }
        }

        /// <summary>
        /// Uses an integer long (32 bits) buffer to store the Huffman encoded bits
        /// and sends them to outStream by the byte.
        /// </summary>
        private void bufferIt(Stream writer, int code, int size)
        {
            int PutBuffer = code;
            int PutBits = this.bufferPutBits;

            PutBuffer &= (1 << size) - 1;
            PutBits += size;
            PutBuffer <<= 24 - PutBits;
            PutBuffer |= this.bufferPutBuffer;

            while (PutBits >= 8)
            {
                int c = (PutBuffer >> 16) & 0xFF;
                writer.WriteByte((byte)c);

                // FF must be escaped
                if (c == 0xFF)
                {
                    writer.WriteByte(0);
                }

                PutBuffer <<= 8;
                PutBits -= 8;
            }

            this.bufferPutBuffer = PutBuffer;
            this.bufferPutBits = PutBits;

        }

        public void FlushBuffer(Stream writer)
        {
            int PutBuffer = this.bufferPutBuffer;
            int PutBits = this.bufferPutBits;
            while (PutBits >= 8)
            {
                int c = (PutBuffer >> 16) & 0xFF;
                writer.WriteByte((byte)c);

                // FF must be escaped
                if (c == 0xFF) writer.WriteByte(0);

                PutBuffer <<= 8;
                PutBits -= 8;
            }

            if (PutBits > 0)
            {
                int c = (PutBuffer >> 16) & 0xFF;
                writer.WriteByte((byte)c);
            }
        }

        /// <summary>
        /// Initialisation of the Huffman codes for Luminance and Chrominance.
        /// This code results in the same tables created in the IJG Jpeg-6a
        /// library.
        /// </summary>
        public void initHuf()
        {
            this.DC_matrix0 = new int[12, 2];
            this.DC_matrix1 = new int[12, 2];
            this.AC_matrix0 = new int[255, 2];
            this.AC_matrix1 = new int[255, 2];
            this.DC_matrix = new int[2][,];
            this.AC_matrix = new int[2][,];
            int p, l, i, lastp, si, code;
            int[] huffsize = new int[257];
            int[] huffcode = new int[257];

            // TODO: Optimize this, no need for two calls.
            short[] bitsDCchrominance = JpegHuffmanTable.StandardDcChrominance.Codes;
            short[] bitsACchrominance = JpegHuffmanTable.StandardAcChrominance.Codes;
            short[] bitsDCluminance = JpegHuffmanTable.StandardDcLuminance.Codes;
            short[] bitsACluminance = JpegHuffmanTable.StandardAcLuminance.Codes;


            short[] valDCchrominance = JpegHuffmanTable.StandardDcChrominance.Values;
            short[] valACchrominance = JpegHuffmanTable.StandardAcChrominance.Values;
            short[] valDCluminance = JpegHuffmanTable.StandardDcLuminance.Values;
            short[] valACluminance = JpegHuffmanTable.StandardAcLuminance.Values;


            /*
            * init of the DC values for the chrominance
            * [,0] is the code   [,1] is the number of bit
            */
            p = 0;
            for (l = 0; l < 16; l++)
            {
                for (i = 1; i <= bitsDCchrominance[l]; i++)
                {
                    huffsize[p++] = l + 1;
                }
            }

            huffsize[p] = 0;
            lastp = p;

            code = 0;
            si = huffsize[0];
            p = 0;
            while (huffsize[p] != 0)
            {
                while (huffsize[p] == si)
                {
                    huffcode[p++] = code;
                    code++;
                }

                code <<= 1;
                si++;
            }

            for (p = 0; p < lastp; p++)
            {
                this.DC_matrix1[valDCchrominance[p], 0] = huffcode[p];
                this.DC_matrix1[valDCchrominance[p], 1] = huffsize[p];
            }

            /*
            * Init of the AC hufmann code for the chrominance
            * matrix [,,0] is the code & matrix[,,1] is the number of bit needed
            */
            p = 0;
            for (l = 0; l < 16; l++)
            {
                for (i = 1; i <= bitsACchrominance[l]; i++)
                {
                    huffsize[p++] = l + 1;
                }
            }

            huffsize[p] = 0;
            lastp = p;

            code = 0;
            si = huffsize[0];
            p = 0;
            while (huffsize[p] != 0)
            {
                while (huffsize[p] == si)
                {
                    huffcode[p++] = code;
                    code++;
                }

                code <<= 1;
                si++;
            }

            for (p = 0; p < lastp; p++)
            {
                this.AC_matrix1[valACchrominance[p], 0] = huffcode[p];
                this.AC_matrix1[valACchrominance[p], 1] = huffsize[p];
            }

            /*
            * init of the DC values for the luminance
            * [,0] is the code   [,1] is the number of bit
            */
            p = 0;
            for (l = 0; l < 16; l++)
            {
                for (i = 1; i <= bitsDCluminance[l]; i++)
                {
                    huffsize[p++] = l + 1;
                }
            }

            huffsize[p] = 0;
            lastp = p;

            code = 0;
            si = huffsize[0];
            p = 0;
            while (huffsize[p] != 0)
            {
                while (huffsize[p] == si)
                {
                    huffcode[p++] = code;
                    code++;
                }

                code <<= 1;
                si++;
            }

            for (p = 0; p < lastp; p++)
            {
                this.DC_matrix0[valDCluminance[p], 0] = huffcode[p];
                this.DC_matrix0[valDCluminance[p], 1] = huffsize[p];
            }

            /*
            * Init of the AC hufmann code for luminance
            * matrix [,,0] is the code & matrix[,,1] is the number of bit
            */
            p = 0;
            for (l = 0; l < 16; l++)
            {
                for (i = 1; i <= bitsACluminance[l]; i++)
                {
                    huffsize[p++] = l + 1;
                }
            }

            huffsize[p] = 0;
            lastp = p;

            code = 0;
            si = huffsize[0];
            p = 0;
            while (huffsize[p] != 0)
            {
                while (huffsize[p] == si)
                {
                    huffcode[p++] = code;
                    code++;
                }

                code <<= 1;
                si++;
            }

            for (int q = 0; q < lastp; q++)
            {
                this.AC_matrix0[valACluminance[q], 0] = huffcode[q];
                this.AC_matrix0[valACluminance[q], 1] = huffsize[q];
            }

            this.DC_matrix[0] = this.DC_matrix0;
            this.DC_matrix[1] = this.DC_matrix1;
            this.AC_matrix[0] = this.AC_matrix0;
            this.AC_matrix[1] = this.AC_matrix1;
        }
    }
}
