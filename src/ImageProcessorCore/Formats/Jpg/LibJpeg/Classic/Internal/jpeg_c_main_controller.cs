/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * Copyright (C) 1994-1996, Thomas G. Lane.
 * This file is part of the Independent JPEG Group's software.
 * For conditions of distribution and use, see the accompanying README file.
 *
 */

/*
 * This file contains the main buffer controller for compression.
 * The main buffer lies between the pre-processor and the JPEG
 * compressor proper; it holds downsampled data in the JPEG colorspace.
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace BitMiracle.LibJpeg.Classic.Internal
{
    /// <summary>
    /// Main buffer control (downsampled-data buffer)
    /// </summary>
    class jpeg_c_main_controller
    {
        private jpeg_compress_struct m_cinfo;

        private int m_cur_iMCU_row;    /* number of current iMCU row */
        private int m_rowgroup_ctr;    /* counts row groups received in iMCU row */
        private bool m_suspended;     /* remember if we suspended output */

        /* If using just a strip buffer, this points to the entire set of buffers
        * (we allocate one for each component).  In the full-image case, this
        * points to the currently accessible strips of the virtual arrays.
        */
        private byte[][][] m_buffer = new byte[JpegConstants.MAX_COMPONENTS][][];

        public jpeg_c_main_controller(jpeg_compress_struct cinfo)
        {
            m_cinfo = cinfo;

            /* Allocate a strip buffer for each component */
            for (int ci = 0; ci < cinfo.m_num_components; ci++)
            {
                m_buffer[ci] = jpeg_common_struct.AllocJpegSamples(
                    cinfo.Component_info[ci].Width_in_blocks * JpegConstants.DCTSIZE,
                    cinfo.Component_info[ci].V_samp_factor * JpegConstants.DCTSIZE);
            }
        }

        // Initialize for a processing pass.
        public void start_pass(J_BUF_MODE pass_mode)
        {
            /* Do nothing in raw-data mode. */
            if (m_cinfo.m_raw_data_in)
                return;

            m_cur_iMCU_row = 0; /* initialize counters */
            m_rowgroup_ctr = 0;
            m_suspended = false;

            if (pass_mode != J_BUF_MODE.JBUF_PASS_THRU)
                m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_BUFFER_MODE);
        }

        /// <summary>
        /// Process some data.
        /// This routine handles the simple pass-through mode,
        /// where we have only a strip buffer.
        /// </summary>
        public void process_data(byte[][] input_buf, ref int in_row_ctr, int in_rows_avail)
        {
            while (m_cur_iMCU_row < m_cinfo.m_total_iMCU_rows)
            {
                /* Read input data if we haven't filled the main buffer yet */
                if (m_rowgroup_ctr < JpegConstants.DCTSIZE)
                    m_cinfo.m_prep.pre_process_data(input_buf, ref in_row_ctr, in_rows_avail, m_buffer, ref m_rowgroup_ctr, JpegConstants.DCTSIZE);

                /* If we don't have a full iMCU row buffered, return to application for
                 * more data.  Note that preprocessor will always pad to fill the iMCU row
                 * at the bottom of the image.
                 */
                if (m_rowgroup_ctr != JpegConstants.DCTSIZE)
                    return;

                /* Send the completed row to the compressor */
                if (!m_cinfo.m_coef.compress_data(m_buffer))
                {
                    /* If compressor did not consume the whole row, then we must need to
                     * suspend processing and return to the application.  In this situation
                     * we pretend we didn't yet consume the last input row; otherwise, if
                     * it happened to be the last row of the image, the application would
                     * think we were done.
                     */
                    if (!m_suspended)
                    {
                        in_row_ctr--;
                        m_suspended = true;
                    }

                    return;
                }

                /* We did finish the row.  Undo our little suspension hack if a previous
                 * call suspended; then mark the main buffer empty.
                 */
                if (m_suspended)
                {
                    in_row_ctr++;
                    m_suspended = false;
                }

                m_rowgroup_ctr = 0;
                m_cur_iMCU_row++;
            }
        }
    }
}
