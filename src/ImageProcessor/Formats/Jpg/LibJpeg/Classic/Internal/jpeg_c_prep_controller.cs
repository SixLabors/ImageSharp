/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * Copyright (C) 1994-1996, Thomas G. Lane.
 * This file is part of the Independent JPEG Group's software.
 * For conditions of distribution and use, see the accompanying README file.
 *
 */

/*
 * This file contains the compression preprocessing controller.
 * This controller manages the color conversion, downsampling,
 * and edge expansion steps.
 *
 * Most of the complexity here is associated with buffering input rows
 * as required by the downsampler.  See the comments at the head of
 * my_downsampler for the downsampler's needs.
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace BitMiracle.LibJpeg.Classic.Internal
{
    /// <summary>
    /// Compression preprocessing (downsampling input buffer control).
    /// 
    /// For the simple (no-context-row) case, we just need to buffer one
    /// row group's worth of pixels for the downsampling step.  At the bottom of
    /// the image, we pad to a full row group by replicating the last pixel row.
    /// The downsampler's last output row is then replicated if needed to pad
    /// out to a full iMCU row.
    /// 
    /// When providing context rows, we must buffer three row groups' worth of
    /// pixels.  Three row groups are physically allocated, but the row pointer
    /// arrays are made five row groups high, with the extra pointers above and
    /// below "wrapping around" to point to the last and first real row groups.
    /// This allows the downsampler to access the proper context rows.
    /// At the top and bottom of the image, we create dummy context rows by
    /// copying the first or last real pixel row.  This copying could be avoided
    /// by pointer hacking as is done in jdmainct.c, but it doesn't seem worth the
    /// trouble on the compression side.
    /// </summary>
    class jpeg_c_prep_controller
    {
        private jpeg_compress_struct m_cinfo;

        /* Downsampling input buffer.  This buffer holds color-converted data
        * until we have enough to do a downsample step.
        */
        private byte[][][] m_color_buf = new byte[JpegConstants.MAX_COMPONENTS][][];
        private int m_colorBufRowsOffset;

        private int m_rows_to_go;  /* counts rows remaining in source image */
        private int m_next_buf_row;       /* index of next row to store in color_buf */

        private int m_this_row_group;     /* starting row index of group to process */
        private int m_next_buf_stop;      /* downsample when we reach this index */

        public jpeg_c_prep_controller(jpeg_compress_struct cinfo)
        {
            m_cinfo = cinfo;

            /* Allocate the color conversion buffer.
            * We make the buffer wide enough to allow the downsampler to edge-expand
            * horizontally within the buffer, if it so chooses.
            */
            if (cinfo.m_downsample.NeedContextRows())
            {
                /* Set up to provide context rows */
                create_context_buffer();
            }
            else
            {
                /* No context, just make it tall enough for one row group */
                for (int ci = 0; ci < cinfo.m_num_components; ci++)
                {
                    m_colorBufRowsOffset = 0;
                    m_color_buf[ci] = jpeg_compress_struct.AllocJpegSamples(
                        (cinfo.Component_info[ci].Width_in_blocks * JpegConstants.DCTSIZE * cinfo.m_max_h_samp_factor) / cinfo.Component_info[ci].H_samp_factor,
                        cinfo.m_max_v_samp_factor);
                }
            }
        }

        /// <summary>
        /// Initialize for a processing pass.
        /// </summary>
        public void start_pass(J_BUF_MODE pass_mode)
        {
            if (pass_mode != J_BUF_MODE.JBUF_PASS_THRU)
                m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_BUFFER_MODE);

            /* Initialize total-height counter for detecting bottom of image */
            m_rows_to_go = m_cinfo.m_image_height;

            /* Mark the conversion buffer empty */
            m_next_buf_row = 0;

            /* Preset additional state variables for context mode.
             * These aren't used in non-context mode, so we needn't test which mode.
             */
            m_this_row_group = 0;

            /* Set next_buf_stop to stop after two row groups have been read in. */
            m_next_buf_stop = 2 * m_cinfo.m_max_v_samp_factor;
        }

        public void pre_process_data(byte[][] input_buf, ref int in_row_ctr, int in_rows_avail, byte[][][] output_buf, ref int out_row_group_ctr, int out_row_groups_avail)
        {
            if (m_cinfo.m_downsample.NeedContextRows())
                pre_process_context(input_buf, ref in_row_ctr, in_rows_avail, output_buf, ref out_row_group_ctr, out_row_groups_avail);
            else
                pre_process_WithoutContext(input_buf, ref in_row_ctr, in_rows_avail, output_buf, ref out_row_group_ctr, out_row_groups_avail);
        }

        /// <summary>
        /// Create the wrapped-around downsampling input buffer needed for context mode.
        /// </summary>
        private void create_context_buffer()
        {
            int rgroup_height = m_cinfo.m_max_v_samp_factor;
            for (int ci = 0; ci < m_cinfo.m_num_components; ci++)
            {
                int samplesPerRow = (m_cinfo.Component_info[ci].Width_in_blocks * JpegConstants.DCTSIZE * m_cinfo.m_max_h_samp_factor) / m_cinfo.Component_info[ci].H_samp_factor;

                byte[][] fake_buffer = new byte[5 * rgroup_height][];
                for (int i = 1; i < 4 * rgroup_height; i++)
                    fake_buffer[i] = new byte [samplesPerRow];

                /* Allocate the actual buffer space (3 row groups) for this component.
                 * We make the buffer wide enough to allow the downsampler to edge-expand
                 * horizontally within the buffer, if it so chooses.
                 */
                byte[][] true_buffer = jpeg_common_struct.AllocJpegSamples(samplesPerRow, 3 * rgroup_height);

                /* Copy true buffer row pointers into the middle of the fake row array */
                for (int  i = 0; i < 3 * rgroup_height; i++)
                    fake_buffer[rgroup_height + i] = true_buffer[i];
                
                /* Fill in the above and below wraparound pointers */
                for (int i = 0; i < rgroup_height; i++)
                {
                    fake_buffer[i] = true_buffer[2 * rgroup_height + i];
                    fake_buffer[4 * rgroup_height + i] = true_buffer[i];
                }

                m_color_buf[ci] = fake_buffer;
                m_colorBufRowsOffset = rgroup_height;
            }
        }

        /// <summary>
        /// Process some data in the simple no-context case.
        /// 
        /// Preprocessor output data is counted in "row groups".  A row group
        /// is defined to be v_samp_factor sample rows of each component.
        /// Downsampling will produce this much data from each max_v_samp_factor
        /// input rows.
        /// </summary>
        private void pre_process_WithoutContext(byte[][] input_buf, ref int in_row_ctr, int in_rows_avail, byte[][][] output_buf, ref int out_row_group_ctr, int out_row_groups_avail)
        {
            while (in_row_ctr < in_rows_avail && out_row_group_ctr < out_row_groups_avail)
            {
                /* Do color conversion to fill the conversion buffer. */
                int inrows = in_rows_avail - in_row_ctr;
                int numrows = m_cinfo.m_max_v_samp_factor - m_next_buf_row;
                numrows = Math.Min(numrows, inrows);
                m_cinfo.m_cconvert.color_convert(input_buf, in_row_ctr, m_color_buf, m_colorBufRowsOffset + m_next_buf_row, numrows);
                in_row_ctr += numrows;
                m_next_buf_row += numrows;
                m_rows_to_go -= numrows;

                /* If at bottom of image, pad to fill the conversion buffer. */
                if (m_rows_to_go == 0 && m_next_buf_row < m_cinfo.m_max_v_samp_factor)
                {
                    for (int ci = 0; ci < m_cinfo.m_num_components; ci++)
                        expand_bottom_edge(m_color_buf[ci], m_colorBufRowsOffset, m_cinfo.m_image_width, m_next_buf_row, m_cinfo.m_max_v_samp_factor);

                    m_next_buf_row = m_cinfo.m_max_v_samp_factor;
                }

                /* If we've filled the conversion buffer, empty it. */
                if (m_next_buf_row == m_cinfo.m_max_v_samp_factor)
                {
                    m_cinfo.m_downsample.downsample(m_color_buf, m_colorBufRowsOffset, output_buf, out_row_group_ctr);
                    m_next_buf_row = 0;
                    out_row_group_ctr++;
                }

                /* If at bottom of image, pad the output to a full iMCU height.
                 * Note we assume the caller is providing a one-iMCU-height output buffer!
                 */
                if (m_rows_to_go == 0 && out_row_group_ctr < out_row_groups_avail)
                {
                    for (int ci = 0; ci < m_cinfo.m_num_components; ci++)
                    {
                        jpeg_component_info componentInfo = m_cinfo.Component_info[ci];
                        expand_bottom_edge(output_buf[ci], 0, componentInfo.Width_in_blocks * JpegConstants.DCTSIZE,
                            out_row_group_ctr * componentInfo.V_samp_factor,
                            out_row_groups_avail * componentInfo.V_samp_factor);
                    }

                    out_row_group_ctr = out_row_groups_avail;
                    break;          /* can exit outer loop without test */
                }
            }
        }

        /// <summary>
        /// Process some data in the context case.
        /// </summary>
        private void pre_process_context(byte[][] input_buf, ref int in_row_ctr, int in_rows_avail, byte[][][] output_buf, ref int out_row_group_ctr, int out_row_groups_avail)
        {
            while (out_row_group_ctr < out_row_groups_avail)
            {
                if (in_row_ctr < in_rows_avail)
                {
                    /* Do color conversion to fill the conversion buffer. */
                    int inrows = in_rows_avail - in_row_ctr;
                    int numrows = m_next_buf_stop - m_next_buf_row;
                    numrows = Math.Min(numrows, inrows);
                    m_cinfo.m_cconvert.color_convert(input_buf, in_row_ctr, m_color_buf, m_colorBufRowsOffset + m_next_buf_row, numrows);

                    /* Pad at top of image, if first time through */
                    if (m_rows_to_go == m_cinfo.m_image_height)
                    {
                        for (int ci = 0; ci < m_cinfo.m_num_components; ci++)
                        {
                            for (int row = 1; row <= m_cinfo.m_max_v_samp_factor; row++)
                                JpegUtils.jcopy_sample_rows(m_color_buf[ci], m_colorBufRowsOffset, m_color_buf[ci], m_colorBufRowsOffset - row, 1, m_cinfo.m_image_width);
                        }
                    }
                    
                    in_row_ctr += numrows;
                    m_next_buf_row += numrows;
                    m_rows_to_go -= numrows;
                }
                else
                {
                    /* Return for more data, unless we are at the bottom of the image. */
                    if (m_rows_to_go != 0)
                        break;

                    /* When at bottom of image, pad to fill the conversion buffer. */
                    if (m_next_buf_row < m_next_buf_stop)
                    {
                        for (int ci = 0; ci < m_cinfo.m_num_components; ci++)
                            expand_bottom_edge(m_color_buf[ci], m_colorBufRowsOffset, m_cinfo.m_image_width, m_next_buf_row, m_next_buf_stop);

                        m_next_buf_row = m_next_buf_stop;
                    }
                }

                /* If we've gotten enough data, downsample a row group. */
                if (m_next_buf_row == m_next_buf_stop)
                {
                    m_cinfo.m_downsample.downsample(m_color_buf, m_colorBufRowsOffset + m_this_row_group, output_buf, out_row_group_ctr);
                    out_row_group_ctr++;
                
                    /* Advance pointers with wraparound as necessary. */
                    m_this_row_group += m_cinfo.m_max_v_samp_factor;
                    int buf_height = m_cinfo.m_max_v_samp_factor * 3;

                    if (m_this_row_group >= buf_height)
                        m_this_row_group = 0;
                    
                    if (m_next_buf_row >= buf_height)
                        m_next_buf_row = 0;
                    
                    m_next_buf_stop = m_next_buf_row + m_cinfo.m_max_v_samp_factor;
                }
            }
        }

        /// <summary>
        /// Expand an image vertically from height input_rows to height output_rows,
        /// by duplicating the bottom row.
        /// </summary>
        private static void expand_bottom_edge(byte[][] image_data, int rowsOffset, int num_cols, int input_rows, int output_rows)
        {
            for (int row = input_rows; row < output_rows; row++)
                JpegUtils.jcopy_sample_rows(image_data, rowsOffset + input_rows - 1, image_data, row, 1, num_cols);
        }
    }
}
