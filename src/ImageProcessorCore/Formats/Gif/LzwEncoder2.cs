namespace ImageProcessorCore.Formats
{
    using System;
    using System.IO;

    /// <summary>
    /// An implementation of the Lempel-Ziv-Welch lossless compression algorithm.
    /// See http://en.wikipedia.org/wiki/Lzw
    /// </summary>
    public class LzwEncoder2
    {
        #region declarations
        private const int _EOF = -1;
        private const int _BITS = 12;
        private const int _HSIZE = 5003; // 80% occupancy

        /// <summary>
        /// A collection of indices within the active colour table of the 
        /// colours of the pixels within the image.
        /// </summary>
        private byte[] _pixels;
        private int _initCodeSize;

        /// <summary>
        /// Number of pixels still to process.
        /// </summary>
        private int _pixelsRemaining;

        /// <summary>
        /// Index of the current position within the IndexedPixels collection.
        /// </summary>
        private int _pixelIndex;

        /// <summary>
        /// Number of bits per encoded code.
        /// </summary>
        int _codeSize; // number of bits/code

        /// <summary>
        /// Maximum number of bits per encoded code.
        /// </summary>
        int _maxCodeSize = _BITS; // user settable max # bits/code

        /// <summary>
        /// The largest possible code given the current value of _codeSize.
        /// </summary>
        int _maxCode; // maximum code, given n_bits

        /// <summary>
        /// The largest possible code given the largest possible value of 
        /// _codeSize, plus 1. We should never output this code.
        /// </summary>
        int _maxMaxCode = 1 << _BITS; // should NEVER generate this code

        int[] _htab = new int[_HSIZE];
        int[] _codetab = new int[_HSIZE];

        int _hsize = _HSIZE; // for dynamic table sizing

        /// <summary>
        /// The next unused code. Initially set to the clear code plus 2.
        /// </summary>
        int _nextAvailableCode; // first unused entry

        // block compression parameters -- after all codes are used up,
        // and compression rate changes, start over.
        bool _clear_flg;

        int _g_init_bits;

        /// <summary>
        /// Clear code. This is written out by the encoder when the dictionary
        /// is full, and is an instruction to the decoder to empty its dictionary
        /// </summary>
        int _clearCode;

        /// <summary>
        /// End of information code. This is set to the clear code plus 1 and
        /// marks the end of the encoded data.
        /// </summary>
        int _endOfInformationCode;

        int _cur_accum;
        int _cur_bits;

        int[] _masks =
        {
            0x0000,
            0x0001,
            0x0003,
            0x0007,
            0x000F,
            0x001F,
            0x003F,
            0x007F,
            0x00FF,
            0x01FF,
            0x03FF,
            0x07FF,
            0x0FFF,
            0x1FFF,
            0x3FFF,
            0x7FFF,
            0xFFFF };

        // Number of characters so far in this 'packet'
        /// <summary>
        /// Number of bytes that have been added to the packet so far.
        /// </summary>
        int _byteCountInPacket;

        // Define the storage for the packet accumulator
        /// <summary>
        /// An array of encoded bytes which are waiting to be written to the
        /// output stream. The bytes are written out once 254 of them have
        /// been populated.
        /// </summary>
        byte[] _packet = new byte[256];
        #endregion

        #region constructor
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="pixels">
        /// Indices in the active colour table of the colours of the pixel 
        /// making up the image.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// The supplied pixel collection is null.
        /// </exception>
        public LzwEncoder2(byte[] pixels)
        {
            if (pixels == null)
            {
                throw new ArgumentNullException("pixels");
            }

            _pixels = pixels;
            //			_initCodeSize = Math.Max(2, colourDepth);
            _initCodeSize = 8; // only seems to work reliably when 8, even if this is sometimes larger than needed
        }
        #endregion

        #region Encode method
        /// <summary>
        /// Encodes the data and writes it to the supplied output stream.
        /// </summary>
        /// <param name="outputStream">Output stream</param>
        /// <exception cref="ArgumentNullException">
        /// The supplied output stream is null.
        /// </exception>
        public void Encode(Stream outputStream)
        {
            if (outputStream == null)
            {
                throw new ArgumentNullException("outputStream");
            }

            outputStream.WriteByte(Convert.ToByte(_initCodeSize)); // write "initial code size" byte

            _pixelsRemaining = _pixels.Length;
            _pixelIndex = 0;

            Compress(_initCodeSize + 1, outputStream); // compress and write the pixel data

            outputStream.WriteByte(0); // write block terminator
        }
        #endregion

        #region private methods

        #region private Add method
        /// <summary>
        /// Add a character to the end of the current packet, and if the packet
        /// is 254 characters long, flush the packet to disk.
        /// </summary>
        /// <param name="c">The character to add</param>
        /// <param name="outputStream">Output stream</param>
        private void Add(byte c, Stream outputStream)
        {
            _packet[_byteCountInPacket++] = c;
            if (_byteCountInPacket >= 254)
            {
                Flush(outputStream);
            }
        }
        #endregion

        #region private ClearTable method
        /// <summary>
        /// Clears out the hash table.
        /// </summary>
        /// <param name="outs">Output stream</param>
        private void ClearTable(Stream outs)
        {
            ResetCodeTable(_hsize);
            _nextAvailableCode = _clearCode + 2;
            _clear_flg = true;

            Output(_clearCode, outs);
        }
        #endregion

        #region private ResetCodeTable method
        /// <summary>
        /// Resets the code table
        /// </summary>
        /// <param name="hsize"></param>
        private void ResetCodeTable(int hsize)
        {
            for (int i = 0; i < hsize; ++i)
            {
                _htab[i] = -1;
            }
        }
        #endregion

        #region private Compress method
        /// <summary>
        /// Compress method
        /// </summary>
        /// <param name="init_bits"></param>
        /// <param name="outs"></param>
        private void Compress(int init_bits, Stream outs)
        {
            int fcode;
            int i /* = 0 */;
            int c;
            int ent;
            int disp;
            int hsize_reg;
            int hshift;

            // Set up the globals:  g_init_bits - initial number of bits
            _g_init_bits = init_bits;

            // Set up the necessary values
            _clear_flg = false;
            _codeSize = _g_init_bits;
            _maxCode = MaxCode(_codeSize);

            _clearCode = 1 << (init_bits - 1);
            _endOfInformationCode = _clearCode + 1;
            _nextAvailableCode = _clearCode + 2;

            _byteCountInPacket = 0; // clear packet

            ent = NextPixel();

            hshift = 0;
            for (fcode = _hsize; fcode < 65536; fcode *= 2)
                ++hshift;
            hshift = 8 - hshift; // set hash code range bound

            hsize_reg = _hsize;
            ResetCodeTable(hsize_reg); // clear hash table

            Output(_clearCode, outs);

            outer_loop: while ((c = NextPixel()) != _EOF)
            {
                fcode = (c << _maxCodeSize) + ent;
                i = (c << hshift) ^ ent; // xor hashing

                if (_htab[i] == fcode)
                {
                    ent = _codetab[i];
                    continue;
                }
                else if (_htab[i] >= 0) // non-empty slot
                {
                    disp = hsize_reg - i; // secondary hash (after G. Knott)
                    if (i == 0)
                        disp = 1;
                    do
                    {
                        if ((i -= disp) < 0)
                            i += hsize_reg;

                        if (_htab[i] == fcode)
                        {
                            ent = _codetab[i];
                            goto outer_loop;
                        }
                    } while (_htab[i] >= 0);
                }
                Output(ent, outs);
                ent = c;
                if (_nextAvailableCode < _maxMaxCode)
                {
                    _codetab[i] = _nextAvailableCode++; // code -> hashtable
                    _htab[i] = fcode;
                }
                else
                    ClearTable(outs);
            }
            // Put out the final code.
            Output(ent, outs);
            Output(_endOfInformationCode, outs);
        }
        #endregion

        #region private Flush method
        /// <summary>
        /// Flush the packet to disk, and reset the accumulator
        /// </summary>
        /// <param name="outs"></param>
        private void Flush(Stream outs)
        {
            if (_byteCountInPacket > 0)
            {
                outs.WriteByte(Convert.ToByte(_byteCountInPacket));
                outs.Write(_packet, 0, _byteCountInPacket);
                _byteCountInPacket = 0;
            }
        }
        #endregion

        #region private static MaxCode method
        /// <summary>
        /// Calculates and returns the maximum possible code given the supplied
        /// code size.
        /// This is calculated as 2 to the power of the code size, minus one.
        /// </summary>
        /// <param name="codeSize">
        /// Code size in bits.
        /// </param>
        /// <returns></returns>
        private static int MaxCode(int codeSize)
        {
            return (1 << codeSize) - 1;
        }
        #endregion

        #region private NextPixel method
        /// <summary>
        /// Gets the next pixel from the supplied IndexedPixels collection,
        /// increments the index of the current position within the collection,
        /// and decrements the number of pixels remaining.
        /// </summary>
        /// <returns></returns>
        private int NextPixel()
        {
            if (_pixelsRemaining == 0)
            {
                // We've processed all the supplied pixel indices so return an
                // end of file indicator.
                return _EOF;
            }

            --_pixelsRemaining;

            byte pix = _pixels[_pixelIndex++];

            return pix;
        }
        #endregion

        #region private Output method
        /// <summary>
        /// Adds an encoded LZW code to a buffer ready to be written to the
        /// output stream. Any full bytes contained in the buffer are then
        /// written to the output stream and removed from the buffer.
        /// </summary>
        /// <param name="code"></param>
        /// <param name="outputStream">
        /// The output stream to write to.
        /// </param>
        private void Output(int code, Stream outputStream)
        {
            _cur_accum &= _masks[_cur_bits];

            if (_cur_bits > 0)
            {
                _cur_accum |= (code << _cur_bits);
            }
            else
            {
                _cur_accum = code;
            }

            _cur_bits += _codeSize;

            while (_cur_bits >= 8)
            {
                Add((byte)(_cur_accum & 0xff), outputStream);
                _cur_accum >>= 8;
                _cur_bits -= 8;
            }

            // If the next entry is going to be too big for the code size,
            // then increase it, if possible.
            if (_nextAvailableCode > _maxCode || _clear_flg)
            {
                if (_clear_flg)
                {
                    _maxCode = MaxCode(_codeSize = _g_init_bits);
                    _clear_flg = false;
                }
                else
                {
                    ++_codeSize;
                    if (_codeSize == _maxCodeSize)
                    {
                        _maxCode = _maxMaxCode;
                    }
                    else
                    {
                        _maxCode = MaxCode(_codeSize);
                    }
                }
            }

            if (code == _endOfInformationCode)
            {
                // At EOF, write the rest of the buffer.
                while (_cur_bits > 0)
                {
                    Add((byte)(_cur_accum & 0xff), outputStream);
                    _cur_accum >>= 8;
                    _cur_bits -= 8;
                }

                Flush(outputStream);
            }
        }
        #endregion

        #endregion
    }
}
