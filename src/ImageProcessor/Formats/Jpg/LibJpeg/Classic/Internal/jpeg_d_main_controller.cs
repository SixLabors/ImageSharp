/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * Copyright (C) 1994-1996, Thomas G. Lane.
 * This file is part of the Independent JPEG Group's software.
 * For conditions of distribution and use, see the accompanying README file.
 *
 */

/*
 * This file contains the main buffer controller for decompression.
 * The main buffer lies between the JPEG decompressor proper and the
 * post-processor; it holds downsampled data in the JPEG colorspace.
 *
 * Note that this code is bypassed in raw-data mode, since the application
 * supplies the equivalent of the main buffer in that case.
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace BitMiracle.LibJpeg.Classic.Internal
{
    /// <summary>
    /// Main buffer control (downsampled-data buffer)
    /// 
    /// In the current system design, the main buffer need never be a full-image
    /// buffer; any full-height buffers will be found inside the coefficient or
    /// postprocessing controllers.  Nonetheless, the main controller is not
    /// trivial.  Its responsibility is to provide context rows for upsampling/
    /// rescaling, and doing this in an efficient fashion is a bit tricky.
    /// 
    /// Postprocessor input data is counted in "row groups".  A row group
    /// is defined to be (v_samp_factor * DCT_scaled_size / min_DCT_scaled_size)
    /// sample rows of each component.  (We require DCT_scaled_size values to be
    /// chosen such that these numbers are integers.  In practice DCT_scaled_size
    /// values will likely be powers of two, so we actually have the stronger
    /// condition that DCT_scaled_size / min_DCT_scaled_size is an integer.)
    /// Upsampling will typically produce max_v_samp_factor pixel rows from each
    /// row group (times any additional scale factor that the upsampler is
    /// applying).
    /// 
    /// The coefficient controller will deliver data to us one iMCU row at a time;
    /// each iMCU row contains v_samp_factor * DCT_scaled_size sample rows, or
    /// exactly min_DCT_scaled_size row groups.  (This amount of data corresponds
    /// to one row of MCUs when the image is fully interleaved.)  Note that the
    /// number of sample rows varies across components, but the number of row
    /// groups does not.  Some garbage sample rows may be included in the last iMCU
    /// row at the bottom of the image.
    /// 
    /// Depending on the vertical scaling algorithm used, the upsampler may need
    /// access to the sample row(s) above and below its current input row group.
    /// The upsampler is required to set need_context_rows true at global selection
    /// time if so.  When need_context_rows is false, this controller can simply
    /// obtain one iMCU row at a time from the coefficient controller and dole it
    /// out as row groups to the postprocessor.
    /// 
    /// When need_context_rows is true, this controller guarantees that the buffer
    /// passed to postprocessing contains at least one row group's worth of samples
    /// above and below the row group(s) being processed.  Note that the context
    /// rows "above" the first passed row group appear at negative row offsets in
    /// the passed buffer.  At the top and bottom of the image, the required
    /// context rows are manufactured by duplicating the first or last real sample
    /// row; this avoids having special cases in the upsampling inner loops.
    /// 
    /// The amount of context is fixed at one row group just because that's a
    /// convenient number for this controller to work with.  The existing
    /// upsamplers really only need one sample row of context.  An upsampler
    /// supporting arbitrary output rescaling might wish for more than one row
    /// group of context when shrinking the image; tough, we don't handle that.
    /// (This is justified by the assumption that downsizing will be handled mostly
    /// by adjusting the DCT_scaled_size values, so that the actual scale factor at
    /// the upsample step needn't be much less than one.)
    /// 
    /// To provide the desired context, we have to retain the last two row groups
    /// of one iMCU row while reading in the next iMCU row.  (The last row group
    /// can't be processed until we have another row group for its below-context,
    /// and so we have to save the next-to-last group too for its above-context.)
    /// We could do this most simply by copying data around in our buffer, but
    /// that'd be very slow.  We can avoid copying any data by creating a rather
    /// strange pointer structure.  Here's how it works.  We allocate a workspace
    /// consisting of M+2 row groups (where M = min_DCT_scaled_size is the number
    /// of row groups per iMCU row).  We create two sets of redundant pointers to
    /// the workspace.  Labeling the physical row groups 0 to M+1, the synthesized
    /// pointer lists look like this:
    ///                   M+1                          M-1
    ///                   master pointer --> 0         master pointer --> 0
    ///                   1                            1
    ///                   ...                          ...
    ///                   M-3                          M-3
    ///                   M-2                           M
    ///                   M-1                          M+1
    ///                    M                           M-2
    ///                   M+1                          M-1
    ///                    0                            0
    /// We read alternate iMCU rows using each master pointer; thus the last two
    /// row groups of the previous iMCU row remain un-overwritten in the workspace.
    /// The pointer lists are set up so that the required context rows appear to
    /// be adjacent to the proper places when we pass the pointer lists to the
    /// upsampler.
    /// 
    /// The above pictures describe the normal state of the pointer lists.
    /// At top and bottom of the image, we diddle the pointer lists to duplicate
    /// the first or last sample row as necessary (this is cheaper than copying
    /// sample rows around).
    /// 
    /// This scheme breaks down if M less than 2, ie, min_DCT_scaled_size is 1.  In that
    /// situation each iMCU row provides only one row group so the buffering logic
    /// must be different (eg, we must read two iMCU rows before we can emit the
    /// first row group).  For now, we simply do not support providing context
    /// rows when min_DCT_scaled_size is 1.  That combination seems unlikely to
    /// be worth providing --- if someone wants a 1/8th-size preview, they probably
    /// want it quick and dirty, so a context-free upsampler is sufficient.
    /// </summary>
    class jpeg_d_main_controller
    {
        private enum DataProcessor
        {
            context_main,
            simple_main,
            crank_post
        }

        /* context_state values: */
        private const int CTX_PREPARE_FOR_IMCU = 0;   /* need to prepare for MCU row */
        private const int CTX_PROCESS_IMCU = 1;   /* feeding iMCU to postprocessor */
        private const int CTX_POSTPONED_ROW = 2;   /* feeding postponed row group */

        private DataProcessor m_dataProcessor;
        private jpeg_decompress_struct m_cinfo;

        /* Pointer to allocated workspace (M or M+2 row groups). */
        private byte[][][] m_buffer = new byte[JpegConstants.MAX_COMPONENTS][][];

        private bool m_buffer_full;       /* Have we gotten an iMCU row from decoder? */
        private int m_rowgroup_ctr;    /* counts row groups output to postprocessor */

        /* Remaining fields are only used in the context case. */

        private int[][][] m_funnyIndices = new int[2][][] { new int[JpegConstants.MAX_COMPONENTS][], new int[JpegConstants.MAX_COMPONENTS][]};
        private int[] m_funnyOffsets = new int[JpegConstants.MAX_COMPONENTS];
        private int m_whichFunny;           /* indicates which funny indices set is now in use */

        private int m_context_state;      /* process_data state machine status */
        private int m_rowgroups_avail; /* row groups available to postprocessor */
        private int m_iMCU_row_ctr;    /* counts iMCU rows to detect image top/bot */

        public jpeg_d_main_controller(jpeg_decompress_struct cinfo)
        {
            m_cinfo = cinfo;

            /* Allocate the workspace.
            * ngroups is the number of row groups we need.
            */
            int ngroups = cinfo.m_min_DCT_scaled_size;
            if (cinfo.m_upsample.NeedContextRows())
            {
                if (cinfo.m_min_DCT_scaled_size < 2) /* unsupported, see comments above */
                    cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);

                alloc_funny_pointers(); /* Alloc space for xbuffer[] lists */
                ngroups = cinfo.m_min_DCT_scaled_size + 2;
            }

            for (int ci = 0; ci < cinfo.m_num_components; ci++)
            {
                /* height of a row group of component */
                int rgroup = (cinfo.Comp_info[ci].V_samp_factor * cinfo.Comp_info[ci].DCT_scaled_size) / cinfo.m_min_DCT_scaled_size;

                m_buffer[ci] = jpeg_common_struct.AllocJpegSamples(
                    cinfo.Comp_info[ci].Width_in_blocks * cinfo.Comp_info[ci].DCT_scaled_size, 
                    rgroup * ngroups);
            }
        }

        /// <summary>
        /// Initialize for a processing pass.
        /// </summary>
        public void start_pass(J_BUF_MODE pass_mode)
        {
            switch (pass_mode)
            {
                case J_BUF_MODE.JBUF_PASS_THRU:
                    if (m_cinfo.m_upsample.NeedContextRows())
                    {
                        m_dataProcessor = DataProcessor.context_main;
                        make_funny_pointers(); /* Create the xbuffer[] lists */
                        m_whichFunny = 0; /* Read first iMCU row into xbuffer[0] */
                        m_context_state = CTX_PREPARE_FOR_IMCU;
                        m_iMCU_row_ctr = 0;
                    }
                    else
                    {
                        /* Simple case with no context needed */
                        m_dataProcessor = DataProcessor.simple_main;
                    }
                    m_buffer_full = false;  /* Mark buffer empty */
                    m_rowgroup_ctr = 0;
                    break;
                case J_BUF_MODE.JBUF_CRANK_DEST:
                    /* For last pass of 2-pass quantization, just crank the postprocessor */
                    m_dataProcessor = DataProcessor.crank_post;
                    break;
                default:
                    m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_BUFFER_MODE);
                    break;
            }
        }

        public void process_data(byte[][] output_buf, ref int out_row_ctr, int out_rows_avail)
        {
            switch (m_dataProcessor)
            {
                case DataProcessor.simple_main:
                    process_data_simple_main(output_buf, ref out_row_ctr, out_rows_avail);
                    break;

                case DataProcessor.context_main:
                    process_data_context_main(output_buf, ref out_row_ctr, out_rows_avail);
                    break;

                case DataProcessor.crank_post:
                    process_data_crank_post(output_buf, ref out_row_ctr, out_rows_avail);
                    break;

                default:
                    m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
                    break;
            }
        }

        /// <summary>
        /// Process some data.
        /// This handles the simple case where no context is required.
        /// </summary>
        private void process_data_simple_main(byte[][] output_buf, ref int out_row_ctr, int out_rows_avail)
        {
            ComponentBuffer[] cb = new ComponentBuffer[JpegConstants.MAX_COMPONENTS];
            for (int i = 0; i < JpegConstants.MAX_COMPONENTS; i++)
            {
                cb[i] = new ComponentBuffer();
                cb[i].SetBuffer(m_buffer[i], null, 0);
            }

            /* Read input data if we haven't filled the main buffer yet */
            if (!m_buffer_full)
            {
                if (m_cinfo.m_coef.decompress_data(cb) == ReadResult.JPEG_SUSPENDED)
                {
                    /* suspension forced, can do nothing more */
                    return;
                }

                /* OK, we have an iMCU row to work with */
                m_buffer_full = true;
            }

            /* There are always min_DCT_scaled_size row groups in an iMCU row. */
            int rowgroups_avail = m_cinfo.m_min_DCT_scaled_size;

            /* Note: at the bottom of the image, we may pass extra garbage row groups
             * to the postprocessor.  The postprocessor has to check for bottom
             * of image anyway (at row resolution), so no point in us doing it too.
             */

            /* Feed the postprocessor */
            m_cinfo.m_post.post_process_data(cb, ref m_rowgroup_ctr, rowgroups_avail, output_buf, ref out_row_ctr, out_rows_avail);

            /* Has postprocessor consumed all the data yet? If so, mark buffer empty */
            if (m_rowgroup_ctr >= rowgroups_avail)
            {
                m_buffer_full = false;
                m_rowgroup_ctr = 0;
            }
        }

        /// <summary>
        /// Process some data.
        /// This handles the case where context rows must be provided.
        /// </summary>
        private void process_data_context_main(byte[][] output_buf, ref int out_row_ctr, int out_rows_avail)
        {
            ComponentBuffer[] cb = new ComponentBuffer[m_cinfo.m_num_components];
            for (int i = 0; i < m_cinfo.m_num_components; i++)
            {
                cb[i] = new ComponentBuffer();
                cb[i].SetBuffer(m_buffer[i], m_funnyIndices[m_whichFunny][i], m_funnyOffsets[i]);
            }

            /* Read input data if we haven't filled the main buffer yet */
            if (!m_buffer_full)
            {
                if (m_cinfo.m_coef.decompress_data(cb) == ReadResult.JPEG_SUSPENDED)
                {
                    /* suspension forced, can do nothing more */
                    return;
                }

                /* OK, we have an iMCU row to work with */
                m_buffer_full = true;

                /* count rows received */
                m_iMCU_row_ctr++;
            }

            /* Postprocessor typically will not swallow all the input data it is handed
             * in one call (due to filling the output buffer first).  Must be prepared
             * to exit and restart.
     
     
             This switch lets us keep track of how far we got.
             * Note that each case falls through to the next on successful completion.
             */
            if (m_context_state == CTX_POSTPONED_ROW)
            {
                /* Call postprocessor using previously set pointers for postponed row */
                m_cinfo.m_post.post_process_data(cb, ref m_rowgroup_ctr,
                    m_rowgroups_avail, output_buf, ref out_row_ctr, out_rows_avail);

                if (m_rowgroup_ctr < m_rowgroups_avail)
                {
                    /* Need to suspend */
                    return;
                }

                m_context_state = CTX_PREPARE_FOR_IMCU;

                if (out_row_ctr >= out_rows_avail)
                {
                    /* Postprocessor exactly filled output buf */
                    return;
                }
            }

            if (m_context_state == CTX_PREPARE_FOR_IMCU)
            {
                /* Prepare to process first M-1 row groups of this iMCU row */
                m_rowgroup_ctr = 0;
                m_rowgroups_avail = m_cinfo.m_min_DCT_scaled_size - 1;

                /* Check for bottom of image: if so, tweak pointers to "duplicate"
                 * the last sample row, and adjust rowgroups_avail to ignore padding rows.
                 */
                if (m_iMCU_row_ctr == m_cinfo.m_total_iMCU_rows)
                    set_bottom_pointers();

                m_context_state = CTX_PROCESS_IMCU;
            }

            if (m_context_state == CTX_PROCESS_IMCU)
            {
                /* Call postprocessor using previously set pointers */
                m_cinfo.m_post.post_process_data(cb, ref m_rowgroup_ctr,
                    m_rowgroups_avail, output_buf, ref out_row_ctr, out_rows_avail);

                if (m_rowgroup_ctr < m_rowgroups_avail)
                {
                    /* Need to suspend */
                    return;
                }

                /* After the first iMCU, change wraparound pointers to normal state */
                if (m_iMCU_row_ctr == 1)
                    set_wraparound_pointers();

                /* Prepare to load new iMCU row using other xbuffer list */
                m_whichFunny ^= 1;    /* 0=>1 or 1=>0 */
                m_buffer_full = false;

                /* Still need to process last row group of this iMCU row, */
                /* which is saved at index M+1 of the other xbuffer */
                m_rowgroup_ctr = m_cinfo.m_min_DCT_scaled_size + 1;
                m_rowgroups_avail = m_cinfo.m_min_DCT_scaled_size + 2;
                m_context_state = CTX_POSTPONED_ROW;
            }
        }

        /// <summary>
        /// Process some data.
        /// Final pass of two-pass quantization: just call the postprocessor.
        /// Source data will be the postprocessor controller's internal buffer.
        /// </summary>
        private void process_data_crank_post(byte[][] output_buf, ref int out_row_ctr, int out_rows_avail)
        {
            int dummy = 0;
            m_cinfo.m_post.post_process_data(null, ref dummy, 0, output_buf, ref out_row_ctr, out_rows_avail);
        }

        /// <summary>
        /// Allocate space for the funny pointer lists.
        /// This is done only once, not once per pass.
        /// </summary>
        private void alloc_funny_pointers()
        {
            int M = m_cinfo.m_min_DCT_scaled_size;
            for (int ci = 0; ci < m_cinfo.m_num_components; ci++)
            {
                /* height of a row group of component */
                int rgroup = (m_cinfo.Comp_info[ci].V_samp_factor * m_cinfo.Comp_info[ci].DCT_scaled_size) / m_cinfo.m_min_DCT_scaled_size;

                /* Get space for pointer lists --- M+4 row groups in each list.
                 */
                m_funnyIndices[0][ci] = new int[rgroup * (M + 4)];
                m_funnyIndices[1][ci] = new int[rgroup * (M + 4)];
                m_funnyOffsets[ci] = rgroup;
            }
        }

        /// <summary>
        /// Create the funny pointer lists discussed in the comments above.
        /// The actual workspace is already allocated (in main.buffer),
        /// and the space for the pointer lists is allocated too.
        /// This routine just fills in the curiously ordered lists.
        /// This will be repeated at the beginning of each pass.
        /// </summary>
        private void make_funny_pointers()
        {
            int M = m_cinfo.m_min_DCT_scaled_size;
            for (int ci = 0; ci < m_cinfo.m_num_components; ci++)
            {
                /* height of a row group of component */
                int rgroup = (m_cinfo.Comp_info[ci].V_samp_factor * m_cinfo.Comp_info[ci].DCT_scaled_size) / m_cinfo.m_min_DCT_scaled_size;

                int[] ind0 = m_funnyIndices[0][ci];
                int[] ind1 = m_funnyIndices[1][ci];

                /* First copy the workspace pointers as-is */
                for (int i = 0; i < rgroup * (M + 2); i++)
                {
                    ind0[i + rgroup] = i;
                    ind1[i + rgroup] = i;
                }

                /* In the second list, put the last four row groups in swapped order */
                for (int i = 0; i < rgroup * 2; i++)
                {
                    ind1[rgroup * (M - 1) + i] = rgroup * M + i;
                    ind1[rgroup * (M + 1) + i] = rgroup * (M - 2) + i;
                }

                /* The wraparound pointers at top and bottom will be filled later
                 * (see set_wraparound_pointers, below).  Initially we want the "above"
                 * pointers to duplicate the first actual data line.  This only needs
                 * to happen in xbuffer[0].
                 */
                for (int i = 0; i < rgroup; i++)
                    ind0[i] = ind0[rgroup];
            }
        }

        /// <summary>
        /// Set up the "wraparound" pointers at top and bottom of the pointer lists.
        /// This changes the pointer list state from top-of-image to the normal state.
        /// </summary>
        private void set_wraparound_pointers()
        {
            int M = m_cinfo.m_min_DCT_scaled_size;
            for (int ci = 0; ci < m_cinfo.m_num_components; ci++)
            {
                /* height of a row group of component */
                int rgroup = (m_cinfo.Comp_info[ci].V_samp_factor * m_cinfo.Comp_info[ci].DCT_scaled_size) / m_cinfo.m_min_DCT_scaled_size;

                int[] ind0 = m_funnyIndices[0][ci];
                int[] ind1 = m_funnyIndices[1][ci];

                for (int i = 0; i < rgroup; i++)
                {
                    ind0[i] = ind0[rgroup * (M + 2) + i];
                    ind1[i] = ind1[rgroup * (M + 2) + i];

                    ind0[rgroup * (M + 3) + i] = ind0[i + rgroup];
                    ind1[rgroup * (M + 3) + i] = ind1[i + rgroup];
                }
            }
        }

        /// <summary>
        /// Change the pointer lists to duplicate the last sample row at the bottom
        /// of the image.  m_whichFunny indicates which m_funnyIndices holds the final iMCU row.
        /// Also sets rowgroups_avail to indicate number of nondummy row groups in row.
        /// </summary>
        private void set_bottom_pointers()
        {
            for (int ci = 0; ci < m_cinfo.m_num_components; ci++)
            {
                /* Count sample rows in one iMCU row and in one row group */
                int iMCUheight = m_cinfo.Comp_info[ci].V_samp_factor * m_cinfo.Comp_info[ci].DCT_scaled_size;
                int rgroup = iMCUheight / m_cinfo.m_min_DCT_scaled_size;

                /* Count nondummy sample rows remaining for this component */
                int rows_left = m_cinfo.Comp_info[ci].downsampled_height % iMCUheight;
                if (rows_left == 0)
                    rows_left = iMCUheight;

                /* Count nondummy row groups.  Should get same answer for each component,
                 * so we need only do it once.
                 */
                if (ci == 0)
                    m_rowgroups_avail = (rows_left - 1) / rgroup + 1;

                /* Duplicate the last real sample row rgroup*2 times; this pads out the
                 * last partial rowgroup and ensures at least one full rowgroup of context.
                 */
                for (int i = 0; i < rgroup * 2; i++)
                    m_funnyIndices[m_whichFunny][ci][rows_left + i + rgroup] = m_funnyIndices[m_whichFunny][ci][rows_left - 1 + rgroup];
            }
        }
    }
}
