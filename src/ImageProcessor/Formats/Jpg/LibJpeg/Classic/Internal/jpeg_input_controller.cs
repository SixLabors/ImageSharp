/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * Copyright (C) 1994-1996, Thomas G. Lane.
 * This file is part of the Independent JPEG Group's software.
 * For conditions of distribution and use, see the accompanying README file.
 *
 */

/*
 * This file contains input control logic for the JPEG decompressor.
 * These routines are concerned with controlling the decompressor's input
 * processing (marker reading and coefficient decoding).
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace BitMiracle.LibJpeg.Classic.Internal
{
    /// <summary>
    /// Input control module
    /// </summary>
    class jpeg_input_controller
    {
        private jpeg_decompress_struct m_cinfo;
        private bool m_consumeData;
        private bool m_inheaders;     /* true until first SOS is reached */
        private bool m_has_multiple_scans;    /* True if file has multiple scans */
        private bool m_eoi_reached;       /* True when EOI has been consumed */

        /// <summary>
        /// Initialize the input controller module.
        /// This is called only once, when the decompression object is created.
        /// </summary>
        public jpeg_input_controller(jpeg_decompress_struct cinfo)
        {
            m_cinfo = cinfo;

            /* Initialize state: can't use reset_input_controller since we don't
            * want to try to reset other modules yet.
            */
            m_inheaders = true;
        }

        public ReadResult consume_input()
        {
            if (m_consumeData)
                return m_cinfo.m_coef.consume_data();

            return consume_markers();
        }

        /// <summary>
        /// Reset state to begin a fresh datastream.
        /// </summary>
        public void reset_input_controller()
        {
            m_consumeData = false;
            m_has_multiple_scans = false; /* "unknown" would be better */
            m_eoi_reached = false;
            m_inheaders = true;

            /* Reset other modules */
            m_cinfo.m_err.reset_error_mgr();
            m_cinfo.m_marker.reset_marker_reader();

            /* Reset progression state -- would be cleaner if entropy decoder did this */
            m_cinfo.m_coef_bits = null;
        }

        /// <summary>
        /// Initialize the input modules to read a scan of compressed data.
        /// The first call to this is done after initializing
        /// the entire decompressor (during jpeg_start_decompress).
        /// Subsequent calls come from consume_markers, below.
        /// </summary>
        public void start_input_pass()
        {
            per_scan_setup();
            latch_quant_tables();
            m_cinfo.m_entropy.start_pass();
            m_cinfo.m_coef.start_input_pass();
            m_consumeData = true;
        }

        /// <summary>
        /// Finish up after inputting a compressed-data scan.
        /// This is called by the coefficient controller after it's read all
        /// the expected data of the scan.
        /// </summary>
        public void finish_input_pass()
        {
            m_consumeData = false;
        }

        public bool HasMultipleScans()
        {
            return m_has_multiple_scans;
        }

        public bool EOIReached()
        {
            return m_eoi_reached;
        }

        /// <summary>
        /// Read JPEG markers before, between, or after compressed-data scans.
        /// Change state as necessary when a new scan is reached.
        /// Return value is JPEG_SUSPENDED, JPEG_REACHED_SOS, or JPEG_REACHED_EOI.
        /// 
        /// The consume_input method pointer points either here or to the
        /// coefficient controller's consume_data routine, depending on whether
        /// we are reading a compressed data segment or inter-segment markers.
        /// </summary>
        private ReadResult consume_markers()
        {
            ReadResult val;

            if (m_eoi_reached) /* After hitting EOI, read no further */
                return ReadResult.JPEG_REACHED_EOI;

            val = m_cinfo.m_marker.read_markers();

            switch (val)
            {
                case ReadResult.JPEG_REACHED_SOS:
                    /* Found SOS */
                    if (m_inheaders)
                    {
                        /* 1st SOS */
                        initial_setup();
                        m_inheaders = false;
                        /* Note: start_input_pass must be called by jpeg_decomp_master
                         * before any more input can be consumed.
                         */
                    }
                    else
                    {
                        /* 2nd or later SOS marker */
                        if (!m_has_multiple_scans)
                        {
                            /* Oops, I wasn't expecting this! */
                            m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_EOI_EXPECTED);
                        }

                        m_cinfo.m_inputctl.start_input_pass();
                    }
                    break;
                case ReadResult.JPEG_REACHED_EOI:
                    /* Found EOI */
                    m_eoi_reached = true;
                    if (m_inheaders)
                    {
                        /* Tables-only datastream, apparently */
                        if (m_cinfo.m_marker.SawSOF())
                            m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_SOF_NO_SOS);
                    }
                    else
                    {
                        /* Prevent infinite loop in coef ctlr's decompress_data routine
                         * if user set output_scan_number larger than number of scans.
                         */
                        if (m_cinfo.m_output_scan_number > m_cinfo.m_input_scan_number)
                            m_cinfo.m_output_scan_number = m_cinfo.m_input_scan_number;
                    }
                    break;
                case ReadResult.JPEG_SUSPENDED:
                    break;
            }

            return val;
        }

        /// <summary>
        /// Routines to calculate various quantities related to the size of the image.
        /// Called once, when first SOS marker is reached
        /// </summary>
        private void initial_setup()
        {
            /* Make sure image isn't bigger than I can handle */
            if (m_cinfo.m_image_height > JpegConstants.JPEG_MAX_DIMENSION ||
                m_cinfo.m_image_width > JpegConstants.JPEG_MAX_DIMENSION)
            {
                m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_IMAGE_TOO_BIG, (int)JpegConstants.JPEG_MAX_DIMENSION);

            }

            /* For now, precision must match compiled-in value... */
            if (m_cinfo.m_data_precision != JpegConstants.BITS_IN_JSAMPLE)
                m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_PRECISION, m_cinfo.m_data_precision);

            /* Check that number of components won't exceed internal array sizes */
            if (m_cinfo.m_num_components > JpegConstants.MAX_COMPONENTS)
                m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_COMPONENT_COUNT, m_cinfo.m_num_components, JpegConstants.MAX_COMPONENTS);

            /* Compute maximum sampling factors; check factor validity */
            m_cinfo.m_max_h_samp_factor = 1;
            m_cinfo.m_max_v_samp_factor = 1;

            for (int ci = 0; ci < m_cinfo.m_num_components; ci++)
            {
                if (m_cinfo.Comp_info[ci].H_samp_factor <= 0 || m_cinfo.Comp_info[ci].H_samp_factor > JpegConstants.MAX_SAMP_FACTOR ||
                    m_cinfo.Comp_info[ci].V_samp_factor <= 0 || m_cinfo.Comp_info[ci].V_samp_factor > JpegConstants.MAX_SAMP_FACTOR)
                {
                    m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_SAMPLING);
                }

                m_cinfo.m_max_h_samp_factor = Math.Max(m_cinfo.m_max_h_samp_factor, m_cinfo.Comp_info[ci].H_samp_factor);
                m_cinfo.m_max_v_samp_factor = Math.Max(m_cinfo.m_max_v_samp_factor, m_cinfo.Comp_info[ci].V_samp_factor);
            }

            /* We initialize DCT_scaled_size and min_DCT_scaled_size to DCTSIZE.
             * In the full decompressor, this will be overridden jpeg_decomp_master;
             * but in the transcoder, jpeg_decomp_master is not used, so we must do it here.
             */
            m_cinfo.m_min_DCT_scaled_size = JpegConstants.DCTSIZE;

            /* Compute dimensions of components */
            for (int ci = 0; ci < m_cinfo.m_num_components; ci++)
            {
                m_cinfo.Comp_info[ci].DCT_scaled_size = JpegConstants.DCTSIZE;
                
                /* Size in DCT blocks */
                m_cinfo.Comp_info[ci].Width_in_blocks = JpegUtils.jdiv_round_up(
                    m_cinfo.m_image_width * m_cinfo.Comp_info[ci].H_samp_factor,
                    m_cinfo.m_max_h_samp_factor * JpegConstants.DCTSIZE);

                m_cinfo.Comp_info[ci].height_in_blocks = JpegUtils.jdiv_round_up(
                    m_cinfo.m_image_height * m_cinfo.Comp_info[ci].V_samp_factor,
                    m_cinfo.m_max_v_samp_factor * JpegConstants.DCTSIZE);

                /* downsampled_width and downsampled_height will also be overridden by
                 * jpeg_decomp_master if we are doing full decompression.  The transcoder library
                 * doesn't use these values, but the calling application might.
                 */
                /* Size in samples */
                m_cinfo.Comp_info[ci].downsampled_width = JpegUtils.jdiv_round_up(
                    m_cinfo.m_image_width * m_cinfo.Comp_info[ci].H_samp_factor,
                    m_cinfo.m_max_h_samp_factor);

                m_cinfo.Comp_info[ci].downsampled_height = JpegUtils.jdiv_round_up(
                    m_cinfo.m_image_height * m_cinfo.Comp_info[ci].V_samp_factor,
                    m_cinfo.m_max_v_samp_factor);

                /* Mark component needed, until color conversion says otherwise */
                m_cinfo.Comp_info[ci].component_needed = true;
                
                /* Mark no quantization table yet saved for component */
                m_cinfo.Comp_info[ci].quant_table = null;
            }

            /* Compute number of fully interleaved MCU rows. */
            m_cinfo.m_total_iMCU_rows = JpegUtils.jdiv_round_up(
                m_cinfo.m_image_height, m_cinfo.m_max_v_samp_factor * JpegConstants.DCTSIZE);

            /* Decide whether file contains multiple scans */
            if (m_cinfo.m_comps_in_scan < m_cinfo.m_num_components || m_cinfo.m_progressive_mode)
                m_cinfo.m_inputctl.m_has_multiple_scans = true;
            else
                m_cinfo.m_inputctl.m_has_multiple_scans = false;
        }

        /// <summary>
        /// Save away a copy of the Q-table referenced by each component present
        /// in the current scan, unless already saved during a prior scan.
        /// 
        /// In a multiple-scan JPEG file, the encoder could assign different components
        /// the same Q-table slot number, but change table definitions between scans
        /// so that each component uses a different Q-table.  (The IJG encoder is not
        /// currently capable of doing this, but other encoders might.)  Since we want
        /// to be able to dequantize all the components at the end of the file, this
        /// means that we have to save away the table actually used for each component.
        /// We do this by copying the table at the start of the first scan containing
        /// the component.
        /// The JPEG spec prohibits the encoder from changing the contents of a Q-table
        /// slot between scans of a component using that slot.  If the encoder does so
        /// anyway, this decoder will simply use the Q-table values that were current
        /// at the start of the first scan for the component.
        /// 
        /// The decompressor output side looks only at the saved quant tables,
        /// not at the current Q-table slots.
        /// </summary>
        private void latch_quant_tables()
        {
            for (int ci = 0; ci < m_cinfo.m_comps_in_scan; ci++)
            {
                jpeg_component_info componentInfo = m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[ci]];
                
                /* No work if we already saved Q-table for this component */
                if (componentInfo.quant_table != null)
                    continue;
                
                /* Make sure specified quantization table is present */
                int qtblno = componentInfo.Quant_tbl_no;
                if (qtblno < 0 || qtblno >= JpegConstants.NUM_QUANT_TBLS || m_cinfo.m_quant_tbl_ptrs[qtblno] == null)
                    m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NO_QUANT_TABLE, qtblno);
                
                /* OK, save away the quantization table */
                JQUANT_TBL qtbl = new JQUANT_TBL();
                Buffer.BlockCopy(m_cinfo.m_quant_tbl_ptrs[qtblno].quantval, 0,
                    qtbl.quantval, 0, qtbl.quantval.Length * sizeof(short));
                qtbl.Sent_table = m_cinfo.m_quant_tbl_ptrs[qtblno].Sent_table;
                componentInfo.quant_table = qtbl;
                m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[ci]] = componentInfo;
            }
        }

        /// <summary>
        /// Do computations that are needed before processing a JPEG scan
        /// cinfo.comps_in_scan and cinfo.cur_comp_info[] were set from SOS marker
        /// </summary>
        private void per_scan_setup()
        {
            if (m_cinfo.m_comps_in_scan == 1)
            {
                /* Noninterleaved (single-component) scan */
                jpeg_component_info componentInfo = m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[0]];

                /* Overall image size in MCUs */
                m_cinfo.m_MCUs_per_row = componentInfo.Width_in_blocks;
                m_cinfo.m_MCU_rows_in_scan = componentInfo.height_in_blocks;

                /* For noninterleaved scan, always one block per MCU */
                componentInfo.MCU_width = 1;
                componentInfo.MCU_height = 1;
                componentInfo.MCU_blocks = 1;
                componentInfo.MCU_sample_width = componentInfo.DCT_scaled_size;
                componentInfo.last_col_width = 1;

                /* For noninterleaved scans, it is convenient to define last_row_height
                 * as the number of block rows present in the last iMCU row.
                 */
                int tmp = componentInfo.height_in_blocks % componentInfo.V_samp_factor;
                if (tmp == 0)
                    tmp = componentInfo.V_samp_factor;
                componentInfo.last_row_height = tmp;
                m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[0]] = componentInfo;

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

                m_cinfo.m_MCU_rows_in_scan = JpegUtils.jdiv_round_up(
                    m_cinfo.m_image_height, m_cinfo.m_max_v_samp_factor * JpegConstants.DCTSIZE);

                m_cinfo.m_blocks_in_MCU = 0;

                for (int ci = 0; ci < m_cinfo.m_comps_in_scan; ci++)
                {
                    jpeg_component_info componentInfo = m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[ci]];

                    /* Sampling factors give # of blocks of component in each MCU */
                    componentInfo.MCU_width = componentInfo.H_samp_factor;
                    componentInfo.MCU_height = componentInfo.V_samp_factor;
                    componentInfo.MCU_blocks = componentInfo.MCU_width * componentInfo.MCU_height;
                    componentInfo.MCU_sample_width = componentInfo.MCU_width * componentInfo.DCT_scaled_size;
                    
                    /* Figure number of non-dummy blocks in last MCU column & row */
                    int tmp = componentInfo.Width_in_blocks % componentInfo.MCU_width;
                    if (tmp == 0)
                        tmp = componentInfo.MCU_width;
                    componentInfo.last_col_width = tmp;
                    
                    tmp = componentInfo.height_in_blocks % componentInfo.MCU_height;
                    if (tmp == 0)
                        tmp = componentInfo.MCU_height;
                    componentInfo.last_row_height = tmp;
                    
                    /* Prepare array describing MCU composition */
                    int mcublks = componentInfo.MCU_blocks;
                    if (m_cinfo.m_blocks_in_MCU + mcublks > JpegConstants.D_MAX_BLOCKS_IN_MCU)
                        m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_MCU_SIZE);

                    m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[ci]] = componentInfo;

                    while (mcublks-- > 0)
                        m_cinfo.m_MCU_membership[m_cinfo.m_blocks_in_MCU++] = ci;
                }
            }
        }
    }
}
