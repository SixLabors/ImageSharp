/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * Copyright (C) 1994-1996, Thomas G. Lane.
 * This file is part of the Independent JPEG Group's software.
 * For conditions of distribution and use, see the accompanying README file.
 *
 */

/*
 * This file contains upsampling routines.
 *
 * Upsampling input data is counted in "row groups".  A row group
 * is defined to be (v_samp_factor * DCT_scaled_size / min_DCT_scaled_size)
 * sample rows of each component.  Upsampling will normally produce
 * max_v_samp_factor pixel rows from each row group (but this could vary
 * if the upsampler is applying a scale factor of its own).
 *
 * An excellent reference for image resampling is
 *   Digital Image Warping, George Wolberg, 1990.
 *   Pub. by IEEE Computer Society Press, Los Alamitos, CA. ISBN 0-8186-8944-7.
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace BitMiracle.LibJpeg.Classic.Internal
{
    class my_upsampler : jpeg_upsampler
    {
        private enum ComponentUpsampler
        {
            noop_upsampler,
            fullsize_upsampler,
            h2v1_fancy_upsampler,
            h2v1_upsampler,
            h2v2_fancy_upsampler,
            h2v2_upsampler,
            int_upsampler
        }

        private jpeg_decompress_struct m_cinfo;

        /* Color conversion buffer.  When using separate upsampling and color
        * conversion steps, this buffer holds one upsampled row group until it
        * has been color converted and output.
        * Note: we do not allocate any storage for component(s) which are full-size,
        * ie do not need rescaling.  The corresponding entry of color_buf[] is
        * simply set to point to the input data array, thereby avoiding copying.
        */
        private ComponentBuffer[] m_color_buf = new ComponentBuffer[JpegConstants.MAX_COMPONENTS];

        // used only for fullsize_upsampler mode
        private int[] m_perComponentOffsets = new int[JpegConstants.MAX_COMPONENTS];

        /* Per-component upsampling method pointers */
        private ComponentUpsampler[] m_upsampleMethods = new ComponentUpsampler[JpegConstants.MAX_COMPONENTS];
        private int m_currentComponent; // component being upsampled
        private int m_upsampleRowOffset;
        
        private int m_next_row_out;       /* counts rows emitted from color_buf */
        private int m_rows_to_go;  /* counts rows remaining in image */

        /* Height of an input row group for each component. */
        private int[] m_rowgroup_height = new int[JpegConstants.MAX_COMPONENTS];

        /* These arrays save pixel expansion factors so that int_expand need not
        * recompute them each time.  They are unused for other upsampling methods.
        */
        private byte[] m_h_expand = new byte[JpegConstants.MAX_COMPONENTS];
        private byte[] m_v_expand = new byte[JpegConstants.MAX_COMPONENTS];

        public my_upsampler(jpeg_decompress_struct cinfo)
        {
            m_cinfo = cinfo;
            m_need_context_rows = false; /* until we find out differently */

            if (cinfo.m_CCIR601_sampling)    /* this isn't supported */
                cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CCIR601_NOTIMPL);

            /* jpeg_d_main_controller doesn't support context rows when min_DCT_scaled_size = 1,
            * so don't ask for it.
            */
            bool do_fancy = cinfo.m_do_fancy_upsampling && cinfo.m_min_DCT_scaled_size > 1;

            /* Verify we can handle the sampling factors, select per-component methods,
            * and create storage as needed.
            */
            for (int ci = 0; ci < cinfo.m_num_components; ci++)
            {
                jpeg_component_info componentInfo = cinfo.Comp_info[ci];

                /* Compute size of an "input group" after IDCT scaling.  This many samples
                * are to be converted to max_h_samp_factor * max_v_samp_factor pixels.
                */
                int h_in_group = (componentInfo.H_samp_factor * componentInfo.DCT_scaled_size) / cinfo.m_min_DCT_scaled_size;
                int v_in_group = (componentInfo.V_samp_factor * componentInfo.DCT_scaled_size) / cinfo.m_min_DCT_scaled_size;
                int h_out_group = cinfo.m_max_h_samp_factor;
                int v_out_group = cinfo.m_max_v_samp_factor;

                /* save for use later */
                m_rowgroup_height[ci] = v_in_group;
                bool need_buffer = true;
                if (!componentInfo.component_needed)
                {
                    /* Don't bother to upsample an uninteresting component. */
                    m_upsampleMethods[ci] = ComponentUpsampler.noop_upsampler;
                    need_buffer = false;
                }
                else if (h_in_group == h_out_group && v_in_group == v_out_group)
                {
                    /* Fullsize components can be processed without any work. */
                    m_upsampleMethods[ci] = ComponentUpsampler.fullsize_upsampler;
                    need_buffer = false;
                }
                else if (h_in_group * 2 == h_out_group && v_in_group == v_out_group)
                {
                    /* Special cases for 2h1v upsampling */
                    if (do_fancy && componentInfo.downsampled_width > 2)
                        m_upsampleMethods[ci] = ComponentUpsampler.h2v1_fancy_upsampler;
                    else
                        m_upsampleMethods[ci] = ComponentUpsampler.h2v1_upsampler;
                }
                else if (h_in_group * 2 == h_out_group && v_in_group * 2 == v_out_group)
                {
                    /* Special cases for 2h2v upsampling */
                    if (do_fancy && componentInfo.downsampled_width > 2)
                    {
                        m_upsampleMethods[ci] = ComponentUpsampler.h2v2_fancy_upsampler;
                        m_need_context_rows = true;
                    }
                    else
                    {
                        m_upsampleMethods[ci] = ComponentUpsampler.h2v2_upsampler;
                    }
                }
                else if ((h_out_group % h_in_group) == 0 && (v_out_group % v_in_group) == 0)
                {
                    /* Generic integral-factors upsampling method */
                    m_upsampleMethods[ci] = ComponentUpsampler.int_upsampler;
                    m_h_expand[ci] = (byte) (h_out_group / h_in_group);
                    m_v_expand[ci] = (byte) (v_out_group / v_in_group);
                }
                else
                    cinfo.ERREXIT(J_MESSAGE_CODE.JERR_FRACT_SAMPLE_NOTIMPL);

                if (need_buffer)
                {
                    ComponentBuffer cb = new ComponentBuffer();
                    cb.SetBuffer(jpeg_common_struct.AllocJpegSamples(JpegUtils.jround_up(cinfo.m_output_width, 
                        cinfo.m_max_h_samp_factor), cinfo.m_max_v_samp_factor), null, 0);

                    m_color_buf[ci] = cb;
                }
            }
        }

        /// <summary>
        /// Initialize for an upsampling pass.
        /// </summary>
        public override void start_pass()
        {
            /* Mark the conversion buffer empty */
            m_next_row_out = m_cinfo.m_max_v_samp_factor;

            /* Initialize total-height counter for detecting bottom of image */
            m_rows_to_go = m_cinfo.m_output_height;
        }

        /// <summary>
        /// Control routine to do upsampling (and color conversion).
        /// 
        /// In this version we upsample each component independently.
        /// We upsample one row group into the conversion buffer, then apply
        /// color conversion a row at a time.
        /// </summary>
        public override void upsample(ComponentBuffer[] input_buf, ref int in_row_group_ctr, int in_row_groups_avail, byte[][] output_buf, ref int out_row_ctr, int out_rows_avail)
        {
            /* Fill the conversion buffer, if it's empty */
            if (m_next_row_out >= m_cinfo.m_max_v_samp_factor)
            {
                for (int ci = 0; ci < m_cinfo.m_num_components; ci++)
                {
                    m_perComponentOffsets[ci] = 0;

                    /* Invoke per-component upsample method.*/
                    m_currentComponent = ci;
                    m_upsampleRowOffset = in_row_group_ctr * m_rowgroup_height[ci];
                    upsampleComponent(ref input_buf[ci]);
                }

                m_next_row_out = 0;
            }

            /* Color-convert and emit rows */

            /* How many we have in the buffer: */
            int num_rows = m_cinfo.m_max_v_samp_factor - m_next_row_out;

            /* Not more than the distance to the end of the image.  Need this test
             * in case the image height is not a multiple of max_v_samp_factor:
             */
            if (num_rows > m_rows_to_go)
                num_rows = m_rows_to_go;

            /* And not more than what the client can accept: */
            out_rows_avail -= out_row_ctr;
            if (num_rows > out_rows_avail)
                num_rows = out_rows_avail;

            m_cinfo.m_cconvert.color_convert(m_color_buf, m_perComponentOffsets, m_next_row_out, output_buf, out_row_ctr, num_rows);

            /* Adjust counts */
            out_row_ctr += num_rows;
            m_rows_to_go -= num_rows;
            m_next_row_out += num_rows;

            /* When the buffer is emptied, declare this input row group consumed */
            if (m_next_row_out >= m_cinfo.m_max_v_samp_factor)
                in_row_group_ctr++;
        }

        private void upsampleComponent(ref ComponentBuffer input_data)
        {
            switch (m_upsampleMethods[m_currentComponent])
            {
                case ComponentUpsampler.noop_upsampler:
                    noop_upsample();
                    break;
                case ComponentUpsampler.fullsize_upsampler:
                    fullsize_upsample(ref input_data);
                    break;
                case ComponentUpsampler.h2v1_fancy_upsampler:
                    h2v1_fancy_upsample(m_cinfo.Comp_info[m_currentComponent].downsampled_width, ref input_data);
                    break;
                case ComponentUpsampler.h2v1_upsampler:
                    h2v1_upsample(ref input_data);
                    break;
                case ComponentUpsampler.h2v2_fancy_upsampler:
                    h2v2_fancy_upsample(m_cinfo.Comp_info[m_currentComponent].downsampled_width, ref input_data);
                    break;
                case ComponentUpsampler.h2v2_upsampler:
                    h2v2_upsample(ref input_data);
                    break;
                case ComponentUpsampler.int_upsampler:
                    int_upsample(ref input_data);
                    break;
                default:
                    m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
                    break;
            }
        }

        /*
         * These are the routines invoked to upsample pixel values
         * of a single component.  One row group is processed per call.
         */

        /// <summary>
        /// This is a no-op version used for "uninteresting" components.
        /// These components will not be referenced by color conversion.
        /// </summary>
        private static void noop_upsample()
        {
            // do nothing
        }

        /// <summary>
        /// For full-size components, we just make color_buf[ci] point at the
        /// input buffer, and thus avoid copying any data.  Note that this is
        /// safe only because sep_upsample doesn't declare the input row group
        /// "consumed" until we are done color converting and emitting it.
        /// </summary>
        private void fullsize_upsample(ref ComponentBuffer input_data)
        {
            m_color_buf[m_currentComponent] = input_data;
            m_perComponentOffsets[m_currentComponent] = m_upsampleRowOffset;
        }

        /// <summary>
        /// Fancy processing for the common case of 2:1 horizontal and 1:1 vertical.
        /// 
        /// The upsampling algorithm is linear interpolation between pixel centers,
        /// also known as a "triangle filter".  This is a good compromise between
        /// speed and visual quality.  The centers of the output pixels are 1/4 and 3/4
        /// of the way between input pixel centers.
        /// 
        /// A note about the "bias" calculations: when rounding fractional values to
        /// integer, we do not want to always round 0.5 up to the next integer.
        /// If we did that, we'd introduce a noticeable bias towards larger values.
        /// Instead, this code is arranged so that 0.5 will be rounded up or down at
        /// alternate pixel locations (a simple ordered dither pattern).
        /// </summary>
        private void h2v1_fancy_upsample(int downsampled_width, ref ComponentBuffer input_data)
        {
            ComponentBuffer output_data = m_color_buf[m_currentComponent];

            for (int inrow = 0; inrow < m_cinfo.m_max_v_samp_factor; inrow++)
            {
                int row = m_upsampleRowOffset + inrow;
                int inIndex = 0;

                int outIndex = 0;

                /* Special case for first column */
                int invalue = input_data[row][inIndex];
                inIndex++;

                output_data[inrow][outIndex] = (byte)invalue;
                outIndex++;
                output_data[inrow][outIndex] = (byte)((invalue * 3 + (int)input_data[row][inIndex] + 2) >> 2);
                outIndex++;

                for (int colctr = downsampled_width - 2; colctr > 0; colctr--)
                {
                    /* General case: 3/4 * nearer pixel + 1/4 * further pixel */
                    invalue = (int)input_data[row][inIndex] * 3;
                    inIndex++;

                    output_data[inrow][outIndex] = (byte)((invalue + (int)input_data[row][inIndex - 2] + 1) >> 2);
                    outIndex++;

                    output_data[inrow][outIndex] = (byte)((invalue + (int)input_data[row][inIndex] + 2) >> 2);
                    outIndex++;
                }

                /* Special case for last column */
                invalue = input_data[row][inIndex];
                output_data[inrow][outIndex] = (byte)((invalue * 3 + (int)input_data[row][inIndex - 1] + 1) >> 2);
                outIndex++;
                output_data[inrow][outIndex] = (byte)invalue;
                outIndex++;
            }
        }

        /// <summary>
        /// Fast processing for the common case of 2:1 horizontal and 1:1 vertical.
        /// It's still a box filter.
        /// </summary>
        private void h2v1_upsample(ref ComponentBuffer input_data)
        {
            ComponentBuffer output_data = m_color_buf[m_currentComponent];

            for (int inrow = 0; inrow < m_cinfo.m_max_v_samp_factor; inrow++)
            {
                int row = m_upsampleRowOffset + inrow;
                int outIndex = 0;

                for (int col = 0; col < m_cinfo.m_output_width; col++)
                {
                    byte invalue = input_data[row][col]; /* don't need GETJSAMPLE() here */
                    output_data[inrow][outIndex] = invalue;
                    outIndex++;
                    output_data[inrow][outIndex] = invalue;
                    outIndex++;
                }
            }
        }

        /// <summary>
        /// Fancy processing for the common case of 2:1 horizontal and 2:1 vertical.
        /// Again a triangle filter; see comments for h2v1 case, above.
        /// 
        /// It is OK for us to reference the adjacent input rows because we demanded
        /// context from the main buffer controller (see initialization code).
        /// </summary>
        private void h2v2_fancy_upsample(int downsampled_width, ref ComponentBuffer input_data)
        {
            ComponentBuffer output_data = m_color_buf[m_currentComponent];

            int inrow = m_upsampleRowOffset;
            int outrow = 0;
            while (outrow < m_cinfo.m_max_v_samp_factor)
            {
                for (int v = 0; v < 2; v++)
                {
                    // nearest input row index
                    int inIndex0 = 0;

                    //next nearest input row index
                    int inIndex1 = 0;
                    int inRow1 = -1;
                    if (v == 0)
                    {
                        /* next nearest is row above */
                        inRow1 = inrow - 1;
                    }
                    else
                    {
                        /* next nearest is row below */
                        inRow1 = inrow + 1;
                    }

                    int row = outrow;
                    int outIndex = 0;
                    outrow++;

                    /* Special case for first column */
                    int thiscolsum = (int)input_data[inrow][inIndex0] * 3 + (int)input_data[inRow1][inIndex1];
                    inIndex0++;
                    inIndex1++;

                    int nextcolsum = (int)input_data[inrow][inIndex0] * 3 + (int)input_data[inRow1][inIndex1];
                    inIndex0++;
                    inIndex1++;

                    output_data[row][outIndex] = (byte)((thiscolsum * 4 + 8) >> 4);
                    outIndex++;

                    output_data[row][outIndex] = (byte)((thiscolsum * 3 + nextcolsum + 7) >> 4);
                    outIndex++;

                    int lastcolsum = thiscolsum;
                    thiscolsum = nextcolsum;

                    for (int colctr = downsampled_width - 2; colctr > 0; colctr--)
                    {
                        /* General case: 3/4 * nearer pixel + 1/4 * further pixel in each */
                        /* dimension, thus 9/16, 3/16, 3/16, 1/16 overall */
                        nextcolsum = (int)input_data[inrow][inIndex0] * 3 + (int)input_data[inRow1][inIndex1];
                        inIndex0++;
                        inIndex1++;

                        output_data[row][outIndex] = (byte)((thiscolsum * 3 + lastcolsum + 8) >> 4);
                        outIndex++;

                        output_data[row][outIndex] = (byte)((thiscolsum * 3 + nextcolsum + 7) >> 4);
                        outIndex++;

                        lastcolsum = thiscolsum;
                        thiscolsum = nextcolsum;
                    }

                    /* Special case for last column */
                    output_data[row][outIndex] = (byte)((thiscolsum * 3 + lastcolsum + 8) >> 4);
                    outIndex++;
                    output_data[row][outIndex] = (byte)((thiscolsum * 4 + 7) >> 4);
                    outIndex++;
                }

                inrow++;
            }
        }

        /// <summary>
        /// Fast processing for the common case of 2:1 horizontal and 2:1 vertical.
        /// It's still a box filter.
        /// </summary>
        private void h2v2_upsample(ref ComponentBuffer input_data)
        {
            ComponentBuffer output_data = m_color_buf[m_currentComponent];

            int inrow = 0;
            int outrow = 0;
            while (outrow < m_cinfo.m_max_v_samp_factor)
            {
                int row = m_upsampleRowOffset + inrow;
                int outIndex = 0;

                for (int col = 0; col < m_cinfo.m_output_width; col++)
                {
                    byte invalue = input_data[row][col]; /* don't need GETJSAMPLE() here */
                    output_data[outrow][outIndex] = invalue;
                    outIndex++;
                    output_data[outrow][outIndex] = invalue;
                    outIndex++;
                }

                JpegUtils.jcopy_sample_rows(output_data, outrow, output_data, outrow + 1, 1, m_cinfo.m_output_width);
                inrow++;
                outrow += 2;
            }
        }

        /// <summary>
        /// This version handles any integral sampling ratios.
        /// This is not used for typical JPEG files, so it need not be fast.
        /// Nor, for that matter, is it particularly accurate: the algorithm is
        /// simple replication of the input pixel onto the corresponding output
        /// pixels.  The hi-falutin sampling literature refers to this as a
        /// "box filter".  A box filter tends to introduce visible artifacts,
        /// so if you are actually going to use 3:1 or 4:1 sampling ratios
        /// you would be well advised to improve this code.
        /// </summary>
        private void int_upsample(ref ComponentBuffer input_data)
        {
            ComponentBuffer output_data = m_color_buf[m_currentComponent];
            int h_expand = m_h_expand[m_currentComponent];
            int v_expand = m_v_expand[m_currentComponent];

            int inrow = 0;
            int outrow = 0;
            while (outrow < m_cinfo.m_max_v_samp_factor)
            {
                /* Generate one output row with proper horizontal expansion */
                int row = m_upsampleRowOffset + inrow;
                for (int col = 0; col < m_cinfo.m_output_width; col++)
                {
                    byte invalue = input_data[row][col]; /* don't need GETJSAMPLE() here */
                    int outIndex = 0;
                    for (int h = h_expand; h > 0; h--)
                    {
                        output_data[outrow][outIndex] = invalue;
                        outIndex++;
                    }
                }
                
                /* Generate any additional output rows by duplicating the first one */
                if (v_expand > 1)
                {
                    JpegUtils.jcopy_sample_rows(output_data, outrow, output_data, 
                        outrow + 1, v_expand - 1, m_cinfo.m_output_width);
                }

                inrow++;
                outrow += v_expand;
            }
        }
    }
}
