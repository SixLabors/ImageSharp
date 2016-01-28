/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * Copyright (C) 1994-1996, Thomas G. Lane.
 * This file is part of the Independent JPEG Group's software.
 * For conditions of distribution and use, see the accompanying README file.
 *
 */

/*
 * This file contains code for merged upsampling/color conversion.
 *
 * This file combines functions from my_upsampler and jpeg_color_deconverter;
 * read those files first to understand what's going on.
 *
 * When the chroma components are to be upsampled by simple replication
 * (ie, box filtering), we can save some work in color conversion by
 * calculating all the output pixels corresponding to a pair of chroma
 * samples at one time.  In the conversion equations
 *  R = Y           + K1 * Cr
 *  G = Y + K2 * Cb + K3 * Cr
 *  B = Y + K4 * Cb
 * only the Y term varies among the group of pixels corresponding to a pair
 * of chroma samples, so the rest of the terms can be calculated just once.
 * At typical sampling ratios, this eliminates half or three-quarters of the
 * multiplications needed for color conversion.
 *
 * This file currently provides implementations for the following cases:
 *  YCbCr => RGB color conversion only.
 *  Sampling ratios of 2h1v or 2h2v.
 *  No scaling needed at upsample time.
 *  Corner-aligned (non-CCIR601) sampling alignment.
 * Other special cases could be added, but in most applications these are
 * the only common cases.  (For uncommon cases we fall back on the more
 * general code in my_upsampler and jpeg_color_deconverter)
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace BitMiracle.LibJpeg.Classic.Internal
{
    class my_merged_upsampler : jpeg_upsampler
    {
        private const int SCALEBITS = 16;  /* speediest right-shift on some machines */
        private const int ONE_HALF = 1 << (SCALEBITS - 1);

        private jpeg_decompress_struct m_cinfo;
        
        private bool m_use_2v_upsample;

        /* Private state for YCC->RGB conversion */
        private int[] m_Cr_r_tab;      /* => table for Cr to R conversion */
        private int[] m_Cb_b_tab;      /* => table for Cb to B conversion */
        private int[] m_Cr_g_tab;        /* => table for Cr to G conversion */
        private int[] m_Cb_g_tab;        /* => table for Cb to G conversion */

        /* For 2:1 vertical sampling, we produce two output rows at a time.
        * We need a "spare" row buffer to hold the second output row if the
        * application provides just a one-row buffer; we also use the spare
        * to discard the dummy last row if the image height is odd.
        */
        private byte[] m_spare_row;
        private bool m_spare_full;        /* T if spare buffer is occupied */

        private int m_out_row_width;   /* samples per output row */
        private int m_rows_to_go;  /* counts rows remaining in image */

        public my_merged_upsampler(jpeg_decompress_struct cinfo)
        {
            m_cinfo = cinfo;
            m_need_context_rows = false;

            m_out_row_width = cinfo.m_output_width * cinfo.m_out_color_components;

            if (cinfo.m_max_v_samp_factor == 2)
            {
                m_use_2v_upsample = true;
                /* Allocate a spare row buffer */
                m_spare_row = new byte[m_out_row_width];
            }
            else
            {
                m_use_2v_upsample = false;
            }

            build_ycc_rgb_table();
        }

        /// <summary>
        /// Initialize for an upsampling pass.
        /// </summary>
        public override void start_pass()
        {
            /* Mark the spare buffer empty */
            m_spare_full = false;

            /* Initialize total-height counter for detecting bottom of image */
            m_rows_to_go = m_cinfo.m_output_height;
        }

        public override void upsample(ComponentBuffer[] input_buf, ref int in_row_group_ctr, int in_row_groups_avail, byte[][] output_buf, ref int out_row_ctr, int out_rows_avail)
        {
            if (m_use_2v_upsample)
                merged_2v_upsample(input_buf, ref in_row_group_ctr, output_buf, ref out_row_ctr, out_rows_avail);
            else
                merged_1v_upsample(input_buf, ref in_row_group_ctr, output_buf, ref out_row_ctr);
        }

        /// <summary>
        /// Control routine to do upsampling (and color conversion).
        /// The control routine just handles the row buffering considerations.
        /// 1:1 vertical sampling case: much easier, never need a spare row.
        /// </summary>
        private void merged_1v_upsample(ComponentBuffer[] input_buf, ref int in_row_group_ctr, byte[][] output_buf, ref int out_row_ctr)
        {
            /* Just do the upsampling. */
            h2v1_merged_upsample(input_buf, in_row_group_ctr, output_buf, out_row_ctr);

            /* Adjust counts */
            out_row_ctr++;
            in_row_group_ctr++;
        }
        
        /// <summary>
        /// Control routine to do upsampling (and color conversion).
        /// The control routine just handles the row buffering considerations.
        /// 2:1 vertical sampling case: may need a spare row.
        /// </summary>
        private void merged_2v_upsample(ComponentBuffer[] input_buf, ref int in_row_group_ctr, byte[][] output_buf, ref int out_row_ctr, int out_rows_avail)
        {
            int num_rows;        /* number of rows returned to caller */
            if (m_spare_full)
            {
                /* If we have a spare row saved from a previous cycle, just return it. */
                byte[][] temp = new byte[1][];
                temp[0] = m_spare_row;
                JpegUtils.jcopy_sample_rows(temp, 0, output_buf, out_row_ctr, 1, m_out_row_width);
                num_rows = 1;
                m_spare_full = false;
            }
            else
            {
                /* Figure number of rows to return to caller. */
                num_rows = 2;

                /* Not more than the distance to the end of the image. */
                if (num_rows > m_rows_to_go)
                    num_rows = m_rows_to_go;
                
                /* And not more than what the client can accept: */
                out_rows_avail -= out_row_ctr;
                if (num_rows > out_rows_avail)
                    num_rows = out_rows_avail;
                
                /* Create output pointer array for upsampler. */
                byte[][] work_ptrs = new byte[2][];
                work_ptrs[0] = output_buf[out_row_ctr];
                if (num_rows > 1)
                {
                    work_ptrs[1] = output_buf[out_row_ctr + 1];
                }
                else
                {
                    work_ptrs[1] = m_spare_row;
                    m_spare_full = true;
                }

                /* Now do the upsampling. */
                h2v2_merged_upsample(input_buf, in_row_group_ctr, work_ptrs);
            }

            /* Adjust counts */
            out_row_ctr += num_rows;
            m_rows_to_go -= num_rows;

            /* When the buffer is emptied, declare this input row group consumed */
            if (!m_spare_full)
                in_row_group_ctr++;
        }

        /*
         * These are the routines invoked by the control routines to do
         * the actual upsampling/conversion.  One row group is processed per call.
         *
         * Note: since we may be writing directly into application-supplied buffers,
         * we have to be honest about the output width; we can't assume the buffer
         * has been rounded up to an even width.
         */

        /// <summary>
        /// Upsample and color convert for the case of 2:1 horizontal and 1:1 vertical.
        /// </summary>
        private void h2v1_merged_upsample(ComponentBuffer[] input_buf, int in_row_group_ctr, byte[][] output_buf, int outRow)
        {
            int inputIndex0 = 0;
            int inputIndex1 = 0;
            int inputIndex2 = 0;
            int outputIndex = 0;

            byte[] limit = m_cinfo.m_sample_range_limit;
            int limitOffset = m_cinfo.m_sampleRangeLimitOffset;

            /* Loop for each pair of output pixels */
            for (int col = m_cinfo.m_output_width >> 1; col > 0; col--)
            {
                /* Do the chroma part of the calculation */
                int cb = input_buf[1][in_row_group_ctr][inputIndex1];
                inputIndex1++;

                int cr = input_buf[2][in_row_group_ctr][inputIndex2];
                inputIndex2++;

                int cred = m_Cr_r_tab[cr];
                int cgreen = JpegUtils.RIGHT_SHIFT(m_Cb_g_tab[cb] + m_Cr_g_tab[cr], SCALEBITS);
                int cblue = m_Cb_b_tab[cb];

                /* Fetch 2 Y values and emit 2 pixels */
                int y = input_buf[0][in_row_group_ctr][inputIndex0];
                inputIndex0++;

                output_buf[outRow][outputIndex + JpegConstants.RGB_RED] = limit[limitOffset + y + cred];
                output_buf[outRow][outputIndex + JpegConstants.RGB_GREEN] = limit[limitOffset + y + cgreen];
                output_buf[outRow][outputIndex + JpegConstants.RGB_BLUE] = limit[limitOffset + y + cblue];
                outputIndex += JpegConstants.RGB_PIXELSIZE;
                
                y = input_buf[0][in_row_group_ctr][inputIndex0];
                inputIndex0++;

                output_buf[outRow][outputIndex + JpegConstants.RGB_RED] = limit[limitOffset + y + cred];
                output_buf[outRow][outputIndex + JpegConstants.RGB_GREEN] = limit[limitOffset + y + cgreen];
                output_buf[outRow][outputIndex + JpegConstants.RGB_BLUE] = limit[limitOffset + y + cblue];
                outputIndex += JpegConstants.RGB_PIXELSIZE;
            }

            /* If image width is odd, do the last output column separately */
            if ((m_cinfo.m_output_width & 1) != 0)
            {
                int cb = input_buf[1][in_row_group_ctr][inputIndex1];
                int cr = input_buf[2][in_row_group_ctr][inputIndex2];
                int cred = m_Cr_r_tab[cr];
                int cgreen = JpegUtils.RIGHT_SHIFT(m_Cb_g_tab[cb] + m_Cr_g_tab[cr], SCALEBITS);
                int cblue = m_Cb_b_tab[cb];
                
                int y = input_buf[0][in_row_group_ctr][inputIndex0];
                output_buf[outRow][outputIndex + JpegConstants.RGB_RED] = limit[limitOffset + y + cred];
                output_buf[outRow][outputIndex + JpegConstants.RGB_GREEN] = limit[limitOffset + y + cgreen];
                output_buf[outRow][outputIndex + JpegConstants.RGB_BLUE] = limit[limitOffset + y + cblue];
            }
        }

        /// <summary>
        /// Upsample and color convert for the case of 2:1 horizontal and 2:1 vertical.
        /// </summary>
        private void h2v2_merged_upsample(ComponentBuffer[] input_buf, int in_row_group_ctr, byte[][] output_buf)
        {
            int inputRow00 = in_row_group_ctr * 2;
            int inputIndex00 = 0;

            int inputRow01 = in_row_group_ctr * 2 + 1;
            int inputIndex01 = 0;

            int inputIndex1 = 0;
            int inputIndex2 = 0;

            int outIndex0 = 0;
            int outIndex1 = 0;

            byte[] limit = m_cinfo.m_sample_range_limit;
            int limitOffset = m_cinfo.m_sampleRangeLimitOffset;

            /* Loop for each group of output pixels */
            for (int col = m_cinfo.m_output_width >> 1; col > 0; col--)
            {
                /* Do the chroma part of the calculation */
                int cb = input_buf[1][in_row_group_ctr][inputIndex1];
                inputIndex1++;

                int cr = input_buf[2][in_row_group_ctr][inputIndex2];
                inputIndex2++;

                int cred = m_Cr_r_tab[cr];
                int cgreen = JpegUtils.RIGHT_SHIFT(m_Cb_g_tab[cb] + m_Cr_g_tab[cr], SCALEBITS);
                int cblue = m_Cb_b_tab[cb];

                /* Fetch 4 Y values and emit 4 pixels */
                int y = input_buf[0][inputRow00][inputIndex00];
                inputIndex00++;

                output_buf[0][outIndex0 + JpegConstants.RGB_RED] = limit[limitOffset + y + cred];
                output_buf[0][outIndex0 + JpegConstants.RGB_GREEN] = limit[limitOffset + y + cgreen];
                output_buf[0][outIndex0 + JpegConstants.RGB_BLUE] = limit[limitOffset + y + cblue];
                outIndex0 += JpegConstants.RGB_PIXELSIZE;
                
                y = input_buf[0][inputRow00][inputIndex00];
                inputIndex00++;

                output_buf[0][outIndex0 + JpegConstants.RGB_RED] = limit[limitOffset + y + cred];
                output_buf[0][outIndex0 + JpegConstants.RGB_GREEN] = limit[limitOffset + y + cgreen];
                output_buf[0][outIndex0 + JpegConstants.RGB_BLUE] = limit[limitOffset + y + cblue];
                outIndex0 += JpegConstants.RGB_PIXELSIZE;
                
                y = input_buf[0][inputRow01][inputIndex01];
                inputIndex01++;

                output_buf[1][outIndex1 + JpegConstants.RGB_RED] = limit[limitOffset + y + cred];
                output_buf[1][outIndex1 + JpegConstants.RGB_GREEN] = limit[limitOffset + y + cgreen];
                output_buf[1][outIndex1 + JpegConstants.RGB_BLUE] = limit[limitOffset + y + cblue];
                outIndex1 += JpegConstants.RGB_PIXELSIZE;
                
                y = input_buf[0][inputRow01][inputIndex01];
                inputIndex01++;

                output_buf[1][outIndex1 + JpegConstants.RGB_RED] = limit[limitOffset + y + cred];
                output_buf[1][outIndex1 + JpegConstants.RGB_GREEN] = limit[limitOffset + y + cgreen];
                output_buf[1][outIndex1 + JpegConstants.RGB_BLUE] = limit[limitOffset + y + cblue];
                outIndex1 += JpegConstants.RGB_PIXELSIZE;
            }

            /* If image width is odd, do the last output column separately */
            if ((m_cinfo.m_output_width & 1) != 0)
            {
                int cb = input_buf[1][in_row_group_ctr][inputIndex1];
                int cr = input_buf[2][in_row_group_ctr][inputIndex2];
                int cred = m_Cr_r_tab[cr];
                int cgreen = JpegUtils.RIGHT_SHIFT(m_Cb_g_tab[cb] + m_Cr_g_tab[cr], SCALEBITS);
                int cblue = m_Cb_b_tab[cb];

                int y = input_buf[0][inputRow00][inputIndex00];
                output_buf[0][outIndex0 + JpegConstants.RGB_RED] = limit[limitOffset + y + cred];
                output_buf[0][outIndex0 + JpegConstants.RGB_GREEN] = limit[limitOffset + y + cgreen];
                output_buf[0][outIndex0 + JpegConstants.RGB_BLUE] = limit[limitOffset + y + cblue];
                
                y = input_buf[0][inputRow01][inputIndex01];
                output_buf[1][outIndex1 + JpegConstants.RGB_RED] = limit[limitOffset + y + cred];
                output_buf[1][outIndex1 + JpegConstants.RGB_GREEN] = limit[limitOffset + y + cgreen];
                output_buf[1][outIndex1 + JpegConstants.RGB_BLUE] = limit[limitOffset + y + cblue];
            }
        }

        /// <summary>
        /// Initialize tables for YCC->RGB colorspace conversion.
        /// This is taken directly from jpeg_color_deconverter; see that file for more info.
        /// </summary>
        private void build_ycc_rgb_table()
        {
            m_Cr_r_tab = new int[JpegConstants.MAXJSAMPLE + 1];
            m_Cb_b_tab = new int[JpegConstants.MAXJSAMPLE + 1];
            m_Cr_g_tab = new int[JpegConstants.MAXJSAMPLE + 1];
            m_Cb_g_tab = new int[JpegConstants.MAXJSAMPLE + 1];

            for (int i = 0, x = -JpegConstants.CENTERJSAMPLE; i <= JpegConstants.MAXJSAMPLE; i++, x++)
            {
                /* i is the actual input pixel value, in the range 0..MAXJSAMPLE */
                /* The Cb or Cr value we are thinking of is x = i - CENTERJSAMPLE */
                /* Cr=>R value is nearest int to 1.40200 * x */
                m_Cr_r_tab[i] = JpegUtils.RIGHT_SHIFT(FIX(1.40200) * x + ONE_HALF, SCALEBITS);

                /* Cb=>B value is nearest int to 1.77200 * x */
                m_Cb_b_tab[i] = JpegUtils.RIGHT_SHIFT(FIX(1.77200) * x + ONE_HALF, SCALEBITS);

                /* Cr=>G value is scaled-up -0.71414 * x */
                m_Cr_g_tab[i] = (-FIX(0.71414)) * x;

                /* Cb=>G value is scaled-up -0.34414 * x */
                /* We also add in ONE_HALF so that need not do it in inner loop */
                m_Cb_g_tab[i] = (-FIX(0.34414)) * x + ONE_HALF;
            }
        }

        private static int FIX(double x)
        {
            return ((int)((x) * (1L << SCALEBITS) + 0.5));
        }
    }
}
