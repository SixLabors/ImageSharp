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
    /// Master control module
    /// </summary>
    class jpeg_comp_master
    {
        private enum c_pass_type
        {
            main_pass,      /* input data, also do first output step */
            huff_opt_pass,  /* Huffman code optimization pass */
            output_pass     /* data output pass */
        }

        private jpeg_compress_struct m_cinfo;

        private bool m_call_pass_startup; /* True if pass_startup must be called */
        private bool m_is_last_pass;      /* True during last pass */

        private c_pass_type m_pass_type;  /* the type of the current pass */

        private int m_pass_number;        /* # of passes completed */
        private int m_total_passes;       /* total # of passes needed */

        private int m_scan_number;        /* current index in scan_info[] */

        public jpeg_comp_master(jpeg_compress_struct cinfo, bool transcode_only)
        {
            m_cinfo = cinfo;

            if (transcode_only)
            {
                /* no main pass in transcoding */
                if (cinfo.m_optimize_coding)
                    m_pass_type = c_pass_type.huff_opt_pass;
                else
                    m_pass_type = c_pass_type.output_pass;
            }
            else
            {
                /* for normal compression, first pass is always this type: */
                m_pass_type = c_pass_type.main_pass;
            }

            if (cinfo.m_optimize_coding)
                m_total_passes = cinfo.m_num_scans * 2;
            else
                m_total_passes = cinfo.m_num_scans;
        }

        /// <summary>
        /// Per-pass setup.
        /// 
        /// This is called at the beginning of each pass.  We determine which 
        /// modules will be active during this pass and give them appropriate 
        /// start_pass calls. 
        /// We also set is_last_pass to indicate whether any more passes will 
        /// be required.
        /// </summary>
        public void prepare_for_pass()
        {
            switch (m_pass_type)
            {
                case c_pass_type.main_pass:
                    prepare_for_main_pass();
                    break;
                case c_pass_type.huff_opt_pass:
                    if (!prepare_for_huff_opt_pass())
                        break;
                    prepare_for_output_pass();
                    break;
                case c_pass_type.output_pass:
                    prepare_for_output_pass();
                    break;
                default:
                    m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOT_COMPILED);
                    break;
            }

            m_is_last_pass = (m_pass_number == m_total_passes - 1);

            /* Set up progress monitor's pass info if present */
            if (m_cinfo.m_progress != null)
            {
                m_cinfo.m_progress.Completed_passes = m_pass_number;
                m_cinfo.m_progress.Total_passes = m_total_passes;
            }
        }

        /// <summary>
        /// Special start-of-pass hook.
        /// 
        /// This is called by jpeg_write_scanlines if call_pass_startup is true.
        /// In single-pass processing, we need this hook because we don't want to
        /// write frame/scan headers during jpeg_start_compress; we want to let the
        /// application write COM markers etc. between jpeg_start_compress and the
        /// jpeg_write_scanlines loop.
        /// In multi-pass processing, this routine is not used.
        /// </summary>
        public void pass_startup()
        {
            m_cinfo.m_master.m_call_pass_startup = false; /* reset flag so call only once */

            m_cinfo.m_marker.write_frame_header();
            m_cinfo.m_marker.write_scan_header();
        }

        /// <summary>
        /// Finish up at end of pass.
        /// </summary>
        public void finish_pass()
        {
            /* The entropy coder always needs an end-of-pass call,
            * either to analyze statistics or to flush its output buffer.
            */
            m_cinfo.m_entropy.finish_pass();

            /* Update state for next pass */
            switch (m_pass_type)
            {
                case c_pass_type.main_pass:
                    /* next pass is either output of scan 0 (after optimization)
                    * or output of scan 1 (if no optimization).
                    */
                    m_pass_type = c_pass_type.output_pass;
                    if (!m_cinfo.m_optimize_coding)
                        m_scan_number++;
                    break;
                case c_pass_type.huff_opt_pass:
                    /* next pass is always output of current scan */
                    m_pass_type = c_pass_type.output_pass;
                    break;
                case c_pass_type.output_pass:
                    /* next pass is either optimization or output of next scan */
                    if (m_cinfo.m_optimize_coding)
                        m_pass_type = c_pass_type.huff_opt_pass;
                    m_scan_number++;
                    break;
            }

            m_pass_number++;
        }

        public bool IsLastPass()
        {
            return m_is_last_pass;
        }

        public bool MustCallPassStartup()
        {
            return m_call_pass_startup;
        }

        private void prepare_for_main_pass()
        {
            /* Initial pass: will collect input data, and do either Huffman
            * optimization or data output for the first scan.
            */
            select_scan_parameters();
            per_scan_setup();

            if (!m_cinfo.m_raw_data_in)
            {
                m_cinfo.m_cconvert.start_pass();
                m_cinfo.m_prep.start_pass(J_BUF_MODE.JBUF_PASS_THRU);
            }

            m_cinfo.m_fdct.start_pass();
            m_cinfo.m_entropy.start_pass(m_cinfo.m_optimize_coding);
            m_cinfo.m_coef.start_pass((m_total_passes > 1 ? J_BUF_MODE.JBUF_SAVE_AND_PASS : J_BUF_MODE.JBUF_PASS_THRU));
            m_cinfo.m_main.start_pass(J_BUF_MODE.JBUF_PASS_THRU);

            if (m_cinfo.m_optimize_coding)
            {
                /* No immediate data output; postpone writing frame/scan headers */
                m_call_pass_startup = false;
            }
            else
            {
                /* Will write frame/scan headers at first jpeg_write_scanlines call */
                m_call_pass_startup = true;
            }
        }

        private bool prepare_for_huff_opt_pass()
        {
            /* Do Huffman optimization for a scan after the first one. */
            select_scan_parameters();
            per_scan_setup();

            if (m_cinfo.m_Ss != 0 || m_cinfo.m_Ah == 0)
            {
                m_cinfo.m_entropy.start_pass(true);
                m_cinfo.m_coef.start_pass(J_BUF_MODE.JBUF_CRANK_DEST);
                m_call_pass_startup = false;
                return false;
            }

            /* Special case: Huffman DC refinement scans need no Huffman table
            * and therefore we can skip the optimization pass for them.
            */
            m_pass_type = c_pass_type.output_pass;
            m_pass_number++;
            return true;
        }

        private void prepare_for_output_pass()
        {
            /* Do a data-output pass. */
            /* We need not repeat per-scan setup if prior optimization pass did it. */
            if (!m_cinfo.m_optimize_coding)
            {
                select_scan_parameters();
                per_scan_setup();
            }

            m_cinfo.m_entropy.start_pass(false);
            m_cinfo.m_coef.start_pass(J_BUF_MODE.JBUF_CRANK_DEST);

            /* We emit frame/scan headers now */
            if (m_scan_number == 0)
                m_cinfo.m_marker.write_frame_header();

            m_cinfo.m_marker.write_scan_header();
            m_call_pass_startup = false;
        }

        // Set up the scan parameters for the current scan
        private void select_scan_parameters()
        {
            if (m_cinfo.m_scan_info != null)
            {
                /* Prepare for current scan --- the script is already validated */
                jpeg_scan_info scanInfo = m_cinfo.m_scan_info[m_scan_number];

                m_cinfo.m_comps_in_scan = scanInfo.comps_in_scan;
                for (int ci = 0; ci < scanInfo.comps_in_scan; ci++)
                    m_cinfo.m_cur_comp_info[ci] = scanInfo.component_index[ci];

                m_cinfo.m_Ss = scanInfo.Ss;
                m_cinfo.m_Se = scanInfo.Se;
                m_cinfo.m_Ah = scanInfo.Ah;
                m_cinfo.m_Al = scanInfo.Al;
            }
            else
            {
                /* Prepare for single sequential-JPEG scan containing all components */
                if (m_cinfo.m_num_components > JpegConstants.MAX_COMPS_IN_SCAN)
                    m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_COMPONENT_COUNT, m_cinfo.m_num_components, JpegConstants.MAX_COMPS_IN_SCAN);

                m_cinfo.m_comps_in_scan = m_cinfo.m_num_components;
                for (int ci = 0; ci < m_cinfo.m_num_components; ci++)
                    m_cinfo.m_cur_comp_info[ci] = ci;

                m_cinfo.m_Ss = 0;
                m_cinfo.m_Se = JpegConstants.DCTSIZE2 - 1;
                m_cinfo.m_Ah = 0;
                m_cinfo.m_Al = 0;
            }
        }

        /// <summary>
        /// Do computations that are needed before processing a JPEG scan
        /// cinfo.comps_in_scan and cinfo.cur_comp_info[] are already set
        /// </summary>
        private void per_scan_setup()
        {
            if (m_cinfo.m_comps_in_scan == 1)
            {
                /* Noninterleaved (single-component) scan */
                int compIndex = m_cinfo.m_cur_comp_info[0];

                /* Overall image size in MCUs */
                m_cinfo.m_MCUs_per_row = m_cinfo.Component_info[compIndex].Width_in_blocks;
                m_cinfo.m_MCU_rows_in_scan = m_cinfo.Component_info[compIndex].height_in_blocks;

                /* For noninterleaved scan, always one block per MCU */
                m_cinfo.Component_info[compIndex].MCU_width = 1;
                m_cinfo.Component_info[compIndex].MCU_height = 1;
                m_cinfo.Component_info[compIndex].MCU_blocks = 1;
                m_cinfo.Component_info[compIndex].MCU_sample_width = JpegConstants.DCTSIZE;
                m_cinfo.Component_info[compIndex].last_col_width = 1;
                
                /* For noninterleaved scans, it is convenient to define last_row_height
                * as the number of block rows present in the last iMCU row.
                */
                int tmp = m_cinfo.Component_info[compIndex].height_in_blocks % m_cinfo.Component_info[compIndex].V_samp_factor;
                if (tmp == 0)
                    tmp = m_cinfo.Component_info[compIndex].V_samp_factor;
                m_cinfo.Component_info[compIndex].last_row_height = tmp;

                /* Prepare array describing MCU composition */
                m_cinfo.m_blocks_in_MCU = 1;
                m_cinfo.m_MCU_membership[0] = 0;
            }
            else
            {
                /* Interleaved (multi-component) scan */
                if (m_cinfo.m_comps_in_scan <= 0 || m_cinfo.m_comps_in_scan > JpegConstants.MAX_COMPS_IN_SCAN)
                    m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_COMPONENT_COUNT, m_cinfo.m_comps_in_scan, JpegConstants.MAX_COMPS_IN_SCAN);

                /* Overall image size in MCUs */
                m_cinfo.m_MCUs_per_row = JpegUtils.jdiv_round_up(
                    m_cinfo.m_image_width, m_cinfo.m_max_h_samp_factor * JpegConstants.DCTSIZE);

                m_cinfo.m_MCU_rows_in_scan = JpegUtils.jdiv_round_up(m_cinfo.m_image_height,
                    m_cinfo.m_max_v_samp_factor * JpegConstants.DCTSIZE);

                m_cinfo.m_blocks_in_MCU = 0;

                for (int ci = 0; ci < m_cinfo.m_comps_in_scan; ci++)
                {
                    int compIndex = m_cinfo.m_cur_comp_info[ci];

                    /* Sampling factors give # of blocks of component in each MCU */
                    m_cinfo.Component_info[compIndex].MCU_width = m_cinfo.Component_info[compIndex].H_samp_factor;
                    m_cinfo.Component_info[compIndex].MCU_height = m_cinfo.Component_info[compIndex].V_samp_factor;
                    m_cinfo.Component_info[compIndex].MCU_blocks = m_cinfo.Component_info[compIndex].MCU_width * m_cinfo.Component_info[compIndex].MCU_height;
                    m_cinfo.Component_info[compIndex].MCU_sample_width = m_cinfo.Component_info[compIndex].MCU_width * JpegConstants.DCTSIZE;
                    
                    /* Figure number of non-dummy blocks in last MCU column & row */
                    int tmp = m_cinfo.Component_info[compIndex].Width_in_blocks % m_cinfo.Component_info[compIndex].MCU_width;
                    if (tmp == 0)
                        tmp = m_cinfo.Component_info[compIndex].MCU_width;
                    m_cinfo.Component_info[compIndex].last_col_width = tmp;

                    tmp = m_cinfo.Component_info[compIndex].height_in_blocks % m_cinfo.Component_info[compIndex].MCU_height;
                    if (tmp == 0)
                        tmp = m_cinfo.Component_info[compIndex].MCU_height;
                    m_cinfo.Component_info[compIndex].last_row_height = tmp;
                    
                    /* Prepare array describing MCU composition */
                    int mcublks = m_cinfo.Component_info[compIndex].MCU_blocks;
                    if (m_cinfo.m_blocks_in_MCU + mcublks > JpegConstants.C_MAX_BLOCKS_IN_MCU)
                        m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_MCU_SIZE);
                    
                    while (mcublks-- > 0)
                        m_cinfo.m_MCU_membership[m_cinfo.m_blocks_in_MCU++] = ci;
                }
            }

            /* Convert restart specified in rows to actual MCU count. */
            /* Note that count must fit in 16 bits, so we provide limiting. */
            if (m_cinfo.m_restart_in_rows > 0)
            {
                int nominal = m_cinfo.m_restart_in_rows * m_cinfo.m_MCUs_per_row;
                m_cinfo.m_restart_interval = Math.Min(nominal, 65535);
            }
        }
    }
}
