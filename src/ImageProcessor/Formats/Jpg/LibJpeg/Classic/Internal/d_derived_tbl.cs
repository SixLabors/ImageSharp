/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * Copyright (C) 1994-1996, Thomas G. Lane.
 * This file is part of the Independent JPEG Group's software.
 * For conditions of distribution and use, see the accompanying README file.
 *
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace BitMiracle.LibJpeg.Classic.Internal
{
    /// <summary>
    /// Derived data constructed for each Huffman table
    /// </summary>
    class d_derived_tbl
    {
        /* Basic tables: (element [0] of each array is unused) */
        public int[] maxcode = new int[18];      /* largest code of length k (-1 if none) */
        /* (maxcode[17] is a sentinel to ensure jpeg_huff_decode terminates) */
        public int[] valoffset = new int[17];        /* huffval[] offset for codes of length k */
        /* valoffset[k] = huffval[] index of 1st symbol of code length k, less
        * the smallest code of length k; so given a code of length k, the
        * corresponding symbol is huffval[code + valoffset[k]]
        */

        /* Link to public Huffman table (needed only in jpeg_huff_decode) */
        public JHUFF_TBL pub;

        /* Lookahead tables: indexed by the next HUFF_LOOKAHEAD bits of
        * the input data stream.  If the next Huffman code is no more
        * than HUFF_LOOKAHEAD bits long, we can obtain its length and
        * the corresponding symbol directly from these tables.
        */
        public int[] look_nbits = new int[1 << JpegConstants.HUFF_LOOKAHEAD]; /* # bits, or 0 if too long */
        public byte[] look_sym = new byte[1 << JpegConstants.HUFF_LOOKAHEAD]; /* symbol, or unused */
    }
}
