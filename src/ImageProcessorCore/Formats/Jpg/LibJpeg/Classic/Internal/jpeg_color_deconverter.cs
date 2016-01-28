/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * Copyright (C) 1994-1996, Thomas G. Lane.
 * This file is part of the Independent JPEG Group's software.
 * For conditions of distribution and use, see the accompanying README file.
 *
 */

/*
 * This file contains output colorspace conversion routines.
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace BitMiracle.LibJpeg.Classic.Internal
{
    /// <summary>
    /// Colorspace conversion
    /// </summary>
    class jpeg_color_deconverter
    {
        private const int SCALEBITS = 16;  /* speediest right-shift on some machines */
        private const int ONE_HALF = 1 << (SCALEBITS - 1);

        private enum ColorConverter
        {
            grayscale_converter,
            ycc_rgb_converter,
            gray_rgb_converter,
            null_converter,
            ycck_cmyk_converter
        }

        private ColorConverter m_converter;
        private jpeg_decompress_struct m_cinfo;

        private int[] m_perComponentOffsets;

        /* Private state for YCC->RGB conversion */
        private int[] m_Cr_r_tab;      /* => table for Cr to R conversion */
        private int[] m_Cb_b_tab;      /* => table for Cb to B conversion */
        private int[] m_Cr_g_tab;        /* => table for Cr to G conversion */
        private int[] m_Cb_g_tab;        /* => table for Cb to G conversion */

        /// <summary>
        /// Module initialization routine for output colorspace conversion.
        /// </summary>
        public jpeg_color_deconverter(jpeg_decompress_struct cinfo)
        {
            m_cinfo = cinfo;

            /* Make sure num_components agrees with jpeg_color_space */
            switch (cinfo.m_jpeg_color_space)
            {
                case J_COLOR_SPACE.JCS_GRAYSCALE:
                    if (cinfo.m_num_components != 1)
                        cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_J_COLORSPACE);
                    break;

                case J_COLOR_SPACE.JCS_RGB:
                case J_COLOR_SPACE.JCS_YCbCr:
                    if (cinfo.m_num_components != 3)
                        cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_J_COLORSPACE);
                    break;

                case J_COLOR_SPACE.JCS_CMYK:
                case J_COLOR_SPACE.JCS_YCCK:
                    if (cinfo.m_num_components != 4)
                        cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_J_COLORSPACE);
                    break;

                default:
                    /* JCS_UNKNOWN can be anything */
                    if (cinfo.m_num_components < 1)
                        cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_J_COLORSPACE);
                    break;
            }

            /* Set out_color_components and conversion method based on requested space.
            * Also clear the component_needed flags for any unused components,
            * so that earlier pipeline stages can avoid useless computation.
            */

            switch (cinfo.m_out_color_space)
            {
                case J_COLOR_SPACE.JCS_GRAYSCALE:
                    cinfo.m_out_color_components = 1;
                    if (cinfo.m_jpeg_color_space == J_COLOR_SPACE.JCS_GRAYSCALE || cinfo.m_jpeg_color_space == J_COLOR_SPACE.JCS_YCbCr)
                    {
                        m_converter = ColorConverter.grayscale_converter;
                        /* For color->grayscale conversion, only the Y (0) component is needed */
                        for (int ci = 1; ci < cinfo.m_num_components; ci++)
                            cinfo.Comp_info[ci].component_needed = false;
                    }
                    else
                        cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
                    break;

                case J_COLOR_SPACE.JCS_RGB:
                    cinfo.m_out_color_components = JpegConstants.RGB_PIXELSIZE;
                    if (cinfo.m_jpeg_color_space == J_COLOR_SPACE.JCS_YCbCr)
                    {
                        m_converter = ColorConverter.ycc_rgb_converter;
                        build_ycc_rgb_table();
                    }
                    else if (cinfo.m_jpeg_color_space == J_COLOR_SPACE.JCS_GRAYSCALE)
                        m_converter = ColorConverter.gray_rgb_converter;
                    else if (cinfo.m_jpeg_color_space == J_COLOR_SPACE.JCS_RGB)
                        m_converter = ColorConverter.null_converter;
                    else
                        cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
                    break;

                case J_COLOR_SPACE.JCS_CMYK:
                    cinfo.m_out_color_components = 4;
                    if (cinfo.m_jpeg_color_space == J_COLOR_SPACE.JCS_YCCK)
                    {
                        m_converter = ColorConverter.ycck_cmyk_converter;
                        build_ycc_rgb_table();
                    }
                    else if (cinfo.m_jpeg_color_space == J_COLOR_SPACE.JCS_CMYK)
                        m_converter = ColorConverter.null_converter;
                    else
                        cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
                    break;

                default:
                    /* Permit null conversion to same output space */
                    if (cinfo.m_out_color_space == cinfo.m_jpeg_color_space)
                    {
                        cinfo.m_out_color_components = cinfo.m_num_components;
                        m_converter = ColorConverter.null_converter;
                    }
                    else
                    {
                        /* unsupported non-null conversion */
                        cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
                    }
                    break;
            }

            if (cinfo.m_quantize_colors)
                cinfo.m_output_components = 1; /* single colormapped output component */
            else
                cinfo.m_output_components = cinfo.m_out_color_components;
        }

        /// <summary>
        /// Convert some rows of samples to the output colorspace.
        /// 
        /// Note that we change from noninterleaved, one-plane-per-component format
        /// to interleaved-pixel format.  The output buffer is therefore three times
        /// as wide as the input buffer.
        /// A starting row offset is provided only for the input buffer.  The caller
        /// can easily adjust the passed output_buf value to accommodate any row
        /// offset required on that side.
        /// </summary>
        public void color_convert(ComponentBuffer[] input_buf, int[] perComponentOffsets, int input_row, byte[][] output_buf, int output_row, int num_rows)
        {
            m_perComponentOffsets = perComponentOffsets;

            switch (m_converter)
            {
                case ColorConverter.grayscale_converter:
                    grayscale_convert(input_buf, input_row, output_buf, output_row, num_rows);
                    break;
                case ColorConverter.ycc_rgb_converter:
                    ycc_rgb_convert(input_buf, input_row, output_buf, output_row, num_rows);
                    break;
                case ColorConverter.gray_rgb_converter:
                    gray_rgb_convert(input_buf, input_row, output_buf, output_row, num_rows);
                    break;
                case ColorConverter.null_converter:
                    null_convert(input_buf, input_row, output_buf, output_row, num_rows);
                    break;
                case ColorConverter.ycck_cmyk_converter:
                    ycck_cmyk_convert(input_buf, input_row, output_buf, output_row, num_rows);
                    break;
                default:
                    m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
                    break;
            }
        }

        /**************** YCbCr -> RGB conversion: most common case **************/

        /*
         * YCbCr is defined per CCIR 601-1, except that Cb and Cr are
         * normalized to the range 0..MAXJSAMPLE rather than -0.5 .. 0.5.
         * The conversion equations to be implemented are therefore
         *  R = Y                + 1.40200 * Cr
         *  G = Y - 0.34414 * Cb - 0.71414 * Cr
         *  B = Y + 1.77200 * Cb
         * where Cb and Cr represent the incoming values less CENTERJSAMPLE.
         * (These numbers are derived from TIFF 6.0 section 21, dated 3-June-92.)
         *
         * To avoid floating-point arithmetic, we represent the fractional constants
         * as integers scaled up by 2^16 (about 4 digits precision); we have to divide
         * the products by 2^16, with appropriate rounding, to get the correct answer.
         * Notice that Y, being an integral input, does not contribute any fraction
         * so it need not participate in the rounding.
         *
         * For even more speed, we avoid doing any multiplications in the inner loop
         * by precalculating the constants times Cb and Cr for all possible values.
         * For 8-bit JSAMPLEs this is very reasonable (only 256 entries per table);
         * for 12-bit samples it is still acceptable.  It's not very reasonable for
         * 16-bit samples, but if you want lossless storage you shouldn't be changing
         * colorspace anyway.
         * The Cr=>R and Cb=>B values can be rounded to integers in advance; the
         * values for the G calculation are left scaled up, since we must add them
         * together before rounding.
         */

        /// <summary>
        /// Initialize tables for YCC->RGB colorspace conversion.
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

        private void ycc_rgb_convert(ComponentBuffer[] input_buf, int input_row, byte[][] output_buf, int output_row, int num_rows)
        {
            int component0RowOffset = m_perComponentOffsets[0];
            int component1RowOffset = m_perComponentOffsets[1];
            int component2RowOffset = m_perComponentOffsets[2];

            byte[] limit = m_cinfo.m_sample_range_limit;
            int limitOffset = m_cinfo.m_sampleRangeLimitOffset;

            for (int row = 0; row < num_rows; row++)
            {
                int columnOffset = 0;
                for (int col = 0; col < m_cinfo.m_output_width; col++)
                {
                    int y = input_buf[0][input_row + component0RowOffset][col];
                    int cb = input_buf[1][input_row + component1RowOffset][col];
                    int cr = input_buf[2][input_row + component2RowOffset][col];

                    /* Range-limiting is essential due to noise introduced by DCT losses. */
                    output_buf[output_row + row][columnOffset + JpegConstants.RGB_RED] = limit[limitOffset + y + m_Cr_r_tab[cr]];
                    output_buf[output_row + row][columnOffset + JpegConstants.RGB_GREEN] = limit[limitOffset + y + JpegUtils.RIGHT_SHIFT(m_Cb_g_tab[cb] + m_Cr_g_tab[cr], SCALEBITS)];
                    output_buf[output_row + row][columnOffset + JpegConstants.RGB_BLUE] = limit[limitOffset + y + m_Cb_b_tab[cb]];
                    columnOffset += JpegConstants.RGB_PIXELSIZE;
                }

                input_row++;
            }
        }

        /**************** Cases other than YCbCr -> RGB **************/

        /// <summary>
        /// Adobe-style YCCK->CMYK conversion.
        /// We convert YCbCr to R=1-C, G=1-M, and B=1-Y using the same
        /// conversion as above, while passing K (black) unchanged.
        /// We assume build_ycc_rgb_table has been called.
        /// </summary>
        private void ycck_cmyk_convert(ComponentBuffer[] input_buf, int input_row, byte[][] output_buf, int output_row, int num_rows)
        {
            int component0RowOffset = m_perComponentOffsets[0];
            int component1RowOffset = m_perComponentOffsets[1];
            int component2RowOffset = m_perComponentOffsets[2];
            int component3RowOffset = m_perComponentOffsets[3];

            byte[] limit = m_cinfo.m_sample_range_limit;
            int limitOffset = m_cinfo.m_sampleRangeLimitOffset;

            int num_cols = m_cinfo.m_output_width;
            for (int row = 0; row < num_rows; row++)
            {
                int columnOffset = 0;
                for (int col = 0; col < num_cols; col++)
                {
                    int y = input_buf[0][input_row + component0RowOffset][col];
                    int cb = input_buf[1][input_row + component1RowOffset][col];
                    int cr = input_buf[2][input_row + component2RowOffset][col];

                    /* Range-limiting is essential due to noise introduced by DCT losses. */
                    output_buf[output_row + row][columnOffset] = limit[limitOffset + JpegConstants.MAXJSAMPLE - (y + m_Cr_r_tab[cr])]; /* red */
                    output_buf[output_row + row][columnOffset + 1] = limit[limitOffset + JpegConstants.MAXJSAMPLE - (y + JpegUtils.RIGHT_SHIFT(m_Cb_g_tab[cb] + m_Cr_g_tab[cr], SCALEBITS))]; /* green */
                    output_buf[output_row + row][columnOffset + 2] = limit[limitOffset + JpegConstants.MAXJSAMPLE - (y + m_Cb_b_tab[cb])]; /* blue */
                    
                    /* K passes through unchanged */
                    /* don't need GETJSAMPLE here */
                    output_buf[output_row + row][columnOffset + 3] = input_buf[3][input_row + component3RowOffset][col];
                    columnOffset += 4;
                }

                input_row++;
            }
        }

        /// <summary>
        /// Convert grayscale to RGB: just duplicate the graylevel three times.
        /// This is provided to support applications that don't want to cope
        /// with grayscale as a separate case.
        /// </summary>
        private void gray_rgb_convert(ComponentBuffer[] input_buf, int input_row, byte[][] output_buf, int output_row, int num_rows)
        {
            int component0RowOffset = m_perComponentOffsets[0];
            int component1RowOffset = m_perComponentOffsets[1];
            int component2RowOffset = m_perComponentOffsets[2];

            int num_cols = m_cinfo.m_output_width;
            for (int row = 0; row < num_rows; row++)
            {
                int columnOffset = 0;
                for (int col = 0; col < num_cols; col++)
                {
                    /* We can dispense with GETJSAMPLE() here */
                    output_buf[output_row + row][columnOffset + JpegConstants.RGB_RED] = input_buf[0][input_row + component0RowOffset][col];
                    output_buf[output_row + row][columnOffset + JpegConstants.RGB_GREEN] = input_buf[0][input_row + component1RowOffset][col];
                    output_buf[output_row + row][columnOffset + JpegConstants.RGB_BLUE] = input_buf[0][input_row + component2RowOffset][col];
                    columnOffset += JpegConstants.RGB_PIXELSIZE;
                }

                input_row++;
            }
        }

        /// <summary>
        /// Color conversion for grayscale: just copy the data.
        /// This also works for YCbCr -> grayscale conversion, in which
        /// we just copy the Y (luminance) component and ignore chrominance.
        /// </summary>
        private void grayscale_convert(ComponentBuffer[] input_buf, int input_row, byte[][] output_buf, int output_row, int num_rows)
        {
            JpegUtils.jcopy_sample_rows(input_buf[0], input_row + m_perComponentOffsets[0], output_buf, output_row, num_rows, m_cinfo.m_output_width);
        }

        /// <summary>
        /// Color conversion for no colorspace change: just copy the data,
        /// converting from separate-planes to interleaved representation.
        /// </summary>
        private void null_convert(ComponentBuffer[] input_buf, int input_row, byte[][] output_buf, int output_row, int num_rows)
        {
            for (int row = 0; row < num_rows; row++)
            {
                for (int ci = 0; ci < m_cinfo.m_num_components; ci++)
                {
                    int columnIndex = 0;
                    int componentOffset = 0;
                    int perComponentOffset = m_perComponentOffsets[ci];

                    for (int count = m_cinfo.m_output_width; count > 0; count--)
                    {
                        /* needn't bother with GETJSAMPLE() here */
                        output_buf[output_row + row][ci + componentOffset] = input_buf[ci][input_row + perComponentOffset][columnIndex];
                        componentOffset += m_cinfo.m_num_components;
                        columnIndex++;
                    }
                }

                input_row++;
            }
        }

        private static int FIX(double x)
        {
            return (int)(x * (1L << SCALEBITS) + 0.5);
        }
    }
}
