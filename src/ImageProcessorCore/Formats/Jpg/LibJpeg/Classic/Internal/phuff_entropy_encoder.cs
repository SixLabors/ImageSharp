/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * Copyright (C) 1994-1996, Thomas G. Lane.
 * This file is part of the Independent JPEG Group's software.
 * For conditions of distribution and use, see the accompanying README file.
 *
 */

/*
 * This file contains Huffman entropy encoding routines for progressive JPEG.
 *
 * We do not support output suspension in this module, since the library
 * currently does not allow multiple-scan files to be written with output
 * suspension.
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace BitMiracle.LibJpeg.Classic.Internal
{
    /// <summary>
    /// Expanded entropy encoder object for progressive Huffman encoding.
    /// </summary>
    class phuff_entropy_encoder : jpeg_entropy_encoder
    {
        private enum MCUEncoder
        {
            mcu_DC_first_encoder,
            mcu_AC_first_encoder,
            mcu_DC_refine_encoder,
            mcu_AC_refine_encoder
        }

        /* MAX_CORR_BITS is the number of bits the AC refinement correction-bit
         * buffer can hold.  Larger sizes may slightly improve compression, but
         * 1000 is already well into the realm of overkill.
         * The minimum safe size is 64 bits.
         */
        private const int MAX_CORR_BITS = 1000; /* Max # of correction bits I can buffer */

        private MCUEncoder m_MCUEncoder;

        /* Mode flag: true for optimization, false for actual data output */
        private bool m_gather_statistics;

        // Bit-level coding status.
        private int m_put_buffer;       /* current bit-accumulation buffer */
        private int m_put_bits;           /* # of bits now in it */

        /* Coding status for DC components */
        private int[] m_last_dc_val = new int[JpegConstants.MAX_COMPS_IN_SCAN]; /* last DC coef for each component */

        /* Coding status for AC components */
        private int m_ac_tbl_no;      /* the table number of the single component */
        private int m_EOBRUN;        /* run length of EOBs */
        private int m_BE;        /* # of buffered correction bits before MCU */
        private char[] m_bit_buffer;       /* buffer for correction bits (1 per char) */
        /* packing correction bits tightly would save some space but cost time... */

        private int m_restarts_to_go;    /* MCUs left in this restart interval */
        private int m_next_restart_num;       /* next restart number to write (0-7) */

        /* Pointers to derived tables (these workspaces have image lifespan).
        * Since any one scan codes only DC or only AC, we only need one set
        * of tables, not one for DC and one for AC.
        */
        private c_derived_tbl[] m_derived_tbls = new c_derived_tbl[JpegConstants.NUM_HUFF_TBLS];

        /* Statistics tables for optimization; again, one set is enough */
        private long[][] m_count_ptrs = new long[JpegConstants.NUM_HUFF_TBLS][];

        public phuff_entropy_encoder(jpeg_compress_struct cinfo)
        {
            m_cinfo = cinfo;

            /* Mark tables unallocated */
            for (int i = 0; i < JpegConstants.NUM_HUFF_TBLS; i++)
            {
                m_derived_tbls[i] = null;
                m_count_ptrs[i] = null;
            }
        }

        // Initialize for a Huffman-compressed scan using progressive JPEG.
        public override void start_pass(bool gather_statistics)
        {
            m_gather_statistics = gather_statistics;

            /* We assume the scan parameters are already validated. */

            /* Select execution routines */
            bool is_DC_band = (m_cinfo.m_Ss == 0);
            if (m_cinfo.m_Ah == 0)
            {
                if (is_DC_band)
                    m_MCUEncoder = MCUEncoder.mcu_DC_first_encoder;
                else
                    m_MCUEncoder = MCUEncoder.mcu_AC_first_encoder;
            }
            else
            {
                if (is_DC_band)
                {
                    m_MCUEncoder = MCUEncoder.mcu_DC_refine_encoder;
                }
                else
                {
                    m_MCUEncoder = MCUEncoder.mcu_AC_refine_encoder;

                    /* AC refinement needs a correction bit buffer */
                    if (m_bit_buffer == null)
                        m_bit_buffer = new char[MAX_CORR_BITS];
                }
            }

            /* Only DC coefficients may be interleaved, so m_cinfo.comps_in_scan = 1
             * for AC coefficients.
             */
            for (int ci = 0; ci < m_cinfo.m_comps_in_scan; ci++)
            {
                jpeg_component_info componentInfo = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[ci]];

                /* Initialize DC predictions to 0 */
                m_last_dc_val[ci] = 0;

                /* Get table index */
                int tbl;
                if (is_DC_band)
                {
                    if (m_cinfo.m_Ah != 0) /* DC refinement needs no table */
                        continue;

                    tbl = componentInfo.Dc_tbl_no;
                }
                else
                {
                    m_ac_tbl_no = componentInfo.Ac_tbl_no;
                    tbl = componentInfo.Ac_tbl_no;
                }

                if (m_gather_statistics)
                {
                    /* Check for invalid table index */
                    /* (make_c_derived_tbl does this in the other path) */
                    if (tbl < 0 || tbl >= JpegConstants.NUM_HUFF_TBLS)
                        m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NO_HUFF_TABLE, tbl);

                    /* Allocate and zero the statistics tables */
                    /* Note that jpeg_gen_optimal_table expects 257 entries in each table! */
                    if (m_count_ptrs[tbl] == null)
                        m_count_ptrs[tbl] = new long[257];

                    Array.Clear(m_count_ptrs[tbl], 0, 257);
                }
                else
                {
                    /* Compute derived values for Huffman table */
                    /* We may do this more than once for a table, but it's not expensive */
                    jpeg_make_c_derived_tbl(is_DC_band, tbl, ref m_derived_tbls[tbl]);
                }
            }

            /* Initialize AC stuff */
            m_EOBRUN = 0;
            m_BE = 0;

            /* Initialize bit buffer to empty */
            m_put_buffer = 0;
            m_put_bits = 0;

            /* Initialize restart stuff */
            m_restarts_to_go = m_cinfo.m_restart_interval;
            m_next_restart_num = 0;
        }

        public override bool encode_mcu(JBLOCK[][] MCU_data)
        {
            switch (m_MCUEncoder)
            {
                case MCUEncoder.mcu_DC_first_encoder:
                    return encode_mcu_DC_first(MCU_data);
                case MCUEncoder.mcu_AC_first_encoder:
                    return encode_mcu_AC_first(MCU_data);
                case MCUEncoder.mcu_DC_refine_encoder:
                    return encode_mcu_DC_refine(MCU_data);
                case MCUEncoder.mcu_AC_refine_encoder:
                    return encode_mcu_AC_refine(MCU_data);
            }

            m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
            return false;
        }

        public override void finish_pass()
        {
            if (m_gather_statistics)
                finish_pass_gather_phuff();
            else
                finish_pass_phuff();
        }

        /// <summary>
        /// MCU encoding for DC initial scan (either spectral selection,
        /// or first pass of successive approximation).
        /// </summary>
        private bool encode_mcu_DC_first(JBLOCK[][] MCU_data)
        {
            /* Emit restart marker if needed */
            if (m_cinfo.m_restart_interval != 0)
            {
                if (m_restarts_to_go == 0)
                    emit_restart(m_next_restart_num);
            }

            /* Encode the MCU data blocks */
            for (int blkn = 0; blkn < m_cinfo.m_blocks_in_MCU; blkn++)
            {
                /* Compute the DC value after the required point transform by Al.
                 * This is simply an arithmetic right shift.
                 */
                int temp2 = IRIGHT_SHIFT(MCU_data[blkn][0][0], m_cinfo.m_Al);

                /* DC differences are figured on the point-transformed values. */
                int ci = m_cinfo.m_MCU_membership[blkn];
                int temp = temp2 - m_last_dc_val[ci];
                m_last_dc_val[ci] = temp2;

                /* Encode the DC coefficient difference per section G.1.2.1 */
                temp2 = temp;
                if (temp < 0)
                {
                    /* temp is abs value of input */
                    temp = -temp;

                    /* For a negative input, want temp2 = bitwise complement of abs(input) */
                    /* This code assumes we are on a two's complement machine */
                    temp2--;
                }

                /* Find the number of bits needed for the magnitude of the coefficient */
                int nbits = 0;
                while (temp != 0)
                {
                    nbits++;
                    temp >>= 1;
                }

                /* Check for out-of-range coefficient values.
                 * Since we're encoding a difference, the range limit is twice as much.
                 */
                if (nbits > MAX_HUFFMAN_COEF_BITS + 1)
                    m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_DCT_COEF);

                /* Count/emit the Huffman-coded symbol for the number of bits */
                emit_symbol(m_cinfo.Component_info[m_cinfo.m_cur_comp_info[ci]].Dc_tbl_no, nbits);

                /* Emit that number of bits of the value, if positive, */
                /* or the complement of its magnitude, if negative. */
                if (nbits != 0)
                {
                    /* emit_bits rejects calls with size 0 */
                    emit_bits(temp2, nbits);
                }
            }

            /* Update restart-interval state too */
            if (m_cinfo.m_restart_interval != 0)
            {
                if (m_restarts_to_go == 0)
                {
                    m_restarts_to_go = m_cinfo.m_restart_interval;
                    m_next_restart_num++;
                    m_next_restart_num &= 7;
                }

                m_restarts_to_go--;
            }

            return true;
        }

        /// <summary>
        /// MCU encoding for AC initial scan (either spectral selection,
        /// or first pass of successive approximation).
        /// </summary>
        private bool encode_mcu_AC_first(JBLOCK[][] MCU_data)
        {
            /* Emit restart marker if needed */
            if (m_cinfo.m_restart_interval != 0)
            {
                if (m_restarts_to_go == 0)
                    emit_restart(m_next_restart_num);
            }

            /* Encode the AC coefficients per section G.1.2.2, fig. G.3 */
            /* r = run length of zeros */
            int r = 0;
            for (int k = m_cinfo.m_Ss; k <= m_cinfo.m_Se; k++)
            {
                int temp = MCU_data[0][0][JpegUtils.jpeg_natural_order[k]];
                if (temp == 0)
                {
                    r++;
                    continue;
                }

                /* We must apply the point transform by Al.  For AC coefficients this
                 * is an integer division with rounding towards 0.  To do this portably
                 * in C, we shift after obtaining the absolute value; so the code is
                 * interwoven with finding the abs value (temp) and output bits (temp2).
                 */
                int temp2;
                if (temp < 0)
                {
                    temp = -temp;       /* temp is abs value of input */
                    temp >>= m_cinfo.m_Al;        /* apply the point transform */
                    /* For a negative coef, want temp2 = bitwise complement of abs(coef) */
                    temp2 = ~temp;
                }
                else
                {
                    temp >>= m_cinfo.m_Al;        /* apply the point transform */
                    temp2 = temp;
                }

                /* Watch out for case that nonzero coef is zero after point transform */
                if (temp == 0)
                {
                    r++;
                    continue;
                }

                /* Emit any pending EOBRUN */
                if (m_EOBRUN > 0)
                    emit_eobrun();

                /* if run length > 15, must emit special run-length-16 codes (0xF0) */
                while (r > 15)
                {
                    emit_symbol(m_ac_tbl_no, 0xF0);
                    r -= 16;
                }

                /* Find the number of bits needed for the magnitude of the coefficient */
                int nbits = 1;          /* there must be at least one 1 bit */
                while ((temp >>= 1) != 0)
                    nbits++;

                /* Check for out-of-range coefficient values */
                if (nbits > MAX_HUFFMAN_COEF_BITS)
                    m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_DCT_COEF);

                /* Count/emit Huffman symbol for run length / number of bits */
                emit_symbol(m_ac_tbl_no, (r << 4) + nbits);

                /* Emit that number of bits of the value, if positive, */
                /* or the complement of its magnitude, if negative. */
                emit_bits(temp2, nbits);

                r = 0;          /* reset zero run length */
            }

            if (r > 0)
            {
                /* If there are trailing zeroes, */
                m_EOBRUN++;      /* count an EOB */
                if (m_EOBRUN == 0x7FFF)
                    emit_eobrun();   /* force it out to avoid overflow */
            }

            /* Update restart-interval state too */
            if (m_cinfo.m_restart_interval != 0)
            {
                if (m_restarts_to_go == 0)
                {
                    m_restarts_to_go = m_cinfo.m_restart_interval;
                    m_next_restart_num++;
                    m_next_restart_num &= 7;
                }
                m_restarts_to_go--;
            }

            return true;
        }

        /// <summary>
        /// MCU encoding for DC successive approximation refinement scan.
        /// Note: we assume such scans can be multi-component, although the spec
        /// is not very clear on the point.
        /// </summary>
        private bool encode_mcu_DC_refine(JBLOCK[][] MCU_data)
        {
            /* Emit restart marker if needed */
            if (m_cinfo.m_restart_interval != 0)
            {
                if (m_restarts_to_go == 0)
                    emit_restart(m_next_restart_num);
            }

            /* Encode the MCU data blocks */
            for (int blkn = 0; blkn < m_cinfo.m_blocks_in_MCU; blkn++)
            {
                /* We simply emit the Al'th bit of the DC coefficient value. */
                int temp = MCU_data[blkn][0][0];
                emit_bits(temp >> m_cinfo.m_Al, 1);
            }

            /* Update restart-interval state too */
            if (m_cinfo.m_restart_interval != 0)
            {
                if (m_restarts_to_go == 0)
                {
                    m_restarts_to_go = m_cinfo.m_restart_interval;
                    m_next_restart_num++;
                    m_next_restart_num &= 7;
                }
                m_restarts_to_go--;
            }

            return true;
        }

        /// <summary>
        /// MCU encoding for AC successive approximation refinement scan.
        /// </summary>
        private bool encode_mcu_AC_refine(JBLOCK[][] MCU_data)
        {
            /* Emit restart marker if needed */
            if (m_cinfo.m_restart_interval != 0)
            {
                if (m_restarts_to_go == 0)
                    emit_restart(m_next_restart_num);
            }

            /* Encode the MCU data block */

            /* It is convenient to make a pre-pass to determine the transformed
             * coefficients' absolute values and the EOB position.
             */
            int EOB = 0;
            int[] absvalues = new int[JpegConstants.DCTSIZE2];
            for (int k = m_cinfo.m_Ss; k <= m_cinfo.m_Se; k++)
            {
                int temp = MCU_data[0][0][JpegUtils.jpeg_natural_order[k]];

                /* We must apply the point transform by Al.  For AC coefficients this
                 * is an integer division with rounding towards 0.  To do this portably
                 * in C, we shift after obtaining the absolute value.
                 */
                if (temp < 0)
                    temp = -temp;       /* temp is abs value of input */

                temp >>= m_cinfo.m_Al;        /* apply the point transform */
                absvalues[k] = temp;    /* save abs value for main pass */
                
                if (temp == 1)
                {
                    /* EOB = index of last newly-nonzero coef */
                    EOB = k;
                }
            }

            /* Encode the AC coefficients per section G.1.2.3, fig. G.7 */

            int r = 0;          /* r = run length of zeros */
            int BR = 0;         /* BR = count of buffered bits added now */
            int bitBufferOffset = m_BE; /* Append bits to buffer */

            for (int k = m_cinfo.m_Ss; k <= m_cinfo.m_Se; k++)
            {
                int temp = absvalues[k];
                if (temp == 0)
                {
                    r++;
                    continue;
                }

                /* Emit any required ZRLs, but not if they can be folded into EOB */
                while (r > 15 && k <= EOB)
                {
                    /* emit any pending EOBRUN and the BE correction bits */
                    emit_eobrun();

                    /* Emit ZRL */
                    emit_symbol(m_ac_tbl_no, 0xF0);
                    r -= 16;
                    
                    /* Emit buffered correction bits that must be associated with ZRL */
                    emit_buffered_bits(bitBufferOffset, BR);
                    bitBufferOffset = 0;/* BE bits are gone now */
                    BR = 0;
                }

                /* If the coef was previously nonzero, it only needs a correction bit.
                 * NOTE: a straight translation of the spec's figure G.7 would suggest
                 * that we also need to test r > 15.  But if r > 15, we can only get here
                 * if k > EOB, which implies that this coefficient is not 1.
                 */
                if (temp > 1)
                {
                    /* The correction bit is the next bit of the absolute value. */
                    m_bit_buffer[bitBufferOffset + BR] = (char) (temp & 1);
                    BR++;
                    continue;
                }

                /* Emit any pending EOBRUN and the BE correction bits */
                emit_eobrun();

                /* Count/emit Huffman symbol for run length / number of bits */
                emit_symbol(m_ac_tbl_no, (r << 4) + 1);

                /* Emit output bit for newly-nonzero coef */
                temp = (MCU_data[0][0][JpegUtils.jpeg_natural_order[k]] < 0) ? 0 : 1;
                emit_bits(temp, 1);

                /* Emit buffered correction bits that must be associated with this code */
                emit_buffered_bits(bitBufferOffset, BR);
                bitBufferOffset = 0;/* BE bits are gone now */
                BR = 0;
                r = 0;          /* reset zero run length */
            }

            if (r > 0 || BR > 0)
            {
                /* If there are trailing zeroes, */
                m_EOBRUN++;      /* count an EOB */
                m_BE += BR;      /* concat my correction bits to older ones */
                
                /* We force out the EOB if we risk either:
                 * 1. overflow of the EOB counter;
                 * 2. overflow of the correction bit buffer during the next MCU.
                 */
                if (m_EOBRUN == 0x7FFF || m_BE > (MAX_CORR_BITS - JpegConstants.DCTSIZE2 + 1))
                    emit_eobrun();
            }

            /* Update restart-interval state too */
            if (m_cinfo.m_restart_interval != 0)
            {
                if (m_restarts_to_go == 0)
                {
                    m_restarts_to_go = m_cinfo.m_restart_interval;
                    m_next_restart_num++;
                    m_next_restart_num &= 7;
                }
                m_restarts_to_go--;
            }

            return true;
        }

        /// <summary>
        /// Finish up at the end of a Huffman-compressed progressive scan.
        /// </summary>
        private void finish_pass_phuff()
        {
            /* Flush out any buffered data */
            emit_eobrun();
            flush_bits();
        }

        /// <summary>
        /// Finish up a statistics-gathering pass and create the new Huffman tables.
        /// </summary>
        private void finish_pass_gather_phuff()
        {
            /* Flush out buffered data (all we care about is counting the EOB symbol) */
            emit_eobrun();

            /* It's important not to apply jpeg_gen_optimal_table more than once
             * per table, because it clobbers the input frequency counts!
             */
            bool[] did = new bool [JpegConstants.NUM_HUFF_TBLS];

            bool is_DC_band = (m_cinfo.m_Ss == 0);
            for (int ci = 0; ci < m_cinfo.m_comps_in_scan; ci++)
            {
                jpeg_component_info componentInfo = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[ci]];
                int tbl = componentInfo.Ac_tbl_no;

                if (is_DC_band)
                {
                    if (m_cinfo.m_Ah != 0) /* DC refinement needs no table */
                        continue;

                    tbl = componentInfo.Dc_tbl_no;
                }
                
                if (!did[tbl])
                {
                    JHUFF_TBL htblptr = null;
                    if (is_DC_band)
                    {
                        if (m_cinfo.m_dc_huff_tbl_ptrs[tbl] == null)
                            m_cinfo.m_dc_huff_tbl_ptrs[tbl] = new JHUFF_TBL();

                        htblptr = m_cinfo.m_dc_huff_tbl_ptrs[tbl];
                    }
                    else
                    {
                        if (m_cinfo.m_ac_huff_tbl_ptrs[tbl] == null)
                            m_cinfo.m_ac_huff_tbl_ptrs[tbl] = new JHUFF_TBL();

                        htblptr = m_cinfo.m_ac_huff_tbl_ptrs[tbl];
                    }

                    jpeg_gen_optimal_table(htblptr, m_count_ptrs[tbl]);
                    did[tbl] = true;
                }
            }
        }
        
        //////////////////////////////////////////////////////////////////////////
        // Outputting bytes to the file.
        // NB: these must be called only when actually outputting,
        // that is, entropy.gather_statistics == false.

        // Emit a byte
        private void emit_byte(int val)
        {
            m_cinfo.m_dest.emit_byte(val);
        }

        /// <summary>
        /// Outputting bits to the file
        /// 
        /// Only the right 24 bits of put_buffer are used; the valid bits are
        /// left-justified in this part.  At most 16 bits can be passed to emit_bits
        /// in one call, and we never retain more than 7 bits in put_buffer
        /// between calls, so 24 bits are sufficient.
        /// </summary>
        private void emit_bits(int code, int size)
        {
            // Emit some bits, unless we are in gather mode
            /* This routine is heavily used, so it's worth coding tightly. */
            int local_put_buffer = code;

            /* if size is 0, caller used an invalid Huffman table entry */
            if (size == 0)
                m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_HUFF_MISSING_CODE);

            if (m_gather_statistics)
            {
                /* do nothing if we're only getting stats */
                return;
            }

            local_put_buffer &= (1 << size) - 1; /* mask off any extra bits in code */

            m_put_bits += size;       /* new number of bits in buffer */

            local_put_buffer <<= 24 - m_put_bits; /* align incoming bits */

            local_put_buffer |= m_put_buffer; /* and merge with old buffer contents */

            while (m_put_bits >= 8)
            {
                int c = (local_put_buffer >> 16) & 0xFF;

                emit_byte(c);
                if (c == 0xFF)
                {
                    /* need to stuff a zero byte? */
                    emit_byte(0);
                }
                local_put_buffer <<= 8;
                m_put_bits -= 8;
            }

            m_put_buffer = local_put_buffer; /* update variables */
        }

        private void flush_bits()
        {
            emit_bits(0x7F, 7); /* fill any partial byte with ones */
            m_put_buffer = 0;     /* and reset bit-buffer to empty */
            m_put_bits = 0;
        }

        // Emit (or just count) a Huffman symbol.
        private void emit_symbol(int tbl_no, int symbol)
        {
            if (m_gather_statistics)
                m_count_ptrs[tbl_no][symbol]++;
            else
                emit_bits(m_derived_tbls[tbl_no].ehufco[symbol], m_derived_tbls[tbl_no].ehufsi[symbol]);
        }

        // Emit bits from a correction bit buffer.
        private void emit_buffered_bits(int offset, int nbits)
        {
            if (m_gather_statistics)
            {
                /* no real work */
                return;
            }

            for (int i = 0; i < nbits; i++)
                emit_bits(m_bit_buffer[offset + i], 1);
        }

        // Emit any pending EOBRUN symbol.
        private void emit_eobrun()
        {
            if (m_EOBRUN > 0)
            {
                /* if there is any pending EOBRUN */
                int temp = m_EOBRUN;
                int nbits = 0;
                while ((temp >>= 1) != 0)
                    nbits++;

                /* safety check: shouldn't happen given limited correction-bit buffer */
                if (nbits > 14)
                    m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_HUFF_MISSING_CODE);

                emit_symbol(m_ac_tbl_no, nbits << 4);
                if (nbits != 0)
                    emit_bits(m_EOBRUN, nbits);

                m_EOBRUN = 0;

                /* Emit any buffered correction bits */
                emit_buffered_bits(0, m_BE);
                m_BE = 0;
            }
        }

        // Emit a restart marker & resynchronize predictions.
        private void emit_restart(int restart_num)
        {
            emit_eobrun();

            if (!m_gather_statistics)
            {
                flush_bits();
                emit_byte(0xFF);
                emit_byte((int)(JPEG_MARKER.RST0 + restart_num));
            }

            if (m_cinfo.m_Ss == 0)
            {
                /* Re-initialize DC predictions to 0 */
                for (int ci = 0; ci < m_cinfo.m_comps_in_scan; ci++)
                    m_last_dc_val[ci] = 0;
            }
            else
            {
                /* Re-initialize all AC-related fields to 0 */
                m_EOBRUN = 0;
                m_BE = 0;
            }
        }

        /// <summary>
        /// IRIGHT_SHIFT is like RIGHT_SHIFT, but works on int rather than int.
        /// We assume that int right shift is unsigned if int right shift is,
        /// which should be safe.
        /// </summary>
        private static int IRIGHT_SHIFT(int x, int shft)
        {
            return (x >> shft);
        }
    }
}
