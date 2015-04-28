/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * Copyright (C) 1994-1996, Thomas G. Lane.
 * This file is part of the Independent JPEG Group's software.
 * For conditions of distribution and use, see the accompanying README file.
 *
 */

/*
 * This file contains Huffman entropy encoding routines.
 *
 * Much of the complexity here has to do with supporting output suspension.
 * If the data destination module demands suspension, we want to be able to
 * back up to the start of the current MCU.  To do this, we copy state
 * variables into local working storage, and update them back to the
 * permanent JPEG objects only upon successful completion of an MCU.
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace BitMiracle.LibJpeg.Classic.Internal
{
    /// <summary>
    /// Expanded entropy encoder object for Huffman encoding.
    /// </summary>
    class huff_entropy_encoder : jpeg_entropy_encoder
    {
        /* The savable_state subrecord contains fields that change within an MCU,
        * but must not be updated permanently until we complete the MCU.
        */
        private class savable_state
        {
            public int put_buffer;       /* current bit-accumulation buffer */
            public int put_bits;           /* # of bits now in it */
            public int[] last_dc_val = new int[JpegConstants.MAX_COMPS_IN_SCAN]; /* last DC coef for each component */
        }

        private bool m_gather_statistics;

        private savable_state m_saved = new savable_state();        /* Bit buffer & DC state at start of MCU */

        /* These fields are NOT loaded into local working state. */
        private int m_restarts_to_go;    /* MCUs left in this restart interval */
        private int m_next_restart_num;       /* next restart number to write (0-7) */

        /* Pointers to derived tables (these workspaces have image lifespan) */
        private c_derived_tbl[] m_dc_derived_tbls = new c_derived_tbl[JpegConstants.NUM_HUFF_TBLS];
        private c_derived_tbl[] m_ac_derived_tbls = new c_derived_tbl[JpegConstants.NUM_HUFF_TBLS];

        /* Statistics tables for optimization */
        private long[][] m_dc_count_ptrs = new long[JpegConstants.NUM_HUFF_TBLS][];
        private long[][] m_ac_count_ptrs = new long[JpegConstants.NUM_HUFF_TBLS][];

        public huff_entropy_encoder(jpeg_compress_struct cinfo)
        {
            m_cinfo = cinfo;

            /* Mark tables unallocated */
            for (int i = 0; i < JpegConstants.NUM_HUFF_TBLS; i++)
            {
                m_dc_derived_tbls[i] = m_ac_derived_tbls[i] = null;
                m_dc_count_ptrs[i] = m_ac_count_ptrs[i] = null;
            }
        }

        /// <summary>
        /// Initialize for a Huffman-compressed scan.
        /// If gather_statistics is true, we do not output anything during the scan,
        /// just count the Huffman symbols used and generate Huffman code tables.
        /// </summary>
        public override void start_pass(bool gather_statistics)
        {
            m_gather_statistics = gather_statistics;

            for (int ci = 0; ci < m_cinfo.m_comps_in_scan; ci++)
            {
                int dctbl = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[ci]].Dc_tbl_no;
                int actbl = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[ci]].Ac_tbl_no;
                if (m_gather_statistics)
                {
                    /* Check for invalid table indexes */
                    /* (make_c_derived_tbl does this in the other path) */
                    if (dctbl < 0 || dctbl >= JpegConstants.NUM_HUFF_TBLS)
                        m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NO_HUFF_TABLE, dctbl);

                    if (actbl < 0 || actbl >= JpegConstants.NUM_HUFF_TBLS)
                        m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NO_HUFF_TABLE, actbl);

                    /* Allocate and zero the statistics tables */
                    /* Note that jpeg_gen_optimal_table expects 257 entries in each table! */
                    if (m_dc_count_ptrs[dctbl] == null)
                        m_dc_count_ptrs[dctbl] = new long[257];

                    Array.Clear(m_dc_count_ptrs[dctbl], 0, m_dc_count_ptrs[dctbl].Length);

                    if (m_ac_count_ptrs[actbl] == null)
                        m_ac_count_ptrs[actbl] = new long[257];

                    Array.Clear(m_ac_count_ptrs[actbl], 0, m_ac_count_ptrs[actbl].Length);
                }
                else
                {
                    /* Compute derived values for Huffman tables */
                    /* We may do this more than once for a table, but it's not expensive */
                    jpeg_make_c_derived_tbl(true, dctbl, ref m_dc_derived_tbls[dctbl]);
                    jpeg_make_c_derived_tbl(false, actbl, ref m_ac_derived_tbls[actbl]);
                }

                /* Initialize DC predictions to 0 */
                m_saved.last_dc_val[ci] = 0;
            }

            /* Initialize bit buffer to empty */
            m_saved.put_buffer = 0;
            m_saved.put_bits = 0;

            /* Initialize restart stuff */
            m_restarts_to_go = m_cinfo.m_restart_interval;
            m_next_restart_num = 0;
        }

        public override bool encode_mcu(JBLOCK[][] MCU_data)
        {
            if (m_gather_statistics)
                return encode_mcu_gather(MCU_data);

            return encode_mcu_huff(MCU_data);
        }

        public override void finish_pass()
        {
            if (m_gather_statistics)
                finish_pass_gather();
            else
                finish_pass_huff();
        }

        /// <summary>
        /// Encode and output one MCU's worth of Huffman-compressed coefficients.
        /// </summary>
        private bool encode_mcu_huff(JBLOCK[][] MCU_data)
        {
            /* Load up working state */
            savable_state state;
            state = m_saved;

            /* Emit restart marker if needed */
            if (m_cinfo.m_restart_interval != 0)
            {
                if (m_restarts_to_go == 0)
                {
                    if (!emit_restart(state, m_next_restart_num))
                        return false;
                }
            }

            /* Encode the MCU data blocks */
            for (int blkn = 0; blkn < m_cinfo.m_blocks_in_MCU; blkn++)
            {
                int ci = m_cinfo.m_MCU_membership[blkn];
                if (!encode_one_block(state, MCU_data[blkn][0].data, state.last_dc_val[ci],
                    m_dc_derived_tbls[m_cinfo.Component_info[m_cinfo.m_cur_comp_info[ci]].Dc_tbl_no],
                    m_ac_derived_tbls[m_cinfo.Component_info[m_cinfo.m_cur_comp_info[ci]].Ac_tbl_no]))
                {
                    return false;
                }

                /* Update last_dc_val */
                state.last_dc_val[ci] = MCU_data[blkn][0][0];
            }

            /* Completed MCU, so update state */
            m_saved = state;

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
        /// Finish up at the end of a Huffman-compressed scan.
        /// </summary>
        private void finish_pass_huff()
        {
            /* Load up working state ... flush_bits needs it */
            savable_state state;
            state = m_saved;

            /* Flush out the last data */
            if (!flush_bits(state))
                m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CANT_SUSPEND);

            /* Update state */
            m_saved = state;
        }

        /// <summary>
        /// Trial-encode one MCU's worth of Huffman-compressed coefficients.
        /// No data is actually output, so no suspension return is possible.
        /// </summary>
        private bool encode_mcu_gather(JBLOCK[][] MCU_data)
        {
            /* Take care of restart intervals if needed */
            if (m_cinfo.m_restart_interval != 0)
            {
                if (m_restarts_to_go == 0)
                {
                    /* Re-initialize DC predictions to 0 */
                    for (int ci = 0; ci < m_cinfo.m_comps_in_scan; ci++)
                        m_saved.last_dc_val[ci] = 0;

                    /* Update restart state */
                    m_restarts_to_go = m_cinfo.m_restart_interval;
                }

                m_restarts_to_go--;
            }

            for (int blkn = 0; blkn < m_cinfo.m_blocks_in_MCU; blkn++)
            {
                int ci = m_cinfo.m_MCU_membership[blkn];
                htest_one_block(MCU_data[blkn][0].data, m_saved.last_dc_val[ci],
                    m_dc_count_ptrs[m_cinfo.Component_info[m_cinfo.m_cur_comp_info[ci]].Dc_tbl_no],
                    m_ac_count_ptrs[m_cinfo.Component_info[m_cinfo.m_cur_comp_info[ci]].Ac_tbl_no]);
                m_saved.last_dc_val[ci] = MCU_data[blkn][0][0];
            }

            return true;
        }

        /// <summary>
        /// Finish up a statistics-gathering pass and create the new Huffman tables.
        /// </summary>
        private void finish_pass_gather()
        {
            /* It's important not to apply jpeg_gen_optimal_table more than once
             * per table, because it clobbers the input frequency counts!
             */
            bool[] did_dc = new bool [JpegConstants.NUM_HUFF_TBLS];
            bool[] did_ac = new bool[JpegConstants.NUM_HUFF_TBLS];

            for (int ci = 0; ci < m_cinfo.m_comps_in_scan; ci++)
            {
                int dctbl = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[ci]].Dc_tbl_no;
                if (!did_dc[dctbl])
                {
                    if (m_cinfo.m_dc_huff_tbl_ptrs[dctbl] == null)
                        m_cinfo.m_dc_huff_tbl_ptrs[dctbl] = new JHUFF_TBL();

                    jpeg_gen_optimal_table(m_cinfo.m_dc_huff_tbl_ptrs[dctbl], m_dc_count_ptrs[dctbl]);
                    did_dc[dctbl] = true;
                }

                int actbl = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[ci]].Ac_tbl_no;
                if (!did_ac[actbl])
                {
                    if (m_cinfo.m_ac_huff_tbl_ptrs[actbl] == null)
                        m_cinfo.m_ac_huff_tbl_ptrs[actbl] = new JHUFF_TBL();

                    jpeg_gen_optimal_table(m_cinfo.m_ac_huff_tbl_ptrs[actbl], m_ac_count_ptrs[actbl]);
                    did_ac[actbl] = true;
                }
            }
        }

        /// <summary>
        /// Encode a single block's worth of coefficients
        /// </summary>
        private bool encode_one_block(savable_state state, short[] block, int last_dc_val, c_derived_tbl dctbl, c_derived_tbl actbl)
        {
            /* Encode the DC coefficient difference per section F.1.2.1 */
            int temp = block[0] - last_dc_val;
            int temp2 = temp;
            if (temp < 0)
            {
                temp = -temp;       /* temp is abs value of input */
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

            /* Emit the Huffman-coded symbol for the number of bits */
            if (!emit_bits(state, dctbl.ehufco[nbits], dctbl.ehufsi[nbits]))
                return false;

            /* Emit that number of bits of the value, if positive, */
            /* or the complement of its magnitude, if negative. */
            if (nbits != 0)
            {
                /* emit_bits rejects calls with size 0 */
                if (!emit_bits(state, temp2, nbits))
                    return false;
            }

            /* Encode the AC coefficients per section F.1.2.2 */
            int r = 0;          /* r = run length of zeros */
            for (int k = 1; k < JpegConstants.DCTSIZE2; k++)
            {
                temp = block[JpegUtils.jpeg_natural_order[k]];
                if (temp == 0)
                {
                    r++;
                }
                else
                {
                    /* if run length > 15, must emit special run-length-16 codes (0xF0) */
                    while (r > 15)
                    {
                        if (!emit_bits(state, actbl.ehufco[0xF0], actbl.ehufsi[0xF0]))
                            return false;
                        r -= 16;
                    }

                    temp2 = temp;
                    if (temp < 0)
                    {
                        temp = -temp;       /* temp is abs value of input */
                        /* This code assumes we are on a two's complement machine */
                        temp2--;
                    }

                    /* Find the number of bits needed for the magnitude of the coefficient */
                    nbits = 1;      /* there must be at least one 1 bit */
                    while ((temp >>= 1) != 0)
                        nbits++;

                    /* Check for out-of-range coefficient values */
                    if (nbits > MAX_HUFFMAN_COEF_BITS)
                        m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_DCT_COEF);

                    /* Emit Huffman symbol for run length / number of bits */
                    int i = (r << 4) + nbits;
                    if (!emit_bits(state, actbl.ehufco[i], actbl.ehufsi[i]))
                        return false;

                    /* Emit that number of bits of the value, if positive, */
                    /* or the complement of its magnitude, if negative. */
                    if (!emit_bits(state, temp2, nbits))
                        return false;

                    r = 0;
                }
            }

            /* If the last coef(s) were zero, emit an end-of-block code */
            if (r > 0)
            {
                if (!emit_bits(state, actbl.ehufco[0], actbl.ehufsi[0]))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Huffman coding optimization.
        /// 
        /// We first scan the supplied data and count the number of uses of each symbol
        /// that is to be Huffman-coded. (This process MUST agree with the code above.)
        /// Then we build a Huffman coding tree for the observed counts.
        /// Symbols which are not needed at all for the particular image are not
        /// assigned any code, which saves space in the DHT marker as well as in
        /// the compressed data.
        /// </summary>
        private void htest_one_block(short[] block, int last_dc_val, long[] dc_counts, long[] ac_counts)
        {
            /* Encode the DC coefficient difference per section F.1.2.1 */
            int temp = block[0] - last_dc_val;
            if (temp < 0)
                temp = -temp;

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

            /* Count the Huffman symbol for the number of bits */
            dc_counts[nbits]++;

            /* Encode the AC coefficients per section F.1.2.2 */
            int r = 0;          /* r = run length of zeros */
            for (int k = 1; k < JpegConstants.DCTSIZE2; k++)
            {
                temp = block[JpegUtils.jpeg_natural_order[k]];
                if (temp == 0)
                {
                    r++;
                }
                else
                {
                    /* if run length > 15, must emit special run-length-16 codes (0xF0) */
                    while (r > 15)
                    {
                        ac_counts[0xF0]++;
                        r -= 16;
                    }

                    /* Find the number of bits needed for the magnitude of the coefficient */
                    if (temp < 0)
                        temp = -temp;

                    /* Find the number of bits needed for the magnitude of the coefficient */
                    nbits = 1;      /* there must be at least one 1 bit */
                    while ((temp >>= 1) != 0)
                        nbits++;

                    /* Check for out-of-range coefficient values */
                    if (nbits > MAX_HUFFMAN_COEF_BITS)
                        m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_DCT_COEF);

                    /* Count Huffman symbol for run length / number of bits */
                    ac_counts[(r << 4) + nbits]++;

                    r = 0;
                }
            }

            /* If the last coef(s) were zero, emit an end-of-block code */
            if (r > 0)
                ac_counts[0]++;
        }

        private bool emit_byte(int val)
        {
            return m_cinfo.m_dest.emit_byte(val);
        }

        /// <summary>
        /// Only the right 24 bits of put_buffer are used; the valid bits are
        /// left-justified in this part.  At most 16 bits can be passed to emit_bits
        /// in one call, and we never retain more than 7 bits in put_buffer
        /// between calls, so 24 bits are sufficient.
        /// </summary>
        private bool emit_bits(savable_state state, int code, int size)
        {
            // Emit some bits; return true if successful, false if must suspend
            /* This routine is heavily used, so it's worth coding tightly. */
            int put_buffer = code;
            int put_bits = state.put_bits;

            /* if size is 0, caller used an invalid Huffman table entry */
            if (size == 0)
                m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_HUFF_MISSING_CODE);

            put_buffer &= (1 << size) - 1; /* mask off any extra bits in code */
            put_bits += size;       /* new number of bits in buffer */
            put_buffer <<= 24 - put_bits; /* align incoming bits */
            put_buffer |= state.put_buffer; /* and merge with old buffer contents */

            while (put_bits >= 8)
            {
                int c = (put_buffer >> 16) & 0xFF;
                if (!emit_byte(c))
                    return false;

                if (c == 0xFF)
                {
                    /* need to stuff a zero byte? */
                    if (!emit_byte(0))
                        return false;
                }

                put_buffer <<= 8;
                put_bits -= 8;
            }

            state.put_buffer = put_buffer; /* update state variables */
            state.put_bits = put_bits;

            return true;
        }

        private bool flush_bits(savable_state state)
        {
            if (!emit_bits(state, 0x7F, 7)) /* fill any partial byte with ones */
                return false;

            state.put_buffer = 0;  /* and reset bit-buffer to empty */
            state.put_bits = 0;
            return true;
        }

        /// <summary>
        /// Emit a restart marker and resynchronize predictions.
        /// </summary>
        private bool emit_restart(savable_state state, int restart_num)
        {
            if (!flush_bits(state))
                return false;

            if (!emit_byte(0xFF))
                return false;

            if (!emit_byte((int)(JPEG_MARKER.RST0 + restart_num)))
                return false;

            /* Re-initialize DC predictions to 0 */
            for (int ci = 0; ci < m_cinfo.m_comps_in_scan; ci++)
                state.last_dc_val[ci] = 0;

            /* The restart counter is not updated until we successfully write the MCU. */
            return true;
        }
    }
}
