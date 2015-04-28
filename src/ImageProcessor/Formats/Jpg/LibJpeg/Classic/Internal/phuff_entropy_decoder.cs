/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * Copyright (C) 1994-1996, Thomas G. Lane.
 * This file is part of the Independent JPEG Group's software.
 * For conditions of distribution and use, see the accompanying README file.
 *
 */

/*
 * This file contains Huffman entropy decoding routines for progressive JPEG.
 *
 * Much of the complexity here has to do with supporting input suspension.
 * If the data source module demands suspension, we want to be able to back
 * up to the start of the current MCU.  To do this, we copy state variables
 * into local working storage, and update them back to the permanent
 * storage only upon successful completion of an MCU.
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace BitMiracle.LibJpeg.Classic.Internal
{
    /// <summary>
    /// Expanded entropy decoder object for progressive Huffman decoding.
    /// 
    /// The savable_state subrecord contains fields that change within an MCU,
    /// but must not be updated permanently until we complete the MCU.
    /// </summary>
    class phuff_entropy_decoder : jpeg_entropy_decoder
    {
        private class savable_state
        {
            //savable_state operator=(savable_state src);
            public int EOBRUN;            /* remaining EOBs in EOBRUN */
            public int[] last_dc_val = new int[JpegConstants.MAX_COMPS_IN_SCAN]; /* last DC coef for each component */

            public void Assign(savable_state ss)
            {
                EOBRUN = ss.EOBRUN;
                Buffer.BlockCopy(ss.last_dc_val, 0, last_dc_val, 0, last_dc_val.Length * sizeof(int));
            }
        }

        private enum MCUDecoder
        {
            mcu_DC_first_decoder,
            mcu_AC_first_decoder,
            mcu_DC_refine_decoder,
            mcu_AC_refine_decoder
        }

        private MCUDecoder m_decoder;

        /* These fields are loaded into local variables at start of each MCU.
        * In case of suspension, we exit WITHOUT updating them.
        */
        private bitread_perm_state m_bitstate;    /* Bit buffer at start of MCU */
        private savable_state m_saved = new savable_state();        /* Other state at start of MCU */

        /* These fields are NOT loaded into local working state. */
        private int m_restarts_to_go;    /* MCUs left in this restart interval */

        /* Pointers to derived tables (these workspaces have image lifespan) */
        private d_derived_tbl[] m_derived_tbls = new d_derived_tbl[JpegConstants.NUM_HUFF_TBLS];

        private d_derived_tbl m_ac_derived_tbl; /* active table during an AC scan */

        public phuff_entropy_decoder(jpeg_decompress_struct cinfo)
        {
            m_cinfo = cinfo;

            /* Mark derived tables unallocated */
            for (int i = 0; i < JpegConstants.NUM_HUFF_TBLS; i++)
                m_derived_tbls[i] = null;

            /* Create progression status table */
            cinfo.m_coef_bits = new int[cinfo.m_num_components][];
            for (int i = 0; i < cinfo.m_num_components; i++)
                cinfo.m_coef_bits[i] = new int[JpegConstants.DCTSIZE2];

            for (int ci = 0; ci < cinfo.m_num_components; ci++)
            {
                for (int i = 0; i < JpegConstants.DCTSIZE2; i++)
                    cinfo.m_coef_bits[ci][i] = -1;
            }
        }

        /// <summary>
        /// Initialize for a Huffman-compressed scan.
        /// </summary>
        public override void start_pass()
        {
            /* Validate scan parameters */
            bool bad = false;
            bool is_DC_band = (m_cinfo.m_Ss == 0);
            if (is_DC_band)
            {
                if (m_cinfo.m_Se != 0)
                    bad = true;
            }
            else
            {
                /* need not check Ss/Se < 0 since they came from unsigned bytes */
                if (m_cinfo.m_Ss > m_cinfo.m_Se || m_cinfo.m_Se >= JpegConstants.DCTSIZE2)
                    bad = true;

                /* AC scans may have only one component */
                if (m_cinfo.m_comps_in_scan != 1)
                    bad = true;
            }

            if (m_cinfo.m_Ah != 0)
            {
                /* Successive approximation refinement scan: must have Al = Ah-1. */
                if (m_cinfo.m_Al != m_cinfo.m_Ah - 1)
                    bad = true;
            }

            if (m_cinfo.m_Al > 13)
            {
                /* need not check for < 0 */
                bad = true;
            }

            /* Arguably the maximum Al value should be less than 13 for 8-bit precision,
             * but the spec doesn't say so, and we try to be liberal about what we
             * accept.  Note: large Al values could result in out-of-range DC
             * coefficients during early scans, leading to bizarre displays due to
             * overflows in the IDCT math.  But we won't crash.
             */
            if (bad)
                m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_PROGRESSION, m_cinfo.m_Ss, m_cinfo.m_Se, m_cinfo.m_Ah, m_cinfo.m_Al);

            /* Update progression status, and verify that scan order is legal.
             * Note that inter-scan inconsistencies are treated as warnings
             * not fatal errors ... not clear if this is right way to behave.
             */
            for (int ci = 0; ci < m_cinfo.m_comps_in_scan; ci++)
            {
                int cindex = m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[ci]].Component_index;
                if (!is_DC_band && m_cinfo.m_coef_bits[cindex][0] < 0) /* AC without prior DC scan */
                    m_cinfo.WARNMS(J_MESSAGE_CODE.JWRN_BOGUS_PROGRESSION, cindex, 0);

                for (int coefi = m_cinfo.m_Ss; coefi <= m_cinfo.m_Se; coefi++)
                {
                    int expected = m_cinfo.m_coef_bits[cindex][coefi];
                    if (expected < 0)
                        expected = 0;

                    if (m_cinfo.m_Ah != expected)
                        m_cinfo.WARNMS(J_MESSAGE_CODE.JWRN_BOGUS_PROGRESSION, cindex, coefi);

                    m_cinfo.m_coef_bits[cindex][coefi] = m_cinfo.m_Al;
                }
            }

            /* Select MCU decoding routine */
            if (m_cinfo.m_Ah == 0)
            {
                if (is_DC_band)
                    m_decoder = MCUDecoder.mcu_DC_first_decoder;
                else
                    m_decoder = MCUDecoder.mcu_AC_first_decoder;
            }
            else
            {
                if (is_DC_band)
                    m_decoder = MCUDecoder.mcu_DC_refine_decoder;
                else
                    m_decoder = MCUDecoder.mcu_AC_refine_decoder;
            }

            for (int ci = 0; ci < m_cinfo.m_comps_in_scan; ci++)
            {
                jpeg_component_info componentInfo = m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[ci]];
                /* Make sure requested tables are present, and compute derived tables.
                 * We may build same derived table more than once, but it's not expensive.
                 */
                if (is_DC_band)
                {
                    if (m_cinfo.m_Ah == 0)
                    {
                        /* DC refinement needs no table */
                        jpeg_make_d_derived_tbl(true, componentInfo.Dc_tbl_no, ref m_derived_tbls[componentInfo.Dc_tbl_no]);
                    }
                }
                else
                {
                    jpeg_make_d_derived_tbl(false, componentInfo.Ac_tbl_no, ref m_derived_tbls[componentInfo.Ac_tbl_no]);

                    /* remember the single active table */
                    m_ac_derived_tbl = m_derived_tbls[componentInfo.Ac_tbl_no];
                }

                /* Initialize DC predictions to 0 */
                m_saved.last_dc_val[ci] = 0;
            }

            /* Initialize bitread state variables */
            m_bitstate.bits_left = 0;
            m_bitstate.get_buffer = 0; /* unnecessary, but keeps Purify quiet */
            m_insufficient_data = false;

            /* Initialize private state variables */
            m_saved.EOBRUN = 0;

            /* Initialize restart counter */
            m_restarts_to_go = m_cinfo.m_restart_interval;
        }

        public override bool decode_mcu(JBLOCK[] MCU_data)
        {
            switch (m_decoder)
            {
                case MCUDecoder.mcu_DC_first_decoder:
                    return decode_mcu_DC_first(MCU_data);
                case MCUDecoder.mcu_AC_first_decoder:
                    return decode_mcu_AC_first(MCU_data);
                case MCUDecoder.mcu_DC_refine_decoder:
                    return decode_mcu_DC_refine(MCU_data);
                case MCUDecoder.mcu_AC_refine_decoder:
                    return decode_mcu_AC_refine(MCU_data);
            }

            m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
            return false;
        }

        /*
         * Huffman MCU decoding.
         * Each of these routines decodes and returns one MCU's worth of
         * Huffman-compressed coefficients. 
         * The coefficients are reordered from zigzag order into natural array order,
         * but are not dequantized.
         *
         * The i'th block of the MCU is stored into the block pointed to by
         * MCU_data[i].  WE ASSUME THIS AREA IS INITIALLY ZEROED BY THE CALLER.
         *
         * We return false if data source requested suspension.  In that case no
         * changes have been made to permanent state.  (Exception: some output
         * coefficients may already have been assigned.  This is harmless for
         * spectral selection, since we'll just re-assign them on the next call.
         * Successive approximation AC refinement has to be more careful, however.)
         */

        /// <summary>
        /// MCU decoding for DC initial scan (either spectral selection,
        /// or first pass of successive approximation).
        /// </summary>
        private bool decode_mcu_DC_first(JBLOCK[] MCU_data)
        {
            /* Process restart marker if needed; may have to suspend */
            if (m_cinfo.m_restart_interval != 0)
            {
                if (m_restarts_to_go == 0)
                {
                    if (!process_restart())
                        return false;
                }
            }

            /* If we've run out of data, just leave the MCU set to zeroes.
             * This way, we return uniform gray for the remainder of the segment.
             */
            if (!m_insufficient_data)
            {
                /* Load up working state */
                int get_buffer;
                int bits_left;
                bitread_working_state br_state = new bitread_working_state();
                BITREAD_LOAD_STATE(m_bitstate, out get_buffer, out bits_left, ref br_state);
                savable_state state = new savable_state();
                state.Assign(m_saved);

                /* Outer loop handles each block in the MCU */
                for (int blkn = 0; blkn < m_cinfo.m_blocks_in_MCU; blkn++)
                {
                    int ci = m_cinfo.m_MCU_membership[blkn];

                    /* Decode a single block's worth of coefficients */

                    /* Section F.2.2.1: decode the DC coefficient difference */
                    int s;
                    if (!HUFF_DECODE(out s, ref br_state, m_derived_tbls[m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[ci]].Dc_tbl_no], ref get_buffer, ref bits_left))
                        return false;

                    if (s != 0)
                    {
                        if (!CHECK_BIT_BUFFER(ref br_state, s, ref get_buffer, ref bits_left))
                            return false;

                        int r = GET_BITS(s, get_buffer, ref bits_left);
                        s = HUFF_EXTEND(r, s);
                    }

                    /* Convert DC difference to actual value, update last_dc_val */
                    s += state.last_dc_val[ci];
                    state.last_dc_val[ci] = s;

                    /* Scale and output the coefficient (assumes jpeg_natural_order[0]=0) */
                    MCU_data[blkn][0] = (short)(s << m_cinfo.m_Al);
                }

                /* Completed MCU, so update state */
                BITREAD_SAVE_STATE(ref m_bitstate, get_buffer, bits_left);
                m_saved.Assign(state);
            }

            /* Account for restart interval (no-op if not using restarts) */
            m_restarts_to_go--;

            return true;
        }

        /// <summary>
        /// MCU decoding for AC initial scan (either spectral selection,
        /// or first pass of successive approximation).
        /// </summary>
        private bool decode_mcu_AC_first(JBLOCK[] MCU_data)
        {
            /* Process restart marker if needed; may have to suspend */
            if (m_cinfo.m_restart_interval != 0)
            {
                if (m_restarts_to_go == 0)
                {
                    if (!process_restart())
                        return false;
                }
            }

            /* If we've run out of data, just leave the MCU set to zeroes.
             * This way, we return uniform gray for the remainder of the segment.
             */
            if (!m_insufficient_data)
            {
                /* Load up working state.
                 * We can avoid loading/saving bitread state if in an EOB run.
                 */
                int EOBRUN = m_saved.EOBRUN; /* only part of saved state we need */

                /* There is always only one block per MCU */

                if (EOBRUN > 0)
                {
                    /* if it's a band of zeroes... */
                    /* ...process it now (we do nothing) */
                    EOBRUN--;
                }
                else
                {
                    int get_buffer;
                    int bits_left;
                    bitread_working_state br_state = new bitread_working_state();
                    BITREAD_LOAD_STATE(m_bitstate, out get_buffer, out bits_left, ref br_state);

                    for (int k = m_cinfo.m_Ss; k <= m_cinfo.m_Se; k++)
                    {
                        int s;
                        if (!HUFF_DECODE(out s, ref br_state, m_ac_derived_tbl, ref get_buffer, ref bits_left))
                            return false;

                        int r = s >> 4;
                        s &= 15;
                        if (s != 0)
                        {
                            k += r;

                            if (!CHECK_BIT_BUFFER(ref br_state, s, ref get_buffer, ref bits_left))
                                return false;

                            r = GET_BITS(s, get_buffer, ref bits_left);
                            s = HUFF_EXTEND(r, s);

                            /* Scale and output coefficient in natural (dezigzagged) order */
                            MCU_data[0][JpegUtils.jpeg_natural_order[k]] = (short) (s << m_cinfo.m_Al);
                        }
                        else
                        {
                            if (r == 15)
                            {
                                /* ZRL */
                                k += 15;        /* skip 15 zeroes in band */
                            }
                            else
                            {
                                /* EOBr, run length is 2^r + appended bits */
                                EOBRUN = 1 << r;
                                if (r != 0)
                                {
                                    /* EOBr, r > 0 */
                                    if (!CHECK_BIT_BUFFER(ref br_state, r, ref get_buffer, ref bits_left))
                                        return false;

                                    r = GET_BITS(r, get_buffer, ref bits_left);
                                    EOBRUN += r;
                                }

                                EOBRUN--;       /* this band is processed at this moment */
                                break;      /* force end-of-band */
                            }
                        }
                    }

                    BITREAD_SAVE_STATE(ref m_bitstate, get_buffer, bits_left);
                }

                /* Completed MCU, so update state */
                m_saved.EOBRUN = EOBRUN; /* only part of saved state we need */
            }

            /* Account for restart interval (no-op if not using restarts) */
            m_restarts_to_go--;

            return true;
        }

        /// <summary>
        /// MCU decoding for DC successive approximation refinement scan.
        /// Note: we assume such scans can be multi-component, although the spec
        /// is not very clear on the point.
        /// </summary>
        private bool decode_mcu_DC_refine(JBLOCK[] MCU_data)
        {
            /* Process restart marker if needed; may have to suspend */
            if (m_cinfo.m_restart_interval != 0)
            {
                if (m_restarts_to_go == 0)
                {
                    if (!process_restart())
                        return false;
                }
            }

            /* Not worth the cycles to check insufficient_data here,
             * since we will not change the data anyway if we read zeroes.
             */

            /* Load up working state */
            int get_buffer;
            int bits_left;
            bitread_working_state br_state = new bitread_working_state();
            BITREAD_LOAD_STATE(m_bitstate, out get_buffer, out bits_left, ref br_state);

            /* Outer loop handles each block in the MCU */

            for (int blkn = 0; blkn < m_cinfo.m_blocks_in_MCU; blkn++)
            {
                /* Encoded data is simply the next bit of the two's-complement DC value */
                if (!CHECK_BIT_BUFFER(ref br_state, 1, ref get_buffer, ref bits_left))
                    return false;

                if (GET_BITS(1, get_buffer, ref bits_left) != 0)
                {
                    /* 1 in the bit position being coded */
                    MCU_data[blkn][0] |= (short)(1 << m_cinfo.m_Al);
                }

                /* Note: since we use |=, repeating the assignment later is safe */
            }

            /* Completed MCU, so update state */
            BITREAD_SAVE_STATE(ref m_bitstate, get_buffer, bits_left);

            /* Account for restart interval (no-op if not using restarts) */
            m_restarts_to_go--;

            return true;
        }

        // There is always only one block per MCU
        private bool decode_mcu_AC_refine(JBLOCK[] MCU_data)
        {
            int p1 = 1 << m_cinfo.m_Al;    /* 1 in the bit position being coded */
            int m1 = -1 << m_cinfo.m_Al; /* -1 in the bit position being coded */

            /* Process restart marker if needed; may have to suspend */
            if (m_cinfo.m_restart_interval != 0)
            {
                if (m_restarts_to_go == 0)
                {
                    if (!process_restart())
                        return false;
                }
            }

            /* If we've run out of data, don't modify the MCU.
             */
            if (!m_insufficient_data)
            {
                /* Load up working state */
                int get_buffer;
                int bits_left;
                bitread_working_state br_state = new bitread_working_state();
                BITREAD_LOAD_STATE(m_bitstate, out get_buffer, out bits_left, ref br_state);
                int EOBRUN = m_saved.EOBRUN; /* only part of saved state we need */

                /* If we are forced to suspend, we must undo the assignments to any newly
                 * nonzero coefficients in the block, because otherwise we'd get confused
                 * next time about which coefficients were already nonzero.
                 * But we need not undo addition of bits to already-nonzero coefficients;
                 * instead, we can test the current bit to see if we already did it.
                 */
                int num_newnz = 0;
                int[] newnz_pos = new int[JpegConstants.DCTSIZE2];

                /* initialize coefficient loop counter to start of band */
                int k = m_cinfo.m_Ss;

                if (EOBRUN == 0)
                {
                    for (; k <= m_cinfo.m_Se; k++)
                    {
                        int s;
                        if (!HUFF_DECODE(out s, ref br_state, m_ac_derived_tbl, ref get_buffer, ref bits_left))
                        {
                            undo_decode_mcu_AC_refine(MCU_data, newnz_pos, num_newnz);
                            return false;
                        }

                        int r = s >> 4;
                        s &= 15;
                        if (s != 0)
                        {
                            if (s != 1)
                            {
                                /* size of new coef should always be 1 */
                                m_cinfo.WARNMS(J_MESSAGE_CODE.JWRN_HUFF_BAD_CODE);
                            }

                            if (!CHECK_BIT_BUFFER(ref br_state, 1, ref get_buffer, ref bits_left))
                            {
                                undo_decode_mcu_AC_refine(MCU_data, newnz_pos, num_newnz);
                                return false;
                            }

                            if (GET_BITS(1, get_buffer, ref bits_left) != 0)
                            {
                                /* newly nonzero coef is positive */
                                s = p1;
                            }
                            else
                            {   
                                /* newly nonzero coef is negative */
                                s = m1;
                            }
                        }
                        else
                        {
                            if (r != 15)
                            {
                                EOBRUN = 1 << r;    /* EOBr, run length is 2^r + appended bits */
                                if (r != 0)
                                {
                                    if (!CHECK_BIT_BUFFER(ref br_state, r, ref get_buffer, ref bits_left))
                                    {
                                        undo_decode_mcu_AC_refine(MCU_data, newnz_pos, num_newnz);
                                        return false;
                                    }

                                    r = GET_BITS(r, get_buffer, ref bits_left);
                                    EOBRUN += r;
                                }
                                break;      /* rest of block is handled by EOB logic */
                            }
                            /* note s = 0 for processing ZRL */
                        }
                        /* Advance over already-nonzero coefs and r still-zero coefs,
                         * appending correction bits to the nonzeroes.  A correction bit is 1
                         * if the absolute value of the coefficient must be increased.
                         */
                        do
                        {
                            int blockIndex = JpegUtils.jpeg_natural_order[k];
                            short thiscoef = MCU_data[0][blockIndex];
                            if (thiscoef != 0)
                            {
                                if (!CHECK_BIT_BUFFER(ref br_state, 1, ref get_buffer, ref bits_left))
                                {
                                    undo_decode_mcu_AC_refine(MCU_data, newnz_pos, num_newnz);
                                    return false;
                                }

                                if (GET_BITS(1, get_buffer, ref bits_left) != 0)
                                {
                                    if ((thiscoef & p1) == 0)
                                    {
                                        /* do nothing if already set it */
                                        if (thiscoef >= 0)
                                            MCU_data[0][blockIndex] += (short)p1;
                                        else
                                            MCU_data[0][blockIndex] += (short)m1;
                                    }
                                }
                            }
                            else
                            {
                                if (--r < 0)
                                    break;      /* reached target zero coefficient */
                            }

                            k++;
                        }
                        while (k <= m_cinfo.m_Se);

                        if (s != 0)
                        {
                            int pos = JpegUtils.jpeg_natural_order[k];
                            
                            /* Output newly nonzero coefficient */
                            MCU_data[0][pos] = (short) s;

                            /* Remember its position in case we have to suspend */
                            newnz_pos[num_newnz++] = pos;
                        }
                    }
                }

                if (EOBRUN > 0)
                {
                    /* Scan any remaining coefficient positions after the end-of-band
                     * (the last newly nonzero coefficient, if any).  Append a correction
                     * bit to each already-nonzero coefficient.  A correction bit is 1
                     * if the absolute value of the coefficient must be increased.
                     */
                    for (; k <= m_cinfo.m_Se; k++)
                    {
                        int blockIndex = JpegUtils.jpeg_natural_order[k];
                        short thiscoef = MCU_data[0][blockIndex];
                        if (thiscoef != 0)
                        {
                            if (!CHECK_BIT_BUFFER(ref br_state, 1, ref get_buffer, ref bits_left))
                            {
                                //undo_decode_mcu_AC_refine(MCU_data[0], newnz_pos, num_newnz);
                                undo_decode_mcu_AC_refine(MCU_data, newnz_pos, num_newnz);
                                return false;
                            }

                            if (GET_BITS(1, get_buffer, ref bits_left) != 0)
                            {
                                if ((thiscoef & p1) == 0)
                                {
                                    /* do nothing if already changed it */
                                    if (thiscoef >= 0)
                                        MCU_data[0][blockIndex] += (short)p1;
                                    else
                                        MCU_data[0][blockIndex] += (short)m1;
                                }
                            }
                        }
                    }

                    /* Count one block completed in EOB run */
                    EOBRUN--;
                }

                /* Completed MCU, so update state */
                BITREAD_SAVE_STATE(ref m_bitstate, get_buffer, bits_left);
                m_saved.EOBRUN = EOBRUN; /* only part of saved state we need */
            }

            /* Account for restart interval (no-op if not using restarts) */
            m_restarts_to_go--;

            return true;
        }

        /// <summary>
        /// Check for a restart marker and resynchronize decoder.
        /// Returns false if must suspend.
        /// </summary>
        private bool process_restart()
        {
            /* Throw away any unused bits remaining in bit buffer; */
            /* include any full bytes in next_marker's count of discarded bytes */
            m_cinfo.m_marker.SkipBytes(m_bitstate.bits_left / 8);
            m_bitstate.bits_left = 0;

            /* Advance past the RSTn marker */
            if (!m_cinfo.m_marker.read_restart_marker())
                return false;

            /* Re-initialize DC predictions to 0 */
            for (int ci = 0; ci < m_cinfo.m_comps_in_scan; ci++)
                m_saved.last_dc_val[ci] = 0;

            /* Re-init EOB run count, too */
            m_saved.EOBRUN = 0;

            /* Reset restart counter */
            m_restarts_to_go = m_cinfo.m_restart_interval;

            /* Reset out-of-data flag, unless read_restart_marker left us smack up
             * against a marker.  In that case we will end up treating the next data
             * segment as empty, and we can avoid producing bogus output pixels by
             * leaving the flag set.
             */
            if (m_cinfo.m_unread_marker == 0)
                m_insufficient_data = false;

            return true;
        }

        /// <summary>
        /// MCU decoding for AC successive approximation refinement scan.
        /// </summary>
        private static void undo_decode_mcu_AC_refine(JBLOCK[] block, int[] newnz_pos, int num_newnz)
        {
            /* Re-zero any output coefficients that we made newly nonzero */
            while (num_newnz > 0)
                block[0][newnz_pos[--num_newnz]] = 0;
        }
    }
}
