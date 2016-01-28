/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * Copyright (C) 1994-1996, Thomas G. Lane.
 * This file is part of the Independent JPEG Group's software.
 * For conditions of distribution and use, see the accompanying README file.
 *
 */

/*
 * This file contains library routines for transcoding compression,
 * that is, writing raw DCT coefficient arrays to an output JPEG file.
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace BitMiracle.LibJpeg.Classic.Internal
{
    /// <summary>
    /// This is a special implementation of the coefficient
    /// buffer controller.  This is similar to jccoefct.c, but it handles only
    /// output from presupplied virtual arrays.  Furthermore, we generate any
    /// dummy padding blocks on-the-fly rather than expecting them to be present
    /// in the arrays.
    /// </summary>
    class my_trans_c_coef_controller : jpeg_c_coef_controller
    {
        private jpeg_compress_struct m_cinfo;

        private int m_iMCU_row_num;    /* iMCU row # within image */
        private int m_mcu_ctr;     /* counts MCUs processed in current row */
        private int m_MCU_vert_offset;        /* counts MCU rows within iMCU row */
        private int m_MCU_rows_per_iMCU_row;  /* number of such rows needed */

        /* Virtual block array for each component. */
        private jvirt_array<JBLOCK>[] m_whole_image;

        /* Workspace for constructing dummy blocks at right/bottom edges. */
        private JBLOCK[][] m_dummy_buffer = new JBLOCK[JpegConstants.C_MAX_BLOCKS_IN_MCU][];

        /// <summary>
        /// Initialize coefficient buffer controller.
        /// 
        /// Each passed coefficient array must be the right size for that
        /// coefficient: width_in_blocks wide and height_in_blocks high,
        /// with unit height at least v_samp_factor.
        /// </summary>
        public my_trans_c_coef_controller(jpeg_compress_struct cinfo, jvirt_array<JBLOCK>[] coef_arrays)
        {
            m_cinfo = cinfo;

            /* Save pointer to virtual arrays */
            m_whole_image = coef_arrays;

            /* Allocate and pre-zero space for dummy DCT blocks. */
            JBLOCK[] buffer = new JBLOCK[JpegConstants.C_MAX_BLOCKS_IN_MCU];
            for (int i = 0; i < JpegConstants.C_MAX_BLOCKS_IN_MCU; i++)
                buffer[i] = new JBLOCK();

            for (int i = 0; i < JpegConstants.C_MAX_BLOCKS_IN_MCU; i++)
            {
                m_dummy_buffer[i] = new JBLOCK[JpegConstants.C_MAX_BLOCKS_IN_MCU - i];
                for (int j = i; j < JpegConstants.C_MAX_BLOCKS_IN_MCU; j++)
                    m_dummy_buffer[i][j - i] = buffer[j];
            }
        }

        /// <summary>
        /// Initialize for a processing pass.
        /// </summary>
        public virtual void start_pass(J_BUF_MODE pass_mode)
        {
            if (pass_mode != J_BUF_MODE.JBUF_CRANK_DEST)
                m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_BUFFER_MODE);

            m_iMCU_row_num = 0;
            start_iMCU_row();
        }

        /// <summary>
        /// Process some data.
        /// We process the equivalent of one fully interleaved MCU row ("iMCU" row)
        /// per call, ie, v_samp_factor block rows for each component in the scan.
        /// The data is obtained from the virtual arrays and fed to the entropy coder.
        /// Returns true if the iMCU row is completed, false if suspended.
        /// 
        /// NB: input_buf is ignored; it is likely to be a null pointer.
        /// </summary>
        public virtual bool compress_data(byte[][][] input_buf)
        {
            /* Align the virtual buffers for the components used in this scan. */
            JBLOCK[][][] buffer = new JBLOCK[JpegConstants.MAX_COMPS_IN_SCAN][][];
            for (int ci = 0; ci < m_cinfo.m_comps_in_scan; ci++)
            {
                jpeg_component_info componentInfo = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[ci]];
                buffer[ci] = m_whole_image[componentInfo.Component_index].Access(
                    m_iMCU_row_num * componentInfo.V_samp_factor, componentInfo.V_samp_factor);
            }

            /* Loop to process one whole iMCU row */
            int last_MCU_col = m_cinfo.m_MCUs_per_row - 1;
            int last_iMCU_row = m_cinfo.m_total_iMCU_rows - 1;
            JBLOCK[][] MCU_buffer = new JBLOCK[JpegConstants.C_MAX_BLOCKS_IN_MCU][];
            for (int yoffset = m_MCU_vert_offset; yoffset < m_MCU_rows_per_iMCU_row; yoffset++)
            {
                for (int MCU_col_num = m_mcu_ctr; MCU_col_num < m_cinfo.m_MCUs_per_row; MCU_col_num++)
                {
                    /* Construct list of pointers to DCT blocks belonging to this MCU */
                    int blkn = 0;           /* index of current DCT block within MCU */
                    for (int ci = 0; ci < m_cinfo.m_comps_in_scan; ci++)
                    {
                        jpeg_component_info componentInfo = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[ci]];
                        int start_col = MCU_col_num * componentInfo.MCU_width;
                        int blockcnt = (MCU_col_num < last_MCU_col) ? componentInfo.MCU_width : componentInfo.last_col_width;
                        for (int yindex = 0; yindex < componentInfo.MCU_height; yindex++)
                        {
                            int xindex = 0;
                            if (m_iMCU_row_num < last_iMCU_row || yindex + yoffset < componentInfo.last_row_height)
                            {
                                /* Fill in pointers to real blocks in this row */
                                for (xindex = 0; xindex < blockcnt; xindex++)
                                {
                                    int bufLength = buffer[ci][yindex + yoffset].Length;
                                    int start = start_col + xindex;
                                    MCU_buffer[blkn] = new JBLOCK[bufLength - start];
                                    for (int j = start; j < bufLength; j++)
                                        MCU_buffer[blkn][j - start] = buffer[ci][yindex + yoffset][j];

                                    blkn++;
                                }
                            }
                            else
                            {
                                /* At bottom of image, need a whole row of dummy blocks */
                                xindex = 0;
                            }

                            /* Fill in any dummy blocks needed in this row.
                            * Dummy blocks are filled in the same way as in jccoefct.c:
                            * all zeroes in the AC entries, DC entries equal to previous
                            * block's DC value.  The init routine has already zeroed the
                            * AC entries, so we need only set the DC entries correctly.
                            */
                            for (; xindex < componentInfo.MCU_width; xindex++)
                            {
                                MCU_buffer[blkn] = m_dummy_buffer[blkn];
                                MCU_buffer[blkn][0][0] = MCU_buffer[blkn - 1][0][0];
                                blkn++;
                            }
                        }
                    }
                
                    /* Try to write the MCU. */
                    if (!m_cinfo.m_entropy.encode_mcu(MCU_buffer))
                    {
                        /* Suspension forced; update state counters and exit */
                        m_MCU_vert_offset = yoffset;
                        m_mcu_ctr = MCU_col_num;
                        return false;
                    }
                }

                /* Completed an MCU row, but perhaps not an iMCU row */
                m_mcu_ctr = 0;
            }

            /* Completed the iMCU row, advance counters for next one */
            m_iMCU_row_num++;
            start_iMCU_row();
            return true;
        }

        /// <summary>
        /// Reset within-iMCU-row counters for a new row
        /// </summary>
        private void start_iMCU_row()
        {
            /* In an interleaved scan, an MCU row is the same as an iMCU row.
            * In a noninterleaved scan, an iMCU row has v_samp_factor MCU rows.
            * But at the bottom of the image, process only what's left.
            */
            if (m_cinfo.m_comps_in_scan > 1)
            {
                m_MCU_rows_per_iMCU_row = 1;
            }
            else
            {
                if (m_iMCU_row_num < (m_cinfo.m_total_iMCU_rows - 1))
                    m_MCU_rows_per_iMCU_row = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[0]].V_samp_factor;
                else
                    m_MCU_rows_per_iMCU_row = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[0]].last_row_height;
            }

            m_mcu_ctr = 0;
            m_MCU_vert_offset = 0;
        }
    }
}
