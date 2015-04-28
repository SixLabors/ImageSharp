/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * Copyright (C) 1994-1996, Thomas G. Lane.
 * This file is part of the Independent JPEG Group's software.
 * For conditions of distribution and use, see the accompanying README file.
 *
 */

/*
 * This file contains 2-pass color quantization (color mapping) routines.
 * These routines provide selection of a custom color map for an image,
 * followed by mapping of the image to that color map, with optional
 * Floyd-Steinberg dithering.
 * It is also possible to use just the second pass to map to an arbitrary
 * externally-given color map.
 *
 * Note: ordered dithering is not supported, since there isn't any fast
 * way to compute intercolor distances; it's unclear that ordered dither's
 * fundamental assumptions even hold with an irregularly spaced color map.
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace BitMiracle.LibJpeg.Classic.Internal
{
    /// <summary>
    /// This module implements the well-known Heckbert paradigm for color
    /// quantization.  Most of the ideas used here can be traced back to
    /// Heckbert's seminal paper
    /// Heckbert, Paul.  "Color Image Quantization for Frame Buffer Display",
    /// Proc. SIGGRAPH '82, Computer Graphics v.16 #3 (July 1982), pp 297-304.
    /// 
    /// In the first pass over the image, we accumulate a histogram showing the
    /// usage count of each possible color.  To keep the histogram to a reasonable
    /// size, we reduce the precision of the input; typical practice is to retain
    /// 5 or 6 bits per color, so that 8 or 4 different input values are counted
    /// in the same histogram cell.
    /// 
    /// Next, the color-selection step begins with a box representing the whole
    /// color space, and repeatedly splits the "largest" remaining box until we
    /// have as many boxes as desired colors.  Then the mean color in each
    /// remaining box becomes one of the possible output colors.
    /// 
    /// The second pass over the image maps each input pixel to the closest output
    /// color (optionally after applying a Floyd-Steinberg dithering correction).
    /// This mapping is logically trivial, but making it go fast enough requires
    /// considerable care.
    /// 
    /// Heckbert-style quantizers vary a good deal in their policies for choosing
    /// the "largest" box and deciding where to cut it.  The particular policies
    /// used here have proved out well in experimental comparisons, but better ones
    /// may yet be found.
    /// 
    /// In earlier versions of the IJG code, this module quantized in YCbCr color
    /// space, processing the raw upsampled data without a color conversion step.
    /// This allowed the color conversion math to be done only once per colormap
    /// entry, not once per pixel.  However, that optimization precluded other
    /// useful optimizations (such as merging color conversion with upsampling)
    /// and it also interfered with desired capabilities such as quantizing to an
    /// externally-supplied colormap.  We have therefore abandoned that approach.
    /// The present code works in the post-conversion color space, typically RGB.
    /// 
    /// To improve the visual quality of the results, we actually work in scaled
    /// RGB space, giving G distances more weight than R, and R in turn more than
    /// B.  To do everything in integer math, we must use integer scale factors.
    /// The 2/3/1 scale factors used here correspond loosely to the relative
    /// weights of the colors in the NTSC grayscale equation.
    /// If you want to use this code to quantize a non-RGB color space, you'll
    /// probably need to change these scale factors.
    /// 
    /// First we have the histogram data structure and routines for creating it.
    /// 
    /// The number of bits of precision can be adjusted by changing these symbols.
    /// We recommend keeping 6 bits for G and 5 each for R and B.
    /// If you have plenty of memory and cycles, 6 bits all around gives marginally
    /// better results; if you are short of memory, 5 bits all around will save
    /// some space but degrade the results.
    /// To maintain a fully accurate histogram, we'd need to allocate a "long"
    /// (preferably unsigned long) for each cell.  In practice this is overkill;
    /// we can get by with 16 bits per cell.  Few of the cell counts will overflow,
    /// and clamping those that do overflow to the maximum value will give close-
    /// enough results.  This reduces the recommended histogram size from 256Kb
    /// to 128Kb, which is a useful savings on PC-class machines.
    /// (In the second pass the histogram space is re-used for pixel mapping data;
    /// in that capacity, each cell must be able to store zero to the number of
    /// desired colors.  16 bits/cell is plenty for that too.)
    /// Since the JPEG code is intended to run in small memory model on 80x86
    /// machines, we can't just allocate the histogram in one chunk.  Instead
    /// of a true 3-D array, we use a row of pointers to 2-D arrays.  Each
    /// pointer corresponds to a C0 value (typically 2^5 = 32 pointers) and
    /// each 2-D array has 2^6*2^5 = 2048 or 2^6*2^6 = 4096 entries.  Note that
    /// on 80x86 machines, the pointer row is in near memory but the actual
    /// arrays are in far memory (same arrangement as we use for image arrays).
    /// 
    /// 
    /// Declarations for Floyd-Steinberg dithering.
    /// 
    /// Errors are accumulated into the array fserrors[], at a resolution of
    /// 1/16th of a pixel count.  The error at a given pixel is propagated
    /// to its not-yet-processed neighbors using the standard F-S fractions,
    ///     ... (here)  7/16
    /// 3/16    5/16    1/16
    /// We work left-to-right on even rows, right-to-left on odd rows.
    /// 
    /// We can get away with a single array (holding one row's worth of errors)
    /// by using it to store the current row's errors at pixel columns not yet
    /// processed, but the next row's errors at columns already processed.  We
    /// need only a few extra variables to hold the errors immediately around the
    /// current column.  (If we are lucky, those variables are in registers, but
    /// even if not, they're probably cheaper to access than array elements are.)
    /// 
    /// The fserrors[] array has (#columns + 2) entries; the extra entry at
    /// each end saves us from special-casing the first and last pixels.
    /// Each entry is three values long, one value for each color component.
    /// </summary>
    class my_2pass_cquantizer : jpeg_color_quantizer
    {
        private struct box
        {
            /* The bounds of the box (inclusive); expressed as histogram indexes */
            public int c0min;
            public int c0max;
            public int c1min;
            public int c1max;
            public int c2min;
            public int c2max;
            /* The volume (actually 2-norm) of the box */
            public int volume;
            /* The number of nonzero histogram cells within this box */
            public long colorcount;
        }

        private enum QuantizerType
        {
            prescan_quantizer,
            pass2_fs_dither_quantizer,
            pass2_no_dither_quantizer
        }

        private const int MAXNUMCOLORS = (JpegConstants.MAXJSAMPLE+1); /* maximum size of colormap */

        /* These will do the right thing for either R,G,B or B,G,R color order,
        * but you may not like the results for other color orders.
        */
        private const int HIST_C0_BITS = 5;     /* bits of precision in R/B histogram */
        private const int HIST_C1_BITS = 6;     /* bits of precision in G histogram */
        private const int HIST_C2_BITS = 5;     /* bits of precision in B/R histogram */

        /* Number of elements along histogram axes. */
        private const int HIST_C0_ELEMS = (1<<HIST_C0_BITS);
        private const int HIST_C1_ELEMS = (1<<HIST_C1_BITS);
        private const int HIST_C2_ELEMS = (1<<HIST_C2_BITS);

        /* These are the amounts to shift an input value to get a histogram index. */
        private const int C0_SHIFT = (JpegConstants.BITS_IN_JSAMPLE-HIST_C0_BITS);
        private const int C1_SHIFT = (JpegConstants.BITS_IN_JSAMPLE - HIST_C1_BITS);
        private const int C2_SHIFT = (JpegConstants.BITS_IN_JSAMPLE - HIST_C2_BITS);

        private const int R_SCALE = 2;       /* scale R distances by this much */
        private const int G_SCALE = 3;       /* scale G distances by this much */
        private const int B_SCALE = 1;       /* and B by this much */

        /* log2(histogram cells in update box) for each axis; this can be adjusted */
        private const int BOX_C0_LOG = (HIST_C0_BITS-3);
        private const int BOX_C1_LOG = (HIST_C1_BITS-3);
        private const int BOX_C2_LOG = (HIST_C2_BITS-3);

        private const int BOX_C0_ELEMS = (1<<BOX_C0_LOG); /* # of hist cells in update box */
        private const int BOX_C1_ELEMS = (1<<BOX_C1_LOG);
        private const int BOX_C2_ELEMS = (1<<BOX_C2_LOG);

        private const int BOX_C0_SHIFT = (C0_SHIFT + BOX_C0_LOG);
        private const int BOX_C1_SHIFT = (C1_SHIFT + BOX_C1_LOG);
        private const int BOX_C2_SHIFT = (C2_SHIFT + BOX_C2_LOG);

        private QuantizerType m_quantizer;

        private bool m_useFinishPass1;

        private jpeg_decompress_struct m_cinfo;

        /* Space for the eventually created colormap is stashed here */
        private byte[][] m_sv_colormap;  /* colormap allocated at init time */
        private int m_desired;            /* desired # of colors = size of colormap */

        /* Variables for accumulating image statistics */
        private ushort[][] m_histogram;     /* pointer to the histogram */
        
        private bool m_needs_zeroed;      /* true if next pass must zero histogram */

        /* Variables for Floyd-Steinberg dithering */
        private short[] m_fserrors;      /* accumulated errors */
        private bool m_on_odd_row;        /* flag to remember which row we are on */
        private int[] m_error_limiter;     /* table for clamping the applied error */

        /// <summary>
        /// Module initialization routine for 2-pass color quantization.
        /// </summary>
        public my_2pass_cquantizer(jpeg_decompress_struct cinfo)
        {
            m_cinfo = cinfo;

            /* Make sure jdmaster didn't give me a case I can't handle */
            if (cinfo.m_out_color_components != 3)
                cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);

            /* Allocate the histogram/inverse colormap storage */
            m_histogram = new ushort[HIST_C0_ELEMS][];
            for (int i = 0; i < HIST_C0_ELEMS; i++)
                m_histogram[i] = new ushort[HIST_C1_ELEMS * HIST_C2_ELEMS];

            m_needs_zeroed = true; /* histogram is garbage now */

            /* Allocate storage for the completed colormap, if required.
            * We do this now since it is FAR storage and may affect
            * the memory manager's space calculations.
            */
            if (cinfo.m_enable_2pass_quant)
            {
                /* Make sure color count is acceptable */
                int desired_local = cinfo.m_desired_number_of_colors;

                /* Lower bound on # of colors ... somewhat arbitrary as long as > 0 */
                if (desired_local < 8)
                    cinfo.ERREXIT(J_MESSAGE_CODE.JERR_QUANT_FEW_COLORS, 8);

                /* Make sure colormap indexes can be represented by JSAMPLEs */
                if (desired_local > MAXNUMCOLORS)
                    cinfo.ERREXIT(J_MESSAGE_CODE.JERR_QUANT_MANY_COLORS, MAXNUMCOLORS);

                m_sv_colormap = jpeg_common_struct.AllocJpegSamples(desired_local, 3);
                m_desired = desired_local;
            }

            /* Only F-S dithering or no dithering is supported. */
            /* If user asks for ordered dither, give him F-S. */
            if (cinfo.m_dither_mode != J_DITHER_MODE.JDITHER_NONE)
                cinfo.m_dither_mode = J_DITHER_MODE.JDITHER_FS;

            /* Allocate Floyd-Steinberg workspace if necessary.
            * This isn't really needed until pass 2, but again it is FAR storage.
            * Although we will cope with a later change in dither_mode,
            * we do not promise to honor max_memory_to_use if dither_mode changes.
            */
            if (cinfo.m_dither_mode == J_DITHER_MODE.JDITHER_FS)
            {
                m_fserrors = new short[(cinfo.m_output_width + 2) * 3];

                /* Might as well create the error-limiting table too. */
                init_error_limit();
            }
        }

        /// <summary>
        /// Initialize for each processing pass.
        /// </summary>
        public virtual void start_pass(bool is_pre_scan)
        {
            /* Only F-S dithering or no dithering is supported. */
            /* If user asks for ordered dither, give him F-S. */
            if (m_cinfo.m_dither_mode != J_DITHER_MODE.JDITHER_NONE)
                m_cinfo.m_dither_mode = J_DITHER_MODE.JDITHER_FS;

            if (is_pre_scan)
            {
                /* Set up method pointers */
                m_quantizer = QuantizerType.prescan_quantizer;
                m_useFinishPass1 = true;
                m_needs_zeroed = true; /* Always zero histogram */
            }
            else
            {
                /* Set up method pointers */
                if (m_cinfo.m_dither_mode == J_DITHER_MODE.JDITHER_FS)
                    m_quantizer = QuantizerType.pass2_fs_dither_quantizer;
                else
                    m_quantizer = QuantizerType.pass2_no_dither_quantizer;

                m_useFinishPass1 = false;

                /* Make sure color count is acceptable */
                int i = m_cinfo.m_actual_number_of_colors;
                if (i < 1)
                    m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_QUANT_FEW_COLORS, 1);

                if (i > MAXNUMCOLORS)
                    m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_QUANT_MANY_COLORS, MAXNUMCOLORS);

                if (m_cinfo.m_dither_mode == J_DITHER_MODE.JDITHER_FS)
                {
                    /* Allocate Floyd-Steinberg workspace if we didn't already. */
                    if (m_fserrors == null)
                    {
                        int arraysize = (m_cinfo.m_output_width + 2) * 3;
                        m_fserrors = new short[arraysize];
                    }
                    else
                    {
                        /* Initialize the propagated errors to zero. */
                        Array.Clear(m_fserrors, 0, m_fserrors.Length);
                    }

                    /* Make the error-limit table if we didn't already. */
                    if (m_error_limiter == null)
                        init_error_limit();

                    m_on_odd_row = false;
                }
            }

            /* Zero the histogram or inverse color map, if necessary */
            if (m_needs_zeroed)
            {
                for (int i = 0; i < HIST_C0_ELEMS; i++)
                    Array.Clear(m_histogram[i], 0, m_histogram[i].Length);

                m_needs_zeroed = false;
            }
        }

        public virtual void color_quantize(byte[][] input_buf, int in_row, byte[][] output_buf, int out_row, int num_rows)
        {
            switch (m_quantizer)
            {
                case QuantizerType.prescan_quantizer:
                    prescan_quantize(input_buf, in_row, num_rows);
                    break;
                case QuantizerType.pass2_fs_dither_quantizer:
                    pass2_fs_dither(input_buf, in_row, output_buf, out_row, num_rows);
                    break;
                case QuantizerType.pass2_no_dither_quantizer:
                    pass2_no_dither(input_buf, in_row, output_buf, out_row, num_rows);
                    break;
                default:
                    m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
                    break;
            }
        }

        public virtual void finish_pass()
        {
            if (m_useFinishPass1)
                finish_pass1();
        }

        /// <summary>
        /// Switch to a new external colormap between output passes.
        /// </summary>
        public virtual void new_color_map()
        {
            /* Reset the inverse color map */
            m_needs_zeroed = true;
        }

        /// <summary>
        /// Prescan some rows of pixels.
        /// In this module the prescan simply updates the histogram, which has been
        /// initialized to zeroes by start_pass.
        /// An output_buf parameter is required by the method signature, but no data
        /// is actually output (in fact the buffer controller is probably passing a
        /// null pointer).
        /// </summary>
        private void prescan_quantize(byte[][] input_buf, int in_row, int num_rows)
        {
            for (int row = 0; row < num_rows; row++)
            {
                int inputIndex = 0;
                for (int col = m_cinfo.m_output_width; col > 0; col--)
                {
                    int rowIndex = (int)input_buf[in_row + row][inputIndex] >> C0_SHIFT;
                    int columnIndex = ((int)input_buf[in_row + row][inputIndex + 1] >> C1_SHIFT) * HIST_C2_ELEMS +
                        ((int)input_buf[in_row + row][inputIndex + 2] >> C2_SHIFT);

                    /* increment pixel value, check for overflow and undo increment if so. */
                    m_histogram[rowIndex][columnIndex]++;
                    if (m_histogram[rowIndex][columnIndex] <= 0)
                        m_histogram[rowIndex][columnIndex]--;

                    inputIndex += 3;
                }
            }
        }

        /// <summary>
        /// Map some rows of pixels to the output colormapped representation.
        /// This version performs Floyd-Steinberg dithering
        /// </summary>
        private void pass2_fs_dither(byte[][] input_buf, int in_row, byte[][] output_buf, int out_row, int num_rows)
        {
            byte[] limit = m_cinfo.m_sample_range_limit;
            int limitOffset = m_cinfo.m_sampleRangeLimitOffset;

            for (int row = 0; row < num_rows; row++)
            {
                int inputPixelIndex = 0;
                int outputPixelIndex = 0;
                int errorIndex = 0;
                int dir;            /* +1 or -1 depending on direction */
                int dir3;           /* 3*dir, for advancing inputIndex & errorIndex */
                if (m_on_odd_row)
                {
                    /* work right to left in this row */
                    inputPixelIndex += (m_cinfo.m_output_width - 1) * 3;   /* so point to rightmost pixel */
                    outputPixelIndex += m_cinfo.m_output_width - 1;
                    dir = -1;
                    dir3 = -3;
                    errorIndex = (m_cinfo.m_output_width + 1) * 3; /* => entry after last column */
                    m_on_odd_row = false; /* flip for next time */
                }
                else
                {
                    /* work left to right in this row */
                    dir = 1;
                    dir3 = 3;
                    errorIndex = 0; /* => entry before first real column */
                    m_on_odd_row = true; /* flip for next time */
                }

                /* Preset error values: no error propagated to first pixel from left */
                /* current error or pixel value */
                int cur0 = 0;
                int cur1 = 0;
                int cur2 = 0;
                /* and no error propagated to row below yet */
                /* error for pixel below cur */
                int belowerr0 = 0;
                int belowerr1 = 0;
                int belowerr2 = 0;
                /* error for below/prev col */
                int bpreverr0 = 0;
                int bpreverr1 = 0;
                int bpreverr2 = 0;

                for (int col = m_cinfo.m_output_width; col > 0; col--)
                {
                    /* curN holds the error propagated from the previous pixel on the
                     * current line.  Add the error propagated from the previous line
                     * to form the complete error correction term for this pixel, and
                     * round the error term (which is expressed * 16) to an integer.
                     * RIGHT_SHIFT rounds towards minus infinity, so adding 8 is correct
                     * for either sign of the error value.
                     * Note: errorIndex is for *previous* column's array entry.
                     */
                    cur0 = JpegUtils.RIGHT_SHIFT(cur0 + m_fserrors[errorIndex + dir3] + 8, 4);
                    cur1 = JpegUtils.RIGHT_SHIFT(cur1 + m_fserrors[errorIndex + dir3 + 1] + 8, 4);
                    cur2 = JpegUtils.RIGHT_SHIFT(cur2 + m_fserrors[errorIndex + dir3 + 2] + 8, 4);

                    /* Limit the error using transfer function set by init_error_limit.
                     * See comments with init_error_limit for rationale.
                     */
                    cur0 = m_error_limiter[JpegConstants.MAXJSAMPLE + cur0];
                    cur1 = m_error_limiter[JpegConstants.MAXJSAMPLE + cur1];
                    cur2 = m_error_limiter[JpegConstants.MAXJSAMPLE + cur2];
                    
                    /* Form pixel value + error, and range-limit to 0..MAXJSAMPLE.
                     * The maximum error is +- MAXJSAMPLE (or less with error limiting);
                     * this sets the required size of the range_limit array.
                     */
                    cur0 += input_buf[in_row + row][inputPixelIndex];
                    cur1 += input_buf[in_row + row][inputPixelIndex + 1];
                    cur2 += input_buf[in_row + row][inputPixelIndex + 2];
                    cur0 = limit[limitOffset + cur0];
                    cur1 = limit[limitOffset + cur1];
                    cur2 = limit[limitOffset + cur2];

                    /* Index into the cache with adjusted pixel value */
                    int hRow = cur0 >> C0_SHIFT;
                    int hColumn = (cur1 >> C1_SHIFT) * HIST_C2_ELEMS + (cur2 >> C2_SHIFT);

                    /* If we have not seen this color before, find nearest colormap */
                    /* entry and update the cache */
                    if (m_histogram[hRow][hColumn] == 0)
                        fill_inverse_cmap(cur0 >> C0_SHIFT, cur1 >> C1_SHIFT, cur2 >> C2_SHIFT);

                    /* Now emit the colormap index for this cell */
                    int pixcode = m_histogram[hRow][hColumn] - 1;
                    output_buf[out_row + row][outputPixelIndex] = (byte) pixcode;

                    /* Compute representation error for this pixel */
                    cur0 -= m_cinfo.m_colormap[0][pixcode];
                    cur1 -= m_cinfo.m_colormap[1][pixcode];
                    cur2 -= m_cinfo.m_colormap[2][pixcode];

                    /* Compute error fractions to be propagated to adjacent pixels.
                     * Add these into the running sums, and simultaneously shift the
                     * next-line error sums left by 1 column.
                     */
                    int bnexterr = cur0;    /* Process component 0 */
                    int delta = cur0 * 2;
                    cur0 += delta;      /* form error * 3 */
                    m_fserrors[errorIndex] = (short) (bpreverr0 + cur0);
                    cur0 += delta;      /* form error * 5 */
                    bpreverr0 = belowerr0 + cur0;
                    belowerr0 = bnexterr;
                    cur0 += delta;      /* form error * 7 */
                    bnexterr = cur1;    /* Process component 1 */
                    delta = cur1 * 2;
                    cur1 += delta;      /* form error * 3 */
                    m_fserrors[errorIndex + 1] = (short) (bpreverr1 + cur1);
                    cur1 += delta;      /* form error * 5 */
                    bpreverr1 = belowerr1 + cur1;
                    belowerr1 = bnexterr;
                    cur1 += delta;      /* form error * 7 */
                    bnexterr = cur2;    /* Process component 2 */
                    delta = cur2 * 2;
                    cur2 += delta;      /* form error * 3 */
                    m_fserrors[errorIndex + 2] = (short) (bpreverr2 + cur2);
                    cur2 += delta;      /* form error * 5 */
                    bpreverr2 = belowerr2 + cur2;
                    belowerr2 = bnexterr;
                    cur2 += delta;      /* form error * 7 */

                    /* At this point curN contains the 7/16 error value to be propagated
                     * to the next pixel on the current line, and all the errors for the
                     * next line have been shifted over.  We are therefore ready to move on.
                     */
                    inputPixelIndex += dir3;      /* Advance pixel pointers to next column */
                    outputPixelIndex += dir;
                    errorIndex += dir3;       /* advance errorIndex to current column */
                }

                /* Post-loop cleanup: we must unload the final error values into the
                 * final fserrors[] entry.  Note we need not unload belowerrN because
                 * it is for the dummy column before or after the actual array.
                 */
                m_fserrors[errorIndex] = (short) bpreverr0; /* unload prev errs into array */
                m_fserrors[errorIndex + 1] = (short) bpreverr1;
                m_fserrors[errorIndex + 2] = (short) bpreverr2;
            }
        }

        /// <summary>
        /// Map some rows of pixels to the output colormapped representation.
        /// This version performs no dithering
        /// </summary>
        private void pass2_no_dither(byte[][] input_buf, int in_row, byte[][] output_buf, int out_row, int num_rows)
        {
            for (int row = 0; row < num_rows; row++)
            {
                int inRow = row + in_row;
                int inIndex = 0;
                int outIndex = 0;
                int outRow = out_row + row;
                for (int col = m_cinfo.m_output_width; col > 0; col--)
                {
                    /* get pixel value and index into the cache */
                    int c0 = (int)input_buf[inRow][inIndex] >> C0_SHIFT;
                    inIndex++;

                    int c1 = (int)input_buf[inRow][inIndex] >> C1_SHIFT;
                    inIndex++;

                    int c2 = (int)input_buf[inRow][inIndex] >> C2_SHIFT;
                    inIndex++;

                    int hRow = c0;
                    int hColumn = c1 * HIST_C2_ELEMS + c2;

                    /* If we have not seen this color before, find nearest colormap entry */
                    /* and update the cache */
                    if (m_histogram[hRow][hColumn] == 0)
                        fill_inverse_cmap(c0, c1, c2);

                    /* Now emit the colormap index for this cell */
                    output_buf[outRow][outIndex] = (byte)(m_histogram[hRow][hColumn] - 1);
                    outIndex++;
                }
            }
        }

        /// <summary>
        /// Finish up at the end of each pass.
        /// </summary>
        private void finish_pass1()
        {
            /* Select the representative colors and fill in cinfo.colormap */
            m_cinfo.m_colormap = m_sv_colormap;
            select_colors(m_desired);

            /* Force next pass to zero the color index table */
            m_needs_zeroed = true;
        }

        /// <summary>
        /// Compute representative color for a box, put it in colormap[icolor]
        /// </summary>
        private void compute_color(box[] boxlist, int boxIndex, int icolor)
        {
            /* Current algorithm: mean weighted by pixels (not colors) */
            /* Note it is important to get the rounding correct! */
            long total = 0;
            long c0total = 0;
            long c1total = 0;
            long c2total = 0;
            box curBox = boxlist[boxIndex];
            for (int c0 = curBox.c0min; c0 <= curBox.c0max; c0++)
            {
                for (int c1 = curBox.c1min; c1 <= curBox.c1max; c1++)
                {
                    int histogramIndex = c1 * HIST_C2_ELEMS + curBox.c2min;
                    for (int c2 = curBox.c2min; c2 <= curBox.c2max; c2++)
                    {
                        long count = m_histogram[c0][histogramIndex];
                        histogramIndex++;

                        if (count != 0)
                        {
                            total += count;
                            c0total += ((c0 << C0_SHIFT) + ((1 << C0_SHIFT) >> 1)) * count;
                            c1total += ((c1 << C1_SHIFT) + ((1 << C1_SHIFT) >> 1)) * count;
                            c2total += ((c2 << C2_SHIFT) + ((1 << C2_SHIFT) >> 1)) * count;
                        }
                    }
                }
            }

            m_cinfo.m_colormap[0][icolor] = (byte)((c0total + (total >> 1)) / total);
            m_cinfo.m_colormap[1][icolor] = (byte)((c1total + (total >> 1)) / total);
            m_cinfo.m_colormap[2][icolor] = (byte)((c2total + (total >> 1)) / total);
        }

        /// <summary>
        /// Master routine for color selection
        /// </summary>
        private void select_colors(int desired_colors)
        {
            /* Allocate workspace for box list */
            box[] boxlist = new box[desired_colors];

            /* Initialize one box containing whole space */
            int numboxes = 1;
            boxlist[0].c0min = 0;
            boxlist[0].c0max = JpegConstants.MAXJSAMPLE >> C0_SHIFT;
            boxlist[0].c1min = 0;
            boxlist[0].c1max = JpegConstants.MAXJSAMPLE >> C1_SHIFT;
            boxlist[0].c2min = 0;
            boxlist[0].c2max = JpegConstants.MAXJSAMPLE >> C2_SHIFT;

            /* Shrink it to actually-used volume and set its statistics */
            update_box(boxlist, 0);

            /* Perform median-cut to produce final box list */
            numboxes = median_cut(boxlist, numboxes, desired_colors);

            /* Compute the representative color for each box, fill colormap */
            for (int i = 0; i < numboxes; i++)
                compute_color(boxlist, i, i);

            m_cinfo.m_actual_number_of_colors = numboxes;
            m_cinfo.TRACEMS(1, J_MESSAGE_CODE.JTRC_QUANT_SELECTED, numboxes);
        }

        /// <summary>
        /// Repeatedly select and split the largest box until we have enough boxes
        /// </summary>
        private int median_cut(box[] boxlist, int numboxes, int desired_colors)
        {
            while (numboxes < desired_colors)
            {
                /* Select box to split.
                 * Current algorithm: by population for first half, then by volume.
                 */
                int foundIndex;
                if (numboxes * 2 <= desired_colors)
                    foundIndex = find_biggest_color_pop(boxlist, numboxes);
                else
                    foundIndex = find_biggest_volume(boxlist, numboxes);

                if (foundIndex == -1)     /* no splittable boxes left! */
                    break;

                /* Copy the color bounds to the new box. */
                boxlist[numboxes].c0max = boxlist[foundIndex].c0max;
                boxlist[numboxes].c1max = boxlist[foundIndex].c1max;
                boxlist[numboxes].c2max = boxlist[foundIndex].c2max;
                boxlist[numboxes].c0min = boxlist[foundIndex].c0min;
                boxlist[numboxes].c1min = boxlist[foundIndex].c1min;
                boxlist[numboxes].c2min = boxlist[foundIndex].c2min;

                /* Choose which axis to split the box on.
                 * Current algorithm: longest scaled axis.
                 * See notes in update_box about scaling distances.
                 */
                int c0 = ((boxlist[foundIndex].c0max - boxlist[foundIndex].c0min) << C0_SHIFT) * R_SCALE;
                int c1 = ((boxlist[foundIndex].c1max - boxlist[foundIndex].c1min) << C1_SHIFT) * G_SCALE;
                int c2 = ((boxlist[foundIndex].c2max - boxlist[foundIndex].c2min) << C2_SHIFT) * B_SCALE;

                /* We want to break any ties in favor of green, then red, blue last.
                 * This code does the right thing for R,G,B or B,G,R color orders only.
                 */
                int cmax = c1;
                int n = 1;

                if (c0 > cmax)
                {
                    cmax = c0;
                    n = 0;
                }

                if (c2 > cmax)
                {
                    n = 2;
                }

                /* Choose split point along selected axis, and update box bounds.
                 * Current algorithm: split at halfway point.
                 * (Since the box has been shrunk to minimum volume,
                 * any split will produce two nonempty subboxes.)
                 * Note that lb value is max for lower box, so must be < old max.
                 */
                int lb;
                switch (n)
                {
                    case 0:
                        lb = (boxlist[foundIndex].c0max + boxlist[foundIndex].c0min) / 2;
                        boxlist[foundIndex].c0max = lb;
                        boxlist[numboxes].c0min = lb + 1;
                        break;
                    case 1:
                        lb = (boxlist[foundIndex].c1max + boxlist[foundIndex].c1min) / 2;
                        boxlist[foundIndex].c1max = lb;
                        boxlist[numboxes].c1min = lb + 1;
                        break;
                    case 2:
                        lb = (boxlist[foundIndex].c2max + boxlist[foundIndex].c2min) / 2;
                        boxlist[foundIndex].c2max = lb;
                        boxlist[numboxes].c2min = lb + 1;
                        break;
                }

                /* Update stats for boxes */
                update_box(boxlist, foundIndex);
                update_box(boxlist, numboxes);
                numboxes++;
            }

            return numboxes;
        }

        /*
         * Next we have the really interesting routines: selection of a colormap
         * given the completed histogram.
         * These routines work with a list of "boxes", each representing a rectangular
         * subset of the input color space (to histogram precision).
         */

        /// <summary>
        /// Find the splittable box with the largest color population
        /// Returns null if no splittable boxes remain
        /// </summary>
        private static int find_biggest_color_pop(box[] boxlist, int numboxes)
        {
            long maxc = 0;
            int which = -1;
            for (int i = 0; i < numboxes; i++)
            {
                if (boxlist[i].colorcount > maxc && boxlist[i].volume > 0)
                {
                    which = i;
                    maxc = boxlist[i].colorcount;
                }
            }

            return which;
        }

        /// <summary>
        /// Find the splittable box with the largest (scaled) volume
        /// Returns null if no splittable boxes remain
        /// </summary>
        private static int find_biggest_volume(box[] boxlist, int numboxes)
        {
            int maxv = 0;
            int which = -1;
            for (int i = 0; i < numboxes; i++)
            {
                if (boxlist[i].volume > maxv)
                {
                    which = i;
                    maxv = boxlist[i].volume;
                }
            }

            return which;
        }

        /// <summary>
        /// Shrink the min/max bounds of a box to enclose only nonzero elements,
        /// and recompute its volume and population
        /// </summary>
        private void update_box(box[] boxlist, int boxIndex)
        {
            box curBox = boxlist[boxIndex];
            bool have_c0min = false;

            if (curBox.c0max > curBox.c0min)
            {
                for (int c0 = curBox.c0min; c0 <= curBox.c0max; c0++)
                {
                    for (int c1 = curBox.c1min; c1 <= curBox.c1max; c1++)
                    {
                        int histogramIndex = c1 * HIST_C2_ELEMS + curBox.c2min;
                        for (int c2 = curBox.c2min; c2 <= curBox.c2max; c2++)
                        {
                            if (m_histogram[c0][histogramIndex++] != 0)
                            {
                                curBox.c0min = c0;
                                have_c0min = true;
                                break;
                            }
                        }

                        if (have_c0min)
                            break;
                    }

                    if (have_c0min)
                        break;
                }
            }

            bool have_c0max = false;
            if (curBox.c0max > curBox.c0min)
            {
                for (int c0 = curBox.c0max; c0 >= curBox.c0min; c0--)
                {
                    for (int c1 = curBox.c1min; c1 <= curBox.c1max; c1++)
                    {
                        int histogramIndex = c1 * HIST_C2_ELEMS + curBox.c2min;
                        for (int c2 = curBox.c2min; c2 <= curBox.c2max; c2++)
                        {
                            if (m_histogram[c0][histogramIndex++] != 0)
                            {
                                curBox.c0max = c0;
                                have_c0max = true;
                                break;
                            }
                        }

                        if (have_c0max)
                            break;
                    }

                    if (have_c0max)
                        break;
                }
            }

            bool have_c1min = false;
            if (curBox.c1max > curBox.c1min)
            {
                for (int c1 = curBox.c1min; c1 <= curBox.c1max; c1++)
                {
                    for (int c0 = curBox.c0min; c0 <= curBox.c0max; c0++)
                    {
                        int histogramIndex = c1 * HIST_C2_ELEMS + curBox.c2min;
                        for (int c2 = curBox.c2min; c2 <= curBox.c2max; c2++)
                        {
                            if (m_histogram[c0][histogramIndex++] != 0)
                            {
                                curBox.c1min = c1;
                                have_c1min = true;
                                break;
                            }
                        }

                        if (have_c1min)
                            break;
                    }

                    if (have_c1min)
                        break;
                }
            }

            bool have_c1max = false;
            if (curBox.c1max > curBox.c1min)
            {
                for (int c1 = curBox.c1max; c1 >= curBox.c1min; c1--)
                {
                    for (int c0 = curBox.c0min; c0 <= curBox.c0max; c0++)
                    {
                        int histogramIndex = c1 * HIST_C2_ELEMS + curBox.c2min;
                        for (int c2 = curBox.c2min; c2 <= curBox.c2max; c2++)
                        {
                            if (m_histogram[c0][histogramIndex++] != 0)
                            {
                                curBox.c1max = c1;
                                have_c1max = true;
                                break;
                            }
                        }

                        if (have_c1max)
                            break;
                    }

                    if (have_c1max)
                        break;
                }
            }

            bool have_c2min = false;
            if (curBox.c2max > curBox.c2min)
            {
                for (int c2 = curBox.c2min; c2 <= curBox.c2max; c2++)
                {
                    for (int c0 = curBox.c0min; c0 <= curBox.c0max; c0++)
                    {
                        int histogramIndex = curBox.c1min * HIST_C2_ELEMS + c2;
                        for (int c1 = curBox.c1min; c1 <= curBox.c1max; c1++, histogramIndex += HIST_C2_ELEMS)
                        {
                            if (m_histogram[c0][histogramIndex] != 0)
                            {
                                curBox.c2min = c2;
                                have_c2min = true;
                                break;
                            }
                        }

                        if (have_c2min)
                            break;
                    }

                    if (have_c2min)
                        break;
                }
            }

            bool have_c2max = false;
            if (curBox.c2max > curBox.c2min)
            {
                for (int c2 = curBox.c2max; c2 >= curBox.c2min; c2--)
                {
                    for (int c0 = curBox.c0min; c0 <= curBox.c0max; c0++)
                    {
                        int histogramIndex = curBox.c1min * HIST_C2_ELEMS + c2;
                        for (int c1 = curBox.c1min; c1 <= curBox.c1max; c1++, histogramIndex += HIST_C2_ELEMS)
                        {
                            if (m_histogram[c0][histogramIndex] != 0)
                            {
                                curBox.c2max = c2;
                                have_c2max = true;
                                break;
                            }
                        }

                        if (have_c2max)
                            break;
                    }

                    if (have_c2max)
                        break;
                }
            }

            /* Update box volume.
             * We use 2-norm rather than real volume here; this biases the method
             * against making long narrow boxes, and it has the side benefit that
             * a box is splittable iff norm > 0.
             * Since the differences are expressed in histogram-cell units,
             * we have to shift back to byte units to get consistent distances;
             * after which, we scale according to the selected distance scale factors.
             */
            int dist0 = ((curBox.c0max - curBox.c0min) << C0_SHIFT) * R_SCALE;
            int dist1 = ((curBox.c1max - curBox.c1min) << C1_SHIFT) * G_SCALE;
            int dist2 = ((curBox.c2max - curBox.c2min) << C2_SHIFT) * B_SCALE;
            curBox.volume = dist0 * dist0 + dist1 * dist1 + dist2 * dist2;

            /* Now scan remaining volume of box and compute population */
            long ccount = 0;
            for (int c0 = curBox.c0min; c0 <= curBox.c0max; c0++)
            {
                for (int c1 = curBox.c1min; c1 <= curBox.c1max; c1++)
                {
                    int histogramIndex = c1 * HIST_C2_ELEMS + curBox.c2min;
                    for (int c2 = curBox.c2min; c2 <= curBox.c2max; c2++, histogramIndex++)
                    {
                        if (m_histogram[c0][histogramIndex] != 0)
                            ccount++;
                    }
                }
            }

            curBox.colorcount = ccount;
            boxlist[boxIndex] = curBox;
        }

        /// <summary>
        /// Initialize the error-limiting transfer function (lookup table).
        /// The raw F-S error computation can potentially compute error values of up to
        /// +- MAXJSAMPLE.  But we want the maximum correction applied to a pixel to be
        /// much less, otherwise obviously wrong pixels will be created.  (Typical
        /// effects include weird fringes at color-area boundaries, isolated bright
        /// pixels in a dark area, etc.)  The standard advice for avoiding this problem
        /// is to ensure that the "corners" of the color cube are allocated as output
        /// colors; then repeated errors in the same direction cannot cause cascading
        /// error buildup.  However, that only prevents the error from getting
        /// completely out of hand; Aaron Giles reports that error limiting improves
        /// the results even with corner colors allocated.
        /// A simple clamping of the error values to about +- MAXJSAMPLE/8 works pretty
        /// well, but the smoother transfer function used below is even better.  Thanks
        /// to Aaron Giles for this idea.
        /// </summary>
        private void init_error_limit()
        {
            m_error_limiter = new int [JpegConstants.MAXJSAMPLE * 2 + 1];
            int tableOffset = JpegConstants.MAXJSAMPLE;

            const int STEPSIZE = ((JpegConstants.MAXJSAMPLE + 1) / 16);

            /* Map errors 1:1 up to +- MAXJSAMPLE/16 */
            int output = 0;
            int input = 0;
            for (; input < STEPSIZE; input++, output++)
            {
                m_error_limiter[tableOffset + input] = output; 
                m_error_limiter[tableOffset - input] = -output;
            }

            /* Map errors 1:2 up to +- 3*MAXJSAMPLE/16 */
            for (; input < STEPSIZE*3; input++)
            {
                m_error_limiter[tableOffset + input] = output; 
                m_error_limiter[tableOffset - input] = -output;
                output += (input & 1) != 0 ? 1 : 0;
            }

            /* Clamp the rest to final output value (which is (MAXJSAMPLE+1)/8) */
            for (; input <= JpegConstants.MAXJSAMPLE; input++)
            {
                m_error_limiter[tableOffset + input] = output; 
                m_error_limiter[tableOffset - input] = -output;
            }
        }

        /*
         * These routines are concerned with the time-critical task of mapping input
         * colors to the nearest color in the selected colormap.
         *
         * We re-use the histogram space as an "inverse color map", essentially a
         * cache for the results of nearest-color searches.  All colors within a
         * histogram cell will be mapped to the same colormap entry, namely the one
         * closest to the cell's center.  This may not be quite the closest entry to
         * the actual input color, but it's almost as good.  A zero in the cache
         * indicates we haven't found the nearest color for that cell yet; the array
         * is cleared to zeroes before starting the mapping pass.  When we find the
         * nearest color for a cell, its colormap index plus one is recorded in the
         * cache for future use.  The pass2 scanning routines call fill_inverse_cmap
         * when they need to use an unfilled entry in the cache.
         *
         * Our method of efficiently finding nearest colors is based on the "locally
         * sorted search" idea described by Heckbert and on the incremental distance
         * calculation described by Spencer W. Thomas in chapter III.1 of Graphics
         * Gems II (James Arvo, ed.  Academic Press, 1991).  Thomas points out that
         * the distances from a given colormap entry to each cell of the histogram can
         * be computed quickly using an incremental method: the differences between
         * distances to adjacent cells themselves differ by a constant.  This allows a
         * fairly fast implementation of the "brute force" approach of computing the
         * distance from every colormap entry to every histogram cell.  Unfortunately,
         * it needs a work array to hold the best-distance-so-far for each histogram
         * cell (because the inner loop has to be over cells, not colormap entries).
         * The work array elements have to be ints, so the work array would need
         * 256Kb at our recommended precision.  This is not feasible in DOS machines.
         *
         * To get around these problems, we apply Thomas' method to compute the
         * nearest colors for only the cells within a small subbox of the histogram.
         * The work array need be only as big as the subbox, so the memory usage
         * problem is solved.  Furthermore, we need not fill subboxes that are never
         * referenced in pass2; many images use only part of the color gamut, so a
         * fair amount of work is saved.  An additional advantage of this
         * approach is that we can apply Heckbert's locality criterion to quickly
         * eliminate colormap entries that are far away from the subbox; typically
         * three-fourths of the colormap entries are rejected by Heckbert's criterion,
         * and we need not compute their distances to individual cells in the subbox.
         * The speed of this approach is heavily influenced by the subbox size: too
         * small means too much overhead, too big loses because Heckbert's criterion
         * can't eliminate as many colormap entries.  Empirically the best subbox
         * size seems to be about 1/512th of the histogram (1/8th in each direction).
         *
         * Thomas' article also describes a refined method which is asymptotically
         * faster than the brute-force method, but it is also far more complex and
         * cannot efficiently be applied to small subboxes.  It is therefore not
         * useful for programs intended to be portable to DOS machines.  On machines
         * with plenty of memory, filling the whole histogram in one shot with Thomas'
         * refined method might be faster than the present code --- but then again,
         * it might not be any faster, and it's certainly more complicated.
         */

        /*
         * The next three routines implement inverse colormap filling.  They could
         * all be folded into one big routine, but splitting them up this way saves
         * some stack space (the mindist[] and bestdist[] arrays need not coexist)
         * and may allow some compilers to produce better code by registerizing more
         * inner-loop variables.
         */

        /// <summary>
        /// Locate the colormap entries close enough to an update box to be candidates
        /// for the nearest entry to some cell(s) in the update box.  The update box
        /// is specified by the center coordinates of its first cell.  The number of
        /// candidate colormap entries is returned, and their colormap indexes are
        /// placed in colorlist[].
        /// This routine uses Heckbert's "locally sorted search" criterion to select
        /// the colors that need further consideration.
        /// </summary>
        private int find_nearby_colors(int minc0, int minc1, int minc2, byte[] colorlist)
        {
            /* Compute true coordinates of update box's upper corner and center.
             * Actually we compute the coordinates of the center of the upper-corner
             * histogram cell, which are the upper bounds of the volume we care about.
             * Note that since ">>" rounds down, the "center" values may be closer to
             * min than to max; hence comparisons to them must be "<=", not "<".
             */
            int maxc0 = minc0 + ((1 << BOX_C0_SHIFT) - (1 << C0_SHIFT));
            int centerc0 = (minc0 + maxc0) >> 1;

            int maxc1 = minc1 + ((1 << BOX_C1_SHIFT) - (1 << C1_SHIFT));
            int centerc1 = (minc1 + maxc1) >> 1;

            int maxc2 = minc2 + ((1 << BOX_C2_SHIFT) - (1 << C2_SHIFT));
            int centerc2 = (minc2 + maxc2) >> 1;

            /* For each color in colormap, find:
             *  1. its minimum squared-distance to any point in the update box
             *     (zero if color is within update box);
             *  2. its maximum squared-distance to any point in the update box.
             * Both of these can be found by considering only the corners of the box.
             * We save the minimum distance for each color in mindist[];
             * only the smallest maximum distance is of interest.
             */
            int minmaxdist = 0x7FFFFFFF;
            int[] mindist = new int[MAXNUMCOLORS];    /* min distance to colormap entry i */

            for (int i = 0; i < m_cinfo.m_actual_number_of_colors; i++)
            {
                /* We compute the squared-c0-distance term, then add in the other two. */
                int x = m_cinfo.m_colormap[0][i];
                int min_dist;
                int max_dist;

                if (x < minc0)
                {
                    int tdist = (x - minc0) * R_SCALE;
                    min_dist = tdist * tdist;
                    tdist = (x - maxc0) * R_SCALE;
                    max_dist = tdist * tdist;
                }
                else if (x > maxc0)
                {
                    int tdist = (x - maxc0) * R_SCALE;
                    min_dist = tdist * tdist;
                    tdist = (x - minc0) * R_SCALE;
                    max_dist = tdist * tdist;
                }
                else
                {
                    /* within cell range so no contribution to min_dist */
                    min_dist = 0;
                    if (x <= centerc0)
                    {
                        int tdist = (x - maxc0) * R_SCALE;
                        max_dist = tdist * tdist;
                    }
                    else
                    {
                        int tdist = (x - minc0) * R_SCALE;
                        max_dist = tdist * tdist;
                    }
                }

                x = m_cinfo.m_colormap[1][i];
                if (x < minc1)
                {
                    int tdist = (x - minc1) * G_SCALE;
                    min_dist += tdist * tdist;
                    tdist = (x - maxc1) * G_SCALE;
                    max_dist += tdist * tdist;
                }
                else if (x > maxc1)
                {
                    int tdist = (x - maxc1) * G_SCALE;
                    min_dist += tdist * tdist;
                    tdist = (x - minc1) * G_SCALE;
                    max_dist += tdist * tdist;
                }
                else
                {
                    /* within cell range so no contribution to min_dist */
                    if (x <= centerc1)
                    {
                        int tdist = (x - maxc1) * G_SCALE;
                        max_dist += tdist * tdist;
                    }
                    else
                    {
                        int tdist = (x - minc1) * G_SCALE;
                        max_dist += tdist * tdist;
                    }
                }

                x = m_cinfo.m_colormap[2][i];
                if (x < minc2)
                {
                    int tdist = (x - minc2) * B_SCALE;
                    min_dist += tdist * tdist;
                    tdist = (x - maxc2) * B_SCALE;
                    max_dist += tdist * tdist;
                }
                else if (x > maxc2)
                {
                    int tdist = (x - maxc2) * B_SCALE;
                    min_dist += tdist * tdist;
                    tdist = (x - minc2) * B_SCALE;
                    max_dist += tdist * tdist;
                }
                else
                {
                    /* within cell range so no contribution to min_dist */
                    if (x <= centerc2)
                    {
                        int tdist = (x - maxc2) * B_SCALE;
                        max_dist += tdist * tdist;
                    }
                    else
                    {
                        int tdist = (x - minc2) * B_SCALE;
                        max_dist += tdist * tdist;
                    }
                }

                mindist[i] = min_dist;  /* save away the results */
                if (max_dist < minmaxdist)
                    minmaxdist = max_dist;
            }

            /* Now we know that no cell in the update box is more than minmaxdist
             * away from some colormap entry.  Therefore, only colors that are
             * within minmaxdist of some part of the box need be considered.
             */
            int ncolors = 0;
            for (int i = 0; i < m_cinfo.m_actual_number_of_colors; i++)
            {
                if (mindist[i] <= minmaxdist)
                    colorlist[ncolors++] = (byte) i;
            }

            return ncolors;
        }

        /// <summary>
        /// Find the closest colormap entry for each cell in the update box,
        /// given the list of candidate colors prepared by find_nearby_colors.
        /// Return the indexes of the closest entries in the bestcolor[] array.
        /// This routine uses Thomas' incremental distance calculation method to
        /// find the distance from a colormap entry to successive cells in the box.
        /// </summary>
        private void find_best_colors(int minc0, int minc1, int minc2, int numcolors, byte[] colorlist, byte[] bestcolor)
        {
            /* Nominal steps between cell centers ("x" in Thomas article) */
            const int STEP_C0 = ((1 << C0_SHIFT) * R_SCALE);
            const int STEP_C1 = ((1 << C1_SHIFT) * G_SCALE);
            const int STEP_C2 = ((1 << C2_SHIFT) * B_SCALE);

            /* This array holds the distance to the nearest-so-far color for each cell */
            int[] bestdist = new int[BOX_C0_ELEMS * BOX_C1_ELEMS * BOX_C2_ELEMS];

            /* Initialize best-distance for each cell of the update box */
            int bestIndex = 0;
            for (int i = BOX_C0_ELEMS * BOX_C1_ELEMS * BOX_C2_ELEMS - 1; i >= 0; i--)
            {
                bestdist[bestIndex] = 0x7FFFFFFF;
                bestIndex++;
            }

            /* For each color selected by find_nearby_colors,
             * compute its distance to the center of each cell in the box.
             * If that's less than best-so-far, update best distance and color number.
             */
            for (int i = 0; i < numcolors; i++)
            {
                int icolor = colorlist[i];
                
                /* Compute (square of) distance from minc0/c1/c2 to this color */
                int inc0 = (minc0 - m_cinfo.m_colormap[0][icolor]) * R_SCALE;
                int dist0 = inc0 * inc0;

                int inc1 = (minc1 - m_cinfo.m_colormap[1][icolor]) * G_SCALE;
                dist0 += inc1 * inc1;
                
                int inc2 = (minc2 - m_cinfo.m_colormap[2][icolor]) * B_SCALE;
                dist0 += inc2 * inc2;

                /* Form the initial difference increments */
                inc0 = inc0 * (2 * STEP_C0) + STEP_C0 * STEP_C0;
                inc1 = inc1 * (2 * STEP_C1) + STEP_C1 * STEP_C1;
                inc2 = inc2 * (2 * STEP_C2) + STEP_C2 * STEP_C2;
                
                /* Now loop over all cells in box, updating distance per Thomas method */
                bestIndex = 0;
                int colorIndex = 0;
                int xx0 = inc0;
                for (int ic0 = BOX_C0_ELEMS - 1; ic0 >= 0; ic0--)
                {
                    int dist1 = dist0;
                    int xx1 = inc1;
                    for (int ic1 = BOX_C1_ELEMS - 1; ic1 >= 0; ic1--)
                    {
                        int dist2 = dist1;
                        int xx2 = inc2;
                        for (int ic2 = BOX_C2_ELEMS - 1; ic2 >= 0; ic2--)
                        {
                            if (dist2 < bestdist[bestIndex])
                            {
                                bestdist[bestIndex] = dist2;
                                bestcolor[colorIndex] = (byte) icolor;
                            }
                            
                            dist2 += xx2;
                            xx2 += 2 * STEP_C2 * STEP_C2;
                            bestIndex++;
                            colorIndex++;
                        }

                        dist1 += xx1;
                        xx1 += 2 * STEP_C1 * STEP_C1;
                    }

                    dist0 += xx0;
                    xx0 += 2 * STEP_C0 * STEP_C0;
                }
            }
        }

        /// <summary>
        /// Fill the inverse-colormap entries in the update box that contains
        /// histogram cell c0/c1/c2.  (Only that one cell MUST be filled, but
        /// we can fill as many others as we wish.)
        /// </summary>
        private void fill_inverse_cmap(int c0, int c1, int c2)
        {
            /* Convert cell coordinates to update box ID */
            c0 >>= BOX_C0_LOG;
            c1 >>= BOX_C1_LOG;
            c2 >>= BOX_C2_LOG;

            /* Compute true coordinates of update box's origin corner.
             * Actually we compute the coordinates of the center of the corner
             * histogram cell, which are the lower bounds of the volume we care about.
             */
            int minc0 = (c0 << BOX_C0_SHIFT) + ((1 << C0_SHIFT) >> 1);
            int minc1 = (c1 << BOX_C1_SHIFT) + ((1 << C1_SHIFT) >> 1);
            int minc2 = (c2 << BOX_C2_SHIFT) + ((1 << C2_SHIFT) >> 1);

            /* Determine which colormap entries are close enough to be candidates
             * for the nearest entry to some cell in the update box.
             */
            /* This array lists the candidate colormap indexes. */
            byte[] colorlist = new byte[MAXNUMCOLORS];
            int numcolors = find_nearby_colors(minc0, minc1, minc2, colorlist);

            /* Determine the actually nearest colors. */
            /* This array holds the actually closest colormap index for each cell. */
            byte[] bestcolor = new byte[BOX_C0_ELEMS * BOX_C1_ELEMS * BOX_C2_ELEMS];
            find_best_colors(minc0, minc1, minc2, numcolors, colorlist, bestcolor);

            /* Save the best color numbers (plus 1) in the main cache array */
            c0 <<= BOX_C0_LOG;      /* convert ID back to base cell indexes */
            c1 <<= BOX_C1_LOG;
            c2 <<= BOX_C2_LOG;
            int bestcolorIndex = 0;
            for (int ic0 = 0; ic0 < BOX_C0_ELEMS; ic0++)
            {
                for (int ic1 = 0; ic1 < BOX_C1_ELEMS; ic1++)
                {
                    int histogramIndex = (c1 + ic1) * HIST_C2_ELEMS + c2;
                    for (int ic2 = 0; ic2 < BOX_C2_ELEMS; ic2++)
                    {
                        m_histogram[c0 + ic0][histogramIndex] = (ushort) ((int)bestcolor[bestcolorIndex] + 1);
                        histogramIndex++;
                        bestcolorIndex++;
                    }
                }
            }
        }
    }
}
