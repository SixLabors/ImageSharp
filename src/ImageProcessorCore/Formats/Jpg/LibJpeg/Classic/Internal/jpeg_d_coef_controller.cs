/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * Copyright (C) 1994-1996, Thomas G. Lane.
 * This file is part of the Independent JPEG Group's software.
 * For conditions of distribution and use, see the accompanying README file.
 *
 */

/*
 * This file contains the coefficient buffer controller for decompression.
 * This controller is the top level of the JPEG decompressor proper.
 * The coefficient buffer lies between entropy decoding and inverse-DCT steps.
 *
 * In buffered-image mode, this controller is the interface between
 * input-oriented processing and output-oriented processing.
 * Also, the input side (only) is used when reading a file for transcoding.
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace BitMiracle.LibJpeg.Classic.Internal
{
    /// <summary>
    /// Coefficient buffer control
    /// 
    /// This code applies interblock smoothing as described by section K.8
    /// of the JPEG standard: the first 5 AC coefficients are estimated from
    /// the DC values of a DCT block and its 8 neighboring blocks.
    /// We apply smoothing only for progressive JPEG decoding, and only if
    /// the coefficients it can estimate are not yet known to full precision.
    /// </summary>
    class jpeg_d_coef_controller
    {
        private const int SAVED_COEFS = 6; /* we save coef_bits[0..5] */

        /* Natural-order array positions of the first 5 zigzag-order coefficients */
        private const int Q01_POS = 1;
        private const int Q10_POS = 8;
        private const int Q20_POS = 16;
        private const int Q11_POS = 9;
        private const int Q02_POS = 2;

        private enum DecompressorType
        {
            Ordinary,
            Smooth,
            OnePass
        }

        private jpeg_decompress_struct m_cinfo;
        private bool m_useDummyConsumeData;
        private DecompressorType m_decompressor;

        /* These variables keep track of the current location of the input side. */
        /* cinfo.input_iMCU_row is also used for this. */
        private int m_MCU_ctr;     /* counts MCUs processed in current row */
        private int m_MCU_vert_offset;        /* counts MCU rows within iMCU row */
        private int m_MCU_rows_per_iMCU_row;  /* number of such rows needed */

        /* The output side's location is represented by cinfo.output_iMCU_row. */

        /* In single-pass modes, it's sufficient to buffer just one MCU.
        * We allocate a workspace of D_MAX_BLOCKS_IN_MCU coefficient blocks,
        * and let the entropy decoder write into that workspace each time.
        * (On 80x86, the workspace is FAR even though it's not really very big;
        * this is to keep the module interfaces unchanged when a large coefficient
        * buffer is necessary.)
        * In multi-pass modes, this array points to the current MCU's blocks
        * within the virtual arrays; it is used only by the input side.
        */
        private JBLOCK[] m_MCU_buffer = new JBLOCK[JpegConstants.D_MAX_BLOCKS_IN_MCU];

        /* In multi-pass modes, we need a virtual block array for each component. */
        private jvirt_array<JBLOCK>[] m_whole_image = new jvirt_array<JBLOCK>[JpegConstants.MAX_COMPONENTS];
        private jvirt_array<JBLOCK>[] m_coef_arrays;

        /* When doing block smoothing, we latch coefficient Al values here */
        private int[] m_coef_bits_latch;
        private int m_coef_bits_savedOffset;

        public jpeg_d_coef_controller(jpeg_decompress_struct cinfo, bool need_full_buffer)
        {
            m_cinfo = cinfo;

            /* Create the coefficient buffer. */
            if (need_full_buffer)
            {
                /* Allocate a full-image virtual array for each component, */
                /* padded to a multiple of samp_factor DCT blocks in each direction. */
                /* Note we ask for a pre-zeroed array. */
                for (int ci = 0; ci < cinfo.m_num_components; ci++)
                {
                    m_whole_image[ci] = jpeg_common_struct.CreateBlocksArray(
                        JpegUtils.jround_up(cinfo.Comp_info[ci].Width_in_blocks, cinfo.Comp_info[ci].H_samp_factor), 
                        JpegUtils.jround_up(cinfo.Comp_info[ci].height_in_blocks, cinfo.Comp_info[ci].V_samp_factor));
                    m_whole_image[ci].ErrorProcessor = cinfo;
                }

                m_useDummyConsumeData = false;
                m_decompressor = DecompressorType.Ordinary;
                m_coef_arrays = m_whole_image; /* link to virtual arrays */
            }
            else
            {
                /* We only need a single-MCU buffer. */
                JBLOCK[] buffer = new JBLOCK[JpegConstants.D_MAX_BLOCKS_IN_MCU];
                for (int i = 0; i < JpegConstants.D_MAX_BLOCKS_IN_MCU; i++)
                {
                    buffer[i] = new JBLOCK();
                    for (int ii = 0; ii < buffer[i].data.Length; ii++)
                        buffer[i].data[ii] = -12851;

                    m_MCU_buffer[i] = buffer[i];
                }

                m_useDummyConsumeData = true;
                m_decompressor = DecompressorType.OnePass;
                m_coef_arrays = null; /* flag for no virtual arrays */
            }
        }

        /// <summary>
        /// Initialize for an input processing pass.
        /// </summary>
        public void start_input_pass()
        {
            m_cinfo.m_input_iMCU_row = 0;
            start_iMCU_row();
        }

        /// <summary>
        /// Consume input data and store it in the full-image coefficient buffer.
        /// We read as much as one fully interleaved MCU row ("iMCU" row) per call,
        /// ie, v_samp_factor block rows for each component in the scan.
        /// </summary>
        public ReadResult consume_data()
        {
            if (m_useDummyConsumeData)
                return ReadResult.JPEG_SUSPENDED;  /* Always indicate nothing was done */

            JBLOCK[][][] buffer = new JBLOCK[JpegConstants.MAX_COMPS_IN_SCAN][][];

            /* Align the virtual buffers for the components used in this scan. */
            for (int ci = 0; ci < m_cinfo.m_comps_in_scan; ci++)
            {
                jpeg_component_info componentInfo = m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[ci]];
                
                buffer[ci] = m_whole_image[componentInfo.Component_index].Access(
                    m_cinfo.m_input_iMCU_row * componentInfo.V_samp_factor, componentInfo.V_samp_factor);

                /* Note: entropy decoder expects buffer to be zeroed,
                 * but this is handled automatically by the memory manager
                 * because we requested a pre-zeroed array.
                 */
            }

            /* Loop to process one whole iMCU row */
            for (int yoffset = m_MCU_vert_offset; yoffset < m_MCU_rows_per_iMCU_row; yoffset++)
            {
                for (int MCU_col_num = m_MCU_ctr; MCU_col_num < m_cinfo.m_MCUs_per_row; MCU_col_num++)
                {
                    /* Construct list of pointers to DCT blocks belonging to this MCU */
                    int blkn = 0;           /* index of current DCT block within MCU */
                    for (int ci = 0; ci < m_cinfo.m_comps_in_scan; ci++)
                    {
                        jpeg_component_info componentInfo = m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[ci]];
                        int start_col = MCU_col_num * componentInfo.MCU_width;
                        for (int yindex = 0; yindex < componentInfo.MCU_height; yindex++)
                        {
                            for (int xindex = 0; xindex < componentInfo.MCU_width; xindex++)
                            {
                                m_MCU_buffer[blkn] = buffer[ci][yindex + yoffset][start_col + xindex];
                                blkn++;
                            }
                        }
                    }

                    /* Try to fetch the MCU. */
                    if (!m_cinfo.m_entropy.decode_mcu(m_MCU_buffer))
                    {
                        /* Suspension forced; update state counters and exit */
                        m_MCU_vert_offset = yoffset;
                        m_MCU_ctr = MCU_col_num;
                        return ReadResult.JPEG_SUSPENDED;
                    }
                }

                /* Completed an MCU row, but perhaps not an iMCU row */
                m_MCU_ctr = 0;
            }

            /* Completed the iMCU row, advance counters for next one */
            m_cinfo.m_input_iMCU_row++;
            if (m_cinfo.m_input_iMCU_row < m_cinfo.m_total_iMCU_rows)
            {
                start_iMCU_row();
                return ReadResult.JPEG_ROW_COMPLETED;
            }

            /* Completed the scan */
            m_cinfo.m_inputctl.finish_input_pass();
            return ReadResult.JPEG_SCAN_COMPLETED;
        }

        /// <summary>
        /// Initialize for an output processing pass.
        /// </summary>
        public void start_output_pass()
        {
            /* If multipass, check to see whether to use block smoothing on this pass */
            if (m_coef_arrays != null)
            {
                if (m_cinfo.m_do_block_smoothing && smoothing_ok())
                    m_decompressor = DecompressorType.Smooth;
                else
                    m_decompressor = DecompressorType.Ordinary;
            }

            m_cinfo.m_output_iMCU_row = 0;
        }

        public ReadResult decompress_data(ComponentBuffer[] output_buf)
        {
            switch (m_decompressor)
            {
                case DecompressorType.Ordinary:
                    return decompress_data_ordinary(output_buf);

                case DecompressorType.Smooth:
                    return decompress_smooth_data(output_buf);

                case DecompressorType.OnePass:
                    return decompress_onepass(output_buf);
            }

            m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
            return 0;
        }

        /* Pointer to array of coefficient virtual arrays, or null if none */
        public jvirt_array<JBLOCK>[] GetCoefArrays()
        {
            return m_coef_arrays;
        }

        /// <summary>
        /// Decompress and return some data in the single-pass case.
        /// Always attempts to emit one fully interleaved MCU row ("iMCU" row).
        /// Input and output must run in lockstep since we have only a one-MCU buffer.
        /// Return value is JPEG_ROW_COMPLETED, JPEG_SCAN_COMPLETED, or JPEG_SUSPENDED.
        /// 
        /// NB: output_buf contains a plane for each component in image,
        /// which we index according to the component's SOF position.
        /// </summary>
        private ReadResult decompress_onepass(ComponentBuffer[] output_buf)
        {
            int last_MCU_col = m_cinfo.m_MCUs_per_row - 1;
            int last_iMCU_row = m_cinfo.m_total_iMCU_rows - 1;

            /* Loop to process as much as one whole iMCU row */
            for (int yoffset = m_MCU_vert_offset; yoffset < m_MCU_rows_per_iMCU_row; yoffset++)
            {
                for (int MCU_col_num = m_MCU_ctr; MCU_col_num <= last_MCU_col; MCU_col_num++)
                {
                    /* Try to fetch an MCU.  Entropy decoder expects buffer to be zeroed. */
                    for (int i = 0; i < m_cinfo.m_blocks_in_MCU; i++)
                        Array.Clear(m_MCU_buffer[i].data, 0, m_MCU_buffer[i].data.Length);
                    
                    if (!m_cinfo.m_entropy.decode_mcu(m_MCU_buffer))
                    {
                        /* Suspension forced; update state counters and exit */
                        m_MCU_vert_offset = yoffset;
                        m_MCU_ctr = MCU_col_num;
                        return ReadResult.JPEG_SUSPENDED;
                    }

                    /* Determine where data should go in output_buf and do the IDCT thing.
                     * We skip dummy blocks at the right and bottom edges (but blkn gets
                     * incremented past them!).  Note the inner loop relies on having
                     * allocated the MCU_buffer[] blocks sequentially.
                     */
                    int blkn = 0;           /* index of current DCT block within MCU */
                    for (int ci = 0; ci < m_cinfo.m_comps_in_scan; ci++)
                    {
                        jpeg_component_info componentInfo = m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[ci]];

                        /* Don't bother to IDCT an uninteresting component. */
                        if (!componentInfo.component_needed)
                        {
                            blkn += componentInfo.MCU_blocks;
                            continue;
                        }

                        int useful_width = (MCU_col_num < last_MCU_col) ? componentInfo.MCU_width : componentInfo.last_col_width;
                        int outputIndex = yoffset * componentInfo.DCT_scaled_size;
                        int start_col = MCU_col_num * componentInfo.MCU_sample_width;
                        for (int yindex = 0; yindex < componentInfo.MCU_height; yindex++)
                        {
                            if (m_cinfo.m_input_iMCU_row < last_iMCU_row || yoffset + yindex < componentInfo.last_row_height)
                            {
                                int output_col = start_col;
                                for (int xindex = 0; xindex < useful_width; xindex++)
                                {
                                    m_cinfo.m_idct.inverse(componentInfo.Component_index,
                                        m_MCU_buffer[blkn + xindex].data, output_buf[componentInfo.Component_index],
                                        outputIndex, output_col);

                                    output_col += componentInfo.DCT_scaled_size;
                                }
                            }

                            blkn += componentInfo.MCU_width;
                            outputIndex += componentInfo.DCT_scaled_size;
                        }
                    }
                }

                /* Completed an MCU row, but perhaps not an iMCU row */
                m_MCU_ctr = 0;
            }

            /* Completed the iMCU row, advance counters for next one */
            m_cinfo.m_output_iMCU_row++;
            m_cinfo.m_input_iMCU_row++;
            if (m_cinfo.m_input_iMCU_row < m_cinfo.m_total_iMCU_rows)
            {
                start_iMCU_row();
                return ReadResult.JPEG_ROW_COMPLETED;
            }

            /* Completed the scan */
            m_cinfo.m_inputctl.finish_input_pass();
            return ReadResult.JPEG_SCAN_COMPLETED;
        }

        /// <summary>
        /// Decompress and return some data in the multi-pass case.
        /// Always attempts to emit one fully interleaved MCU row ("iMCU" row).
        /// Return value is JPEG_ROW_COMPLETED, JPEG_SCAN_COMPLETED, or JPEG_SUSPENDED.
        /// 
        /// NB: output_buf contains a plane for each component in image.
        /// </summary>
        private ReadResult decompress_data_ordinary(ComponentBuffer[] output_buf)
        {
            /* Force some input to be done if we are getting ahead of the input. */
            while (m_cinfo.m_input_scan_number < m_cinfo.m_output_scan_number ||
                   (m_cinfo.m_input_scan_number == m_cinfo.m_output_scan_number &&
                    m_cinfo.m_input_iMCU_row <= m_cinfo.m_output_iMCU_row))
            {
                if (m_cinfo.m_inputctl.consume_input() == ReadResult.JPEG_SUSPENDED)
                    return ReadResult.JPEG_SUSPENDED;
            }

            int last_iMCU_row = m_cinfo.m_total_iMCU_rows - 1;

            /* OK, output from the virtual arrays. */
            for (int ci = 0; ci < m_cinfo.m_num_components; ci++)
            {
                jpeg_component_info componentInfo = m_cinfo.Comp_info[ci];

                /* Don't bother to IDCT an uninteresting component. */
                if (!componentInfo.component_needed)
                    continue;

                /* Align the virtual buffer for this component. */
                JBLOCK[][] buffer = m_whole_image[ci].Access(m_cinfo.m_output_iMCU_row * componentInfo.V_samp_factor,
                    componentInfo.V_samp_factor);

                /* Count non-dummy DCT block rows in this iMCU row. */
                int block_rows;
                if (m_cinfo.m_output_iMCU_row < last_iMCU_row)
                    block_rows = componentInfo.V_samp_factor;
                else
                {
                    /* NB: can't use last_row_height here; it is input-side-dependent! */
                    block_rows = componentInfo.height_in_blocks % componentInfo.V_samp_factor;
                    if (block_rows == 0)
                        block_rows = componentInfo.V_samp_factor;
                }

                /* Loop over all DCT blocks to be processed. */
                int rowIndex = 0;
                for (int block_row = 0; block_row < block_rows; block_row++)
                {
                    int output_col = 0;
                    for (int block_num = 0; block_num < componentInfo.Width_in_blocks; block_num++)
                    {
                        m_cinfo.m_idct.inverse(componentInfo.Component_index,
                            buffer[block_row][block_num].data, output_buf[ci], rowIndex, output_col);

                        output_col += componentInfo.DCT_scaled_size;
                    }

                    rowIndex += componentInfo.DCT_scaled_size;
                }
            }

            m_cinfo.m_output_iMCU_row++;
            if (m_cinfo.m_output_iMCU_row < m_cinfo.m_total_iMCU_rows)
                return ReadResult.JPEG_ROW_COMPLETED;

            return ReadResult.JPEG_SCAN_COMPLETED;
        }

        /// <summary>
        /// Variant of decompress_data for use when doing block smoothing.
        /// </summary>
        private ReadResult decompress_smooth_data(ComponentBuffer[] output_buf)
        {
            /* Force some input to be done if we are getting ahead of the input. */
            while (m_cinfo.m_input_scan_number <= m_cinfo.m_output_scan_number && !m_cinfo.m_inputctl.EOIReached())
            {
                if (m_cinfo.m_input_scan_number == m_cinfo.m_output_scan_number)
                {
                    /* If input is working on current scan, we ordinarily want it to
                     * have completed the current row.  But if input scan is DC,
                     * we want it to keep one row ahead so that next block row's DC
                     * values are up to date.
                     */
                    int delta = (m_cinfo.m_Ss == 0) ? 1 : 0;
                    if (m_cinfo.m_input_iMCU_row > m_cinfo.m_output_iMCU_row + delta)
                        break;
                }

                if (m_cinfo.m_inputctl.consume_input() == ReadResult.JPEG_SUSPENDED)
                    return ReadResult.JPEG_SUSPENDED;
            }

            int last_iMCU_row = m_cinfo.m_total_iMCU_rows - 1;

            /* OK, output from the virtual arrays. */
            for (int ci = 0; ci < m_cinfo.m_num_components; ci++)
            {
                jpeg_component_info componentInfo = m_cinfo.Comp_info[ci];

                /* Don't bother to IDCT an uninteresting component. */
                if (!componentInfo.component_needed)
                    continue;
                
                int block_rows;
                int access_rows;
                bool last_row;
                /* Count non-dummy DCT block rows in this iMCU row. */
                if (m_cinfo.m_output_iMCU_row < last_iMCU_row)
                {
                    block_rows = componentInfo.V_samp_factor;
                    access_rows = block_rows * 2; /* this and next iMCU row */
                    last_row = false;
                }
                else
                {
                    /* NB: can't use last_row_height here; it is input-side-dependent! */
                    block_rows = componentInfo.height_in_blocks % componentInfo.V_samp_factor;
                    if (block_rows == 0)
                        block_rows = componentInfo.V_samp_factor;
                    access_rows = block_rows; /* this iMCU row only */
                    last_row = true;
                }
                
                /* Align the virtual buffer for this component. */
                JBLOCK[][] buffer = null;
                bool first_row;
                int bufferRowOffset = 0;
                if (m_cinfo.m_output_iMCU_row > 0)
                {
                    access_rows += componentInfo.V_samp_factor; /* prior iMCU row too */
                    buffer = m_whole_image[ci].Access((m_cinfo.m_output_iMCU_row - 1) * componentInfo.V_samp_factor, access_rows);
                    bufferRowOffset = componentInfo.V_samp_factor; /* point to current iMCU row */
                    first_row = false;
                }
                else
                {
                    buffer = m_whole_image[ci].Access(0, access_rows);
                    first_row = true;
                }

                /* Fetch component-dependent info */
                int coefBitsOffset = ci * SAVED_COEFS;
                int Q00 = componentInfo.quant_table.quantval[0];
                int Q01 = componentInfo.quant_table.quantval[Q01_POS];
                int Q10 = componentInfo.quant_table.quantval[Q10_POS];
                int Q20 = componentInfo.quant_table.quantval[Q20_POS];
                int Q11 = componentInfo.quant_table.quantval[Q11_POS];
                int Q02 = componentInfo.quant_table.quantval[Q02_POS];
                int outputIndex = ci;
                
                /* Loop over all DCT blocks to be processed. */
                for (int block_row = 0; block_row < block_rows; block_row++)
                {
                    int bufferIndex = bufferRowOffset + block_row;
                    
                    int prev_block_row = 0;
                    if (first_row && block_row == 0)
                        prev_block_row = bufferIndex;
                    else
                        prev_block_row = bufferIndex - 1;

                    int next_block_row = 0;
                    if (last_row && block_row == block_rows - 1)
                        next_block_row = bufferIndex;
                    else
                        next_block_row = bufferIndex + 1;
                    
                    /* We fetch the surrounding DC values using a sliding-register approach.
                     * Initialize all nine here so as to do the right thing on narrow pics.
                     */
                    int DC1 = buffer[prev_block_row][0][0];
                    int DC2 = DC1;
                    int DC3 = DC1;

                    int DC4 = buffer[bufferIndex][0][0];
                    int DC5 = DC4;
                    int DC6 = DC4;

                    int DC7 = buffer[next_block_row][0][0];
                    int DC8 = DC7;
                    int DC9 = DC7;

                    int output_col = 0;
                    int last_block_column = componentInfo.Width_in_blocks - 1;
                    for (int block_num = 0; block_num <= last_block_column; block_num++)
                    {
                        /* Fetch current DCT block into workspace so we can modify it. */
                        JBLOCK workspace = new JBLOCK();
                        Buffer.BlockCopy(buffer[bufferIndex][0].data, 0, workspace.data, 0, workspace.data.Length * sizeof(short));

                        /* Update DC values */
                        if (block_num < last_block_column)
                        {
                            DC3 = buffer[prev_block_row][1][0];
                            DC6 = buffer[bufferIndex][1][0];
                            DC9 = buffer[next_block_row][1][0];
                        }

                        /* Compute coefficient estimates per K.8.
                         * An estimate is applied only if coefficient is still zero,
                         * and is not known to be fully accurate.
                         */
                        /* AC01 */
                        int Al = m_coef_bits_latch[m_coef_bits_savedOffset + coefBitsOffset + 1];
                        if (Al != 0 && workspace[1] == 0)
                        {
                            int pred;
                            int num = 36 * Q00 * (DC4 - DC6);
                            if (num >= 0)
                            {
                                pred = ((Q01 << 7) + num) / (Q01 << 8);
                                if (Al > 0 && pred >= (1 << Al))
                                    pred = (1 << Al) - 1;
                            }
                            else
                            {
                                pred = ((Q01 << 7) - num) / (Q01 << 8);
                                if (Al > 0 && pred >= (1 << Al))
                                    pred = (1 << Al) - 1;
                                pred = -pred;
                            }
                            workspace[1] = (short) pred;
                        }

                        /* AC10 */
                        Al = m_coef_bits_latch[m_coef_bits_savedOffset + coefBitsOffset + 2];
                        if (Al != 0 && workspace[8] == 0)
                        {
                            int pred;
                            int num = 36 * Q00 * (DC2 - DC8);
                            if (num >= 0)
                            {
                                pred = ((Q10 << 7) + num) / (Q10 << 8);
                                if (Al > 0 && pred >= (1 << Al))
                                    pred = (1 << Al) - 1;
                            }
                            else
                            {
                                pred = ((Q10 << 7) - num) / (Q10 << 8);
                                if (Al > 0 && pred >= (1 << Al))
                                    pred = (1 << Al) - 1;
                                pred = -pred;
                            }
                            workspace[8] = (short) pred;
                        }

                        /* AC20 */
                        Al = m_coef_bits_latch[m_coef_bits_savedOffset + coefBitsOffset + 3];
                        if (Al != 0 && workspace[16] == 0)
                        {
                            int pred;
                            int num = 9 * Q00 * (DC2 + DC8 - 2 * DC5);
                            if (num >= 0)
                            {
                                pred = ((Q20 << 7) + num) / (Q20 << 8);
                                if (Al > 0 && pred >= (1 << Al))
                                    pred = (1 << Al) - 1;
                            }
                            else
                            {
                                pred = ((Q20 << 7) - num) / (Q20 << 8);
                                if (Al > 0 && pred >= (1 << Al))
                                    pred = (1 << Al) - 1;
                                pred = -pred;
                            }
                            workspace[16] = (short) pred;
                        }

                        /* AC11 */
                        Al = m_coef_bits_latch[m_coef_bits_savedOffset + coefBitsOffset + 4];
                        if (Al != 0 && workspace[9] == 0)
                        {
                            int pred;
                            int num = 5 * Q00 * (DC1 - DC3 - DC7 + DC9);
                            if (num >= 0)
                            {
                                pred = ((Q11 << 7) + num) / (Q11 << 8);
                                if (Al > 0 && pred >= (1 << Al))
                                    pred = (1 << Al) - 1;
                            }
                            else
                            {
                                pred = ((Q11 << 7) - num) / (Q11 << 8);
                                if (Al > 0 && pred >= (1 << Al))
                                    pred = (1 << Al) - 1;
                                pred = -pred;
                            }
                            workspace[9] = (short) pred;
                        }

                        /* AC02 */
                        Al = m_coef_bits_latch[m_coef_bits_savedOffset + coefBitsOffset + 5];
                        if (Al != 0 && workspace[2] == 0)
                        {
                            int pred;
                            int num = 9 * Q00 * (DC4 + DC6 - 2 * DC5);
                            if (num >= 0)
                            {
                                pred = ((Q02 << 7) + num) / (Q02 << 8);
                                if (Al > 0 && pred >= (1 << Al))
                                    pred = (1 << Al) - 1;
                            }
                            else
                            {
                                pred = ((Q02 << 7) - num) / (Q02 << 8);
                                if (Al > 0 && pred >= (1 << Al))
                                    pred = (1 << Al) - 1;
                                pred = -pred;
                            }
                            workspace[2] = (short) pred;
                        }

                        /* OK, do the IDCT */
                        m_cinfo.m_idct.inverse(componentInfo.Component_index, workspace.data, output_buf[outputIndex], 0, output_col);
                        
                        /* Advance for next column */
                        DC1 = DC2; 
                        DC2 = DC3;
                        DC4 = DC5; 
                        DC5 = DC6;
                        DC7 = DC8; 
                        DC8 = DC9;

                        bufferIndex++;
                        prev_block_row++;
                        next_block_row++;
                        
                        output_col += componentInfo.DCT_scaled_size;
                    }

                    outputIndex += componentInfo.DCT_scaled_size;
                }
            }

            m_cinfo.m_output_iMCU_row++;
            if (m_cinfo.m_output_iMCU_row < m_cinfo.m_total_iMCU_rows)
                return ReadResult.JPEG_ROW_COMPLETED;

            return ReadResult.JPEG_SCAN_COMPLETED;
        }

        /// <summary>
        /// Determine whether block smoothing is applicable and safe.
        /// We also latch the current states of the coef_bits[] entries for the
        /// AC coefficients; otherwise, if the input side of the decompressor
        /// advances into a new scan, we might think the coefficients are known
        /// more accurately than they really are.
        /// </summary>
        private bool smoothing_ok()
        {
            if (!m_cinfo.m_progressive_mode || m_cinfo.m_coef_bits == null)
                return false;

            /* Allocate latch area if not already done */
            if (m_coef_bits_latch == null)
            {
                m_coef_bits_latch = new int[m_cinfo.m_num_components * SAVED_COEFS];
                m_coef_bits_savedOffset = 0;
            }

            bool smoothing_useful = false;
            for (int ci = 0; ci < m_cinfo.m_num_components; ci++)
            {
                /* All components' quantization values must already be latched. */
                JQUANT_TBL qtable = m_cinfo.Comp_info[ci].quant_table;
                if (qtable == null)
                    return false;

                /* Verify DC & first 5 AC quantizers are nonzero to avoid zero-divide. */
                if (qtable.quantval[0] == 0 || qtable.quantval[Q01_POS] == 0 ||
                    qtable.quantval[Q10_POS] == 0 || qtable.quantval[Q20_POS] == 0 ||
                    qtable.quantval[Q11_POS] == 0 || qtable.quantval[Q02_POS] == 0)
                {
                    return false;
                }

                /* DC values must be at least partly known for all components. */
                if (m_cinfo.m_coef_bits[ci][0] < 0)
                    return false;

                /* Block smoothing is helpful if some AC coefficients remain inaccurate. */
                for (int coefi = 1; coefi <= 5; coefi++)
                {
                    m_coef_bits_latch[m_coef_bits_savedOffset + coefi] = m_cinfo.m_coef_bits[ci][coefi];
                    if (m_cinfo.m_coef_bits[ci][coefi] != 0)
                        smoothing_useful = true;
                }

                m_coef_bits_savedOffset += SAVED_COEFS;
            }

            return smoothing_useful;
        }

        /// <summary>
        /// Reset within-iMCU-row counters for a new row (input side)
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
                jpeg_component_info componentInfo = m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[0]];

                if (m_cinfo.m_input_iMCU_row < (m_cinfo.m_total_iMCU_rows - 1))
                    m_MCU_rows_per_iMCU_row = componentInfo.V_samp_factor;
                else
                    m_MCU_rows_per_iMCU_row = componentInfo.last_row_height;
            }

            m_MCU_ctr = 0;
            m_MCU_vert_offset = 0;
        }
    }
}
