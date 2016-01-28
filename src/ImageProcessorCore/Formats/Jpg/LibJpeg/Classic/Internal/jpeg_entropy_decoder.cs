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
    /// Entropy decoding
    /// </summary>
    abstract class jpeg_entropy_decoder
    {
        // Figure F.12: extend sign bit.
        // entry n is 2**(n-1)
        private static int[] extend_test = 
        { 
            0, 0x0001, 0x0002, 0x0004, 0x0008, 0x0010, 0x0020, 
            0x0040, 0x0080, 0x0100, 0x0200, 0x0400, 0x0800, 
            0x1000, 0x2000, 0x4000 
        };

        // entry n is (-1 << n) + 1
        private static int[] extend_offset = 
        { 
            0, (-1 << 1) + 1, (-1 << 2) + 1, 
            (-1 << 3) + 1, (-1 << 4) + 1, (-1 << 5) + 1,
            (-1 << 6) + 1, (-1 << 7) + 1, (-1 << 8) + 1,
            (-1 << 9) + 1, (-1 << 10) + 1,
            (-1 << 11) + 1, (-1 << 12) + 1,
            (-1 << 13) + 1, (-1 << 14) + 1,
            (-1 << 15) + 1 
        };
        
        /* Fetching the next N bits from the input stream is a time-critical operation
        * for the Huffman decoders.  We implement it with a combination of inline
        * macros and out-of-line subroutines.  Note that N (the number of bits
        * demanded at one time) never exceeds 15 for JPEG use.
        *
        * We read source bytes into get_buffer and dole out bits as needed.
        * If get_buffer already contains enough bits, they are fetched in-line
        * by the macros CHECK_BIT_BUFFER and GET_BITS.  When there aren't enough
        * bits, jpeg_fill_bit_buffer is called; it will attempt to fill get_buffer
        * as full as possible (not just to the number of bits needed; this
        * prefetching reduces the overhead cost of calling jpeg_fill_bit_buffer).
        * Note that jpeg_fill_bit_buffer may return false to indicate suspension.
        * On true return, jpeg_fill_bit_buffer guarantees that get_buffer contains
        * at least the requested number of bits --- dummy zeroes are inserted if
        * necessary.
        */
        protected const int BIT_BUF_SIZE = 32;    /* size of buffer in bits */

        /*
        * Out-of-line code for bit fetching (shared with jdphuff.c).
        * See jdhuff.h for info about usage.
        * Note: current values of get_buffer and bits_left are passed as parameters,
        * but are returned in the corresponding fields of the state struct.
        *
        * On most machines MIN_GET_BITS should be 25 to allow the full 32-bit width
        * of get_buffer to be used.  (On machines with wider words, an even larger
        * buffer could be used.)  However, on some machines 32-bit shifts are
        * quite slow and take time proportional to the number of places shifted.
        * (This is true with most PC compilers, for instance.)  In this case it may
        * be a win to set MIN_GET_BITS to the minimum value of 15.  This reduces the
        * average shift distance at the cost of more calls to jpeg_fill_bit_buffer.
        */

        protected const int MIN_GET_BITS = BIT_BUF_SIZE - 7;

        protected jpeg_decompress_struct m_cinfo;

        /* This is here to share code between baseline and progressive decoders; */
        /* other modules probably should not use it */
        protected bool m_insufficient_data; /* set true after emitting warning */

        public abstract void start_pass();
        public abstract bool decode_mcu(JBLOCK[] MCU_data);

        protected static int HUFF_EXTEND(int x, int s)
        {
            return ((x) < extend_test[s] ? (x) + extend_offset[s] : (x));
        }

        protected void BITREAD_LOAD_STATE(bitread_perm_state bitstate, out int get_buffer, out int bits_left, ref bitread_working_state br_state)
        {
            br_state.cinfo = m_cinfo;
            get_buffer = bitstate.get_buffer;
            bits_left = bitstate.bits_left;
        }

        protected static void BITREAD_SAVE_STATE(ref bitread_perm_state bitstate, int get_buffer, int bits_left)
        {
            bitstate.get_buffer = get_buffer;
            bitstate.bits_left = bits_left;
        }

        /// <summary>
        /// Expand a Huffman table definition into the derived format
        /// This routine also performs some validation checks on the table.
        /// </summary>
        protected void jpeg_make_d_derived_tbl(bool isDC, int tblno, ref d_derived_tbl dtbl)
        {
            /* Note that huffsize[] and huffcode[] are filled in code-length order,
            * paralleling the order of the symbols themselves in htbl.huffval[].
            */

            /* Find the input Huffman table */
            if (tblno < 0 || tblno >= JpegConstants.NUM_HUFF_TBLS)
                m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NO_HUFF_TABLE, tblno);

            JHUFF_TBL htbl = isDC ? m_cinfo.m_dc_huff_tbl_ptrs[tblno] : m_cinfo.m_ac_huff_tbl_ptrs[tblno];
            if (htbl == null)
                m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NO_HUFF_TABLE, tblno);

            /* Allocate a workspace if we haven't already done so. */
            if (dtbl == null)
                dtbl = new d_derived_tbl();

            dtbl.pub = htbl;       /* fill in back link */

            /* Figure C.1: make table of Huffman code length for each symbol */

            int p = 0;
            char[] huffsize = new char[257];
            for (int l = 1; l <= 16; l++)
            {
                int i = htbl.Bits[l];
                if (i < 0 || p + i> 256)    /* protect against table overrun */
                    m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_HUFF_TABLE);

                while ((i--) != 0)
                    huffsize[p++] = (char) l;
            }
            huffsize[p] = (char)0;
            int numsymbols = p;

            /* Figure C.2: generate the codes themselves */
            /* We also validate that the counts represent a legal Huffman code tree. */

            int code = 0;
            int si = huffsize[0];
            int[] huffcode = new int[257];
            p = 0;
            while (huffsize[p] != 0)
            {
                while (((int)huffsize[p]) == si)
                {
                    huffcode[p++] = code;
                    code++;
                }

                /* code is now 1 more than the last code used for codelength si; but
                * it must still fit in si bits, since no code is allowed to be all ones.
                */
                if (code >= (1 << si))
                    m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_HUFF_TABLE);
                code <<= 1;
                si++;
            }

            /* Figure F.15: generate decoding tables for bit-sequential decoding */

            p = 0;
            for (int l = 1; l <= 16; l++)
            {
                if (htbl.Bits[l] != 0)
                {
                    /* valoffset[l] = huffval[] index of 1st symbol of code length l,
                    * minus the minimum code of length l
                    */
                    dtbl.valoffset[l] = p - huffcode[p];
                    p += htbl.Bits[l];
                    dtbl.maxcode[l] = huffcode[p - 1]; /* maximum code of length l */
                }
                else
                {
                    /* -1 if no codes of this length */
                    dtbl.maxcode[l] = -1;
                }
            }
            dtbl.maxcode[17] = 0xFFFFF; /* ensures jpeg_huff_decode terminates */

            /* Compute lookahead tables to speed up decoding.
            * First we set all the table entries to 0, indicating "too long";
            * then we iterate through the Huffman codes that are short enough and
            * fill in all the entries that correspond to bit sequences starting
            * with that code.
            */

            Array.Clear(dtbl.look_nbits, 0, dtbl.look_nbits.Length);
            p = 0;
            for (int l = 1; l <= JpegConstants.HUFF_LOOKAHEAD; l++)
            {
                for (int i = 1; i <= htbl.Bits[l]; i++, p++)
                {
                    /* l = current code's length, p = its index in huffcode[] & huffval[]. */
                    /* Generate left-justified code followed by all possible bit sequences */
                    int lookbits = huffcode[p] << (JpegConstants.HUFF_LOOKAHEAD - l);
                    for (int ctr = 1 << (JpegConstants.HUFF_LOOKAHEAD - l); ctr > 0; ctr--)
                    {
                        dtbl.look_nbits[lookbits] = l;
                        dtbl.look_sym[lookbits] = htbl.Huffval[p];
                        lookbits++;
                    }
                }
            }

            /* Validate symbols as being reasonable.
            * For AC tables, we make no check, but accept all byte values 0..255.
            * For DC tables, we require the symbols to be in range 0..15.
            * (Tighter bounds could be applied depending on the data depth and mode,
            * but this is sufficient to ensure safe decoding.)
            */
            if (isDC)
            {
                for (int i = 0; i < numsymbols; i++)
                {
                    int sym = htbl.Huffval[i];
                    if (sym < 0 || sym> 15)
                        m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_HUFF_TABLE);
                }
            }
        }

        /*
        * These methods provide the in-line portion of bit fetching.
        * Use CHECK_BIT_BUFFER to ensure there are N bits in get_buffer
        * before using GET_BITS, PEEK_BITS, or DROP_BITS.
        * The variables get_buffer and bits_left are assumed to be locals,
        * but the state struct might not be (jpeg_huff_decode needs this).
        *  CHECK_BIT_BUFFER(state,n,action);
        *      Ensure there are N bits in get_buffer; if suspend, take action.
        *      val = GET_BITS(n);
        *      Fetch next N bits.
        *      val = PEEK_BITS(n);
        *      Fetch next N bits without removing them from the buffer.
        *  DROP_BITS(n);
        *      Discard next N bits.
        * The value N should be a simple variable, not an expression, because it
        * is evaluated multiple times.
        */

        protected static bool CHECK_BIT_BUFFER(ref bitread_working_state state, int nbits, ref int get_buffer, ref int bits_left)
        {
            if (bits_left < nbits)
            {
                if (!jpeg_fill_bit_buffer(ref state, get_buffer, bits_left, nbits))
                    return false;

                get_buffer = state.get_buffer;
                bits_left = state.bits_left;
            }

            return true;
        }

        protected static int GET_BITS(int nbits, int get_buffer, ref int bits_left)
        {
            return (((int)(get_buffer >> (bits_left -= nbits))) & ((1 << nbits) - 1));
        }

        protected static int PEEK_BITS(int nbits, int get_buffer, int bits_left)
        {
            return (((int)(get_buffer >> (bits_left - nbits))) & ((1 << nbits) - 1));
        }

        protected static void DROP_BITS(int nbits, ref int bits_left)
        {
            bits_left -= nbits;
        }

        /* Load up the bit buffer to a depth of at least nbits */
        protected static bool jpeg_fill_bit_buffer(ref bitread_working_state state, int get_buffer, int bits_left, int nbits)
        {
            /* Attempt to load at least MIN_GET_BITS bits into get_buffer. */
            /* (It is assumed that no request will be for more than that many bits.) */
            /* We fail to do so only if we hit a marker or are forced to suspend. */

            bool noMoreBytes = false;

            if (state.cinfo.m_unread_marker == 0)
            {
                /* cannot advance past a marker */
                while (bits_left < MIN_GET_BITS)
                {
                    int c;
                    state.cinfo.m_src.GetByte(out c);

                    /* If it's 0xFF, check and discard stuffed zero byte */
                    if (c == 0xFF)
                    {
                        /* Loop here to discard any padding FF's on terminating marker,
                        * so that we can save a valid unread_marker value.  NOTE: we will
                        * accept multiple FF's followed by a 0 as meaning a single FF data
                        * byte.  This data pattern is not valid according to the standard.
                        */
                        do
                        {
                            state.cinfo.m_src.GetByte(out c);
                        }
                        while (c == 0xFF);

                        if (c == 0)
                        {
                            /* Found FF/00, which represents an FF data byte */
                            c = 0xFF;
                        }
                        else
                        {
                            /* Oops, it's actually a marker indicating end of compressed data.
                            * Save the marker code for later use.
                            * Fine point: it might appear that we should save the marker into
                            * bitread working state, not straight into permanent state.  But
                            * once we have hit a marker, we cannot need to suspend within the
                            * current MCU, because we will read no more bytes from the data
                            * source.  So it is OK to update permanent state right away.
                            */
                            state.cinfo.m_unread_marker = c;
                            /* See if we need to insert some fake zero bits. */
                            noMoreBytes = true;
                            break;
                        }
                    }

                    /* OK, load c into get_buffer */
                    get_buffer = (get_buffer << 8) | c;
                    bits_left += 8;
                } /* end while */
            }
            else
                noMoreBytes = true;

            if (noMoreBytes)
            {
                /* We get here if we've read the marker that terminates the compressed
                * data segment.  There should be enough bits in the buffer register
                * to satisfy the request; if so, no problem.
                */
                if (nbits > bits_left)
                {
                    /* Uh-oh.  Report corrupted data to user and stuff zeroes into
                    * the data stream, so that we can produce some kind of image.
                    * We use a nonvolatile flag to ensure that only one warning message
                    * appears per data segment.
                    */
                    if (!state.cinfo.m_entropy.m_insufficient_data)
                    {
                        state.cinfo.WARNMS(J_MESSAGE_CODE.JWRN_HIT_MARKER);
                        state.cinfo.m_entropy.m_insufficient_data = true;
                    }

                    /* Fill the buffer with zero bits */
                    get_buffer <<= MIN_GET_BITS - bits_left;
                    bits_left = MIN_GET_BITS;
                }
            }

            /* Unload the local registers */
            state.get_buffer = get_buffer;
            state.bits_left = bits_left;

            return true;
        }

        /*
        * Code for extracting next Huffman-coded symbol from input bit stream.
        * Again, this is time-critical and we make the main paths be macros.
        *
        * We use a lookahead table to process codes of up to HUFF_LOOKAHEAD bits
        * without looping.  Usually, more than 95% of the Huffman codes will be 8
        * or fewer bits long.  The few overlength codes are handled with a loop,
        * which need not be inline code.
        *
        * Notes about the HUFF_DECODE macro:
        * 1. Near the end of the data segment, we may fail to get enough bits
        *    for a lookahead.  In that case, we do it the hard way.
        * 2. If the lookahead table contains no entry, the next code must be
        *    more than HUFF_LOOKAHEAD bits long.
        * 3. jpeg_huff_decode returns -1 if forced to suspend.
        */
        protected static bool HUFF_DECODE(out int result, ref bitread_working_state state, d_derived_tbl htbl, ref int get_buffer, ref int bits_left)
        {
            int nb = 0;
            bool doSlow = false;

            if (bits_left < JpegConstants.HUFF_LOOKAHEAD)
            {
                if (!jpeg_fill_bit_buffer(ref state, get_buffer, bits_left, 0))
                {
                    result = -1;
                    return false;
                }

                get_buffer = state.get_buffer;
                bits_left = state.bits_left;
                if (bits_left < JpegConstants.HUFF_LOOKAHEAD)
                {
                    nb = 1;
                    doSlow = true;
                }
            }

            if (!doSlow)
            {
                int look = PEEK_BITS(JpegConstants.HUFF_LOOKAHEAD, get_buffer, bits_left);
                if ((nb = htbl.look_nbits[look]) != 0)
                {
                    DROP_BITS(nb, ref bits_left);
                    result = htbl.look_sym[look];
                    return true;
                }

                nb = JpegConstants.HUFF_LOOKAHEAD + 1;
            }

            result = jpeg_huff_decode(ref state, get_buffer, bits_left, htbl, nb);
            if (result < 0)
                return false;

            get_buffer = state.get_buffer;
            bits_left = state.bits_left;

            return true;
        }

        /* Out-of-line case for Huffman code fetching */
        protected static int jpeg_huff_decode(ref bitread_working_state state, int get_buffer, int bits_left, d_derived_tbl htbl, int min_bits)
        {
            /* HUFF_DECODE has determined that the code is at least min_bits */
            /* bits long, so fetch that many bits in one swoop. */
            int l = min_bits;
            if (!CHECK_BIT_BUFFER(ref state, l, ref get_buffer, ref bits_left))
                return -1;

            int code = GET_BITS(l, get_buffer, ref bits_left);

            /* Collect the rest of the Huffman code one bit at a time. */
            /* This is per Figure F.16 in the JPEG spec. */

            while (code > htbl.maxcode[l])
            {
                code <<= 1;
                if (!CHECK_BIT_BUFFER(ref state, 1, ref get_buffer, ref bits_left))
                    return -1;

                code |= GET_BITS(1, get_buffer, ref bits_left);
                l++;
            }

            /* Unload the local registers */
            state.get_buffer = get_buffer;
            state.bits_left = bits_left;

            /* With garbage input we may reach the sentinel value l = 17. */

            if (l > 16)
            {
                state.cinfo.WARNMS(J_MESSAGE_CODE.JWRN_HUFF_BAD_CODE);
                /* fake a zero as the safest result */
                return 0;
            }

            return htbl.pub.Huffval[code + htbl.valoffset[l]];
        }
    }
}
