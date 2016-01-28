/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * Copyright (C) 1994-1996, Thomas G. Lane.
 * This file is part of the Independent JPEG Group's software.
 * For conditions of distribution and use, see the accompanying README file.
 *
 */

/*
 * This file contains master control logic for the JPEG decompressor.
 * These routines are concerned with selecting the modules to be executed
 * and with determining the number of passes and the work to be done in each
 * pass.
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace BitMiracle.LibJpeg.Classic.Internal
{
    /// <summary>
    /// Master control module
    /// </summary>
    class jpeg_decomp_master
    {
        private jpeg_decompress_struct m_cinfo;

        private int m_pass_number;        /* # of passes completed */
        private bool m_is_dummy_pass; /* True during 1st pass for 2-pass quant */

        private bool m_using_merged_upsample; /* true if using merged upsample/cconvert */

        /* Saved references to initialized quantizer modules,
        * in case we need to switch modes.
        */
        private jpeg_color_quantizer m_quantizer_1pass;
        private jpeg_color_quantizer m_quantizer_2pass;

        public jpeg_decomp_master(jpeg_decompress_struct cinfo)
        {
            m_cinfo = cinfo;
            master_selection();
        }

        /// <summary>
        /// Per-pass setup.
        /// This is called at the beginning of each output pass.  We determine which
        /// modules will be active during this pass and give them appropriate
        /// start_pass calls.  We also set is_dummy_pass to indicate whether this
        /// is a "real" output pass or a dummy pass for color quantization.
        /// (In the latter case, we will crank the pass to completion.)
        /// </summary>
        public void prepare_for_output_pass()
        {
            if (m_is_dummy_pass)
            {
                /* Final pass of 2-pass quantization */
                m_is_dummy_pass = false;
                m_cinfo.m_cquantize.start_pass(false);
                m_cinfo.m_post.start_pass(J_BUF_MODE.JBUF_CRANK_DEST);
                m_cinfo.m_main.start_pass(J_BUF_MODE.JBUF_CRANK_DEST);
            }
            else
            {
                if (m_cinfo.m_quantize_colors && m_cinfo.m_colormap == null)
                {
                    /* Select new quantization method */
                    if (m_cinfo.m_two_pass_quantize && m_cinfo.m_enable_2pass_quant)
                    {
                        m_cinfo.m_cquantize = m_quantizer_2pass;
                        m_is_dummy_pass = true;
                    }
                    else if (m_cinfo.m_enable_1pass_quant)
                        m_cinfo.m_cquantize = m_quantizer_1pass;
                    else
                        m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_MODE_CHANGE);
                }

                m_cinfo.m_idct.start_pass();
                m_cinfo.m_coef.start_output_pass();

                if (!m_cinfo.m_raw_data_out)
                {
                    m_cinfo.m_upsample.start_pass();

                    if (m_cinfo.m_quantize_colors)
                        m_cinfo.m_cquantize.start_pass(m_is_dummy_pass);

                    m_cinfo.m_post.start_pass((m_is_dummy_pass ? J_BUF_MODE.JBUF_SAVE_AND_PASS : J_BUF_MODE.JBUF_PASS_THRU));
                    m_cinfo.m_main.start_pass(J_BUF_MODE.JBUF_PASS_THRU);
                }
            }

            /* Set up progress monitor's pass info if present */
            if (m_cinfo.m_progress != null)
            {
                m_cinfo.m_progress.Completed_passes = m_pass_number;
                m_cinfo.m_progress.Total_passes = m_pass_number + (m_is_dummy_pass ? 2 : 1);

                /* In buffered-image mode, we assume one more output pass if EOI not
                 * yet reached, but no more passes if EOI has been reached.
                 */
                if (m_cinfo.m_buffered_image && !m_cinfo.m_inputctl.EOIReached())
                    m_cinfo.m_progress.Total_passes += (m_cinfo.m_enable_2pass_quant ? 2 : 1);
            }
        }

        /// <summary>
        /// Finish up at end of an output pass.
        /// </summary>
        public void finish_output_pass()
        {
            if (m_cinfo.m_quantize_colors)
                m_cinfo.m_cquantize.finish_pass();

            m_pass_number++;
        }

        public bool IsDummyPass()
        {
            return m_is_dummy_pass;
        }

        /// <summary>
        /// Master selection of decompression modules.
        /// This is done once at jpeg_start_decompress time.  We determine
        /// which modules will be used and give them appropriate initialization calls.
        /// We also initialize the decompressor input side to begin consuming data.
        /// 
        /// Since jpeg_read_header has finished, we know what is in the SOF
        /// and (first) SOS markers.  We also have all the application parameter
        /// settings.
        /// </summary>
        private void master_selection()
        {
            /* Initialize dimensions and other stuff */
            m_cinfo.jpeg_calc_output_dimensions();
            prepare_range_limit_table();

            /* Width of an output scanline must be representable as int. */
            long samplesperrow = m_cinfo.m_output_width * m_cinfo.m_out_color_components;
            int jd_samplesperrow = (int)samplesperrow;
            if ((long)jd_samplesperrow != samplesperrow)
                m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_WIDTH_OVERFLOW);

            /* Initialize my private state */
            m_pass_number = 0;
            m_using_merged_upsample = m_cinfo.use_merged_upsample();

            /* Color quantizer selection */
            m_quantizer_1pass = null;
            m_quantizer_2pass = null;

            /* No mode changes if not using buffered-image mode. */
            if (!m_cinfo.m_quantize_colors || !m_cinfo.m_buffered_image)
            {
                m_cinfo.m_enable_1pass_quant = false;
                m_cinfo.m_enable_external_quant = false;
                m_cinfo.m_enable_2pass_quant = false;
            }

            if (m_cinfo.m_quantize_colors)
            {
                if (m_cinfo.m_raw_data_out)
                    m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);

                /* 2-pass quantizer only works in 3-component color space. */
                if (m_cinfo.m_out_color_components != 3)
                {
                    m_cinfo.m_enable_1pass_quant = true;
                    m_cinfo.m_enable_external_quant = false;
                    m_cinfo.m_enable_2pass_quant = false;
                    m_cinfo.m_colormap = null;
                }
                else if (m_cinfo.m_colormap != null)
                    m_cinfo.m_enable_external_quant = true;
                else if (m_cinfo.m_two_pass_quantize)
                    m_cinfo.m_enable_2pass_quant = true;
                else
                    m_cinfo.m_enable_1pass_quant = true;

                if (m_cinfo.m_enable_1pass_quant)
                {
                    m_cinfo.m_cquantize = new my_1pass_cquantizer(m_cinfo);
                    m_quantizer_1pass = m_cinfo.m_cquantize;
                }

                /* We use the 2-pass code to map to external colormaps. */
                if (m_cinfo.m_enable_2pass_quant || m_cinfo.m_enable_external_quant)
                {
                    m_cinfo.m_cquantize = new my_2pass_cquantizer(m_cinfo);
                    m_quantizer_2pass = m_cinfo.m_cquantize;
                }
                /* If both quantizers are initialized, the 2-pass one is left active;
                 * this is necessary for starting with quantization to an external map.
                 */
            }

            /* Post-processing: in particular, color conversion first */
            if (!m_cinfo.m_raw_data_out)
            {
                if (m_using_merged_upsample)
                {
                    /* does color conversion too */
                    m_cinfo.m_upsample = new my_merged_upsampler(m_cinfo);
                }
                else
                {
                    m_cinfo.m_cconvert = new jpeg_color_deconverter(m_cinfo);
                    m_cinfo.m_upsample = new my_upsampler(m_cinfo);
                }

                m_cinfo.m_post = new jpeg_d_post_controller(m_cinfo, m_cinfo.m_enable_2pass_quant);
            }

            /* Inverse DCT */
            m_cinfo.m_idct = new jpeg_inverse_dct(m_cinfo);

            if (m_cinfo.m_progressive_mode)
                m_cinfo.m_entropy = new phuff_entropy_decoder(m_cinfo);
            else
                m_cinfo.m_entropy = new huff_entropy_decoder(m_cinfo);

            /* Initialize principal buffer controllers. */
            bool use_c_buffer = m_cinfo.m_inputctl.HasMultipleScans() || m_cinfo.m_buffered_image;
            m_cinfo.m_coef = new jpeg_d_coef_controller(m_cinfo, use_c_buffer);

            if (!m_cinfo.m_raw_data_out)
                m_cinfo.m_main = new jpeg_d_main_controller(m_cinfo);

            /* Initialize input side of decompressor to consume first scan. */
            m_cinfo.m_inputctl.start_input_pass();

            /* If jpeg_start_decompress will read the whole file, initialize
             * progress monitoring appropriately.  The input step is counted
             * as one pass.
             */
            if (m_cinfo.m_progress != null && !m_cinfo.m_buffered_image && m_cinfo.m_inputctl.HasMultipleScans())
            {
                /* Estimate number of scans to set pass_limit. */
                int nscans;
                if (m_cinfo.m_progressive_mode)
                {
                    /* Arbitrarily estimate 2 interleaved DC scans + 3 AC scans/component. */
                    nscans = 2 + 3 * m_cinfo.m_num_components;
                }
                else
                {
                    /* For a non progressive multiscan file, estimate 1 scan per component. */
                    nscans = m_cinfo.m_num_components;
                }

                m_cinfo.m_progress.Pass_counter = 0;
                m_cinfo.m_progress.Pass_limit = m_cinfo.m_total_iMCU_rows * nscans;
                m_cinfo.m_progress.Completed_passes = 0;
                m_cinfo.m_progress.Total_passes = (m_cinfo.m_enable_2pass_quant ? 3 : 2);

                /* Count the input pass as done */
                m_pass_number++;
            }
        }

        /// <summary>
        /// Allocate and fill in the sample_range_limit table.
        /// 
        /// Several decompression processes need to range-limit values to the range
        /// 0..MAXJSAMPLE; the input value may fall somewhat outside this range
        /// due to noise introduced by quantization, roundoff error, etc. These
        /// processes are inner loops and need to be as fast as possible. On most
        /// machines, particularly CPUs with pipelines or instruction prefetch,
        /// a (subscript-check-less) C table lookup
        ///     x = sample_range_limit[x];
        /// is faster than explicit tests
        /// <c>
        ///     if (x &amp; 0)
        ///        x = 0;
        ///     else if (x > MAXJSAMPLE)
        ///        x = MAXJSAMPLE;
        /// </c>
        /// These processes all use a common table prepared by the routine below.
        /// 
        /// For most steps we can mathematically guarantee that the initial value
        /// of x is within MAXJSAMPLE + 1 of the legal range, so a table running from
        /// -(MAXJSAMPLE + 1) to 2 * MAXJSAMPLE + 1 is sufficient.  But for the initial
        /// limiting step (just after the IDCT), a wildly out-of-range value is 
        /// possible if the input data is corrupt.  To avoid any chance of indexing
        /// off the end of memory and getting a bad-pointer trap, we perform the
        /// post-IDCT limiting thus: <c>x = range_limit[x &amp; MASK];</c>
        /// where MASK is 2 bits wider than legal sample data, ie 10 bits for 8-bit
        /// samples.  Under normal circumstances this is more than enough range and
        /// a correct output will be generated; with bogus input data the mask will
        /// cause wraparound, and we will safely generate a bogus-but-in-range output.
        /// For the post-IDCT step, we want to convert the data from signed to unsigned
        /// representation by adding CENTERJSAMPLE at the same time that we limit it.
        /// So the post-IDCT limiting table ends up looking like this:
        /// <pre>
        ///     CENTERJSAMPLE, CENTERJSAMPLE + 1, ..., MAXJSAMPLE,
        ///     MAXJSAMPLE (repeat 2 * (MAXJSAMPLE + 1) - CENTERJSAMPLE times),
        ///     0          (repeat 2 * (MAXJSAMPLE + 1) - CENTERJSAMPLE times),
        ///     0, 1, ..., CENTERJSAMPLE - 1
        /// </pre>
        /// Negative inputs select values from the upper half of the table after
        /// masking.
        /// 
        /// We can save some space by overlapping the start of the post-IDCT table
        /// with the simpler range limiting table.  The post-IDCT table begins at
        /// sample_range_limit + CENTERJSAMPLE.
        /// 
        /// Note that the table is allocated in near data space on PCs; it's small
        /// enough and used often enough to justify this.
        /// </summary>
        private void prepare_range_limit_table()
        {
            byte[] table = new byte[5 * (JpegConstants.MAXJSAMPLE + 1) + JpegConstants.CENTERJSAMPLE];

            /* allow negative subscripts of simple table */
            int tableOffset = JpegConstants.MAXJSAMPLE + 1;
            m_cinfo.m_sample_range_limit = table;
            m_cinfo.m_sampleRangeLimitOffset = tableOffset;

            /* First segment of "simple" table: limit[x] = 0 for x < 0 */
            Array.Clear(table, 0, JpegConstants.MAXJSAMPLE + 1);

            /* Main part of "simple" table: limit[x] = x */
            for (int i = 0; i <= JpegConstants.MAXJSAMPLE; i++)
                table[tableOffset + i] = (byte) i;

            tableOffset += JpegConstants.CENTERJSAMPLE; /* Point to where post-IDCT table starts */

            /* End of simple table, rest of first half of post-IDCT table */
            for (int i = JpegConstants.CENTERJSAMPLE; i < 2 * (JpegConstants.MAXJSAMPLE + 1); i++)
                table[tableOffset + i] = JpegConstants.MAXJSAMPLE;

            /* Second half of post-IDCT table */
            Array.Clear(table, tableOffset + 2 * (JpegConstants.MAXJSAMPLE + 1),
                2 * (JpegConstants.MAXJSAMPLE + 1) - JpegConstants.CENTERJSAMPLE);

            Buffer.BlockCopy(m_cinfo.m_sample_range_limit, 0, table,
                tableOffset + 4 * (JpegConstants.MAXJSAMPLE + 1) - JpegConstants.CENTERJSAMPLE, JpegConstants.CENTERJSAMPLE);
        }
    }
}
