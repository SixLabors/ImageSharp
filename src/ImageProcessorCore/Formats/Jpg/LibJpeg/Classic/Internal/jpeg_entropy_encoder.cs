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
    /// Entropy encoding
    /// </summary>
    abstract class jpeg_entropy_encoder
    {
        /* Derived data constructed for each Huffman table */
        protected class c_derived_tbl
        {
            public int[] ehufco = new int[256];   /* code for each symbol */
            public char[] ehufsi = new char[256];       /* length of code for each symbol */
            /* If no code has been allocated for a symbol S, ehufsi[S] contains 0 */
        }

        /* The legal range of a DCT coefficient is
        *  -1024 .. +1023  for 8-bit data;
        * -16384 .. +16383 for 12-bit data.
        * Hence the magnitude should always fit in 10 or 14 bits respectively.
        */
        protected static int MAX_HUFFMAN_COEF_BITS = 10;
        private static int MAX_CLEN = 32;     /* assumed maximum initial code length */

        protected jpeg_compress_struct m_cinfo;

        public abstract void start_pass(bool gather_statistics);
        public abstract bool encode_mcu(JBLOCK[][] MCU_data);
        public abstract void finish_pass();

        /// <summary>
        /// Expand a Huffman table definition into the derived format
        /// Compute the derived values for a Huffman table.
        /// This routine also performs some validation checks on the table.
        /// </summary>
        protected void jpeg_make_c_derived_tbl(bool isDC, int tblno, ref c_derived_tbl dtbl)
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
                dtbl = new c_derived_tbl();

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
            int lastp = p;

            /* Figure C.2: generate the codes themselves */
            /* We also validate that the counts represent a legal Huffman code tree. */

            int code = 0;
            int si = huffsize[0];
            p = 0;
            int[] huffcode = new int[257];
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

            /* Figure C.3: generate encoding tables */
            /* These are code and size indexed by symbol value */

            /* Set all codeless symbols to have code length 0;
            * this lets us detect duplicate VAL entries here, and later
            * allows emit_bits to detect any attempt to emit such symbols.
            */
            Array.Clear(dtbl.ehufsi, 0, dtbl.ehufsi.Length);

            /* This is also a convenient place to check for out-of-range
            * and duplicated VAL entries.  We allow 0..255 for AC symbols
            * but only 0..15 for DC.  (We could constrain them further
            * based on data depth and mode, but this seems enough.)
            */
            int maxsymbol = isDC ? 15 : 255;

            for (p = 0; p < lastp; p++)
            {
                int i = htbl.Huffval[p];
                if (i < 0 || i> maxsymbol || dtbl.ehufsi[i] != 0)
                    m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_HUFF_TABLE);

                dtbl.ehufco[i] = huffcode[p];
                dtbl.ehufsi[i] = huffsize[p];
            }
        }

        /// <summary>
        /// Generate the best Huffman code table for the given counts, fill htbl.
        /// 
        /// The JPEG standard requires that no symbol be assigned a codeword of all
        /// one bits (so that padding bits added at the end of a compressed segment
        /// can't look like a valid code).  Because of the canonical ordering of
        /// codewords, this just means that there must be an unused slot in the
        /// longest codeword length category.  Section K.2 of the JPEG spec suggests
        /// reserving such a slot by pretending that symbol 256 is a valid symbol
        /// with count 1.  In theory that's not optimal; giving it count zero but
        /// including it in the symbol set anyway should give a better Huffman code.
        /// But the theoretically better code actually seems to come out worse in
        /// practice, because it produces more all-ones bytes (which incur stuffed
        /// zero bytes in the final file).  In any case the difference is tiny.
        /// 
        /// The JPEG standard requires Huffman codes to be no more than 16 bits long.
        /// If some symbols have a very small but nonzero probability, the Huffman tree
        /// must be adjusted to meet the code length restriction.  We currently use
        /// the adjustment method suggested in JPEG section K.2.  This method is *not*
        /// optimal; it may not choose the best possible limited-length code.  But
        /// typically only very-low-frequency symbols will be given less-than-optimal
        /// lengths, so the code is almost optimal.  Experimental comparisons against
        /// an optimal limited-length-code algorithm indicate that the difference is
        /// microscopic --- usually less than a hundredth of a percent of total size.
        /// So the extra complexity of an optimal algorithm doesn't seem worthwhile.
        /// </summary>
        protected void jpeg_gen_optimal_table(JHUFF_TBL htbl, long[] freq)
        {
            byte[] bits = new byte[MAX_CLEN + 1];   /* bits[k] = # of symbols with code length k */
            int[] codesize = new int[257];      /* codesize[k] = code length of symbol k */
            int[] others = new int[257];        /* next symbol in current branch of tree */
            int c1, c2;
            int p, i, j;
            long v;

            /* This algorithm is explained in section K.2 of the JPEG standard */
            for (i = 0; i < 257; i++)
                others[i] = -1;     /* init links to empty */

            freq[256] = 1;      /* make sure 256 has a nonzero count */
            /* Including the pseudo-symbol 256 in the Huffman procedure guarantees
            * that no real symbol is given code-value of all ones, because 256
            * will be placed last in the largest codeword category.
            */

            /* Huffman's basic algorithm to assign optimal code lengths to symbols */

            for (; ;)
            {
                /* Find the smallest nonzero frequency, set c1 = its symbol */
                /* In case of ties, take the larger symbol number */
                c1 = -1;
                v = 1000000000L;
                for (i = 0; i <= 256; i++)
                {
                    if (freq[i] != 0 && freq[i] <= v)
                    {
                        v = freq[i];
                        c1 = i;
                    }
                }

                /* Find the next smallest nonzero frequency, set c2 = its symbol */
                /* In case of ties, take the larger symbol number */
                c2 = -1;
                v = 1000000000L;
                for (i = 0; i <= 256; i++)
                {
                    if (freq[i] != 0 && freq[i] <= v && i != c1)
                    {
                        v = freq[i];
                        c2 = i;
                    }
                }

                /* Done if we've merged everything into one frequency */
                if (c2 < 0)
                    break;

                /* Else merge the two counts/trees */
                freq[c1] += freq[c2];
                freq[c2] = 0;

                /* Increment the codesize of everything in c1's tree branch */
                codesize[c1]++;
                while (others[c1] >= 0)
                {
                    c1 = others[c1];
                    codesize[c1]++;
                }

                others[c1] = c2;        /* chain c2 onto c1's tree branch */

                /* Increment the codesize of everything in c2's tree branch */
                codesize[c2]++;
                while (others[c2] >= 0)
                {
                    c2 = others[c2];
                    codesize[c2]++;
                }
            }

            /* Now count the number of symbols of each code length */
            for (i = 0; i <= 256; i++)
            {
                if (codesize[i] != 0)
                {
                    /* The JPEG standard seems to think that this can't happen, */
                    /* but I'm paranoid... */
                    if (codesize[i] > MAX_CLEN)
                        m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_HUFF_CLEN_OVERFLOW);

                    bits[codesize[i]]++;
                }
            }

            /* JPEG doesn't allow symbols with code lengths over 16 bits, so if the pure
            * Huffman procedure assigned any such lengths, we must adjust the coding.
            * Here is what the JPEG spec says about how this next bit works:
            * Since symbols are paired for the longest Huffman code, the symbols are
            * removed from this length category two at a time.  The prefix for the pair
            * (which is one bit shorter) is allocated to one of the pair; then,
            * skipping the BITS entry for that prefix length, a code word from the next
            * shortest nonzero BITS entry is converted into a prefix for two code words
            * one bit longer.
            */

            for (i = MAX_CLEN; i > 16; i--)
            {
                while (bits[i] > 0)
                {
                    j = i - 2;      /* find length of new prefix to be used */
                    while (bits[j] == 0)
                        j--;

                    bits[i] -= 2;       /* remove two symbols */
                    bits[i - 1]++;      /* one goes in this length */
                    bits[j + 1] += 2;       /* two new symbols in this length */
                    bits[j]--;      /* symbol of this length is now a prefix */
                }
            }

            /* Remove the count for the pseudo-symbol 256 from the largest codelength */
            while (bits[i] == 0)        /* find largest codelength still in use */
                i--;
            bits[i]--;

            /* Return final symbol counts (only for lengths 0..16) */
            Buffer.BlockCopy(bits, 0, htbl.Bits, 0, htbl.Bits.Length);

            /* Return a list of the symbols sorted by code length */
            /* It's not real clear to me why we don't need to consider the codelength
            * changes made above, but the JPEG spec seems to think this works.
            */
            p = 0;
            for (i = 1; i <= MAX_CLEN; i++)
            {
                for (j = 0; j <= 255; j++)
                {
                    if (codesize[j] == i)
                    {
                        htbl.Huffval[p] = (byte) j;
                        p++;
                    }
                }
            }

            /* Set sent_table false so updated table will be written to JPEG file. */
            htbl.Sent_table = false;
        }
    }
}
