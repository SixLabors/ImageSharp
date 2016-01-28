/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * Copyright (C) 1994-1996, Thomas G. Lane.
 * This file is part of the Independent JPEG Group's software.
 * For conditions of distribution and use, see the accompanying README file.
 *
 */

/*
 * This file contains input colorspace conversion routines.
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace BitMiracle.LibJpeg.Classic.Internal
{
    /// <summary>
    /// Colorspace conversion
    /// </summary>
    class jpeg_color_converter
    {
        private const int SCALEBITS = 16;  /* speediest right-shift on some machines */
        private const int CBCR_OFFSET = JpegConstants.CENTERJSAMPLE << SCALEBITS;
        private const int ONE_HALF = 1 << (SCALEBITS - 1);

        // We allocate one big table and divide it up into eight parts, instead of
        // doing eight alloc_small requests.  This lets us use a single table base
        // address, which can be held in a register in the inner loops on many
        // machines (more than can hold all eight addresses, anyway).
        private const int R_Y_OFF = 0;           /* offset to R => Y section */
        private const int G_Y_OFF = (1 * (JpegConstants.MAXJSAMPLE+1));  /* offset to G => Y section */
        private const int B_Y_OFF = (2 * (JpegConstants.MAXJSAMPLE+1));  /* etc. */
        private const int R_CB_OFF = (3 * (JpegConstants.MAXJSAMPLE+1));
        private const int G_CB_OFF = (4 * (JpegConstants.MAXJSAMPLE+1));
        private const int B_CB_OFF = (5 * (JpegConstants.MAXJSAMPLE+1));
        private const int R_CR_OFF = B_CB_OFF;        /* B=>Cb, R=>Cr are the same */
        private const int G_CR_OFF = (6 * (JpegConstants.MAXJSAMPLE+1));
        private const int B_CR_OFF = (7 * (JpegConstants.MAXJSAMPLE+1));
        private const int TABLE_SIZE = (8 * (JpegConstants.MAXJSAMPLE + 1));

        private jpeg_compress_struct m_cinfo;

        private bool m_useNullStart;

        private bool m_useCmykYcckConvert;
        private bool m_useGrayscaleConvert;
        private bool m_useNullConvert;
        private bool m_useRgbGrayConvert;
        private bool m_useRgbYccConvert;

        private int[] m_rgb_ycc_tab;     /* => table for RGB to YCbCr conversion */

        public jpeg_color_converter(jpeg_compress_struct cinfo)
        {
            m_cinfo = cinfo;

            /* set start_pass to null method until we find out differently */
            m_useNullStart = true;

            /* Make sure input_components agrees with in_color_space */
            switch (cinfo.m_in_color_space)
            {
                case J_COLOR_SPACE.JCS_GRAYSCALE:
                    if (cinfo.m_input_components != 1)
                        cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_IN_COLORSPACE);
                    break;

                case J_COLOR_SPACE.JCS_RGB:
                case J_COLOR_SPACE.JCS_YCbCr:
                    if (cinfo.m_input_components != 3)
                        cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_IN_COLORSPACE);
                    break;

                case J_COLOR_SPACE.JCS_CMYK:
                case J_COLOR_SPACE.JCS_YCCK:
                    if (cinfo.m_input_components != 4)
                        cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_IN_COLORSPACE);
                    break;

                default:
                    /* JCS_UNKNOWN can be anything */
                    if (cinfo.m_input_components < 1)
                        cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_IN_COLORSPACE);
                    break;
            }

            /* Check num_components, set conversion method based on requested space */
            clearConvertFlags();
            switch (cinfo.m_jpeg_color_space)
            {
                case J_COLOR_SPACE.JCS_GRAYSCALE:
                    if (cinfo.m_num_components != 1)
                        cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_J_COLORSPACE);
                    
                    if (cinfo.m_in_color_space == J_COLOR_SPACE.JCS_GRAYSCALE)
                        m_useGrayscaleConvert = true;
                    else if (cinfo.m_in_color_space == J_COLOR_SPACE.JCS_RGB)
                    {
                        m_useNullStart = false; // use rgb_ycc_start
                        m_useRgbGrayConvert = true;
                    }
                    else if (cinfo.m_in_color_space == J_COLOR_SPACE.JCS_YCbCr)
                        m_useGrayscaleConvert = true;
                    else
                        cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
                    break;

                case J_COLOR_SPACE.JCS_RGB:
                    if (cinfo.m_num_components != 3)
                        cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_J_COLORSPACE);
                    
                    if (cinfo.m_in_color_space == J_COLOR_SPACE.JCS_RGB)
                        m_useNullConvert = true;
                    else
                        cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
                    break;

                case J_COLOR_SPACE.JCS_YCbCr:
                    if (cinfo.m_num_components != 3)
                        cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_J_COLORSPACE);
                    
                    if (cinfo.m_in_color_space == J_COLOR_SPACE.JCS_RGB)
                    {
                        m_useNullStart = false; // use rgb_ycc_start
                        m_useRgbYccConvert = true;
                    }
                    else if (cinfo.m_in_color_space == J_COLOR_SPACE.JCS_YCbCr)
                        m_useNullConvert = true;
                    else
                        cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
                    break;

                case J_COLOR_SPACE.JCS_CMYK:
                    if (cinfo.m_num_components != 4)
                        cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_J_COLORSPACE);
                    
                    if (cinfo.m_in_color_space == J_COLOR_SPACE.JCS_CMYK)
                        m_useNullConvert = true;
                    else
                        cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
                    break;

                case J_COLOR_SPACE.JCS_YCCK:
                    if (cinfo.m_num_components != 4)
                        cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_J_COLORSPACE);
                    
                    if (cinfo.m_in_color_space == J_COLOR_SPACE.JCS_CMYK)
                    {
                        m_useNullStart = false; // use rgb_ycc_start
                        m_useCmykYcckConvert = true;
                    }
                    else if (cinfo.m_in_color_space == J_COLOR_SPACE.JCS_YCCK)
                        m_useNullConvert = true;
                    else
                        cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
                    break;

                default:
                    /* allow null conversion of JCS_UNKNOWN */
                    if (cinfo.m_jpeg_color_space != cinfo.m_in_color_space || cinfo.m_num_components != cinfo.m_input_components)
                        cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);

                    m_useNullConvert = true;
                    break;
            }
        }

        public void start_pass()
        {
            if (!m_useNullStart)
                rgb_ycc_start();
        }

        /// <summary>
        /// Convert some rows of samples to the JPEG colorspace.
        /// 
        /// Note that we change from the application's interleaved-pixel format
        /// to our internal noninterleaved, one-plane-per-component format.
        /// The input buffer is therefore three times as wide as the output buffer.
        /// 
        /// A starting row offset is provided only for the output buffer.  The caller
        /// can easily adjust the passed input_buf value to accommodate any row
        /// offset required on that side.
        /// </summary>
        public void color_convert(byte[][] input_buf, int input_row, byte[][][] output_buf, int output_row, int num_rows)
        {
            if (m_useCmykYcckConvert)
                cmyk_ycck_convert(input_buf, input_row, output_buf, output_row, num_rows);
            else if (m_useGrayscaleConvert)
                grayscale_convert(input_buf, input_row, output_buf, output_row, num_rows);
            else if (m_useRgbGrayConvert)
                rgb_gray_convert(input_buf, input_row, output_buf, output_row, num_rows);
            else if (m_useRgbYccConvert)
                rgb_ycc_convert(input_buf, input_row, output_buf, output_row, num_rows);
            else if (m_useNullConvert)
                null_convert(input_buf, input_row, output_buf, output_row, num_rows);
            else
                m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
        }

        /// <summary>
        /// Initialize for RGB->YCC colorspace conversion.
        /// </summary>
        private void rgb_ycc_start()
        {
            /* Allocate and fill in the conversion tables. */
            m_rgb_ycc_tab = new int[TABLE_SIZE];

            for (int i = 0; i <= JpegConstants.MAXJSAMPLE; i++)
            {
                m_rgb_ycc_tab[i + R_Y_OFF] = FIX(0.29900) * i;
                m_rgb_ycc_tab[i + G_Y_OFF] = FIX(0.58700) * i;
                m_rgb_ycc_tab[i + B_Y_OFF] = FIX(0.11400) * i + ONE_HALF;
                m_rgb_ycc_tab[i + R_CB_OFF] = (-FIX(0.16874)) * i;
                m_rgb_ycc_tab[i + G_CB_OFF] = (-FIX(0.33126)) * i;

                /* We use a rounding fudge-factor of 0.5-epsilon for Cb and Cr.
                 * This ensures that the maximum output will round to MAXJSAMPLE
                 * not MAXJSAMPLE+1, and thus that we don't have to range-limit.
                 */
                m_rgb_ycc_tab[i + B_CB_OFF] = FIX(0.50000) * i + CBCR_OFFSET + ONE_HALF - 1;

                /*  B=>Cb and R=>Cr tables are the same
                    rgb_ycc_tab[i+R_CR_OFF] = FIX(0.50000) * i    + CBCR_OFFSET + ONE_HALF-1;
                */
                m_rgb_ycc_tab[i + G_CR_OFF] = (-FIX(0.41869)) * i;
                m_rgb_ycc_tab[i + B_CR_OFF] = (-FIX(0.08131)) * i;
            }
        }

        private void clearConvertFlags()
        {
            m_useCmykYcckConvert = false;
            m_useGrayscaleConvert = false;
            m_useNullConvert = false;
            m_useRgbGrayConvert = false;
            m_useRgbYccConvert = false;
        }

        private static int FIX(double x)
        {
            return (int)(x * (1L << SCALEBITS) + 0.5);
        }

        /// <summary>
        /// RGB -&gt; YCbCr conversion: most common case
        /// YCbCr is defined per CCIR 601-1, except that Cb and Cr are
        /// normalized to the range 0..MAXJSAMPLE rather than -0.5 .. 0.5.
        /// The conversion equations to be implemented are therefore
        /// Y  =  0.29900 * R + 0.58700 * G + 0.11400 * B
        /// Cb = -0.16874 * R - 0.33126 * G + 0.50000 * B  + CENTERJSAMPLE
        /// Cr =  0.50000 * R - 0.41869 * G - 0.08131 * B  + CENTERJSAMPLE
        /// (These numbers are derived from TIFF 6.0 section 21, dated 3-June-92.)
        /// To avoid floating-point arithmetic, we represent the fractional constants
        /// as integers scaled up by 2^16 (about 4 digits precision); we have to divide
        /// the products by 2^16, with appropriate rounding, to get the correct answer.
        /// For even more speed, we avoid doing any multiplications in the inner loop
        /// by precalculating the constants times R,G,B for all possible values.
        /// For 8-bit JSAMPLEs this is very reasonable (only 256 entries per table);
        /// for 12-bit samples it is still acceptable.  It's not very reasonable for
        /// 16-bit samples, but if you want lossless storage you shouldn't be changing
        /// colorspace anyway.
        /// The CENTERJSAMPLE offsets and the rounding fudge-factor of 0.5 are included
        /// in the tables to save adding them separately in the inner loop.
        /// </summary>
        private void rgb_ycc_convert(byte[][] input_buf, int input_row, byte[][][] output_buf, int output_row, int num_rows)
        {
            int num_cols = m_cinfo.m_image_width;
            for (int row = 0; row < num_rows; row++)
            {
                int columnOffset = 0;
                for (int col = 0; col < num_cols; col++)
                {
                    int r = input_buf[input_row + row][columnOffset + JpegConstants.RGB_RED];
                    int g = input_buf[input_row + row][columnOffset + JpegConstants.RGB_GREEN];
                    int b = input_buf[input_row + row][columnOffset + JpegConstants.RGB_BLUE];
                    columnOffset += JpegConstants.RGB_PIXELSIZE;

                    /* If the inputs are 0..MAXJSAMPLE, the outputs of these equations
                     * must be too; we do not need an explicit range-limiting operation.
                     * Hence the value being shifted is never negative, and we don't
                     * need the general RIGHT_SHIFT macro.
                     */
                    /* Y */
                    output_buf[0][output_row][col] = (byte)((m_rgb_ycc_tab[r + R_Y_OFF] + m_rgb_ycc_tab[g + G_Y_OFF] + m_rgb_ycc_tab[b + B_Y_OFF]) >> SCALEBITS);
                    /* Cb */
                    output_buf[1][output_row][col] = (byte)((m_rgb_ycc_tab[r + R_CB_OFF] + m_rgb_ycc_tab[g + G_CB_OFF] + m_rgb_ycc_tab[b + B_CB_OFF]) >> SCALEBITS);
                    /* Cr */
                    output_buf[2][output_row][col] = (byte)((m_rgb_ycc_tab[r + R_CR_OFF] + m_rgb_ycc_tab[g + G_CR_OFF] + m_rgb_ycc_tab[b + B_CR_OFF]) >> SCALEBITS);
                }

                output_row++;
            }
        }

        /// <summary>
        /// Convert some rows of samples to the JPEG colorspace.
        /// This version handles RGB->grayscale conversion, which is the same
        /// as the RGB->Y portion of RGB->YCbCr.
        /// We assume rgb_ycc_start has been called (we only use the Y tables).
        /// </summary>
        private void rgb_gray_convert(byte[][] input_buf, int input_row, byte[][][] output_buf, int output_row, int num_rows)
        {
            int num_cols = m_cinfo.m_image_width;
            for (int row = 0; row < num_rows; row++)
            {
                int columnOffset = 0;
                for (int col = 0; col < num_cols; col++)
                {
                    int r = input_buf[input_row + row][columnOffset + JpegConstants.RGB_RED];
                    int g = input_buf[input_row + row][columnOffset + JpegConstants.RGB_GREEN];
                    int b = input_buf[input_row + row][columnOffset + JpegConstants.RGB_BLUE];
                    columnOffset += JpegConstants.RGB_PIXELSIZE;

                    /* Y */
                    output_buf[0][output_row][col] = (byte)((m_rgb_ycc_tab[r + R_Y_OFF] + m_rgb_ycc_tab[g + G_Y_OFF] + m_rgb_ycc_tab[b + B_Y_OFF]) >> SCALEBITS);
                }

                output_row++;
            }
        }

        /// <summary>
        /// Convert some rows of samples to the JPEG colorspace.
        /// This version handles Adobe-style CMYK->YCCK conversion,
        /// where we convert R=1-C, G=1-M, and B=1-Y to YCbCr using the same
        /// conversion as above, while passing K (black) unchanged.
        /// We assume rgb_ycc_start has been called.
        /// </summary>
        private void cmyk_ycck_convert(byte[][] input_buf, int input_row, byte[][][] output_buf, int output_row, int num_rows)
        {
            int num_cols = m_cinfo.m_image_width;
            for (int row = 0; row < num_rows; row++)
            {
                int columnOffset = 0;
                for (int col = 0; col < num_cols; col++)
                {
                    int r = JpegConstants.MAXJSAMPLE - input_buf[input_row + row][columnOffset];
                    int g = JpegConstants.MAXJSAMPLE - input_buf[input_row + row][columnOffset + 1];
                    int b = JpegConstants.MAXJSAMPLE - input_buf[input_row + row][columnOffset + 2];

                    /* K passes through as-is */
                    /* don't need GETJSAMPLE here */
                    output_buf[3][output_row][col] = input_buf[input_row + row][columnOffset + 3];
                    columnOffset += 4;

                    /* If the inputs are 0..MAXJSAMPLE, the outputs of these equations
                     * must be too; we do not need an explicit range-limiting operation.
                     * Hence the value being shifted is never negative, and we don't
                     * need the general RIGHT_SHIFT macro.
                     */
                    /* Y */
                    output_buf[0][output_row][col] = (byte)((m_rgb_ycc_tab[r + R_Y_OFF] + m_rgb_ycc_tab[g + G_Y_OFF] + m_rgb_ycc_tab[b + B_Y_OFF]) >> SCALEBITS);
                    /* Cb */
                    output_buf[1][output_row][col] = (byte)((m_rgb_ycc_tab[r + R_CB_OFF] + m_rgb_ycc_tab[g + G_CB_OFF] + m_rgb_ycc_tab[b + B_CB_OFF]) >> SCALEBITS);
                    /* Cr */
                    output_buf[2][output_row][col] = (byte)((m_rgb_ycc_tab[r + R_CR_OFF] + m_rgb_ycc_tab[g + G_CR_OFF] + m_rgb_ycc_tab[b + B_CR_OFF]) >> SCALEBITS);
                }

                output_row++;
            }
        }

        /// <summary>
        /// Convert some rows of samples to the JPEG colorspace.
        /// This version handles grayscale output with no conversion.
        /// The source can be either plain grayscale or YCbCr (since Y == gray).
        /// </summary>
        private void grayscale_convert(byte[][] input_buf, int input_row, byte[][][] output_buf, int output_row, int num_rows)
        {
            int num_cols = m_cinfo.m_image_width;
            int instride = m_cinfo.m_input_components;

            for (int row = 0; row < num_rows; row++)
            {
                int columnOffset = 0;
                for (int col = 0; col < num_cols; col++)
                {
                    /* don't need GETJSAMPLE() here */
                    output_buf[0][output_row][col] = input_buf[input_row + row][columnOffset];
                    columnOffset += instride;
                }

                output_row++;
            }
        }

        /// <summary>
        /// Convert some rows of samples to the JPEG colorspace.
        /// This version handles multi-component colorspaces without conversion.
        /// We assume input_components == num_components.
        /// </summary>
        private void null_convert(byte[][] input_buf, int input_row, byte[][][] output_buf, int output_row, int num_rows)
        {
            int nc = m_cinfo.m_num_components;
            int num_cols = m_cinfo.m_image_width;

            for (int row = 0; row < num_rows; row++)
            {
                /* It seems fastest to make a separate pass for each component. */
                for (int ci = 0; ci < nc; ci++)
                {
                    int columnOffset = 0;
                    for (int col = 0; col < num_cols; col++)
                    {
                        /* don't need GETJSAMPLE() here */
                        output_buf[ci][output_row][col] = input_buf[input_row + row][columnOffset + ci];
                        columnOffset += nc;
                    }
                }

                output_row++;
            }
        }
    }
}
