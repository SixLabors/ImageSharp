/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * Copyright (C) 1994-1996, Thomas G. Lane.
 * This file is part of the Independent JPEG Group's software.
 * For conditions of distribution and use, see the accompanying README file.
 *
 */

/*
 * This file contains Huffman entropy decoding routines.
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
    /// Expanded entropy decoder object for Huffman decoding.
    /// 
    /// The savable_state subrecord contains fields that change within an MCU,
    /// but must not be updated permanently until we complete the MCU.
    /// </summary>
    class huff_entropy_decoder : jpeg_entropy_decoder
    {
        private class savable_state
        {
            public int[] last_dc_val = new int[JpegConstants.MAX_COMPS_IN_SCAN]; /* last DC coef for each component */

            public void Assign(savable_state ss)
            {
                Buffer.BlockCopy(ss.last_dc_val, 0, last_dc_val, 0, last_dc_val.Length * sizeof(int));
            }
        }

        /* These fields are loaded into local variables at start of each MCU.
        * In case of suspension, we exit WITHOUT updating them.
        */
        private bitread_perm_state m_bitstate;    /* Bit buffer at start of MCU */
        private savable_state m_saved = new savable_state();        /* Other state at start of MCU */

        /* These fields are NOT loaded into local working state. */
        private int m_restarts_to_go;    /* MCUs left in this restart interval */

        /* Pointers to derived tables (these workspaces have image lifespan) */
        private d_derived_tbl[] m_dc_derived_tbls = new d_derived_tbl[JpegConstants.NUM_HUFF_TBLS];
        private d_derived_tbl[] m_ac_derived_tbls = new d_derived_tbl[JpegConstants.NUM_HUFF_TBLS];

        /* Precalculated info set up by start_pass for use in decode_mcu: */

        /* Pointers to derived tables to be used for each block within an MCU */
        private d_derived_tbl[] m_dc_cur_tbls = new d_derived_tbl[JpegConstants.D_MAX_BLOCKS_IN_MCU];
        private d_derived_tbl[] m_ac_cur_tbls = new d_derived_tbl[JpegConstants.D_MAX_BLOCKS_IN_MCU];

        /* Whether we care about the DC and AC coefficient values for each block */
        private bool[] m_dc_needed = new bool[JpegConstants.D_MAX_BLOCKS_IN_MCU];
        private bool[] m_ac_needed = new bool[JpegConstants.D_MAX_BLOCKS_IN_MCU];

        public huff_entropy_decoder(jpeg_decompress_struct cinfo)
        {
            m_cinfo = cinfo;

            /* Mark tables unallocated */
            for (int i = 0; i < JpegConstants.NUM_HUFF_TBLS; i++)
                m_dc_derived_tbls[i] = m_ac_derived_tbls[i] = null;
        }

        /// <summary>
        /// Initialize for a Huffman-compressed scan.
        /// </summary>
        public override void start_pass()
        {
            /* Check that the scan parameters Ss, Se, Ah/Al are OK for sequential JPEG.
             * This ought to be an error condition, but we make it a warning because
             * there are some baseline files out there with all zeroes in these bytes.
             */
            if (m_cinfo.m_Ss != 0 || m_cinfo.m_Se != JpegConstants.DCTSIZE2 - 1 || m_cinfo.m_Ah != 0 || m_cinfo.m_Al != 0)
                m_cinfo.WARNMS(J_MESSAGE_CODE.JWRN_NOT_SEQUENTIAL);

            for (int ci = 0; ci < m_cinfo.m_comps_in_scan; ci++)
            {
                jpeg_component_info componentInfo = m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[ci]];
                int dctbl = componentInfo.Dc_tbl_no;
                int actbl = componentInfo.Ac_tbl_no;

                /* Compute derived values for Huffman tables */
                /* We may do this more than once for a table, but it's not expensive */
                jpeg_make_d_derived_tbl(true, dctbl, ref m_dc_derived_tbls[dctbl]);
                jpeg_make_d_derived_tbl(false, actbl, ref m_ac_derived_tbls[actbl]);

                /* Initialize DC predictions to 0 */
                m_saved.last_dc_val[ci] = 0;
            }

            /* Precalculate decoding info for each block in an MCU of this scan */
            for (int blkn = 0; blkn < m_cinfo.m_blocks_in_MCU; blkn++)
            {
                int ci = m_cinfo.m_MCU_membership[blkn];
                jpeg_component_info componentInfo = m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[ci]];

                /* Precalculate which table to use for each block */
                m_dc_cur_tbls[blkn] = m_dc_derived_tbls[componentInfo.Dc_tbl_no];
                m_ac_cur_tbls[blkn] = m_ac_derived_tbls[componentInfo.Ac_tbl_no];

                /* Decide whether we really care about the coefficient values */
                if (componentInfo.component_needed)
                {
                    m_dc_needed[blkn] = true;
                    /* we don't need the ACs if producing a 1/8th-size image */
                    m_ac_needed[blkn] = (componentInfo.DCT_scaled_size > 1);
                }
                else
                {
                    m_dc_needed[blkn] = m_ac_needed[blkn] = false;
                }
            }

            /* Initialize bitread state variables */
            m_bitstate.bits_left = 0;
            m_bitstate.get_buffer = 0;
            m_insufficient_data = false;

            /* Initialize restart counter */
            m_restarts_to_go = m_cinfo.m_restart_interval;
        }

        /// <summary>
        /// Decode and return one MCU's worth of Huffman-compressed coefficients.
        /// The coefficients are reordered from zigzag order into natural array order,
        /// but are not dequantized.
        /// 
        /// The i'th block of the MCU is stored into the block pointed to by
        /// MCU_data[i].  WE ASSUME THIS AREA HAS BEEN ZEROED BY THE CALLER.
        /// (Wholesale zeroing is usually a little faster than retail...)
        /// 
        /// Returns false if data source requested suspension.  In that case no
        /// changes have been made to permanent state.  (Exception: some output
        /// coefficients may already have been assigned.  This is harmless for
        /// this module, since we'll just re-assign them on the next call.)
        /// </summary>
        public override bool decode_mcu(JBLOCK[] MCU_data)
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
                    /* Decode a single block's worth of coefficients */

                    /* Section F.2.2.1: decode the DC coefficient difference */
                    int s;
                    if (!HUFF_DECODE(out s, ref br_state, m_dc_cur_tbls[blkn], ref get_buffer, ref bits_left))
                        return false;

                    if (s != 0)
                    {
                        if (!CHECK_BIT_BUFFER(ref br_state, s, ref get_buffer, ref bits_left))
                            return false;

                        int r = GET_BITS(s, get_buffer, ref bits_left);
                        s = HUFF_EXTEND(r, s);
                    }

                    if (m_dc_needed[blkn])
                    {
                        /* Convert DC difference to actual value, update last_dc_val */
                        int ci = m_cinfo.m_MCU_membership[blkn];
                        s += state.last_dc_val[ci];
                        state.last_dc_val[ci] = s;

                        /* Output the DC coefficient (assumes jpeg_natural_order[0] = 0) */
                        MCU_data[blkn][0] = (short) s;
                    }

                    if (m_ac_needed[blkn])
                    {
                        /* Section F.2.2.2: decode the AC coefficients */
                        /* Since zeroes are skipped, output area must be cleared beforehand */
                        for (int k = 1; k < JpegConstants.DCTSIZE2; k++)
                        {
                            if (!HUFF_DECODE(out s, ref br_state, m_ac_cur_tbls[blkn], ref get_buffer, ref bits_left))
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
                        
                                /* Output coefficient in natural (dezigzagged) order.
                                   * Note: the extra entries in jpeg_natural_order[] will save us
                                   * if k >= DCTSIZE2, which could happen if the data is corrupted.
                                   */
                                MCU_data[blkn][JpegUtils.jpeg_natural_order[k]] = (short) s;
                            }
                            else
                            {
                                if (r != 15)
                                    break;

                                k += 15;
                            }
                        }
                    }
                    else
                    {
                        /* Section F.2.2.2: decode the AC coefficients */
                        /* In this path we just discard the values */
                        for (int k = 1; k < JpegConstants.DCTSIZE2; k++)
                        {
                            if (!HUFF_DECODE(out s, ref br_state, m_ac_cur_tbls[blkn], ref get_buffer, ref bits_left))
                                return false;

                            int r = s >> 4;
                            s &= 15;

                            if (s != 0)
                            {
                                k += r;
                                if (!CHECK_BIT_BUFFER(ref br_state, s, ref get_buffer, ref bits_left))
                                    return false;

                                DROP_BITS(s, ref bits_left);
                            }
                            else
                            {
                                if (r != 15)
                                    break;
                                
                                k += 15;
                            }
                        }
                    }
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
    }
}
