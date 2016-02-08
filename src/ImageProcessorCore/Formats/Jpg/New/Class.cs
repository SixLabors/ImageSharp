

namespace ImageProcessorCore.Formats
{
    using System.Collections.Generic;

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

        int bufferPutBits, bufferPutBuffer;
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
                huffval = table.Values;
                bits = table.Codes;

                GenerateSizeTable();
                GenerateCodeTable();
                GenerateDecoderTables();
            }
            else
            {
                // Encode initialization

                bitsList = new List<short[]>();
                bitsList.Add(JpegHuffmanTable.StandardDcLuminance.Codes);
                bitsList.Add(JpegHuffmanTable.StandardAcLuminance.Codes);
                bitsList.Add(JpegHuffmanTable.StandardDcChrominance.Codes);
                bitsList.Add(JpegHuffmanTable.StandardAcChrominance.Codes);

                val = new List<short[]>();
                val.Add(JpegHuffmanTable.StandardDcLuminance.Values);
                val.Add(JpegHuffmanTable.StandardAcLuminance.Values);
                val.Add(JpegHuffmanTable.StandardDcChrominance.Values);
                val.Add(JpegHuffmanTable.StandardAcChrominance.Values);

                initHuf();
            }
        }

        /// <summary>See Figure C.1</summary>
        private void GenerateSizeTable()
        {
            short index = 0;
            for (short i = 0; i < bits.Length; i++)
            {
                for (short j = 0; j < bits[i]; j++)
                {
                    huffsize[index] = (short)(i + 1);
                    index++;
                }
            }
            lastk = index;
        }

        /// <summary>See Figure C.2</summary>
        private void GenerateCodeTable()
        {
            short k = 0;
            short si = huffsize[0];
            short code = 0;
            for (short i = 0; i < huffsize.Length; i++)
            {
                while (huffsize[k] == si)
                {
                    huffcode[k] = code;
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
                if (bits[i] != 0)
                    valptr[i] = bitcount;
                for (int j = 0; j < bits[i]; j++)
                {
                    if (huffcode[j + bitcount] < mincode[i] || mincode[i] == -1)
                        mincode[i] = huffcode[j + bitcount];

                    if (huffcode[j + bitcount] > maxcode[i])
                        maxcode[i] = huffcode[j + bitcount];
                }
                if (mincode[i] != -1)
                    valptr[i] = (short)(valptr[i] - mincode[i]);
                bitcount += bits[i];
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

        /// <summary>Figure F.16 - Reads the huffman code bit-by-bit.</summary>
        //public int Decode(JPEGBinaryReader JPEGStream)
        //{
        //    int i = 0;
        //    short code = (short)JPEGStream.ReadBits(1);
        //    while (code > maxcode[i])
        //    {
        //        i++;
        //        code <<= 1;
        //        code |= (short)JPEGStream.ReadBits(1);
        //    }
        //    int val = huffval[code + (valptr[i])];
        //    if (val < 0)
        //        val = 256 + val;
        //    return val;
        //}

        /// <summary>
        /// HuffmanBlockEncoder run length encodes and Huffman encodes the quantized data.
        /// </summary>
        internal void HuffmanBlockEncoder(EndianBinaryWriter outStream, int[] zigzag, int prec, int DCcode, int ACcode)
        {
            int temp, temp2, nbits, k, r, i;

            NumOfDCTables = 2;
            NumOfACTables = 2;

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
            if (nbits > 11) nbits = 11;


            bufferIt(outStream,
                DC_matrix[DCcode][nbits, 0],
                DC_matrix[DCcode][nbits, 1]);

            // The arguments in bufferIt are code and size.
            if (nbits != 0)
            {
                bufferIt(outStream, temp2, nbits);
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
                        bufferIt(outStream,
                            AC_matrix[ACcode][0xF0, 0],
                            AC_matrix[ACcode][0xF0, 1]);

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
                    bufferIt(outStream,
                        AC_matrix[ACcode][i, 0],
                        AC_matrix[ACcode][i, 1]);
                    bufferIt(outStream, temp2, nbits);

                    r = 0;
                }
            }

            if (r > 0)
            {
                bufferIt(outStream,
                    AC_matrix[ACcode][0, 0],
                    AC_matrix[ACcode][0, 1]);
            }
        }

        /// <summary>
        /// Uses an integer long (32 bits) buffer to store the Huffman encoded bits
        /// and sends them to outStream by the byte.
        /// </summary>
        void bufferIt(EndianBinaryWriter outStream, int code, int size)
        {
            int PutBuffer = code;
            int PutBits = bufferPutBits;

            PutBuffer &= (1 << size) - 1;
            PutBits += size;
            PutBuffer <<= 24 - PutBits;
            PutBuffer |= bufferPutBuffer;

            while (PutBits >= 8)
            {
                int c = ((PutBuffer >> 16) & 0xFF);
                outStream.Write((byte)c);

                // FF must be escaped
                if (c == 0xFF) outStream.Write(0);

                PutBuffer <<= 8;
                PutBits -= 8;
            }
            bufferPutBuffer = PutBuffer;
            bufferPutBits = PutBits;

        }

        public void FlushBuffer(EndianBinaryWriter outStream)
        {
            int PutBuffer = bufferPutBuffer;
            int PutBits = bufferPutBits;
            while (PutBits >= 8)
            {
                int c = ((PutBuffer >> 16) & 0xFF);
                outStream.Write((byte)c);

                // FF must be escaped
                if (c == 0xFF) outStream.Write(0);

                PutBuffer <<= 8;
                PutBits -= 8;
            }
            if (PutBits > 0)
            {
                int c = ((PutBuffer >> 16) & 0xFF);
                outStream.Write((byte)c);
            }
        }


        /// <summary>
        /// Initialisation of the Huffman codes for Luminance and Chrominance.
        /// This code results in the same tables created in the IJG Jpeg-6a
        /// library.
        /// </summary>
        public void initHuf()
        {
            DC_matrix0 = new int[12, 2];
            DC_matrix1 = new int[12, 2];
            AC_matrix0 = new int[255, 2];
            AC_matrix1 = new int[255, 2];
            DC_matrix = new int[2][,];
            AC_matrix = new int[2][,];
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
                DC_matrix1[valDCchrominance[p], 0] = huffcode[p];
                DC_matrix1[valDCchrominance[p], 1] = huffsize[p];
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
                AC_matrix1[valACchrominance[p], 0] = huffcode[p];
                AC_matrix1[valACchrominance[p], 1] = huffsize[p];
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
                DC_matrix0[valDCluminance[p], 0] = huffcode[p];
                DC_matrix0[valDCluminance[p], 1] = huffsize[p];
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
                AC_matrix0[valACluminance[q], 0] = huffcode[q];
                AC_matrix0[valACluminance[q], 1] = huffsize[q];
            }

            DC_matrix[0] = DC_matrix0;
            DC_matrix[1] = DC_matrix1;
            AC_matrix[0] = AC_matrix0;
            AC_matrix[1] = AC_matrix1;
        }


    }
}
