/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * Copyright (C) 1994-1996, Thomas G. Lane.
 * This file is part of the Independent JPEG Group's software.
 * For conditions of distribution and use, see the accompanying README file.
 *
 */

/*
 * This file contains the forward-DCT management logic.
 * This code selects a particular DCT implementation to be used,
 * and it performs related housekeeping chores including coefficient
 * quantization.
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace BitMiracle.LibJpeg.Classic.Internal
{
    /// <summary>
    /// Forward DCT (also controls coefficient quantization)
    /// 
    /// A forward DCT routine is given a pointer to a work area of type DCTELEM[];
    /// the DCT is to be performed in-place in that buffer.  Type DCTELEM is int
    /// for 8-bit samples, int for 12-bit samples.  (NOTE: Floating-point DCT
    /// implementations use an array of type float, instead.)
    /// The DCT inputs are expected to be signed (range +-CENTERJSAMPLE).
    /// The DCT outputs are returned scaled up by a factor of 8; they therefore
    /// have a range of +-8K for 8-bit data, +-128K for 12-bit data. This
    /// convention improves accuracy in integer implementations and saves some
    /// work in floating-point ones.
    /// 
    /// Each IDCT routine has its own ideas about the best dct_table element type.
    /// </summary>
    class jpeg_forward_dct
    {
        private const int FAST_INTEGER_CONST_BITS = 8;

        /* We use the following pre-calculated constants.
        * If you change FAST_INTEGER_CONST_BITS you may want to add appropriate values.
        * 
        * Convert a positive real constant to an integer scaled by CONST_SCALE.
        * static int FAST_INTEGER_FIX(double x)
        *{
        *    return ((int) ((x) * (((int) 1) << FAST_INTEGER_CONST_BITS) + 0.5));
        *}
        */
        private const int FAST_INTEGER_FIX_0_382683433 = 98;        /* FIX(0.382683433) */
        private const int FAST_INTEGER_FIX_0_541196100 = 139;       /* FIX(0.541196100) */
        private const int FAST_INTEGER_FIX_0_707106781 = 181;       /* FIX(0.707106781) */
        private const int FAST_INTEGER_FIX_1_306562965 = 334;       /* FIX(1.306562965) */

        private const int SLOW_INTEGER_CONST_BITS = 13;
        private const int SLOW_INTEGER_PASS1_BITS = 2;

        /* We use the following pre-calculated constants.
        * If you change SLOW_INTEGER_CONST_BITS you may want to add appropriate values.
        * 
        * Convert a positive real constant to an integer scaled by CONST_SCALE.
        *
        * static int SLOW_INTEGER_FIX(double x)
        * {
        *     return ((int) ((x) * (((int) 1) << SLOW_INTEGER_CONST_BITS) + 0.5));
        * }
        */
        private const int SLOW_INTEGER_FIX_0_298631336 = 2446;   /* FIX(0.298631336) */
        private const int SLOW_INTEGER_FIX_0_390180644 = 3196;   /* FIX(0.390180644) */
        private const int SLOW_INTEGER_FIX_0_541196100 = 4433;   /* FIX(0.541196100) */
        private const int SLOW_INTEGER_FIX_0_765366865 = 6270;   /* FIX(0.765366865) */
        private const int SLOW_INTEGER_FIX_0_899976223 = 7373;   /* FIX(0.899976223) */
        private const int SLOW_INTEGER_FIX_1_175875602 = 9633;   /* FIX(1.175875602) */
        private const int SLOW_INTEGER_FIX_1_501321110 = 12299;  /* FIX(1.501321110) */
        private const int SLOW_INTEGER_FIX_1_847759065 = 15137;  /* FIX(1.847759065) */
        private const int SLOW_INTEGER_FIX_1_961570560 = 16069;  /* FIX(1.961570560) */
        private const int SLOW_INTEGER_FIX_2_053119869 = 16819;  /* FIX(2.053119869) */
        private const int SLOW_INTEGER_FIX_2_562915447 = 20995;  /* FIX(2.562915447) */
        private const int SLOW_INTEGER_FIX_3_072711026 = 25172;  /* FIX(3.072711026) */

        /* For AA&N IDCT method, divisors are equal to quantization
         * coefficients scaled by scalefactor[row]*scalefactor[col], where
         *   scalefactor[0] = 1
         *   scalefactor[k] = cos(k*PI/16) * sqrt(2)    for k=1..7
         * We apply a further scale factor of 8.
         */
        private const int CONST_BITS = 14;

        /* precomputed values scaled up by 14 bits */
        private static short[] aanscales = { 
            16384, 22725, 21407, 19266, 16384, 12873, 8867, 4520, 22725, 31521, 29692, 26722, 22725, 17855,
            12299, 6270, 21407, 29692, 27969, 25172, 21407, 16819, 11585,
            5906, 19266, 26722, 25172, 22654, 19266, 15137, 10426, 5315,
            16384, 22725, 21407, 19266, 16384, 12873, 8867, 4520, 12873,
            17855, 16819, 15137, 12873, 10114, 6967, 3552, 8867, 12299,
            11585, 10426, 8867, 6967, 4799, 2446, 4520, 6270, 5906, 5315,
            4520, 3552, 2446, 1247 };

        /* For float AA&N IDCT method, divisors are equal to quantization
         * coefficients scaled by scalefactor[row]*scalefactor[col], where
         *   scalefactor[0] = 1
         *   scalefactor[k] = cos(k*PI/16) * sqrt(2)    for k=1..7
         * We apply a further scale factor of 8.
         * What's actually stored is 1/divisor so that the inner loop can
         * use a multiplication rather than a division.
         */
        private static double[] aanscalefactor = { 
            1.0, 1.387039845, 1.306562965, 1.175875602, 1.0,
            0.785694958, 0.541196100, 0.275899379 };

        private jpeg_compress_struct m_cinfo;
        private bool m_useSlowMethod;
        private bool m_useFloatMethod;

        /* The actual post-DCT divisors --- not identical to the quant table
        * entries, because of scaling (especially for an unnormalized DCT).
        * Each table is given in normal array order.
        */
        private int[][] m_divisors = new int [JpegConstants.NUM_QUANT_TBLS][];

        /* Same as above for the floating-point case. */
        private float[][] m_float_divisors = new float[JpegConstants.NUM_QUANT_TBLS][];

        public jpeg_forward_dct(jpeg_compress_struct cinfo)
        {
            m_cinfo = cinfo;

            switch (cinfo.m_dct_method)
            {
                case J_DCT_METHOD.JDCT_ISLOW:
                    m_useFloatMethod = false;
                    m_useSlowMethod = true;
                    break;
                case J_DCT_METHOD.JDCT_IFAST:
                    m_useFloatMethod = false;
                    m_useSlowMethod = false;
                    break;
                case J_DCT_METHOD.JDCT_FLOAT:
                    m_useFloatMethod = true;
                    break;
                default:
                    cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOT_COMPILED);
                    break;
            }

            /* Mark divisor tables unallocated */
            for (int i = 0; i < JpegConstants.NUM_QUANT_TBLS; i++)
            {
                m_divisors[i] = null;
                m_float_divisors[i] = null;
            }
        }

        /// <summary>
        /// Initialize for a processing pass.
        /// Verify that all referenced Q-tables are present, and set up
        /// the divisor table for each one.
        /// In the current implementation, DCT of all components is done during
        /// the first pass, even if only some components will be output in the
        /// first scan.  Hence all components should be examined here.
        /// </summary>
        public virtual void start_pass()
        {
            for (int ci = 0; ci < m_cinfo.m_num_components; ci++)
            {
                int qtblno = m_cinfo.Component_info[ci].Quant_tbl_no;

                /* Make sure specified quantization table is present */
                if (qtblno < 0 || qtblno >= JpegConstants.NUM_QUANT_TBLS || m_cinfo.m_quant_tbl_ptrs[qtblno] == null)
                    m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NO_QUANT_TABLE, qtblno);

                JQUANT_TBL qtbl = m_cinfo.m_quant_tbl_ptrs[qtblno];

                /* Compute divisors for this quant table */
                /* We may do this more than once for same table, but it's not a big deal */
                int i = 0;
                switch (m_cinfo.m_dct_method)
                {
                    case J_DCT_METHOD.JDCT_ISLOW:
                        /* For LL&M IDCT method, divisors are equal to raw quantization
                         * coefficients multiplied by 8 (to counteract scaling).
                         */
                        if (m_divisors[qtblno] == null)
                            m_divisors[qtblno] = new int [JpegConstants.DCTSIZE2];

                        for (i = 0; i < JpegConstants.DCTSIZE2; i++)
                            m_divisors[qtblno][i] = ((int)qtbl.quantval[i]) << 3;

                        break;
                    case J_DCT_METHOD.JDCT_IFAST:
                        if (m_divisors[qtblno] == null)
                            m_divisors[qtblno] = new int [JpegConstants.DCTSIZE2];

                        for (i = 0; i < JpegConstants.DCTSIZE2; i++)
                            m_divisors[qtblno][i] = JpegUtils.DESCALE((int)qtbl.quantval[i] * (int)aanscales[i], CONST_BITS - 3);
                        break;
                    case J_DCT_METHOD.JDCT_FLOAT:
                        if (m_float_divisors[qtblno] == null)
                            m_float_divisors[qtblno] = new float [JpegConstants.DCTSIZE2];

                        float[] fdtbl = m_float_divisors[qtblno];
                        i = 0;
                        for (int row = 0; row < JpegConstants.DCTSIZE; row++)
                        {
                            for (int col = 0; col < JpegConstants.DCTSIZE; col++)
                            {
                                fdtbl[i] = (float)(1.0 / (((double) qtbl.quantval[i] * aanscalefactor[row] * aanscalefactor[col] * 8.0)));
                                i++;
                            }
                        }
                        break;
                    default:
                        m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOT_COMPILED);
                        break;
                }
            }
        }

        /// <summary>
        /// Perform forward DCT on one or more blocks of a component.
        /// 
        /// The input samples are taken from the sample_data[] array starting at
        /// position start_row/start_col, and moving to the right for any additional
        /// blocks. The quantized coefficients are returned in coef_blocks[].
        /// </summary>
        public virtual void forward_DCT(int quant_tbl_no, byte[][] sample_data, JBLOCK[] coef_blocks, int start_row, int start_col, int num_blocks)
        {
            if (m_useFloatMethod)
                forwardDCTFloatImpl(quant_tbl_no, sample_data, coef_blocks, start_row, start_col, num_blocks);
            else
                forwardDCTImpl(quant_tbl_no, sample_data, coef_blocks, start_row, start_col, num_blocks);
        }

        // This version is used for integer DCT implementations.
        private void forwardDCTImpl(int quant_tbl_no, byte[][] sample_data, JBLOCK[] coef_blocks, int start_row, int start_col, int num_blocks)
        {
            /* This routine is heavily used, so it's worth coding it tightly. */
            int[] workspace = new int [JpegConstants.DCTSIZE2];    /* work area for FDCT subroutine */
            for (int bi = 0; bi < num_blocks; bi++, start_col += JpegConstants.DCTSIZE)
            {
                /* Load data into workspace, applying unsigned->signed conversion */
                int workspaceIndex = 0;
                for (int elemr = 0; elemr < JpegConstants.DCTSIZE; elemr++)
                {
                    for (int column = 0; column < JpegConstants.DCTSIZE; column++)
                    {
                        workspace[workspaceIndex] = (int)sample_data[start_row + elemr][start_col + column] - JpegConstants.CENTERJSAMPLE;
                        workspaceIndex++;
                    }
                }

                /* Perform the DCT */
                if (m_useSlowMethod)
                    jpeg_fdct_islow(workspace);
                else
                    jpeg_fdct_ifast(workspace);

                /* Quantize/descale the coefficients, and store into coef_blocks[] */
                for (int i = 0; i < JpegConstants.DCTSIZE2; i++)
                {
                    int qval = m_divisors[quant_tbl_no][i];
                    int temp = workspace[i];

                    if (temp < 0)
                    {
                        temp = -temp;
                        temp += qval >> 1;  /* for rounding */

                        if (temp >= qval)
                            temp /= qval;
                        else
                            temp = 0;

                        temp = -temp;
                    }
                    else
                    {
                        temp += qval >> 1;  /* for rounding */

                        if (temp >= qval)
                            temp /= qval;
                        else
                            temp = 0;
                    }

                    coef_blocks[bi][i] = (short) temp;
                }
            }
        }

        // This version is used for floating-point DCT implementations.
        private void forwardDCTFloatImpl(int quant_tbl_no, byte[][] sample_data, JBLOCK[] coef_blocks, int start_row, int start_col, int num_blocks)
        {
            /* This routine is heavily used, so it's worth coding it tightly. */
            float[] workspace = new float [JpegConstants.DCTSIZE2]; /* work area for FDCT subroutine */
            for (int bi = 0; bi < num_blocks; bi++, start_col += JpegConstants.DCTSIZE)
            {
                /* Load data into workspace, applying unsigned->signed conversion */
                int workspaceIndex = 0;
                for (int elemr = 0; elemr < JpegConstants.DCTSIZE; elemr++)
                {
                    for (int column = 0; column < JpegConstants.DCTSIZE; column++)
                    {
                        workspace[workspaceIndex] = (float)((int)sample_data[start_row + elemr][start_col + column] - JpegConstants.CENTERJSAMPLE);
                        workspaceIndex++;
                    }
                }

                /* Perform the DCT */
                jpeg_fdct_float(workspace);

                /* Quantize/descale the coefficients, and store into coef_blocks[] */
                for (int i = 0; i < JpegConstants.DCTSIZE2; i++)
                {
                    /* Apply the quantization and scaling factor */
                    float temp = workspace[i] * m_float_divisors[quant_tbl_no][i];

                    /* Round to nearest integer.
                     * Since C does not specify the direction of rounding for negative
                     * quotients, we have to force the dividend positive for portability.
                     * The maximum coefficient size is +-16K (for 12-bit data), so this
                     * code should work for either 16-bit or 32-bit ints.
                     */
                    coef_blocks[bi][i] = (short)((int)(temp + (float)16384.5) - 16384);
                }
            }
        }

        /// <summary>
        /// Perform the forward DCT on one block of samples.
        /// NOTE: this code only copes with 8x8 DCTs.
        /// 
        /// A floating-point implementation of the 
        /// forward DCT (Discrete Cosine Transform).
        /// 
        /// This implementation should be more accurate than either of the integer
        /// DCT implementations.  However, it may not give the same results on all
        /// machines because of differences in roundoff behavior.  Speed will depend
        /// on the hardware's floating point capacity.
        /// 
        /// A 2-D DCT can be done by 1-D DCT on each row followed by 1-D DCT
        /// on each column.  Direct algorithms are also available, but they are
        /// much more complex and seem not to be any faster when reduced to code.
        /// 
        /// This implementation is based on Arai, Agui, and Nakajima's algorithm for
        /// scaled DCT.  Their original paper (Trans. IEICE E-71(11):1095) is in
        /// Japanese, but the algorithm is described in the Pennebaker &amp; Mitchell
        /// JPEG textbook (see REFERENCES section in file README).  The following code
        /// is based directly on figure 4-8 in P&amp;M.
        /// While an 8-point DCT cannot be done in less than 11 multiplies, it is
        /// possible to arrange the computation so that many of the multiplies are
        /// simple scalings of the final outputs.  These multiplies can then be
        /// folded into the multiplications or divisions by the JPEG quantization
        /// table entries.  The AA&amp;N method leaves only 5 multiplies and 29 adds
        /// to be done in the DCT itself.
        /// The primary disadvantage of this method is that with a fixed-point
        /// implementation, accuracy is lost due to imprecise representation of the
        /// scaled quantization values.  However, that problem does not arise if
        /// we use floating point arithmetic.
        /// </summary>
        private static void jpeg_fdct_float(float[] data)
        {
            /* Pass 1: process rows. */
            int dataIndex = 0;
            for (int ctr = JpegConstants.DCTSIZE - 1; ctr >= 0; ctr--)
            {
                float tmp0 = data[dataIndex + 0] + data[dataIndex + 7];
                float tmp7 = data[dataIndex + 0] - data[dataIndex + 7];
                float tmp1 = data[dataIndex + 1] + data[dataIndex + 6];
                float tmp6 = data[dataIndex + 1] - data[dataIndex + 6];
                float tmp2 = data[dataIndex + 2] + data[dataIndex + 5];
                float tmp5 = data[dataIndex + 2] - data[dataIndex + 5];
                float tmp3 = data[dataIndex + 3] + data[dataIndex + 4];
                float tmp4 = data[dataIndex + 3] - data[dataIndex + 4];

                /* Even part */

                float tmp10 = tmp0 + tmp3;    /* phase 2 */
                float tmp13 = tmp0 - tmp3;
                float tmp11 = tmp1 + tmp2;
                float tmp12 = tmp1 - tmp2;

                data[dataIndex + 0] = tmp10 + tmp11; /* phase 3 */
                data[dataIndex + 4] = tmp10 - tmp11;

                float z1 = (tmp12 + tmp13) * ((float)0.707106781); /* c4 */
                data[dataIndex + 2] = tmp13 + z1;    /* phase 5 */
                data[dataIndex + 6] = tmp13 - z1;

                /* Odd part */

                tmp10 = tmp4 + tmp5;    /* phase 2 */
                tmp11 = tmp5 + tmp6;
                tmp12 = tmp6 + tmp7;

                /* The rotator is modified from fig 4-8 to avoid extra negations. */
                float z5 = (tmp10 - tmp12) * ((float)0.382683433); /* c6 */
                float z2 = ((float)0.541196100) * tmp10 + z5; /* c2-c6 */
                float z4 = ((float)1.306562965) * tmp12 + z5; /* c2+c6 */
                float z3 = tmp11 * ((float)0.707106781); /* c4 */

                float z11 = tmp7 + z3;        /* phase 5 */
                float z13 = tmp7 - z3;

                data[dataIndex + 5] = z13 + z2;  /* phase 6 */
                data[dataIndex + 3] = z13 - z2;
                data[dataIndex + 1] = z11 + z4;
                data[dataIndex + 7] = z11 - z4;

                dataIndex += JpegConstants.DCTSIZE;     /* advance pointer to next row */
            }

            /* Pass 2: process columns. */

            dataIndex = 0;
            for (int ctr = JpegConstants.DCTSIZE - 1; ctr >= 0; ctr--)
            {
                float tmp0 = data[dataIndex + JpegConstants.DCTSIZE * 0] + data[dataIndex + JpegConstants.DCTSIZE * 7];
                float tmp7 = data[dataIndex + JpegConstants.DCTSIZE * 0] - data[dataIndex + JpegConstants.DCTSIZE * 7];
                float tmp1 = data[dataIndex + JpegConstants.DCTSIZE * 1] + data[dataIndex + JpegConstants.DCTSIZE * 6];
                float tmp6 = data[dataIndex + JpegConstants.DCTSIZE * 1] - data[dataIndex + JpegConstants.DCTSIZE * 6];
                float tmp2 = data[dataIndex + JpegConstants.DCTSIZE * 2] + data[dataIndex + JpegConstants.DCTSIZE * 5];
                float tmp5 = data[dataIndex + JpegConstants.DCTSIZE * 2] - data[dataIndex + JpegConstants.DCTSIZE * 5];
                float tmp3 = data[dataIndex + JpegConstants.DCTSIZE * 3] + data[dataIndex + JpegConstants.DCTSIZE * 4];
                float tmp4 = data[dataIndex + JpegConstants.DCTSIZE * 3] - data[dataIndex + JpegConstants.DCTSIZE * 4];

                /* Even part */

                float tmp10 = tmp0 + tmp3;    /* phase 2 */
                float tmp13 = tmp0 - tmp3;
                float tmp11 = tmp1 + tmp2;
                float tmp12 = tmp1 - tmp2;

                data[dataIndex + JpegConstants.DCTSIZE * 0] = tmp10 + tmp11; /* phase 3 */
                data[dataIndex + JpegConstants.DCTSIZE * 4] = tmp10 - tmp11;

                float z1 = (tmp12 + tmp13) * ((float)0.707106781); /* c4 */
                data[dataIndex + JpegConstants.DCTSIZE * 2] = tmp13 + z1; /* phase 5 */
                data[dataIndex + JpegConstants.DCTSIZE * 6] = tmp13 - z1;

                /* Odd part */

                tmp10 = tmp4 + tmp5;    /* phase 2 */
                tmp11 = tmp5 + tmp6;
                tmp12 = tmp6 + tmp7;

                /* The rotator is modified from fig 4-8 to avoid extra negations. */
                float z5 = (tmp10 - tmp12) * ((float)0.382683433); /* c6 */
                float z2 = ((float)0.541196100) * tmp10 + z5; /* c2-c6 */
                float z4 = ((float)1.306562965) * tmp12 + z5; /* c2+c6 */
                float z3 = tmp11 * ((float)0.707106781); /* c4 */

                float z11 = tmp7 + z3;        /* phase 5 */
                float z13 = tmp7 - z3;

                data[dataIndex + JpegConstants.DCTSIZE * 5] = z13 + z2; /* phase 6 */
                data[dataIndex + JpegConstants.DCTSIZE * 3] = z13 - z2;
                data[dataIndex + JpegConstants.DCTSIZE * 1] = z11 + z4;
                data[dataIndex + JpegConstants.DCTSIZE * 7] = z11 - z4;

                dataIndex++;          /* advance pointer to next column */
            }
        }

        /// <summary>
        /// Perform the forward DCT on one block of samples.
        /// NOTE: this code only copes with 8x8 DCTs.
        /// This file contains a fast, not so accurate integer implementation of the
        /// forward DCT (Discrete Cosine Transform).
        /// 
        /// A 2-D DCT can be done by 1-D DCT on each row followed by 1-D DCT
        /// on each column.  Direct algorithms are also available, but they are
        /// much more complex and seem not to be any faster when reduced to code.
        /// 
        /// This implementation is based on Arai, Agui, and Nakajima's algorithm for
        /// scaled DCT.  Their original paper (Trans. IEICE E-71(11):1095) is in
        /// Japanese, but the algorithm is described in the Pennebaker &amp; Mitchell
        /// JPEG textbook (see REFERENCES section in file README).  The following code
        /// is based directly on figure 4-8 in P&amp;M.
        /// While an 8-point DCT cannot be done in less than 11 multiplies, it is
        /// possible to arrange the computation so that many of the multiplies are
        /// simple scalings of the final outputs.  These multiplies can then be
        /// folded into the multiplications or divisions by the JPEG quantization
        /// table entries.  The AA&amp;N method leaves only 5 multiplies and 29 adds
        /// to be done in the DCT itself.
        /// The primary disadvantage of this method is that with fixed-point math,
        /// accuracy is lost due to imprecise representation of the scaled
        /// quantization values.  The smaller the quantization table entry, the less
        /// precise the scaled value, so this implementation does worse with high-
        /// quality-setting files than with low-quality ones.
        /// 
        /// Scaling decisions are generally the same as in the LL&amp;M algorithm;
        /// see jpeg_fdct_islow for more details.  However, we choose to descale
        /// (right shift) multiplication products as soon as they are formed,
        /// rather than carrying additional fractional bits into subsequent additions.
        /// This compromises accuracy slightly, but it lets us save a few shifts.
        /// More importantly, 16-bit arithmetic is then adequate (for 8-bit samples)
        /// everywhere except in the multiplications proper; this saves a good deal
        /// of work on 16-bit-int machines.
        /// 
        /// Again to save a few shifts, the intermediate results between pass 1 and
        /// pass 2 are not upscaled, but are represented only to integral precision.
        /// 
        /// A final compromise is to represent the multiplicative constants to only
        /// 8 fractional bits, rather than 13.  This saves some shifting work on some
        /// machines, and may also reduce the cost of multiplication (since there
        /// are fewer one-bits in the constants).
        /// </summary>
        private static void jpeg_fdct_ifast(int[] data)
        {
            /* Pass 1: process rows. */
            int dataIndex = 0;
            for (int ctr = JpegConstants.DCTSIZE - 1; ctr >= 0; ctr--)
            {
                int tmp0 = data[dataIndex + 0] + data[dataIndex + 7];
                int tmp7 = data[dataIndex + 0] - data[dataIndex + 7];
                int tmp1 = data[dataIndex + 1] + data[dataIndex + 6];
                int tmp6 = data[dataIndex + 1] - data[dataIndex + 6];
                int tmp2 = data[dataIndex + 2] + data[dataIndex + 5];
                int tmp5 = data[dataIndex + 2] - data[dataIndex + 5];
                int tmp3 = data[dataIndex + 3] + data[dataIndex + 4];
                int tmp4 = data[dataIndex + 3] - data[dataIndex + 4];

                /* Even part */

                int tmp10 = tmp0 + tmp3;    /* phase 2 */
                int tmp13 = tmp0 - tmp3;
                int tmp11 = tmp1 + tmp2;
                int tmp12 = tmp1 - tmp2;

                data[dataIndex + 0] = tmp10 + tmp11; /* phase 3 */
                data[dataIndex + 4] = tmp10 - tmp11;

                int z1 = FAST_INTEGER_MULTIPLY(tmp12 + tmp13, FAST_INTEGER_FIX_0_707106781); /* c4 */
                data[dataIndex + 2] = tmp13 + z1;    /* phase 5 */
                data[dataIndex + 6] = tmp13 - z1;

                /* Odd part */

                tmp10 = tmp4 + tmp5;    /* phase 2 */
                tmp11 = tmp5 + tmp6;
                tmp12 = tmp6 + tmp7;

                /* The rotator is modified from fig 4-8 to avoid extra negations. */
                int z5 = FAST_INTEGER_MULTIPLY(tmp10 - tmp12, FAST_INTEGER_FIX_0_382683433); /* c6 */
                int z2 = FAST_INTEGER_MULTIPLY(tmp10, FAST_INTEGER_FIX_0_541196100) + z5; /* c2-c6 */
                int z4 = FAST_INTEGER_MULTIPLY(tmp12, FAST_INTEGER_FIX_1_306562965) + z5; /* c2+c6 */
                int z3 = FAST_INTEGER_MULTIPLY(tmp11, FAST_INTEGER_FIX_0_707106781); /* c4 */

                int z11 = tmp7 + z3;        /* phase 5 */
                int z13 = tmp7 - z3;

                data[dataIndex + 5] = z13 + z2;  /* phase 6 */
                data[dataIndex + 3] = z13 - z2;
                data[dataIndex + 1] = z11 + z4;
                data[dataIndex + 7] = z11 - z4;

                dataIndex += JpegConstants.DCTSIZE;     /* advance pointer to next row */
            }

            /* Pass 2: process columns. */

            dataIndex = 0;
            for (int ctr = JpegConstants.DCTSIZE - 1; ctr >= 0; ctr--)
            {
                int tmp0 = data[dataIndex + JpegConstants.DCTSIZE * 0] + data[dataIndex + JpegConstants.DCTSIZE * 7];
                int tmp7 = data[dataIndex + JpegConstants.DCTSIZE * 0] - data[dataIndex + JpegConstants.DCTSIZE * 7];
                int tmp1 = data[dataIndex + JpegConstants.DCTSIZE * 1] + data[dataIndex + JpegConstants.DCTSIZE * 6];
                int tmp6 = data[dataIndex + JpegConstants.DCTSIZE * 1] - data[dataIndex + JpegConstants.DCTSIZE * 6];
                int tmp2 = data[dataIndex + JpegConstants.DCTSIZE * 2] + data[dataIndex + JpegConstants.DCTSIZE * 5];
                int tmp5 = data[dataIndex + JpegConstants.DCTSIZE * 2] - data[dataIndex + JpegConstants.DCTSIZE * 5];
                int tmp3 = data[dataIndex + JpegConstants.DCTSIZE * 3] + data[dataIndex + JpegConstants.DCTSIZE * 4];
                int tmp4 = data[dataIndex + JpegConstants.DCTSIZE * 3] - data[dataIndex + JpegConstants.DCTSIZE * 4];

                /* Even part */

                int tmp10 = tmp0 + tmp3;    /* phase 2 */
                int tmp13 = tmp0 - tmp3;
                int tmp11 = tmp1 + tmp2;
                int tmp12 = tmp1 - tmp2;

                data[dataIndex + JpegConstants.DCTSIZE * 0] = tmp10 + tmp11; /* phase 3 */
                data[dataIndex + JpegConstants.DCTSIZE * 4] = tmp10 - tmp11;

                int z1 = FAST_INTEGER_MULTIPLY(tmp12 + tmp13, FAST_INTEGER_FIX_0_707106781); /* c4 */
                data[dataIndex + JpegConstants.DCTSIZE * 2] = tmp13 + z1; /* phase 5 */
                data[dataIndex + JpegConstants.DCTSIZE * 6] = tmp13 - z1;

                /* Odd part */

                tmp10 = tmp4 + tmp5;    /* phase 2 */
                tmp11 = tmp5 + tmp6;
                tmp12 = tmp6 + tmp7;

                /* The rotator is modified from fig 4-8 to avoid extra negations. */
                int z5 = FAST_INTEGER_MULTIPLY(tmp10 - tmp12, FAST_INTEGER_FIX_0_382683433); /* c6 */
                int z2 = FAST_INTEGER_MULTIPLY(tmp10, FAST_INTEGER_FIX_0_541196100) + z5; /* c2-c6 */
                int z4 = FAST_INTEGER_MULTIPLY(tmp12, FAST_INTEGER_FIX_1_306562965) + z5; /* c2+c6 */
                int z3 = FAST_INTEGER_MULTIPLY(tmp11, FAST_INTEGER_FIX_0_707106781); /* c4 */

                int z11 = tmp7 + z3;        /* phase 5 */
                int z13 = tmp7 - z3;

                data[dataIndex + JpegConstants.DCTSIZE * 5] = z13 + z2; /* phase 6 */
                data[dataIndex + JpegConstants.DCTSIZE * 3] = z13 - z2;
                data[dataIndex + JpegConstants.DCTSIZE * 1] = z11 + z4;
                data[dataIndex + JpegConstants.DCTSIZE * 7] = z11 - z4;

                dataIndex++;          /* advance pointer to next column */
            }
        }

        /// <summary>
        /// Perform the forward DCT on one block of samples.
        /// NOTE: this code only copes with 8x8 DCTs.
        /// 
        /// A slow-but-accurate integer implementation of the
        /// forward DCT (Discrete Cosine Transform).
        /// 
        /// A 2-D DCT can be done by 1-D DCT on each row followed by 1-D DCT
        /// on each column.  Direct algorithms are also available, but they are
        /// much more complex and seem not to be any faster when reduced to code.
        /// 
        /// This implementation is based on an algorithm described in
        /// C. Loeffler, A. Ligtenberg and G. Moschytz, "Practical Fast 1-D DCT
        /// Algorithms with 11 Multiplications", Proc. Int'l. Conf. on Acoustics,
        /// Speech, and Signal Processing 1989 (ICASSP '89), pp. 988-991.
        /// The primary algorithm described there uses 11 multiplies and 29 adds.
        /// We use their alternate method with 12 multiplies and 32 adds.
        /// The advantage of this method is that no data path contains more than one
        /// multiplication; this allows a very simple and accurate implementation in
        /// scaled fixed-point arithmetic, with a minimal number of shifts.
        /// 
        /// The poop on this scaling stuff is as follows:
        /// 
        /// Each 1-D DCT step produces outputs which are a factor of sqrt(N)
        /// larger than the true DCT outputs.  The final outputs are therefore
        /// a factor of N larger than desired; since N=8 this can be cured by
        /// a simple right shift at the end of the algorithm.  The advantage of
        /// this arrangement is that we save two multiplications per 1-D DCT,
        /// because the y0 and y4 outputs need not be divided by sqrt(N).
        /// In the IJG code, this factor of 8 is removed by the quantization 
        /// step, NOT here.
        /// 
        /// We have to do addition and subtraction of the integer inputs, which
        /// is no problem, and multiplication by fractional constants, which is
        /// a problem to do in integer arithmetic.  We multiply all the constants
        /// by CONST_SCALE and convert them to integer constants (thus retaining
        /// SLOW_INTEGER_CONST_BITS bits of precision in the constants).  After doing a
        /// multiplication we have to divide the product by CONST_SCALE, with proper
        /// rounding, to produce the correct output.  This division can be done
        /// cheaply as a right shift of SLOW_INTEGER_CONST_BITS bits.  We postpone shifting
        /// as long as possible so that partial sums can be added together with
        /// full fractional precision.
        /// 
        /// The outputs of the first pass are scaled up by SLOW_INTEGER_PASS1_BITS bits so that
        /// they are represented to better-than-integral precision.  These outputs
        /// require BITS_IN_JSAMPLE + SLOW_INTEGER_PASS1_BITS + 3 bits; this fits in a 16-bit word
        /// with the recommended scaling.  (For 12-bit sample data, the intermediate
        /// array is int anyway.)
        /// 
        /// To avoid overflow of the 32-bit intermediate results in pass 2, we must
        /// have BITS_IN_JSAMPLE + SLOW_INTEGER_CONST_BITS + SLOW_INTEGER_PASS1_BITS &lt;= 26.  Error analysis
        /// shows that the values given below are the most effective.
        /// </summary>
        private static void jpeg_fdct_islow(int[] data)
        {
            /* Pass 1: process rows. */
            /* Note results are scaled up by sqrt(8) compared to a true DCT; */
            /* furthermore, we scale the results by 2**SLOW_INTEGER_PASS1_BITS. */
            int dataIndex = 0;
            for (int ctr = JpegConstants.DCTSIZE - 1; ctr >= 0; ctr--)
            {
                int tmp0 = data[dataIndex + 0] + data[dataIndex + 7];
                int tmp7 = data[dataIndex + 0] - data[dataIndex + 7];
                int tmp1 = data[dataIndex + 1] + data[dataIndex + 6];
                int tmp6 = data[dataIndex + 1] - data[dataIndex + 6];
                int tmp2 = data[dataIndex + 2] + data[dataIndex + 5];
                int tmp5 = data[dataIndex + 2] - data[dataIndex + 5];
                int tmp3 = data[dataIndex + 3] + data[dataIndex + 4];
                int tmp4 = data[dataIndex + 3] - data[dataIndex + 4];

                /* Even part per LL&M figure 1 --- note that published figure is faulty;
                * rotator "sqrt(2)*c1" should be "sqrt(2)*c6".
                */

                int tmp10 = tmp0 + tmp3;
                int tmp13 = tmp0 - tmp3;
                int tmp11 = tmp1 + tmp2;
                int tmp12 = tmp1 - tmp2;

                data[dataIndex + 0] = (tmp10 + tmp11) << SLOW_INTEGER_PASS1_BITS;
                data[dataIndex + 4] = (tmp10 - tmp11) << SLOW_INTEGER_PASS1_BITS;

                int z1 = (tmp12 + tmp13) * SLOW_INTEGER_FIX_0_541196100;
                data[dataIndex + 2] = JpegUtils.DESCALE(z1 + tmp13 * SLOW_INTEGER_FIX_0_765366865,
                                                SLOW_INTEGER_CONST_BITS - SLOW_INTEGER_PASS1_BITS);
                data[dataIndex + 6] = JpegUtils.DESCALE(z1 + tmp12 * (-SLOW_INTEGER_FIX_1_847759065),
                                                SLOW_INTEGER_CONST_BITS - SLOW_INTEGER_PASS1_BITS);

                /* Odd part per figure 8 --- note paper omits factor of sqrt(2).
                * cK represents cos(K*pi/16).
                * i0..i3 in the paper are tmp4..tmp7 here.
                */

                z1 = tmp4 + tmp7;
                int z2 = tmp5 + tmp6;
                int z3 = tmp4 + tmp6;
                int z4 = tmp5 + tmp7;
                int z5 = (z3 + z4) * SLOW_INTEGER_FIX_1_175875602; /* sqrt(2) * c3 */

                tmp4 = tmp4 * SLOW_INTEGER_FIX_0_298631336; /* sqrt(2) * (-c1+c3+c5-c7) */
                tmp5 = tmp5 * SLOW_INTEGER_FIX_2_053119869; /* sqrt(2) * ( c1+c3-c5+c7) */
                tmp6 = tmp6 * SLOW_INTEGER_FIX_3_072711026; /* sqrt(2) * ( c1+c3+c5-c7) */
                tmp7 = tmp7 * SLOW_INTEGER_FIX_1_501321110; /* sqrt(2) * ( c1+c3-c5-c7) */
                z1 = z1 * (-SLOW_INTEGER_FIX_0_899976223); /* sqrt(2) * (c7-c3) */
                z2 = z2 * (-SLOW_INTEGER_FIX_2_562915447); /* sqrt(2) * (-c1-c3) */
                z3 = z3 * (-SLOW_INTEGER_FIX_1_961570560); /* sqrt(2) * (-c3-c5) */
                z4 = z4 * (-SLOW_INTEGER_FIX_0_390180644); /* sqrt(2) * (c5-c3) */

                z3 += z5;
                z4 += z5;

                data[dataIndex + 7] = JpegUtils.DESCALE(tmp4 + z1 + z3, SLOW_INTEGER_CONST_BITS - SLOW_INTEGER_PASS1_BITS);
                data[dataIndex + 5] = JpegUtils.DESCALE(tmp5 + z2 + z4, SLOW_INTEGER_CONST_BITS - SLOW_INTEGER_PASS1_BITS);
                data[dataIndex + 3] = JpegUtils.DESCALE(tmp6 + z2 + z3, SLOW_INTEGER_CONST_BITS - SLOW_INTEGER_PASS1_BITS);
                data[dataIndex + 1] = JpegUtils.DESCALE(tmp7 + z1 + z4, SLOW_INTEGER_CONST_BITS - SLOW_INTEGER_PASS1_BITS);

                dataIndex += JpegConstants.DCTSIZE;     /* advance pointer to next row */
            }

            /* Pass 2: process columns.
            * We remove the SLOW_INTEGER_PASS1_BITS scaling, but leave the results scaled up
            * by an overall factor of 8.
            */

            dataIndex = 0;
            for (int ctr = JpegConstants.DCTSIZE - 1; ctr >= 0; ctr--)
            {
                int tmp0 = data[dataIndex + JpegConstants.DCTSIZE * 0] + data[dataIndex + JpegConstants.DCTSIZE * 7];
                int tmp7 = data[dataIndex + JpegConstants.DCTSIZE * 0] - data[dataIndex + JpegConstants.DCTSIZE * 7];
                int tmp1 = data[dataIndex + JpegConstants.DCTSIZE * 1] + data[dataIndex + JpegConstants.DCTSIZE * 6];
                int tmp6 = data[dataIndex + JpegConstants.DCTSIZE * 1] - data[dataIndex + JpegConstants.DCTSIZE * 6];
                int tmp2 = data[dataIndex + JpegConstants.DCTSIZE * 2] + data[dataIndex + JpegConstants.DCTSIZE * 5];
                int tmp5 = data[dataIndex + JpegConstants.DCTSIZE * 2] - data[dataIndex + JpegConstants.DCTSIZE * 5];
                int tmp3 = data[dataIndex + JpegConstants.DCTSIZE * 3] + data[dataIndex + JpegConstants.DCTSIZE * 4];
                int tmp4 = data[dataIndex + JpegConstants.DCTSIZE * 3] - data[dataIndex + JpegConstants.DCTSIZE * 4];

                /* Even part per LL&M figure 1 --- note that published figure is faulty;
                * rotator "sqrt(2)*c1" should be "sqrt(2)*c6".
                */

                int tmp10 = tmp0 + tmp3;
                int tmp13 = tmp0 - tmp3;
                int tmp11 = tmp1 + tmp2;
                int tmp12 = tmp1 - tmp2;

                data[dataIndex + JpegConstants.DCTSIZE * 0] = JpegUtils.DESCALE(tmp10 + tmp11, SLOW_INTEGER_PASS1_BITS);
                data[dataIndex + JpegConstants.DCTSIZE * 4] = JpegUtils.DESCALE(tmp10 - tmp11, SLOW_INTEGER_PASS1_BITS);

                int z1 = (tmp12 + tmp13) * SLOW_INTEGER_FIX_0_541196100;
                data[dataIndex + JpegConstants.DCTSIZE * 2] = JpegUtils.DESCALE(z1 + tmp13 * SLOW_INTEGER_FIX_0_765366865,
                                                          SLOW_INTEGER_CONST_BITS + SLOW_INTEGER_PASS1_BITS);
                data[dataIndex + JpegConstants.DCTSIZE * 6] = JpegUtils.DESCALE(z1 + tmp12 * (-SLOW_INTEGER_FIX_1_847759065),
                                                          SLOW_INTEGER_CONST_BITS + SLOW_INTEGER_PASS1_BITS);

                /* Odd part per figure 8 --- note paper omits factor of sqrt(2).
                * cK represents cos(K*pi/16).
                * i0..i3 in the paper are tmp4..tmp7 here.
                */

                z1 = tmp4 + tmp7;
                int z2 = tmp5 + tmp6;
                int z3 = tmp4 + tmp6;
                int z4 = tmp5 + tmp7;
                int z5 = (z3 + z4) * SLOW_INTEGER_FIX_1_175875602; /* sqrt(2) * c3 */

                tmp4 = tmp4 * SLOW_INTEGER_FIX_0_298631336; /* sqrt(2) * (-c1+c3+c5-c7) */
                tmp5 = tmp5 * SLOW_INTEGER_FIX_2_053119869; /* sqrt(2) * ( c1+c3-c5+c7) */
                tmp6 = tmp6 * SLOW_INTEGER_FIX_3_072711026; /* sqrt(2) * ( c1+c3+c5-c7) */
                tmp7 = tmp7 * SLOW_INTEGER_FIX_1_501321110; /* sqrt(2) * ( c1+c3-c5-c7) */
                z1 = z1 * (-SLOW_INTEGER_FIX_0_899976223); /* sqrt(2) * (c7-c3) */
                z2 = z2 * (-SLOW_INTEGER_FIX_2_562915447); /* sqrt(2) * (-c1-c3) */
                z3 = z3 * (-SLOW_INTEGER_FIX_1_961570560); /* sqrt(2) * (-c3-c5) */
                z4 = z4 * (-SLOW_INTEGER_FIX_0_390180644); /* sqrt(2) * (c5-c3) */

                z3 += z5;
                z4 += z5;

                data[dataIndex + JpegConstants.DCTSIZE * 7] = JpegUtils.DESCALE(tmp4 + z1 + z3, SLOW_INTEGER_CONST_BITS + SLOW_INTEGER_PASS1_BITS);
                data[dataIndex + JpegConstants.DCTSIZE * 5] = JpegUtils.DESCALE(tmp5 + z2 + z4, SLOW_INTEGER_CONST_BITS + SLOW_INTEGER_PASS1_BITS);
                data[dataIndex + JpegConstants.DCTSIZE * 3] = JpegUtils.DESCALE(tmp6 + z2 + z3, SLOW_INTEGER_CONST_BITS + SLOW_INTEGER_PASS1_BITS);
                data[dataIndex + JpegConstants.DCTSIZE * 1] = JpegUtils.DESCALE(tmp7 + z1 + z4, SLOW_INTEGER_CONST_BITS + SLOW_INTEGER_PASS1_BITS);

                dataIndex++;          /* advance pointer to next column */
            }
        }

        /// <summary>
        /// Multiply a DCTELEM variable by an int constant, and immediately
        /// descale to yield a DCTELEM result.
        /// </summary>
        private static int FAST_INTEGER_MULTIPLY(int var, int c)
        {
#if !USE_ACCURATE_ROUNDING
            return (JpegUtils.RIGHT_SHIFT((var) * (c), FAST_INTEGER_CONST_BITS));
#else
            return (JpegUtils.DESCALE((var) * (c), FAST_INTEGER_CONST_BITS));
#endif
        }
    }
}
