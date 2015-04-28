/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * Copyright (C) 1994-1996, Thomas G. Lane.
 * This file is part of the Independent JPEG Group's software.
 * For conditions of distribution and use, see the accompanying README file.
 *
 */

/*
 * This file contains downsampling routines.
 *
 * Downsampling input data is counted in "row groups".  A row group
 * is defined to be max_v_samp_factor pixel rows of each component,
 * from which the downsampler produces v_samp_factor sample rows.
 * A single row group is processed in each call to the downsampler module.
 *
 * The downsampler is responsible for edge-expansion of its output data
 * to fill an integral number of DCT blocks horizontally.  The source buffer
 * may be modified if it is helpful for this purpose (the source buffer is
 * allocated wide enough to correspond to the desired output width).
 * The caller (the prep controller) is responsible for vertical padding.
 *
 * The downsampler may request "context rows" by setting need_context_rows
 * during startup.  In this case, the input arrays will contain at least
 * one row group's worth of pixels above and below the passed-in data;
 * the caller will create dummy rows at image top and bottom by replicating
 * the first or last real pixel row.
 *
 * An excellent reference for image resampling is
 *   Digital Image Warping, George Wolberg, 1990.
 *   Pub. by IEEE Computer Society Press, Los Alamitos, CA. ISBN 0-8186-8944-7.
 *
 * The downsampling algorithm used here is a simple average of the source
 * pixels covered by the output pixel.  The hi-falutin sampling literature
 * refers to this as a "box filter".  In general the characteristics of a box
 * filter are not very good, but for the specific cases we normally use (1:1
 * and 2:1 ratios) the box is equivalent to a "triangle filter" which is not
 * nearly so bad.  If you intend to use other sampling ratios, you'd be well
 * advised to improve this code.
 *
 * A simple input-smoothing capability is provided.  This is mainly intended
 * for cleaning up color-dithered GIF input files (if you find it inadequate,
 * we suggest using an external filtering program such as pnmconvol).  When
 * enabled, each input pixel P is replaced by a weighted sum of itself and its
 * eight neighbors.  P's weight is 1-8*SF and each neighbor's weight is SF,
 * where SF = (smoothing_factor / 1024).
 * Currently, smoothing is only supported for 2h2v sampling factors.
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace BitMiracle.LibJpeg.Classic.Internal
{
    /// <summary>
    /// Downsampling
    /// </summary>
    class jpeg_downsampler
    {
        private enum downSampleMethod
        {
            fullsize_smooth_downsampler,
            fullsize_downsampler,
            h2v1_downsampler,
            h2v2_smooth_downsampler,
            h2v2_downsampler,
            int_downsampler
        };

        /* Downsamplers, one per component */
        private downSampleMethod[] m_downSamplers = new downSampleMethod[JpegConstants.MAX_COMPONENTS];

        private jpeg_compress_struct m_cinfo;
        private bool m_need_context_rows; /* true if need rows above & below */

        public jpeg_downsampler(jpeg_compress_struct cinfo)
        {
            m_cinfo = cinfo;
            m_need_context_rows = false;

            if (cinfo.m_CCIR601_sampling)
                cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CCIR601_NOTIMPL);

            /* Verify we can handle the sampling factors, and set up method pointers */
            bool smoothok = true;
            for (int ci = 0; ci < cinfo.m_num_components; ci++)
            {
                jpeg_component_info componentInfo = cinfo.Component_info[ci];

                if (componentInfo.H_samp_factor == cinfo.m_max_h_samp_factor &&
                    componentInfo.V_samp_factor == cinfo.m_max_v_samp_factor)
                {
                    if (cinfo.m_smoothing_factor != 0)
                    {
                        m_downSamplers[ci] = downSampleMethod.fullsize_smooth_downsampler;
                        m_need_context_rows = true;
                    }
                    else
                    {
                        m_downSamplers[ci] = downSampleMethod.fullsize_downsampler;
                    }
                }
                else if (componentInfo.H_samp_factor * 2 == cinfo.m_max_h_samp_factor &&
                         componentInfo.V_samp_factor == cinfo.m_max_v_samp_factor)
                {
                    smoothok = false;
                    m_downSamplers[ci] = downSampleMethod.h2v1_downsampler;
                }
                else if (componentInfo.H_samp_factor * 2 == cinfo.m_max_h_samp_factor &&
                         componentInfo.V_samp_factor * 2 == cinfo.m_max_v_samp_factor)
                {
                    if (cinfo.m_smoothing_factor != 0)
                    {
                        m_downSamplers[ci] = downSampleMethod.h2v2_smooth_downsampler;
                        m_need_context_rows = true;
                    }
                    else
                    {
                        m_downSamplers[ci] = downSampleMethod.h2v2_downsampler;
                    }
                }
                else if ((cinfo.m_max_h_samp_factor % componentInfo.H_samp_factor) == 0 &&
                         (cinfo.m_max_v_samp_factor % componentInfo.V_samp_factor) == 0)
                {
                    smoothok = false;
                    m_downSamplers[ci] = downSampleMethod.int_downsampler;
                }
                else
                    cinfo.ERREXIT(J_MESSAGE_CODE.JERR_FRACT_SAMPLE_NOTIMPL);
            }

            if (cinfo.m_smoothing_factor != 0 && !smoothok)
                cinfo.TRACEMS(0, J_MESSAGE_CODE.JTRC_SMOOTH_NOTIMPL);
        }

        /// <summary>
        /// Do downsampling for a whole row group (all components).
        /// 
        /// In this version we simply downsample each component independently.
        /// </summary>
        public void downsample(byte[][][] input_buf, int in_row_index, byte[][][] output_buf, int out_row_group_index)
        {
            for (int ci = 0; ci < m_cinfo.m_num_components; ci++)
            {
                int outIndex = out_row_group_index * m_cinfo.Component_info[ci].V_samp_factor;
                switch (m_downSamplers[ci])
                {
                    case downSampleMethod.fullsize_smooth_downsampler:
                        fullsize_smooth_downsample(ci, input_buf[ci], in_row_index, output_buf[ci], outIndex);
                        break;

                    case downSampleMethod.fullsize_downsampler:
                        fullsize_downsample(ci, input_buf[ci], in_row_index, output_buf[ci], outIndex);
                        break;

                    case downSampleMethod.h2v1_downsampler:
                        h2v1_downsample(ci, input_buf[ci], in_row_index, output_buf[ci], outIndex);
                        break;

                    case downSampleMethod.h2v2_smooth_downsampler:
                        h2v2_smooth_downsample(ci, input_buf[ci], in_row_index, output_buf[ci], outIndex);
                        break;

                    case downSampleMethod.h2v2_downsampler:
                        h2v2_downsample(ci, input_buf[ci], in_row_index, output_buf[ci], outIndex);
                        break;

                    case downSampleMethod.int_downsampler:
                        int_downsample(ci, input_buf[ci], in_row_index, output_buf[ci], outIndex);
                        break;
                };
            }
        }

        public bool NeedContextRows()
        {
            return m_need_context_rows;
        }
        
        /// <summary>
        /// Downsample pixel values of a single component.
        /// One row group is processed per call.
        /// This version handles arbitrary integral sampling ratios, without smoothing.
        /// Note that this version is not actually used for customary sampling ratios.
        /// </summary>
        private void int_downsample(int componentIndex, byte[][] input_data, int startInputRow, byte[][] output_data, int startOutRow)
        {
            /* Expand input data enough to let all the output samples be generated
             * by the standard loop.  Special-casing padded output would be more
             * efficient.
             */
            int output_cols = m_cinfo.Component_info[componentIndex].Width_in_blocks * JpegConstants.DCTSIZE;
            int h_expand = m_cinfo.m_max_h_samp_factor / m_cinfo.Component_info[componentIndex].H_samp_factor;
            expand_right_edge(input_data, startInputRow, m_cinfo.m_max_v_samp_factor, m_cinfo.m_image_width, output_cols * h_expand);

            int v_expand = m_cinfo.m_max_v_samp_factor / m_cinfo.Component_info[componentIndex].V_samp_factor;
            int numpix = h_expand * v_expand;
            int numpix2 = numpix / 2;
            int inrow = 0;
            for (int outrow = 0; outrow < m_cinfo.Component_info[componentIndex].V_samp_factor; outrow++)
            {
                for (int outcol = 0, outcol_h = 0; outcol < output_cols; outcol++, outcol_h += h_expand)
                {
                    int outvalue = 0;
                    for (int v = 0; v < v_expand; v++)
                    {
                        for (int h = 0; h < h_expand; h++)
                            outvalue += input_data[startInputRow + inrow + v][outcol_h + h];
                    }

                    output_data[startOutRow + outrow][outcol] = (byte)((outvalue + numpix2) / numpix);
                }

                inrow += v_expand;
            }
        }

        /// <summary>
        /// Downsample pixel values of a single component.
        /// This version handles the special case of a full-size component,
        /// without smoothing.
        /// </summary>
        private void fullsize_downsample(int componentIndex, byte[][] input_data, int startInputRow, byte[][] output_data, int startOutRow)
        {
            /* Copy the data */
            JpegUtils.jcopy_sample_rows(input_data, startInputRow, output_data, startOutRow, m_cinfo.m_max_v_samp_factor, m_cinfo.m_image_width);

            /* Edge-expand */
            expand_right_edge(output_data, startOutRow, m_cinfo.m_max_v_samp_factor, m_cinfo.m_image_width, m_cinfo.Component_info[componentIndex].Width_in_blocks * JpegConstants.DCTSIZE);
        }

        /// <summary>
        /// Downsample pixel values of a single component.
        /// This version handles the common case of 2:1 horizontal and 1:1 vertical,
        /// without smoothing.
        /// 
        /// A note about the "bias" calculations: when rounding fractional values to
        /// integer, we do not want to always round 0.5 up to the next integer.
        /// If we did that, we'd introduce a noticeable bias towards larger values.
        /// Instead, this code is arranged so that 0.5 will be rounded up or down at
        /// alternate pixel locations (a simple ordered dither pattern).
        /// </summary>
        private void h2v1_downsample(int componentIndex, byte[][] input_data, int startInputRow, byte[][] output_data, int startOutRow)
        {
            /* Expand input data enough to let all the output samples be generated
             * by the standard loop.  Special-casing padded output would be more
             * efficient.
             */
            int output_cols = m_cinfo.Component_info[componentIndex].Width_in_blocks * JpegConstants.DCTSIZE;
            expand_right_edge(input_data, startInputRow, m_cinfo.m_max_v_samp_factor, m_cinfo.m_image_width, output_cols * 2);

            for (int outrow = 0; outrow < m_cinfo.Component_info[componentIndex].V_samp_factor; outrow++)
            {
                /* bias = 0,1,0,1,... for successive samples */
                int bias = 0;
                int inputColumn = 0;
                for (int outcol = 0; outcol < output_cols; outcol++)
                {
                    output_data[startOutRow + outrow][outcol] = (byte)(
                        ((int)input_data[startInputRow + outrow][inputColumn] +
                        (int)input_data[startInputRow + outrow][inputColumn + 1] + bias) >> 1);

                    bias ^= 1;      /* 0=>1, 1=>0 */
                    inputColumn += 2;
                }
            }
        }

        /// <summary>
        /// Downsample pixel values of a single component.
        /// This version handles the standard case of 2:1 horizontal and 2:1 vertical,
        /// without smoothing.
        /// </summary>
        private void h2v2_downsample(int componentIndex, byte[][] input_data, int startInputRow, byte[][] output_data, int startOutRow)
        {
            /* Expand input data enough to let all the output samples be generated
             * by the standard loop.  Special-casing padded output would be more
             * efficient.
             */
            int output_cols = m_cinfo.Component_info[componentIndex].Width_in_blocks * JpegConstants.DCTSIZE;
            expand_right_edge(input_data, startInputRow, m_cinfo.m_max_v_samp_factor, m_cinfo.m_image_width, output_cols * 2);

            int inrow = 0;
            for (int outrow = 0; outrow < m_cinfo.Component_info[componentIndex].V_samp_factor; outrow++)
            {
                /* bias = 1,2,1,2,... for successive samples */
                int bias = 1;
                int inputColumn = 0;
                for (int outcol = 0; outcol < output_cols; outcol++)
                {
                    output_data[startOutRow + outrow][outcol] = (byte)((
                        (int)input_data[startInputRow + inrow][inputColumn] +
                        (int)input_data[startInputRow + inrow][inputColumn + 1] +
                        (int)input_data[startInputRow + inrow + 1][inputColumn] +
                        (int)input_data[startInputRow + inrow + 1][inputColumn + 1] + bias) >> 2);

                    bias ^= 3;      /* 1=>2, 2=>1 */
                    inputColumn += 2;
                }

                inrow += 2;
            }
        }

        /// <summary>
        /// Downsample pixel values of a single component.
        /// This version handles the standard case of 2:1 horizontal and 2:1 vertical,
        /// with smoothing.  One row of context is required.
        /// </summary>
        private void h2v2_smooth_downsample(int componentIndex, byte[][] input_data, int startInputRow, byte[][] output_data, int startOutRow)
        {
            /* Expand input data enough to let all the output samples be generated
             * by the standard loop.  Special-casing padded output would be more
             * efficient.
             */
            int output_cols = m_cinfo.Component_info[componentIndex].Width_in_blocks * JpegConstants.DCTSIZE;
            expand_right_edge(input_data, startInputRow - 1, m_cinfo.m_max_v_samp_factor + 2, m_cinfo.m_image_width, output_cols * 2);

            /* We don't bother to form the individual "smoothed" input pixel values;
             * we can directly compute the output which is the average of the four
             * smoothed values.  Each of the four member pixels contributes a fraction
             * (1-8*SF) to its own smoothed image and a fraction SF to each of the three
             * other smoothed pixels, therefore a total fraction (1-5*SF)/4 to the final
             * output.  The four corner-adjacent neighbor pixels contribute a fraction
             * SF to just one smoothed pixel, or SF/4 to the final output; while the
             * eight edge-adjacent neighbors contribute SF to each of two smoothed
             * pixels, or SF/2 overall.  In order to use integer arithmetic, these
             * factors are scaled by 2^16 = 65536.
             * Also recall that SF = smoothing_factor / 1024.
             */

            int memberscale = 16384 - m_cinfo.m_smoothing_factor * 80; /* scaled (1-5*SF)/4 */
            int neighscale = m_cinfo.m_smoothing_factor * 16; /* scaled SF/4 */

            for (int inrow = 0, outrow = 0; outrow < m_cinfo.Component_info[componentIndex].V_samp_factor; outrow++)
            {
                int outIndex = 0;
                int inIndex0 = 0;
                int inIndex1 = 0;
                int aboveIndex = 0;
                int belowIndex = 0;

                /* Special case for first column: pretend column -1 is same as column 0 */
                int membersum = input_data[startInputRow + inrow][inIndex0] +
                    input_data[startInputRow + inrow][inIndex0 + 1] +
                    input_data[startInputRow + inrow + 1][inIndex1] +
                    input_data[startInputRow + inrow + 1][inIndex1 + 1];

                int neighsum = input_data[startInputRow + inrow - 1][aboveIndex] +
                    input_data[startInputRow + inrow - 1][aboveIndex + 1] +
                    input_data[startInputRow + inrow + 2][belowIndex] +
                    input_data[startInputRow + inrow + 2][belowIndex + 1] +
                    input_data[startInputRow + inrow][inIndex0] +
                    input_data[startInputRow + inrow][inIndex0 + 2] +
                    input_data[startInputRow + inrow + 1][inIndex1] +
                    input_data[startInputRow + inrow + 1][inIndex1 + 2];

                neighsum += neighsum;
                neighsum += input_data[startInputRow + inrow - 1][aboveIndex] +
                    input_data[startInputRow + inrow - 1][aboveIndex + 2] +
                    input_data[startInputRow + inrow + 2][belowIndex] +
                    input_data[startInputRow + inrow + 2][belowIndex + 2];

                membersum = membersum * memberscale + neighsum * neighscale;
                output_data[startOutRow + outrow][outIndex] = (byte)((membersum + 32768) >> 16);
                outIndex++;

                inIndex0 += 2;
                inIndex1 += 2;
                aboveIndex += 2;
                belowIndex += 2;

                for (int colctr = output_cols - 2; colctr > 0; colctr--)
                {
                    /* sum of pixels directly mapped to this output element */
                    membersum = input_data[startInputRow + inrow][inIndex0] +
                        input_data[startInputRow + inrow][inIndex0 + 1] +
                        input_data[startInputRow + inrow + 1][inIndex1] +
                        input_data[startInputRow + inrow + 1][inIndex1 + 1];

                    /* sum of edge-neighbor pixels */
                    neighsum = input_data[startInputRow + inrow - 1][aboveIndex] +
                        input_data[startInputRow + inrow - 1][aboveIndex + 1] +
                        input_data[startInputRow + inrow + 2][belowIndex] +
                        input_data[startInputRow + inrow + 2][belowIndex + 1] +
                        input_data[startInputRow + inrow][inIndex0 - 1] +
                        input_data[startInputRow + inrow][inIndex0 + 2] +
                        input_data[startInputRow + inrow + 1][inIndex1 - 1] +
                        input_data[startInputRow + inrow + 1][inIndex1 + 2];

                    /* The edge-neighbors count twice as much as corner-neighbors */
                    neighsum += neighsum;

                    /* Add in the corner-neighbors */
                    neighsum += input_data[startInputRow + inrow - 1][aboveIndex - 1] +
                        input_data[startInputRow + inrow - 1][aboveIndex + 2] +
                        input_data[startInputRow + inrow + 2][belowIndex - 1] +
                        input_data[startInputRow + inrow + 2][belowIndex + 2];

                    /* form final output scaled up by 2^16 */
                    membersum = membersum * memberscale + neighsum * neighscale;

                    /* round, descale and output it */
                    output_data[startOutRow + outrow][outIndex] = (byte)((membersum + 32768) >> 16);
                    outIndex++;

                    inIndex0 += 2;
                    inIndex1 += 2;
                    aboveIndex += 2;
                    belowIndex += 2;
                }

                /* Special case for last column */
                membersum = input_data[startInputRow + inrow][inIndex0] +
                    input_data[startInputRow + inrow][inIndex0 + 1] +
                    input_data[startInputRow + inrow + 1][inIndex1] +
                    input_data[startInputRow + inrow + 1][inIndex1 + 1];

                neighsum = input_data[startInputRow + inrow - 1][aboveIndex] +
                    input_data[startInputRow + inrow - 1][aboveIndex + 1] +
                    input_data[startInputRow + inrow + 2][belowIndex] +
                    input_data[startInputRow + inrow + 2][belowIndex + 1] +
                    input_data[startInputRow + inrow][inIndex0 - 1] +
                    input_data[startInputRow + inrow][inIndex0 + 1] +
                    input_data[startInputRow + inrow + 1][inIndex1 - 1] +
                    input_data[startInputRow + inrow + 1][inIndex1 + 1];

                neighsum += neighsum;
                neighsum += input_data[startInputRow + inrow - 1][aboveIndex - 1] +
                    input_data[startInputRow + inrow - 1][aboveIndex + 1] +
                    input_data[startInputRow + inrow + 2][belowIndex - 1] +
                    input_data[startInputRow + inrow + 2][belowIndex + 1];

                membersum = membersum * memberscale + neighsum * neighscale;
                output_data[startOutRow + outrow][outIndex] = (byte)((membersum + 32768) >> 16);

                inrow += 2;
            }
        }

        /// <summary>
        /// Downsample pixel values of a single component.
        /// This version handles the special case of a full-size component,
        /// with smoothing.  One row of context is required.
        /// </summary>
        private void fullsize_smooth_downsample(int componentIndex, byte[][] input_data, int startInputRow, byte[][] output_data, int startOutRow)
        {
            /* Expand input data enough to let all the output samples be generated
             * by the standard loop.  Special-casing padded output would be more
             * efficient.
             */
            int output_cols = m_cinfo.Component_info[componentIndex].Width_in_blocks * JpegConstants.DCTSIZE;
            expand_right_edge(input_data, startInputRow - 1, m_cinfo.m_max_v_samp_factor + 2, m_cinfo.m_image_width, output_cols);

            /* Each of the eight neighbor pixels contributes a fraction SF to the
             * smoothed pixel, while the main pixel contributes (1-8*SF).  In order
             * to use integer arithmetic, these factors are multiplied by 2^16 = 65536.
             * Also recall that SF = smoothing_factor / 1024.
             */

            int memberscale = 65536 - m_cinfo.m_smoothing_factor * 512; /* scaled 1-8*SF */
            int neighscale = m_cinfo.m_smoothing_factor * 64; /* scaled SF */

            for (int outrow = 0; outrow < m_cinfo.Component_info[componentIndex].V_samp_factor; outrow++)
            {
                int outIndex = 0;
                int inIndex = 0;
                int aboveIndex = 0;
                int belowIndex = 0;

                /* Special case for first column */
                int colsum = input_data[startInputRow + outrow - 1][aboveIndex] +
                    input_data[startInputRow + outrow + 1][belowIndex] +
                    input_data[startInputRow + outrow][inIndex];

                aboveIndex++;
                belowIndex++;

                int membersum = input_data[startInputRow + outrow][inIndex];
                inIndex++;

                int nextcolsum = input_data[startInputRow + outrow - 1][aboveIndex] +
                    input_data[startInputRow + outrow + 1][belowIndex] +
                    input_data[startInputRow + outrow][inIndex];

                int neighsum = colsum + (colsum - membersum) + nextcolsum;

                membersum = membersum * memberscale + neighsum * neighscale;
                output_data[startOutRow + outrow][outIndex] = (byte)((membersum + 32768) >> 16);
                outIndex++;

                int lastcolsum = colsum;
                colsum = nextcolsum;

                for (int colctr = output_cols - 2; colctr > 0; colctr--)
                {
                    membersum = input_data[startInputRow + outrow][inIndex];

                    inIndex++;
                    aboveIndex++;
                    belowIndex++;

                    nextcolsum = input_data[startInputRow + outrow - 1][aboveIndex] +
                        input_data[startInputRow + outrow + 1][belowIndex] +
                        input_data[startInputRow + outrow][inIndex];

                    neighsum = lastcolsum + (colsum - membersum) + nextcolsum;
                    membersum = membersum * memberscale + neighsum * neighscale;

                    output_data[startOutRow + outrow][outIndex] = (byte)((membersum + 32768) >> 16);
                    outIndex++;

                    lastcolsum = colsum;
                    colsum = nextcolsum;
                }

                /* Special case for last column */
                membersum = input_data[startInputRow + outrow][inIndex];
                neighsum = lastcolsum + (colsum - membersum) + colsum;
                membersum = membersum * memberscale + neighsum * neighscale;
                output_data[startOutRow + outrow][outIndex] = (byte)((membersum + 32768) >> 16);
            }
        }

        /// <summary>
        /// Expand a component horizontally from width input_cols to width output_cols,
        /// by duplicating the rightmost samples.
        /// </summary>
        private static void expand_right_edge(byte[][] image_data, int startInputRow, int num_rows, int input_cols, int output_cols)
        {
            int numcols = output_cols - input_cols;
            if (numcols > 0)
            {
                for (int row = startInputRow; row < (startInputRow + num_rows); row++)
                {
                    /* don't need GETJSAMPLE() here */
                    byte pixval = image_data[row][input_cols - 1];
                    for (int count = 0; count < numcols; count++)
                        image_data[row][input_cols + count] = pixval;
                }
            }
        }
    }
}
