// <copyright file="JpegDecoderCore.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// Performs the jpeg decoding operation.
    /// </summary>
    internal class JpegDecoderCore
    {
        /// <summary>
        /// The maximum (inclusive) number of bits in a Huffman code.
        /// </summary>
        private const int MaxCodeLength = 16;

        /// <summary>
        /// The maximum (inclusive) number of codes in a Huffman tree.
        /// </summary>
        private const int MaxNCodes = 256;

        /// <summary>
        /// The log-2 size of the Huffman decoder's look-up table.
        /// </summary>
        private const int LutSize = 8;

        /// <summary>
        /// The maximum number of color components
        /// </summary>
        private const int MaxComponents = 4;

        private const int maxTc = 1;

        private const int maxTh = 3;

        private const int maxTq = 3;

        private const int dcTable = 0;

        private const int acTable = 1;

        /// <summary>
        /// Unzig maps from the zigzag ordering to the natural ordering. For example,
        /// unzig[3] is the column and row of the fourth element in zigzag order. The
        /// value is 16, which means first column (16%8 == 0) and third row (16/8 == 2).
        /// </summary>
        private static readonly int[] Unzig =
        {
            0, 1, 8, 16, 9, 2, 3, 10, 17, 24, 32, 25, 18, 11, 4, 5, 12, 19, 26,
            33, 40, 48, 41, 34, 27, 20, 13, 6, 7, 14, 21, 28, 35, 42, 49, 56, 57,
            50, 43, 36, 29, 22, 15, 23, 30, 37, 44, 51, 58, 59, 52, 45, 38, 31,
            39, 46, 53, 60, 61, 54, 47, 55, 62, 63,
        };

        /// <summary>
        /// The component array
        /// </summary>
        private readonly Component[] componentArray;

        /// <summary>
        /// Saved state between progressive-mode scans.
        /// </summary>
        private readonly Block[][] progCoeffs;

        /// <summary>
        /// The huffman trees
        /// </summary>
        private readonly Huffman[,] huffmanTrees;

        /// <summary>
        /// Quantization tables, in zigzag order.
        /// </summary>
        private readonly Block[] quantizationTables;

        /// <summary>
        /// The byte buffer.
        /// </summary>
        private readonly Bytes bytes;

        /// <summary>
        /// The image width
        /// </summary>
        private int imageWidth;

        /// <summary>
        /// The image height
        /// </summary>
        private int imageHeight;

        /// <summary>
        /// The number of color components within the image.
        /// </summary>
        private int componentCount;

        /// <summary>
        /// A grayscale image to decode to.
        /// </summary>
        private GrayImage grayImage;

        /// <summary>
        /// The full color image to decode to.
        /// </summary>
        private YCbCrImage ycbcrImage;

        /// <summary>
        /// The input stream.
        /// </summary>
        private Stream inputStream;

        /// <summary>
        /// Holds the unprocessed bits that have been taken from the byte-stream.
        /// </summary>
        private Bits bits;

        /// <summary>
        /// The array of keyline pixels in a CMYK image
        /// </summary>
        private byte[] blackPixels;

        /// <summary>
        /// The width in bytes or a single row of keyline pixels in a CMYK image
        /// </summary>
        private int blackStride;

        /// <summary>
        /// The restart interval
        /// </summary>
        private int restartInterval;

        /// <summary>
        /// Whether the image is interlaced (progressive)
        /// </summary>
        private bool isProgressive;

        /// <summary>
        /// Whether the image has a JFIF header
        /// </summary>
        private bool isJfif;

        private bool adobeTransformValid;

        private byte adobeTransform;

        /// <summary>
        /// End-of-Band run, specified in section G.1.2.2.
        /// </summary>
        private ushort eobRun;

        /// <summary>
        /// A temporary buffer for holding pixels
        /// </summary>
        private byte[] temp;

        /// <summary>
        /// The horizontal resolution. Calculated if the image has a JFIF header.
        /// </summary>
        private short horizontalResolution;

        /// <summary>
        /// The vertical resolution. Calculated if the image has a JFIF header.
        /// </summary>
        private short verticalResolution;

        /// <summary>
        /// Initializes a new instance of the <see cref="JpegDecoderCore"/> class.
        /// </summary>
        public JpegDecoderCore()
        {
            this.huffmanTrees = new Huffman[maxTc + 1, maxTh + 1];
            this.quantizationTables = new Block[maxTq + 1];
            this.temp = new byte[2 * Block.BlockSize];
            this.componentArray = new Component[MaxComponents];
            this.progCoeffs = new Block[MaxComponents][];
            this.bits = new Bits();
            this.bytes = new Bytes();

            for (int i = 0; i < maxTc + 1; i++)
            {
                for (int j = 0; j < maxTh + 1; j++)
                {
                    this.huffmanTrees[i, j] = new Huffman(LutSize, MaxNCodes, MaxCodeLength);
                }
            }

            for (int i = 0; i < this.quantizationTables.Length; i++)
            {
                this.quantizationTables[i] = new Block();
            }

            for (int i = 0; i < this.componentArray.Length; i++)
            {
                this.componentArray[i] = new Component();
            }
        }

        /// <summary>
        /// Decodes the image from the specified this._stream and sets
        /// the data to image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="stream">The stream, where the image should be
        /// decoded from. Cannot be null (Nothing in Visual Basic).</param>
        /// <param name="image">The image, where the data should be set to.
        /// Cannot be null (Nothing in Visual Basic).</param>
        /// <param name="configOnly">Whether to decode metadata only.</param>
        public void Decode<TColor, TPacked>(Image<TColor, TPacked> image, Stream stream, bool configOnly)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            this.inputStream = stream;

            // Check for the Start Of Image marker.
            this.ReadFull(this.temp, 0, 2);
            if (this.temp[0] != JpegConstants.Markers.XFF || this.temp[1] != JpegConstants.Markers.SOI)
            {
                throw new ImageFormatException("Missing SOI marker.");
            }

            // Process the remaining segments until the End Of Image marker.
            while (true)
            {
                this.ReadFull(this.temp, 0, 2);
                while (this.temp[0] != 0xff)
                {
                    // Strictly speaking, this is a format error. However, libjpeg is
                    // liberal in what it accepts. As of version 9, next_marker in
                    // jdmarker.c treats this as a warning (JWRN_EXTRANEOUS_DATA) and
                    // continues to decode the stream. Even before next_marker sees
                    // extraneous data, jpeg_fill_bit_buffer in jdhuff.c reads as many
                    // bytes as it can, possibly past the end of a scan's data. It
                    // effectively puts back any markers that it overscanned (e.g. an
                    // "\xff\xd9" EOI marker), but it does not put back non-marker data,
                    // and thus it can silently ignore a small number of extraneous
                    // non-marker bytes before next_marker has a chance to see them (and
                    // print a warning).
                    // We are therefore also liberal in what we accept. Extraneous data
                    // is silently ignore
                    // This is similar to, but not exactly the same as, the restart
                    // mechanism within a scan (the RST[0-7] markers).
                    // Note that extraneous 0xff bytes in e.g. SOS data are escaped as
                    // "\xff\x00", and so are detected a little further down below.
                    this.temp[0] = this.temp[1];
                    this.temp[1] = this.ReadByte();
                }

                byte marker = this.temp[1];
                if (marker == 0)
                {
                    // Treat "\xff\x00" as extraneous data.
                    continue;
                }

                while (marker == 0xff)
                {
                    // Section B.1.1.2 says, "Any marker may optionally be preceded by any
                    // number of fill bytes, which are bytes assigned code X'FF'".
                    marker = this.ReadByte();
                }

                // End Of Image.
                if (marker == JpegConstants.Markers.EOI)
                {
                    break;
                }

                if (JpegConstants.Markers.RST0 <= marker && marker <= JpegConstants.Markers.RST7)
                {
                    // Figures B.2 and B.16 of the specification suggest that restart markers should
                    // only occur between Entropy Coded Segments and not after the final ECS.
                    // However, some encoders may generate incorrect JPEGs with a final restart
                    // marker. That restart marker will be seen here instead of inside the ProcessSOS
                    // method, and is ignored as a harmless error. Restart markers have no extra data,
                    // so we check for this before we read the 16-bit length of the segment.
                    continue;
                }

                // Read the 16-bit length of the segment. The value includes the 2 bytes for the
                // length itself, so we subtract 2 to get the number of remaining bytes.
                this.ReadFull(this.temp, 0, 2);
                int remaining = ((int)this.temp[0] << 8) + (int)this.temp[1] - 2;
                if (remaining < 0)
                {
                    throw new ImageFormatException("Short segment length.");
                }

                switch (marker)
                {
                    case JpegConstants.Markers.SOF0:
                    case JpegConstants.Markers.SOF1:
                    case JpegConstants.Markers.SOF2:
                        this.isProgressive = marker == JpegConstants.Markers.SOF2;
                        this.ProcessSOF(remaining);
                        if (configOnly && this.isJfif)
                        {
                            return;
                        }

                        break;
                    case JpegConstants.Markers.DHT:
                        if (configOnly)
                        {
                            this.Skip(remaining);
                        }
                        else
                        {
                            this.ProcessDht(remaining);
                        }

                        break;
                    case JpegConstants.Markers.DQT:
                        if (configOnly)
                        {
                            this.Skip(remaining);
                        }
                        else this.ProcessDqt(remaining);
                        break;
                    case JpegConstants.Markers.SOS:
                        if (configOnly)
                        {
                            return;
                        }

                        this.ProcessStartOfScan(remaining);
                        break;
                    case JpegConstants.Markers.DRI:
                        if (configOnly)
                        {
                            this.Skip(remaining);
                        }
                        else
                        {
                            this.ProcessDri(remaining);
                        }

                        break;
                    case JpegConstants.Markers.APP0:
                        this.ProcessApp0Marker(remaining);
                        break;
                    case JpegConstants.Markers.APP1:
                        this.ProcessApp1Marker(remaining, image);
                        break;
                    case JpegConstants.Markers.APP14:
                        this.ProcessApp14Marker(remaining);
                        break;
                    default:
                        if (JpegConstants.Markers.APP0 <= marker && marker <= JpegConstants.Markers.APP15 || marker == JpegConstants.Markers.COM)
                        {
                            this.Skip(remaining);
                        }
                        else if (marker < JpegConstants.Markers.SOF0)
                        {
                            // See Table B.1 "Marker code assignments".
                            throw new ImageFormatException("Unknown marker");
                        }
                        else
                        {
                            throw new ImageFormatException("Unknown marker");
                        }

                        break;
                }
            }

            if (this.grayImage != null)
            {
                this.ConvertFromGrayScale(this.imageWidth, this.imageHeight, image);
            }
            else if (this.ycbcrImage != null)
            {
                if (this.componentCount == 4)
                {
                    this.ConvertFromCmyk(this.imageWidth, this.imageHeight, image);
                    return;
                }

                if (this.componentCount == 3)
                {
                    if (this.IsRGB())
                    {
                        this.ConvertFromRGB(this.imageWidth, this.imageHeight, image);
                        return;
                    }

                    this.ConvertFromYCbCr(this.imageWidth, this.imageHeight, image);
                    return;
                }

                throw new ImageFormatException("JpegDecoder only supports RGB, CMYK and Grayscale color spaces.");
            }
            else
            {
                throw new ImageFormatException("Missing SOS marker.");
            }
        }

        /// <summary>
        /// Reads bytes from the byte buffer to ensure that bits.UnreadBits is at
        /// least n. For best performance (avoiding function calls inside hot loops),
        /// the caller is the one responsible for first checking that bits.UnreadBits &lt; n.
        /// </summary>
        /// <param name="n">The number of bits to ensure.</param>
        private void EnsureNBits(int n)
        {
            while (true)
            {
                byte c = this.ReadByteStuffedByte();
                this.bits.Accumulator = (this.bits.Accumulator << 8) | c;
                this.bits.UnreadBits += 8;
                if (this.bits.Mask == 0)
                {
                    this.bits.Mask = 1 << 7;
                }
                else
                {
                    this.bits.Mask <<= 8;
                }

                if (this.bits.UnreadBits >= n)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// The composition of RECEIVE and EXTEND, specified in section F.2.2.1.
        /// </summary>
        /// <param name="t">The byte</param>
        /// <returns>The <see cref="int"/></returns>
        private int ReceiveExtend(byte t)
        {
            if (this.bits.UnreadBits < t)
            {
                this.EnsureNBits(t);
            }

            this.bits.UnreadBits -= t;
            this.bits.Mask >>= t;
            int s = 1 << t;
            int x = (int)((this.bits.Accumulator >> this.bits.UnreadBits) & (s - 1));

            if (x < (s >> 1))
            {
                x += ((-1) << t) + 1;
            }

            return x;
        }

        /// <summary>
        /// Processes a Define Huffman Table marker, and initializes a huffman
        /// struct from its contents. Specified in section B.2.4.2.
        /// </summary>
        /// <param name="n"></param>
        private void ProcessDht(int n)
        {
            while (n > 0)
            {
                if (n < 17)
                {
                    throw new ImageFormatException("DHT has wrong length");
                }

                this.ReadFull(this.temp, 0, 17);

                int tc = this.temp[0] >> 4;
                if (tc > maxTc)
                {
                    throw new ImageFormatException("Bad Tc value");
                }

                int th = this.temp[0] & 0x0f;
                if (th > maxTh || (!this.isProgressive && (th > 1)))
                {
                    throw new ImageFormatException("Bad Th value");
                }

                Huffman huffman = this.huffmanTrees[tc, th];

                // Read nCodes and huffman.Valuess (and derive h.Length).
                // nCodes[i] is the number of codes with code length i.
                // h.Length is the total number of codes.
                huffman.Length = 0;

                int[] ncodes = new int[MaxCodeLength];
                for (int i = 0; i < ncodes.Length; i++)
                {
                    ncodes[i] = this.temp[i + 1];
                    huffman.Length += ncodes[i];
                }

                if (huffman.Length == 0)
                {
                    throw new ImageFormatException("Huffman table has zero length");
                }

                if (huffman.Length > MaxNCodes)
                {
                    throw new ImageFormatException("Huffman table has excessive length");
                }

                n -= huffman.Length + 17;
                if (n < 0)
                {
                    throw new ImageFormatException("DHT has wrong length");
                }

                this.ReadFull(huffman.Values, 0, huffman.Length);

                // Derive the look-up table.
                for (int i = 0; i < huffman.Lut.Length; i++)
                {
                    huffman.Lut[i] = 0;
                }

                uint x = 0, code = 0;

                for (int i = 0; i < LutSize; i++)
                {
                    code <<= 1;

                    for (int j = 0; j < ncodes[i]; j++)
                    {
                        // The codeLength is 1+i, so shift code by 8-(1+i) to
                        // calculate the high bits for every 8-bit sequence
                        // whose codeLength's high bits matches code.
                        // The high 8 bits of lutValue are the encoded value.
                        // The low 8 bits are 1 plus the codeLength.
                        byte base2 = (byte)(code << (7 - i));
                        ushort lutValue = (ushort)((huffman.Values[x] << 8) | (2 + i));

                        for (int k = 0; k < 1 << (7 - i); k++)
                        {
                            huffman.Lut[base2 | k] = lutValue;
                        }

                        code++;
                        x++;
                    }
                }

                // Derive minCodes, maxCodes, and indices.
                int c = 0, index = 0;
                for (int i = 0; i < ncodes.Length; i++)
                {
                    int nc = ncodes[i];
                    if (nc == 0)
                    {
                        huffman.MinCodes[i] = -1;
                        huffman.MaxCodes[i] = -1;
                        huffman.Indices[i] = -1;
                    }
                    else
                    {
                        huffman.MinCodes[i] = c;
                        huffman.MaxCodes[i] = c + nc - 1;
                        huffman.Indices[i] = index;
                        c += nc;
                        index += nc;
                    }

                    c <<= 1;
                }
            }
        }

        /// <summary>
        /// Returns the next Huffman-coded value from the bit-stream,
        /// decoded according to the given value.
        /// </summary>
        /// <param name="huffman">The huffman value</param>
        /// <returns>The <see cref="byte"/></returns>
        private byte DecodeHuffman(Huffman huffman)
        {
            if (huffman.Length == 0)
            {
                throw new ImageFormatException("Uninitialized Huffman table");
            }

            if (this.bits.UnreadBits < 8)
            {
                try
                {
                    this.EnsureNBits(8);

                    ushort v = huffman.Lut[(this.bits.Accumulator >> (this.bits.UnreadBits - LutSize)) & 0xff];

                    if (v != 0)
                    {
                        byte n = (byte)((v & 0xff) - 1);
                        this.bits.UnreadBits -= n;
                        this.bits.Mask >>= n;
                        return (byte)(v >> 8);
                    }
                }
                catch (MissingFF00Exception)
                {
                    if (this.bytes.UnreadableBytes != 0)
                    {
                        this.UnreadByteStuffedByte();
                    }
                }
                catch (ShortHuffmanDataException)
                {
                    if (this.bytes.UnreadableBytes != 0)
                    {
                        this.UnreadByteStuffedByte();
                    }
                }
            }

            int code = 0;
            for (int i = 0; i < MaxCodeLength; i++)
            {
                if (this.bits.UnreadBits == 0)
                {
                    this.EnsureNBits(1);
                }

                if ((this.bits.Accumulator & this.bits.Mask) != 0)
                {
                    code |= 1;
                }

                this.bits.UnreadBits--;
                this.bits.Mask >>= 1;

                if (code <= huffman.MaxCodes[i])
                {
                    return huffman.Values[huffman.Indices[i] + code - huffman.MinCodes[i]];
                }

                code <<= 1;
            }

            throw new ImageFormatException("Bad Huffman code");
        }

        /// <summary>
        /// Decodes a single bit
        /// </summary>
        /// <returns>The <see cref="bool"/></returns>
        private bool DecodeBit()
        {
            if (this.bits.UnreadBits == 0)
            {
                this.EnsureNBits(1);
            }

            bool ret = (this.bits.Accumulator & this.bits.Mask) != 0;
            this.bits.UnreadBits--;
            this.bits.Mask >>= 1;
            return ret;
        }

        /// <summary>
        /// Decodes the given number of bits
        /// </summary>
        /// <param name="count">The number of bits to decode.</param>
        /// <returns>The <see cref="uint"/></returns>
        private uint DecodeBits(int count)
        {
            if (this.bits.UnreadBits < count)
            {
                this.EnsureNBits(count);
            }

            uint ret = this.bits.Accumulator >> (this.bits.UnreadBits - count);
            ret = (uint)(ret & ((1 << count) - 1));
            this.bits.UnreadBits -= count;
            this.bits.Mask >>= count;
            return ret;
        }

        /// <summary>
        /// Fills up the bytes buffer from the underlying stream. 
        /// It should only be called when there are no unread bytes in bytes.
        /// </summary>
        private void Fill()
        {
            if (this.bytes.I != this.bytes.J)
            {
                throw new ImageFormatException("Fill called when unread bytes exist.");
            }

            // Move the last 2 bytes to the start of the buffer, in case we need
            // to call UnreadByteStuffedByte.
            if (this.bytes.J > 2)
            {
                this.bytes.Buffer[0] = this.bytes.Buffer[this.bytes.J - 2];
                this.bytes.Buffer[1] = this.bytes.Buffer[this.bytes.J - 1];
                this.bytes.I = 2;
                this.bytes.J = 2;
            }

            // Fill in the rest of the buffer.
            int n = this.inputStream.Read(this.bytes.Buffer, this.bytes.J, this.bytes.Buffer.Length - this.bytes.J);
            if (n == 0)
            {
                throw new EOFException();
            }

            this.bytes.J += n;
        }

        /// <summary>
        /// Undoes the most recent ReadByteStuffedByte call,
        /// giving a byte of data back from bits to bytes. The Huffman look-up table
        /// requires at least 8 bits for look-up, which means that Huffman decoding can
        /// sometimes overshoot and read one or two too many bytes. Two-byte overshoot
        /// can happen when expecting to read a 0xff 0x00 byte-stuffed byte.
        /// </summary>
        private void UnreadByteStuffedByte()
        {
            this.bytes.I -= this.bytes.UnreadableBytes;
            this.bytes.UnreadableBytes = 0;
            if (this.bits.UnreadBits >= 8)
            {
                this.bits.Accumulator >>= 8;
                this.bits.UnreadBits -= 8;
                this.bits.Mask >>= 8;
            }
        }

        /// <summary>
        /// Returns the next byte, whether buffered or not buffered. It does not care about byte stuffing.
        /// </summary>
        /// <returns>The <see cref="byte"/></returns>
        private byte ReadByte()
        {
            while (this.bytes.I == this.bytes.J)
            {
                this.Fill();
            }

            byte x = this.bytes.Buffer[this.bytes.I];
            this.bytes.I++;
            this.bytes.UnreadableBytes = 0;
            return x;
        }

        /// <summary>
        /// ReadByteStuffedByte is like ReadByte but is for byte-stuffed Huffman data.
        /// </summary>
        /// <returns>The <see cref="byte"/></returns>
        private byte ReadByteStuffedByte()
        {
            byte x;

            // Take the fast path if bytes.buf contains at least two bytes.
            if (this.bytes.I + 2 <= this.bytes.J)
            {
                x = this.bytes.Buffer[this.bytes.I];
                this.bytes.I++;
                this.bytes.UnreadableBytes = 1;
                if (x != JpegConstants.Markers.XFF)
                {
                    return x;
                }

                if (this.bytes.Buffer[this.bytes.I] != 0x00)
                {
                    throw new MissingFF00Exception();
                }

                this.bytes.I++;
                this.bytes.UnreadableBytes = 2;
                return JpegConstants.Markers.XFF;
            }

            this.bytes.UnreadableBytes = 0;

            x = this.ReadByte();
            this.bytes.UnreadableBytes = 1;
            if (x != JpegConstants.Markers.XFF)
            {
                return x;
            }

            x = this.ReadByte();
            this.bytes.UnreadableBytes = 2;
            if (x != 0x00)
            {
                throw new MissingFF00Exception();
            }

            return JpegConstants.Markers.XFF;
        }

        /// <summary>
        /// Reads exactly length bytes into data. It does not care about byte stuffing.
        /// </summary>
        /// <param name="data">The data to write to.</param>
        /// <param name="offset">The offset in the source buffer</param>
        /// <param name="length">The number of bytes to read</param>
        private void ReadFull(byte[] data, int offset, int length)
        {
            // Unread the overshot bytes, if any.
            if (this.bytes.UnreadableBytes != 0)
            {
                if (this.bits.UnreadBits >= 8)
                {
                    this.UnreadByteStuffedByte();
                }

                this.bytes.UnreadableBytes = 0;
            }

            while (length > 0)
            {
                if (this.bytes.J - this.bytes.I >= length)
                {
                    Array.Copy(this.bytes.Buffer, this.bytes.I, data, offset, length);
                    this.bytes.I += length;
                    length -= length;
                }
                else
                {
                    Array.Copy(this.bytes.Buffer, this.bytes.I, data, offset, this.bytes.J - this.bytes.I);
                    offset += this.bytes.J - this.bytes.I;
                    length -= this.bytes.J - this.bytes.I;
                    this.bytes.I += this.bytes.J - this.bytes.I;

                    this.Fill();
                }
            }
        }

        /// <summary>
        /// Skips the next n bytes.
        /// </summary>
        /// <param name="count">The number of bytes to ignore.</param>
        private void Skip(int count)
        {
            // Unread the overshot bytes, if any.
            if (this.bytes.UnreadableBytes != 0)
            {
                if (this.bits.UnreadBits >= 8)
                {
                    this.UnreadByteStuffedByte();
                }

                this.bytes.UnreadableBytes = 0;
            }

            while (true)
            {
                int m = this.bytes.J - this.bytes.I;
                if (m > count)
                {
                    m = count;
                }

                this.bytes.I += m;
                count -= m;
                if (count == 0)
                {
                    break;
                }

                this.Fill();
            }
        }

        // Specified in section B.2.2.
        private void ProcessSOF(int n)
        {
            if (this.componentCount != 0)
            {
                throw new ImageFormatException("multiple SOF markers");
            }

            switch (n)
            {
                case 6 + (3 * 1): // Grayscale image.
                    this.componentCount = 1;
                    break;
                case 6 + (3 * 3): // YCbCr or RGB image.
                    this.componentCount = 3;
                    break;
                case 6 + (3 * 4): // YCbCrK or CMYK image.
                    this.componentCount = 4;
                    break;
                default:
                    throw new ImageFormatException("Incorrect number of components");
            }

            this.ReadFull(this.temp, 0, n);

            // We only support 8-bit precision.
            if (this.temp[0] != 8)
            {
                throw new ImageFormatException("Only 8-Bit precision supported.");
            }

            this.imageHeight = (this.temp[1] << 8) + this.temp[2];
            this.imageWidth = (this.temp[3] << 8) + this.temp[4];
            if (this.temp[5] != this.componentCount)
            {
                throw new ImageFormatException("SOF has wrong length");
            }

            for (int i = 0; i < this.componentCount; i++)
            {
                this.componentArray[i].Identifier = this.temp[6 + (3 * i)];

                // Section B.2.2 states that "the value of C_i shall be different from
                // the values of C_1 through C_(i-1)".
                for (int j = 0; j < i; j++)
                {
                    if (this.componentArray[i].Identifier == this.componentArray[j].Identifier)
                    {
                        throw new ImageFormatException("Repeated component identifier");
                    }
                }

                this.componentArray[i].Selector = this.temp[8 + (3 * i)];
                if (this.componentArray[i].Selector > maxTq)
                {
                    throw new ImageFormatException("Bad Tq value");
                }

                byte hv = this.temp[7 + (3 * i)];
                int h = hv >> 4;
                int v = hv & 0x0f;
                if (h < 1 || h > 4 || v < 1 || v > 4)
                {
                    throw new ImageFormatException("Unsupported Luma/chroma subsampling ratio");
                }

                if (h == 3 || v == 3)
                {
                    throw new ImageFormatException("Lnsupported subsampling ratio");
                }

                switch (this.componentCount)
                {
                    case 1:

                        // If a JPEG image has only one component, section A.2 says "this data
                        // is non-interleaved by definition" and section A.2.2 says "[in this
                        // case...] the order of data units within a scan shall be left-to-right
                        // and top-to-bottom... regardless of the values of H_1 and V_1". Section
                        // 4.8.2 also says "[for non-interleaved data], the MCU is defined to be
                        // one data unit". Similarly, section A.1.1 explains that it is the ratio
                        // of H_i to max_j(H_j) that matters, and similarly for V. For grayscale
                        // images, H_1 is the maximum H_j for all components j, so that ratio is
                        // always 1. The component's (h, v) is effectively always (1, 1): even if
                        // the nominal (h, v) is (2, 1), a 20x5 image is encoded in three 8x8
                        // MCUs, not two 16x8 MCUs.
                        h = 1;
                        v = 1;
                        break;

                    case 3:

                        // For YCbCr images, we only support 4:4:4, 4:4:0, 4:2:2, 4:2:0,
                        // 4:1:1 or 4:1:0 chroma subsampling ratios. This implies that the
                        // (h, v) values for the Y component are either (1, 1), (1, 2),
                        // (2, 1), (2, 2), (4, 1) or (4, 2), and the Y component's values
                        // must be a multiple of the Cb and Cr component's values. We also
                        // assume that the two chroma components have the same subsampling
                        // ratio.
                        switch (i)
                        {
                            case 0:
                                {
                                    // Y.
                                    // We have already verified, above, that h and v are both
                                    // either 1, 2 or 4, so invalid (h, v) combinations are those
                                    // with v == 4.
                                    if (v == 4)
                                    {
                                        throw new ImageFormatException("Unsupported subsampling ratio");
                                    }

                                    break;
                                }

                            case 1:
                                {
                                    // Cb.
                                    if (this.componentArray[0].HorizontalFactor % h != 0 || this.componentArray[0].VerticalFactor % v != 0)
                                    {
                                        throw new ImageFormatException("Unsupported subsampling ratio");
                                    }

                                    break;
                                }

                            case 2:
                                {
                                    // Cr.
                                    if (this.componentArray[1].HorizontalFactor != h || this.componentArray[1].VerticalFactor != v)
                                    {
                                        throw new ImageFormatException("Unsupported subsampling ratio");
                                    }

                                    break;
                                }
                        }

                        break;

                    case 4:

                        // For 4-component images (either CMYK or YCbCrK), we only support two
                        // hv vectors: [0x11 0x11 0x11 0x11] and [0x22 0x11 0x11 0x22].
                        // Theoretically, 4-component JPEG images could mix and match hv values
                        // but in practice, those two combinations are the only ones in use,
                        // and it simplifies the applyBlack code below if we can assume that:
                        // - for CMYK, the C and K channels have full samples, and if the M
                        // and Y channels subsample, they subsample both horizontally and
                        // vertically.
                        // - for YCbCrK, the Y and K channels have full samples.
                        switch (i)
                        {
                            case 0:
                                if (hv != 0x11 && hv != 0x22)
                                {
                                    throw new ImageFormatException("Unsupported subsampling ratio");
                                }

                                break;
                            case 1:
                            case 2:
                                if (hv != 0x11)
                                {
                                    throw new ImageFormatException("Unsupported subsampling ratio");
                                }

                                break;
                            case 3:
                                if (this.componentArray[0].HorizontalFactor != h || this.componentArray[0].VerticalFactor != v)
                                {
                                    throw new ImageFormatException("Unsupported subsampling ratio");
                                }

                                break;
                        }

                        break;
                }

                this.componentArray[i].HorizontalFactor = h;
                this.componentArray[i].VerticalFactor = v;
            }
        }

        // Specified in section B.2.4.1.
        private void ProcessDqt(int n)
        {
            while (n > 0)
            {
                bool done = false;

                n--;
                byte x = this.ReadByte();
                byte tq = (byte)(x & 0x0f);
                if (tq > maxTq)
                {
                    throw new ImageFormatException("Bad Tq value");
                }

                switch (x >> 4)
                {
                    case 0:
                        if (n < Block.BlockSize)
                        {
                            done = true;
                            break;
                        }

                        n -= Block.BlockSize;
                        this.ReadFull(this.temp, 0, Block.BlockSize);

                        for (int i = 0; i < Block.BlockSize; i++)
                        {
                            this.quantizationTables[tq][i] = this.temp[i];
                        }

                        break;
                    case 1:
                        if (n < 2 * Block.BlockSize)
                        {
                            done = true;
                            break;
                        }

                        n -= 2 * Block.BlockSize;
                        this.ReadFull(this.temp, 0, 2 * Block.BlockSize);

                        for (int i = 0; i < Block.BlockSize; i++)
                        {
                            this.quantizationTables[tq][i] = (this.temp[2 * i] << 8) | this.temp[(2 * i) + 1];
                        }

                        break;
                    default:
                        throw new ImageFormatException("Bad Pq value");
                }

                if (done)
                {
                    break;
                }
            }

            if (n != 0)
            {
                throw new ImageFormatException("DQT has wrong length");
            }
        }

        // Specified in section B.2.4.4.
        private void ProcessDri(int n)
        {
            if (n != 2)
            {
                throw new ImageFormatException("DRI has wrong length");
            }

            this.ReadFull(this.temp, 0, 2);
            this.restartInterval = ((int)this.temp[0] << 8) + (int)this.temp[1];
        }

        private void ProcessApp0Marker(int n)
        {
            if (n < 5)
            {
                this.Skip(n);
                return;
            }

            this.ReadFull(this.temp, 0, 13);
            n -= 13;

            // TODO: We should be using constants for this.
            this.isJfif = this.temp[0] == 'J' &&
                          this.temp[1] == 'F' &&
                          this.temp[2] == 'I' &&
                          this.temp[3] == 'F' &&
                          this.temp[4] == '\x00';

            if (this.isJfif)
            {
                this.horizontalResolution = (short)(this.temp[9] + (this.temp[10] << 8));
                this.verticalResolution = (short)(this.temp[11] + (this.temp[12] << 8));
            }

            if (n > 0)
            {
                this.Skip(n);
            }
        }

        /// <summary>
        /// Processes the App1 marker retrieving any stored metadata
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="n">The position in the stream.</param>
        /// <param name="image">The image.</param>
        private void ProcessApp1Marker<TColor, TPacked>(int n, Image<TColor, TPacked> image)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            if (n < 6)
            {
                this.Skip(n);
                return;
            }

            byte[] profile = new byte[n];
            this.ReadFull(profile, 0, n);

            if (profile[0] == 'E' &&
                profile[1] == 'x' &&
                profile[2] == 'i' &&
                profile[3] == 'f' &&
                profile[4] == '\0' &&
                profile[5] == '\0')
            {
                image.ExifProfile = new ExifProfile(profile);
            }
        }

        private void ProcessApp14Marker(int n)
        {
            if (n < 12)
            {
                this.Skip(n);
                return;
            }

            this.ReadFull(this.temp, 0, 12);
            n -= 12;

            if (this.temp[0] == 'A' &&
                this.temp[1] == 'd' &&
                this.temp[2] == 'o' &&
                this.temp[3] == 'b' &&
                this.temp[4] == 'e')
            {
                this.adobeTransformValid = true;
                this.adobeTransform = this.temp[11];
            }

            if (n > 0)
            {
                this.Skip(n);
            }
        }

        /// <summary>
        /// Converts the image from the original Cmyk image pixels.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="width">The image width.</param>
        /// <param name="height">The image height.</param>
        /// <param name="image">The image.</param>
        private void ConvertFromCmyk<TColor, TPacked>(int width, int height, Image<TColor, TPacked> image)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            if (!this.adobeTransformValid)
            {
                throw new ImageFormatException("Unknown color model: 4-component JPEG doesn't have Adobe APP14 metadata");
            }

            // If the 4-component JPEG image isn't explicitly marked as "Unknown (RGB
            // or CMYK)" as per http://www.sno.phy.queensu.ca/~phil/exiftool/TagNames/JPEG.html#Adobe
            if (this.adobeTransform != JpegConstants.Adobe.ColorTransformUnknown)
            {
                int scale = this.componentArray[0].HorizontalFactor / this.componentArray[1].HorizontalFactor;

                TColor[] pixels = new TColor[width * height];

                // Convert the YCbCr part of the YCbCrK to RGB, invert the RGB to get
                // CMY, and patch in the original K. The RGB to CMY inversion cancels
                // out the 'Adobe inversion' described in the applyBlack doc comment
                // above, so in practice, only the fourth channel (black) is inverted.
                Parallel.For(
                    0,
                    height,
                    y =>
                    {
                        int yo = this.ycbcrImage.GetRowYOffset(y);
                        int co = this.ycbcrImage.GetRowCOffset(y);

                        for (int x = 0; x < width; x++)
                        {
                            byte yy = this.ycbcrImage.YChannel[yo + x];
                            byte cb = this.ycbcrImage.CbChannel[co + (x / scale)];
                            byte cr = this.ycbcrImage.CrChannel[co + (x / scale)];

                            int index = (y * width) + x;

                            // Implicit casting FTW
                            Color color = new YCbCr(yy, cb, cr);
                            int keyline = 255 - this.blackPixels[y * this.blackStride + x];
                            Color final = new Cmyk(color.R / 255F, color.G / 255F, color.B / 255F, keyline / 255F);

                            TColor packed = default(TColor);
                            packed.PackFromVector4(final.ToVector4());
                            pixels[index] = packed;
                        }
                    });

                image.SetPixels(width, height, pixels);
            }
        }

        /// <summary>
        /// Converts the image from the original grayscale image pixels.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>long, float.</example></typeparam>
        /// <param name="width">The image width.</param>
        /// <param name="height">The image height.</param>
        /// <param name="image">The image.</param>
        private void ConvertFromGrayScale<TColor, TPacked>(int width, int height, Image<TColor, TPacked> image)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            TColor[] pixels = new TColor[width * height];

            Parallel.For(
                0,
                height,
                Bootstrapper.Instance.ParallelOptions,
                y =>
                {
                    int yoff = this.grayImage.GetRowOffset(y);
                    for (int x = 0; x < width; x++)
                    {
                        int offset = (y * width) + x;
                        byte rgb = this.grayImage.Pixels[yoff + x];

                        TColor packed = default(TColor);
                        packed.PackFromVector4(new Color(rgb, rgb, rgb).ToVector4());
                        pixels[offset] = packed;
                    }
                });

            image.SetPixels(width, height, pixels);
            this.AssignResolution(image);
        }

        /// <summary>
        /// Converts the image from the original YCbCr image pixels.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="width">The image width.</param>
        /// <param name="height">The image height.</param>
        /// <param name="image">The image.</param>
        private void ConvertFromYCbCr<TColor, TPacked>(int width, int height, Image<TColor, TPacked> image)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            int scale = this.componentArray[0].HorizontalFactor / this.componentArray[1].HorizontalFactor;

            TColor[] pixels = new TColor[width * height];

            Parallel.For(
                0,
                height,
                Bootstrapper.Instance.ParallelOptions,
                y =>
                    {
                        int yo = this.ycbcrImage.GetRowYOffset(y);
                        int co = this.ycbcrImage.GetRowCOffset(y);

                        for (int x = 0; x < width; x++)
                        {
                            byte yy = this.ycbcrImage.YChannel[yo + x];
                            byte cb = this.ycbcrImage.CbChannel[co + (x / scale)];
                            byte cr = this.ycbcrImage.CrChannel[co + (x / scale)];

                            int index = (y * width) + x;

                            // Implicit casting FTW
                            Color color = new YCbCr(yy, cb, cr);
                            TColor packed = default(TColor);
                            packed.PackFromVector4(color.ToVector4());
                            pixels[index] = packed;
                        }
                    });

            image.SetPixels(width, height, pixels);
            this.AssignResolution(image);
        }

        /// <summary>
        /// Converts the image from the original RBG image pixels.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="width">The image width.</param>
        /// <param name="height">The height.</param>
        /// <param name="image">The image.</param>
        private void ConvertFromRGB<TColor, TPacked>(int width, int height, Image<TColor, TPacked> image)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            int scale = this.componentArray[0].HorizontalFactor / this.componentArray[1].HorizontalFactor;
            TColor[] pixels = new TColor[width * height];

            Parallel.For(
                0,
                height,
                Bootstrapper.Instance.ParallelOptions,
                y =>
                    {
                        int yo = this.ycbcrImage.GetRowYOffset(y);
                        int co = this.ycbcrImage.GetRowCOffset(y);

                        for (int x = 0; x < width; x++)
                        {
                            byte red = this.ycbcrImage.YChannel[yo + x];
                            byte green = this.ycbcrImage.CbChannel[co + (x / scale)];
                            byte blue = this.ycbcrImage.CrChannel[co + (x / scale)];

                            int index = (y * width) + x;
                            TColor packed = default(TColor);
                            packed.PackFromVector4(new Color(red, green, blue).ToVector4());

                            pixels[index] = packed;
                        }
                    });

            image.SetPixels(width, height, pixels);
            this.AssignResolution(image);
        }

        /// <summary>
        /// Assigns the horizontal and vertical resolution to the image if it has a JFIF header.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="image">The image to assign the resolution to.</param>
        private void AssignResolution<TColor, TPacked>(Image<TColor, TPacked> image)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            if (this.isJfif && this.horizontalResolution > 0 && this.verticalResolution > 0)
            {
                image.HorizontalResolution = this.horizontalResolution;
                image.VerticalResolution = this.verticalResolution;
            }
        }

        /// <summary>
        /// Processes the SOS (Start of scan marker).
        /// </summary>
        /// <remarks>
        /// TODO: This also needs some significant refactoring to follow a more OO format.
        /// </remarks>
        /// <param name="n">
        /// The first byte of the current image marker.
        /// </param>
        /// <exception cref="ImageFormatException">
        /// Missing SOF Marker
        /// SOS has wrong length
        /// </exception>
        private void ProcessStartOfScan(int n)
        {
            if (this.componentCount == 0)
            {
                throw new ImageFormatException("Missing SOF marker");
            }

            if (n < 6 || 4 + (2 * this.componentCount) < n || n % 2 != 0)
            {
                throw new ImageFormatException("SOS has wrong length");
            }

            this.ReadFull(this.temp, 0, n);
            byte lnComp = this.temp[0];

            if (n != 4 + (2 * lnComp))
            {
                throw new ImageFormatException("SOS length inconsistent with number of components");
            }

            Scan[] scan = new Scan[MaxComponents];
            int totalHv = 0;

            for (int i = 0; i < lnComp; i++)
            {
                // Component selector.
                int cs = this.temp[1 + (2 * i)];
                int compIndex = -1;
                for (int j = 0; j < this.componentCount; j++)
                {
                    Component compv = this.componentArray[j];
                    if (cs == compv.Identifier)
                    {
                        compIndex = j;
                    }
                }

                if (compIndex < 0)
                {
                    throw new ImageFormatException("Unknown component selector");
                }

                scan[i].Index = (byte)compIndex;

                // Section B.2.3 states that "the value of Cs_j shall be different from
                // the values of Cs_1 through Cs_(j-1)". Since we have previously
                // verified that a frame's component identifiers (C_i values in section
                // B.2.2) are unique, it suffices to check that the implicit indexes
                // into comp are unique.
                for (int j = 0; j < i; j++)
                {
                    if (scan[i].Index == scan[j].Index)
                    {
                        throw new ImageFormatException("Repeated component selector");
                    }
                }

                totalHv += this.componentArray[compIndex].HorizontalFactor
                           * this.componentArray[compIndex].VerticalFactor;

                scan[i].DcTableSelector = (byte)(this.temp[2 + (2 * i)] >> 4);
                if (scan[i].DcTableSelector > maxTh)
                {
                    throw new ImageFormatException("Bad DC table selector value");
                }

                scan[i].AcTableSelector = (byte)(this.temp[2 + (2 * i)] & 0x0f);
                if (scan[i].AcTableSelector > maxTh)
                {
                    throw new ImageFormatException("Bad AC table selector  value");
                }
            }

            // Section B.2.3 states that if there is more than one component then the
            // total H*V values in a scan must be <= 10.
            if (this.componentCount > 1 && totalHv > 10)
            {
                throw new ImageFormatException("Total sampling factors too large.");
            }

            // zigStart and zigEnd are the spectral selection bounds.
            // ah and al are the successive approximation high and low values.
            // The spec calls these values Ss, Se, Ah and Al.
            // For progressive JPEGs, these are the two more-or-less independent
            // aspects of progression. Spectral selection progression is when not
            // all of a block's 64 DCT coefficients are transmitted in one pass.
            // For example, three passes could transmit coefficient 0 (the DC
            // component), coefficients 1-5, and coefficients 6-63, in zig-zag
            // order. Successive approximation is when not all of the bits of a
            // band of coefficients are transmitted in one pass. For example,
            // three passes could transmit the 6 most significant bits, followed
            // by the second-least significant bit, followed by the least
            // significant bit.
            // For baseline JPEGs, these parameters are hard-coded to 0/63/0/0.
            int zigStart = 0;
            int zigEnd = Block.BlockSize - 1;
            int ah = 0;
            int al = 0;

            if (this.isProgressive)
            {
                zigStart = (int)this.temp[1 + (2 * lnComp)];
                zigEnd = (int)this.temp[2 + (2 * lnComp)];
                ah = (int)(this.temp[3 + (2 * lnComp)] >> 4);
                al = (int)(this.temp[3 + (2 * lnComp)] & 0x0f);

                if ((zigStart == 0 && zigEnd != 0) || zigStart > zigEnd || Block.BlockSize <= zigEnd)
                {
                    throw new ImageFormatException("Bad spectral selection bounds");
                }

                if (zigStart != 0 && lnComp != 1)
                {
                    throw new ImageFormatException("Progressive AC coefficients for more than one component");
                }

                if (ah != 0 && ah != al + 1)
                {
                    throw new ImageFormatException("Bad successive approximation values");
                }
            }

            // mxx and myy are the number of MCUs (Minimum Coded Units) in the image.
            int h0 = this.componentArray[0].HorizontalFactor;
            int v0 = this.componentArray[0].VerticalFactor;
            int mxx = (this.imageWidth + (8 * h0) - 1) / (8 * h0);
            int myy = (this.imageHeight + (8 * v0) - 1) / (8 * v0);

            if (this.grayImage == null && this.ycbcrImage == null)
            {
                this.MakeImage(mxx, myy);
            }

            if (this.isProgressive)
            {
                for (int i = 0; i < lnComp; i++)
                {
                    int compIndex = scan[i].Index;
                    if (this.progCoeffs[compIndex] == null)
                    {
                        this.progCoeffs[compIndex] = new Block[mxx * myy * this.componentArray[compIndex].HorizontalFactor * this.componentArray[compIndex].VerticalFactor];

                        for (int j = 0; j < this.progCoeffs[compIndex].Length; j++)
                        {
                            this.progCoeffs[compIndex][j] = new Block();
                        }
                    }
                }
            }

            this.bits = new Bits();

            int mcu = 0;
            byte expectedRst = JpegConstants.Markers.RST0;

            // b is the decoded coefficients, in natural (not zig-zag) order.
            Block b;
            int[] dc = new int[MaxComponents];

            // bx and by are the location of the current block, in units of 8x8
            // blocks: the third block in the first row has (bx, by) = (2, 0).
            int bx, by, blockCount = 0;

            for (int my = 0; my < myy; my++)
            {
                for (int mx = 0; mx < mxx; mx++)
                {
                    for (int i = 0; i < lnComp; i++)
                    {
                        int compIndex = scan[i].Index;
                        int hi = this.componentArray[compIndex].HorizontalFactor;
                        int vi = this.componentArray[compIndex].VerticalFactor;
                        Block qt = this.quantizationTables[this.componentArray[compIndex].Selector];

                        for (int j = 0; j < hi * vi; j++)
                        {
                            // The blocks are traversed one MCU at a time. For 4:2:0 chroma
                            // subsampling, there are four Y 8x8 blocks in every 16x16 MCU.
                            // For a baseline 32x16 pixel image, the Y blocks visiting order is:
                            // 0 1 4 5
                            // 2 3 6 7
                            // For progressive images, the interleaved scans (those with nComp > 1)
                            // are traversed as above, but non-interleaved scans are traversed left
                            // to right, top to bottom:
                            // 0 1 2 3
                            // 4 5 6 7
                            // Only DC scans (zigStart == 0) can be interleave AC scans must have
                            // only one component.
                            // To further complicate matters, for non-interleaved scans, there is no
                            // data for any blocks that are inside the image at the MCU level but
                            // outside the image at the pixel level. For example, a 24x16 pixel 4:2:0
                            // progressive image consists of two 16x16 MCUs. The interleaved scans
                            // will process 8 Y blocks:
                            // 0 1 4 5
                            // 2 3 6 7
                            // The non-interleaved scans will process only 6 Y blocks:
                            // 0 1 2
                            // 3 4 5
                            if (lnComp != 1)
                            {
                                bx = (hi * mx) + (j % hi);
                                by = (vi * my) + (j / hi);
                            }
                            else
                            {
                                int q = mxx * hi;
                                bx = blockCount % q;
                                by = blockCount / q;
                                blockCount++;
                                if (bx * 8 >= this.imageWidth || by * 8 >= this.imageHeight)
                                {
                                    continue;
                                }
                            }

                            // Load the previous partially decoded coefficients, if applicable.
                            b = this.isProgressive ? this.progCoeffs[compIndex][((@by * mxx) * hi) + bx] : new Block();

                            if (ah != 0)
                            {
                                this.Refine(b, this.huffmanTrees[acTable, scan[i].AcTableSelector], zigStart, zigEnd, 1 << al);
                            }
                            else
                            {
                                int zig = zigStart;
                                if (zig == 0)
                                {
                                    zig++;

                                    // Decode the DC coefficient, as specified in section F.2.2.1.
                                    byte value = this.DecodeHuffman(this.huffmanTrees[dcTable, scan[i].DcTableSelector]);
                                    if (value > 16)
                                    {
                                        throw new ImageFormatException("Excessive DC component");
                                    }

                                    int dcDelta = this.ReceiveExtend(value);
                                    dc[compIndex] += dcDelta;
                                    b[0] = dc[compIndex] << al;
                                }

                                if (zig <= zigEnd && this.eobRun > 0)
                                {
                                    this.eobRun--;
                                }
                                else
                                {
                                    // Decode the AC coefficients, as specified in section F.2.2.2.
                                    Huffman huffv = this.huffmanTrees[acTable, scan[i].AcTableSelector];
                                    for (; zig <= zigEnd; zig++)
                                    {
                                        byte value = this.DecodeHuffman(huffv);
                                        byte val0 = (byte)(value >> 4);
                                        byte val1 = (byte)(value & 0x0f);
                                        if (val1 != 0)
                                        {
                                            zig += val0;
                                            if (zig > zigEnd)
                                            {
                                                break;
                                            }

                                            int ac = this.ReceiveExtend(val1);
                                            b[Unzig[zig]] = ac << al;
                                        }
                                        else
                                        {
                                            if (val0 != 0x0f)
                                            {
                                                this.eobRun = (ushort)(1 << val0);
                                                if (val0 != 0)
                                                {
                                                    this.eobRun |= (ushort)this.DecodeBits(val0);
                                                }

                                                this.eobRun--;
                                                break;
                                            }

                                            zig += 0x0f;
                                        }
                                    }
                                }
                            }

                            if (this.isProgressive)
                            {
                                if (zigEnd != Block.BlockSize - 1 || al != 0)
                                {
                                    // We haven't completely decoded this 8x8 block. Save the coefficients.
                                    this.progCoeffs[compIndex][((by * mxx) * hi) + bx] = b;

                                    // At this point, we could execute the rest of the loop body to dequantize and
                                    // perform the inverse DCT, to save early stages of a progressive image to the
                                    // *image.YCbCr buffers (the whole point of progressive encoding), but in Go,
                                    // the jpeg.Decode function does not return until the entire image is decoded,
                                    // so we "continue" here to avoid wasted computation.
                                    continue;
                                }
                            }

                            // Dequantize, perform the inverse DCT and store the block to the image.
                            for (int zig = 0; zig < Block.BlockSize; zig++)
                            {
                                b[Unzig[zig]] *= qt[zig];
                            }

                            IDCT.Transform(b);

                            byte[] dst;
                            int offset;
                            int stride;

                            if (this.componentCount == 1)
                            {
                                dst = this.grayImage.Pixels;
                                stride = this.grayImage.Stride;
                                offset = this.grayImage.Offset + (8 * ((by * this.grayImage.Stride) + bx));
                            }
                            else
                            {
                                switch (compIndex)
                                {
                                    case 0:
                                        dst = this.ycbcrImage.YChannel;
                                        stride = this.ycbcrImage.YStride;
                                        offset = this.ycbcrImage.YOffset + (8 * ((by * this.ycbcrImage.YStride) + bx));
                                        break;

                                    case 1:
                                        dst = this.ycbcrImage.CbChannel;
                                        stride = this.ycbcrImage.CStride;
                                        offset = this.ycbcrImage.COffset + (8 * ((by * this.ycbcrImage.CStride) + bx));
                                        break;

                                    case 2:
                                        dst = this.ycbcrImage.CrChannel;
                                        stride = this.ycbcrImage.CStride;
                                        offset = this.ycbcrImage.COffset + (8 * ((by * this.ycbcrImage.CStride) + bx));
                                        break;

                                    case 3:

                                        dst = this.blackPixels;
                                        stride = this.blackStride;
                                        offset = 8 * ((by * this.blackStride) + bx);
                                        break;

                                    default:
                                        throw new ImageFormatException("Too many components");
                                }
                            }

                            // Level shift by +128, clip to [0, 255], and write to dst.
                            for (int y = 0; y < 8; y++)
                            {
                                int y8 = y * 8;
                                int yStride = y * stride;

                                for (int x = 0; x < 8; x++)
                                {
                                    int c = b[y8 + x];
                                    if (c < -128)
                                    {
                                        c = 0;
                                    }
                                    else if (c > 127)
                                    {
                                        c = 255;
                                    }
                                    else
                                    {
                                        c += 128;
                                    }

                                    dst[yStride + x + offset] = (byte)c;
                                }
                            }
                        }

                        // for j
                    }

                    // for i
                    mcu++;

                    if (this.restartInterval > 0 && mcu % this.restartInterval == 0 && mcu < mxx * myy)
                    {
                        // A more sophisticated decoder could use RST[0-7] markers to resynchronize from corrupt input,
                        // but this one assumes well-formed input, and hence the restart marker follows immediately.
                        this.ReadFull(this.temp, 0, 2);
                        if (this.temp[0] != 0xff || this.temp[1] != expectedRst)
                        {
                            throw new ImageFormatException("Bad RST marker");
                        }

                        expectedRst++;
                        if (expectedRst == JpegConstants.Markers.RST7 + 1)
                        {
                            expectedRst = JpegConstants.Markers.RST0;
                        }

                        // Reset the Huffman decoder.
                        this.bits = new Bits();

                        // Reset the DC components, as per section F.2.1.3.1.
                        dc = new int[MaxComponents];

                        // Reset the progressive decoder state, as per section G.1.2.2.
                        this.eobRun = 0;
                    }
                }

                // for mx
            }

            // for my
        }

        /// <summary>
        /// Decodes a successive approximation refinement block, as specified in section G.1.2.
        /// </summary>
        /// <param name="b"></param>
        /// <param name="h"></param>
        /// <param name="zigStart"></param>
        /// <param name="zigEnd"></param>
        /// <param name="delta"></param>
        private void Refine(Block b, Huffman h, int zigStart, int zigEnd, int delta)
        {
            // Refining a DC component is trivial.
            if (zigStart == 0)
            {
                if (zigEnd != 0)
                {
                    throw new ImageFormatException("Invalid state for zig DC component");
                }

                bool bit = this.DecodeBit();
                if (bit)
                {
                    b[0] |= delta;
                }

                return;
            }

            // Refining AC components is more complicated; see sections G.1.2.2 and G.1.2.3.
            int zig = zigStart;
            if (this.eobRun == 0)
            {
                for (; zig <= zigEnd; zig++)
                {
                    bool done = false;
                    int z = 0;
                    byte val = this.DecodeHuffman(h);
                    int val0 = val >> 4;
                    int val1 = val & 0x0f;

                    switch (val1)
                    {
                        case 0:
                            if (val0 != 0x0f)
                            {
                                this.eobRun = (ushort)(1 << val0);
                                if (val0 != 0)
                                {
                                    uint bits = this.DecodeBits(val0);
                                    this.eobRun |= (ushort)bits;
                                }

                                done = true;
                            }

                            break;
                        case 1:
                            z = delta;
                            bool bit = this.DecodeBit();
                            if (!bit)
                            {
                                z = -z;
                            }

                            break;
                        default:
                            throw new ImageFormatException("Unexpected Huffman code");
                    }

                    if (done)
                    {
                        break;
                    }

                    zig = this.RefineNonZeroes(b, zig, zigEnd, val0, delta);
                    if (zig > zigEnd)
                    {
                        throw new ImageFormatException($"Too many coefficients {zig} > {zigEnd}");
                    }

                    if (z != 0)
                    {
                        b[Unzig[zig]] = z;
                    }
                }
            }

            if (this.eobRun > 0)
            {
                this.eobRun--;
                this.RefineNonZeroes(b, zig, zigEnd, -1, delta);
            }
        }

        // refineNonZeroes refines non-zero entries of b in zig-zag order. If nz >= 0,
        // the first nz zero entries are skipped over.
        private int RefineNonZeroes(Block b, int zig, int zigEnd, int nz, int delta)
        {
            for (; zig <= zigEnd; zig++)
            {
                int u = Unzig[zig];
                if (b[u] == 0)
                {
                    if (nz == 0)
                    {
                        break;
                    }

                    nz--;
                    continue;
                }

                bool bit = this.DecodeBit();
                if (!bit)
                {
                    continue;
                }

                if (b[u] >= 0)
                {
                    b[u] += delta;
                }
                else
                {
                    b[u] -= delta;
                }
            }

            return zig;
        }

        /// <summary>
        /// Makes the image from the buffer.
        /// </summary>
        /// <param name="mxx"></param>
        /// <param name="myy"></param>
        private void MakeImage(int mxx, int myy)
        {
            if (this.componentCount == 1)
            {
                GrayImage m = new GrayImage(8 * mxx, 8 * myy);
                this.grayImage = m.Subimage(0, 0, this.imageWidth, this.imageHeight);
            }
            else
            {
                int h0 = this.componentArray[0].HorizontalFactor;
                int v0 = this.componentArray[0].VerticalFactor;
                int horizontalRatio = h0 / this.componentArray[1].HorizontalFactor;
                int verticalRatio = v0 / this.componentArray[1].VerticalFactor;

                YCbCrImage.YCbCrSubsampleRatio ratio = YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio444;
                switch ((horizontalRatio << 4) | verticalRatio)
                {
                    case 0x11:
                        ratio = YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio444;
                        break;
                    case 0x12:
                        ratio = YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio440;
                        break;
                    case 0x21:
                        ratio = YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio422;
                        break;
                    case 0x22:
                        ratio = YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio420;
                        break;
                    case 0x41:
                        ratio = YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio411;
                        break;
                    case 0x42:
                        ratio = YCbCrImage.YCbCrSubsampleRatio.YCbCrSubsampleRatio410;
                        break;
                }

                YCbCrImage m = new YCbCrImage(8 * h0 * mxx, 8 * v0 * myy, ratio);
                this.ycbcrImage = m.Subimage(0, 0, this.imageWidth, this.imageHeight);

                if (this.componentCount == 4)
                {
                    int h3 = this.componentArray[3].HorizontalFactor;
                    int v3 = this.componentArray[3].VerticalFactor;
                    this.blackPixels = new byte[8 * h3 * mxx * 8 * v3 * myy];
                    this.blackStride = 8 * h3 * mxx;
                }
            }
        }

        /// <summary>
        /// Returns a value indicating whether the image in an RGB image.
        /// </summary>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        private bool IsRGB()
        {
            if (this.isJfif)
            {
                return false;
            }

            if (this.adobeTransformValid && this.adobeTransform == JpegConstants.Adobe.ColorTransformUnknown)
            {
                // http://www.sno.phy.queensu.ca/~phil/exiftool/TagNames/JPEG.html#Adobe
                // says that 0 means Unknown (and in practice RGB) and 1 means YCbCr.
                return true;
            }

            return this.componentArray[0].Identifier == 'R' && this.componentArray[1].Identifier == 'G' && this.componentArray[2].Identifier == 'B';
        }

        /// <summary>
        /// Represents a component scan
        /// </summary>
        private struct Scan
        {
            /// <summary>
            /// Gets or sets the component index.
            /// </summary>
            public byte Index { get; set; }

            /// <summary>
            /// Gets or sets the DC table selector
            /// </summary>
            public byte DcTableSelector { get; set; }

            /// <summary>
            /// Gets or sets the AC table selector
            /// </summary>
            public byte AcTableSelector { get; set; }
        }

        /// <summary>
        /// The missing ff00 exception.
        /// </summary>
        private class MissingFF00Exception : Exception
        {
        }

        /// <summary>
        /// The short huffman data exception.
        /// </summary>
        private class ShortHuffmanDataException : Exception
        {
        }

        private class EOFException : Exception
        {
        }
    }
}
