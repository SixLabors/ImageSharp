/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * Copyright (C) 1994-1996, Thomas G. Lane.
 * This file is part of the Independent JPEG Group's software.
 * For conditions of distribution and use, see the accompanying README file.
 *
 */

/*
 * This file contains 1-pass color quantization (color mapping) routines.
 * These routines provide mapping to a fixed color map using equally spaced
 * color values.  Optional Floyd-Steinberg or ordered dithering is available.
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace BitMiracle.LibJpeg.Classic.Internal
{
    /// <summary>
    /// The main purpose of 1-pass quantization is to provide a fast, if not very
    /// high quality, colormapped output capability.  A 2-pass quantizer usually
    /// gives better visual quality; however, for quantized grayscale output this
    /// quantizer is perfectly adequate.  Dithering is highly recommended with this
    /// quantizer, though you can turn it off if you really want to.
    /// 
    /// In 1-pass quantization the colormap must be chosen in advance of seeing the
    /// image.  We use a map consisting of all combinations of Ncolors[i] color
    /// values for the i'th component.  The Ncolors[] values are chosen so that
    /// their product, the total number of colors, is no more than that requested.
    /// (In most cases, the product will be somewhat less.)
    /// 
    /// Since the colormap is orthogonal, the representative value for each color
    /// component can be determined without considering the other components;
    /// then these indexes can be combined into a colormap index by a standard
    /// N-dimensional-array-subscript calculation.  Most of the arithmetic involved
    /// can be precalculated and stored in the lookup table colorindex[].
    /// colorindex[i][j] maps pixel value j in component i to the nearest
    /// representative value (grid plane) for that component; this index is
    /// multiplied by the array stride for component i, so that the
    /// index of the colormap entry closest to a given pixel value is just
    ///     sum( colorindex[component-number][pixel-component-value] )
    /// Aside from being fast, this scheme allows for variable spacing between
    /// representative values with no additional lookup cost.
    /// 
    /// If gamma correction has been applied in color conversion, it might be wise
    /// to adjust the color grid spacing so that the representative colors are
    /// equidistant in linear space.  At this writing, gamma correction is not
    /// implemented, so nothing is done here.
    /// 
    /// 
    /// Declarations for Floyd-Steinberg dithering.
    /// 
    /// Errors are accumulated into the array fserrors[], at a resolution of
    /// 1/16th of a pixel count.  The error at a given pixel is propagated
    /// to its not-yet-processed neighbors using the standard F-S fractions,
    ///     ...	(here)	7/16
    ///    3/16	5/16	1/16
    /// We work left-to-right on even rows, right-to-left on odd rows.
    /// 
    /// We can get away with a single array (holding one row's worth of errors)
    /// by using it to store the current row's errors at pixel columns not yet
    /// processed, but the next row's errors at columns already processed.  We
    /// need only a few extra variables to hold the errors immediately around the
    /// current column.  (If we are lucky, those variables are in registers, but
    /// even if not, they're probably cheaper to access than array elements are.)
    /// 
    /// The fserrors[] array is indexed [component#][position].
    /// We provide (#columns + 2) entries per component; the extra entry at each
    /// end saves us from special-casing the first and last pixels.
    /// 
    /// 
    /// Declarations for ordered dithering.
    /// 
    /// We use a standard 16x16 ordered dither array.  The basic concept of ordered
    /// dithering is described in many references, for instance Dale Schumacher's
    /// chapter II.2 of Graphics Gems II (James Arvo, ed. Academic Press, 1991).
    /// In place of Schumacher's comparisons against a "threshold" value, we add a
    /// "dither" value to the input pixel and then round the result to the nearest
    /// output value.  The dither value is equivalent to (0.5 - threshold) times
    /// the distance between output values.  For ordered dithering, we assume that
    /// the output colors are equally spaced; if not, results will probably be
    /// worse, since the dither may be too much or too little at a given point.
    /// 
    /// The normal calculation would be to form pixel value + dither, range-limit
    /// this to 0..MAXJSAMPLE, and then index into the colorindex table as usual.
    /// We can skip the separate range-limiting step by extending the colorindex
    /// table in both directions.
    /// </summary>
    class my_1pass_cquantizer : jpeg_color_quantizer
    {
        private enum QuantizerType
        {
            color_quantizer3,
            color_quantizer,
            quantize3_ord_dither_quantizer,
            quantize_ord_dither_quantizer,
            quantize_fs_dither_quantizer
        }

        private static int[] RGB_order = { JpegConstants.RGB_GREEN, JpegConstants.RGB_RED, JpegConstants.RGB_BLUE };
        private const int MAX_Q_COMPS = 4; /* max components I can handle */
    
        private const int ODITHER_SIZE = 16; /* dimension of dither matrix */

        /* NB: if ODITHER_SIZE is not a power of 2, ODITHER_MASK uses will break */
        private const int ODITHER_CELLS = (ODITHER_SIZE * ODITHER_SIZE); /* # cells in matrix */
        private const int ODITHER_MASK = (ODITHER_SIZE-1); /* mask for wrapping around counters */

        /* Bayer's order-4 dither array.  Generated by the code given in
        * Stephen Hawley's article "Ordered Dithering" in Graphics Gems I.
        * The values in this array must range from 0 to ODITHER_CELLS-1.
        */
        private static byte[][] base_dither_matrix = new byte[][] 
        {
            new byte[] {   0,192, 48,240, 12,204, 60,252,  3,195, 51,243, 15,207, 63,255 },
            new byte[] { 128, 64,176,112,140, 76,188,124,131, 67,179,115,143, 79,191,127 },
            new byte[] {  32,224, 16,208, 44,236, 28,220, 35,227, 19,211, 47,239, 31,223 },
            new byte[] { 160, 96,144, 80,172,108,156, 92,163, 99,147, 83,175,111,159, 95 },
            new byte[] {   8,200, 56,248,  4,196, 52,244, 11,203, 59,251,  7,199, 55,247 },
            new byte[] { 136, 72,184,120,132, 68,180,116,139, 75,187,123,135, 71,183,119 },
            new byte[] {  40,232, 24,216, 36,228, 20,212, 43,235, 27,219, 39,231, 23,215 },
            new byte[] { 168,104,152, 88,164,100,148, 84,171,107,155, 91,167,103,151, 87 },
            new byte[] {   2,194, 50,242, 14,206, 62,254,  1,193, 49,241, 13,205, 61,253 },
            new byte[] { 130, 66,178,114,142, 78,190,126,129, 65,177,113,141, 77,189,125 },
            new byte[] {  34,226, 18,210, 46,238, 30,222, 33,225, 17,209, 45,237, 29,221 },
            new byte[] { 162, 98,146, 82,174,110,158, 94,161, 97,145, 81,173,109,157, 93 },
            new byte[] {  10,202, 58,250,  6,198, 54,246,  9,201, 57,249,  5,197, 53,245 },
            new byte[] { 138, 74,186,122,134, 70,182,118,137, 73,185,121,133, 69,181,117 },
            new byte[] {  42,234, 26,218, 38,230, 22,214, 41,233, 25,217, 37,229, 21,213 },
            new byte[] { 170,106,154, 90,166,102,150, 86,169,105,153, 89,165,101,149, 85 }
        };

        private QuantizerType m_quantizer;

        private jpeg_decompress_struct m_cinfo;

        /* Initially allocated colormap is saved here */
        private byte[][] m_sv_colormap;	/* The color map as a 2-D pixel array */
        private int m_sv_actual;		/* number of entries in use */

        private byte[][] m_colorindex;	/* Precomputed mapping for speed */
        private int[] m_colorindexOffset;

        /* colorindex[i][j] = index of color closest to pixel value j in component i,
        * premultiplied as described above.  Since colormap indexes must fit into
        * bytes, the entries of this array will too.
        */
        private bool m_is_padded;		/* is the colorindex padded for odither? */

        private int[] m_Ncolors = new int[MAX_Q_COMPS];	/* # of values alloced to each component */

        /* Variables for ordered dithering */
        private int m_row_index;		/* cur row's vertical index in dither matrix */
        private int[][][] m_odither = new int[MAX_Q_COMPS][][]; /* one dither array per component */

        /* Variables for Floyd-Steinberg dithering */
        private short[][] m_fserrors = new short[MAX_Q_COMPS][]; /* accumulated errors */
        private bool m_on_odd_row;		/* flag to remember which row we are on */

        /// <summary>
        /// Module initialization routine for 1-pass color quantization.
        /// </summary>
        /// <param name="cinfo">The cinfo.</param>
        public my_1pass_cquantizer(jpeg_decompress_struct cinfo)
        {
            m_cinfo = cinfo;

            m_fserrors[0] = null; /* Flag FS workspace not allocated */
            m_odither[0] = null;    /* Also flag odither arrays not allocated */

            /* Make sure my internal arrays won't overflow */
            if (cinfo.m_out_color_components > MAX_Q_COMPS)
                cinfo.ERREXIT(J_MESSAGE_CODE.JERR_QUANT_COMPONENTS, MAX_Q_COMPS);

            /* Make sure colormap indexes can be represented by JSAMPLEs */
            if (cinfo.m_desired_number_of_colors > (JpegConstants.MAXJSAMPLE + 1))
                cinfo.ERREXIT(J_MESSAGE_CODE.JERR_QUANT_MANY_COLORS, JpegConstants.MAXJSAMPLE + 1);

            /* Create the colormap and color index table. */
            create_colormap();
            create_colorindex();

            /* Allocate Floyd-Steinberg workspace now if requested.
            * We do this now since it is FAR storage and may affect the memory
            * manager's space calculations.  If the user changes to FS dither
            * mode in a later pass, we will allocate the space then, and will
            * possibly overrun the max_memory_to_use setting.
            */
            if (cinfo.m_dither_mode == J_DITHER_MODE.JDITHER_FS)
                alloc_fs_workspace();
        }

        /// <summary>
        /// Initialize for one-pass color quantization.
        /// </summary>
        public virtual void start_pass(bool is_pre_scan)
        {
            /* Install my colormap. */
            m_cinfo.m_colormap = m_sv_colormap;
            m_cinfo.m_actual_number_of_colors = m_sv_actual;

            /* Initialize for desired dithering mode. */
            switch (m_cinfo.m_dither_mode)
            {
                case J_DITHER_MODE.JDITHER_NONE:
                    if (m_cinfo.m_out_color_components == 3)
                        m_quantizer = QuantizerType.color_quantizer3;
                    else
                        m_quantizer = QuantizerType.color_quantizer;

                    break;
                case J_DITHER_MODE.JDITHER_ORDERED:
                    if (m_cinfo.m_out_color_components == 3)
                        m_quantizer = QuantizerType.quantize3_ord_dither_quantizer;
                    else
                        m_quantizer = QuantizerType.quantize3_ord_dither_quantizer;

                    /* initialize state for ordered dither */
                    m_row_index = 0;

                    /* If user changed to ordered dither from another mode,
                     * we must recreate the color index table with padding.
                     * This will cost extra space, but probably isn't very likely.
                     */
                    if (!m_is_padded)
                        create_colorindex();

                    /* Create ordered-dither tables if we didn't already. */
                    if (m_odither[0] == null)
                        create_odither_tables();

                    break;
                case J_DITHER_MODE.JDITHER_FS:
                    m_quantizer = QuantizerType.quantize_fs_dither_quantizer;

                    /* initialize state for F-S dither */
                    m_on_odd_row = false;

                    /* Allocate Floyd-Steinberg workspace if didn't already. */
                    if (m_fserrors[0] == null)
                        alloc_fs_workspace();

                    /* Initialize the propagated errors to zero. */
                    int arraysize = m_cinfo.m_output_width + 2;
                    for (int i = 0; i < m_cinfo.m_out_color_components; i++)
                        Array.Clear(m_fserrors[i], 0, arraysize);

                    break;
                default:
                    m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOT_COMPILED);
                    break;
            }
        }

        public virtual void color_quantize(byte[][] input_buf, int in_row, byte[][] output_buf, int out_row, int num_rows)
        {
            switch (m_quantizer)
            {
                case QuantizerType.color_quantizer3:
                    quantize3(input_buf, in_row, output_buf, out_row, num_rows);
                    break;
                case QuantizerType.color_quantizer:
                    quantize(input_buf, in_row, output_buf, out_row, num_rows);
                    break;
                case QuantizerType.quantize3_ord_dither_quantizer:
                    quantize3_ord_dither(input_buf, in_row, output_buf, out_row, num_rows);
                    break;
                case QuantizerType.quantize_ord_dither_quantizer:
                    quantize_ord_dither(input_buf, in_row, output_buf, out_row, num_rows);
                    break;
                case QuantizerType.quantize_fs_dither_quantizer:
                    quantize_fs_dither(input_buf, in_row, output_buf, out_row, num_rows);
                    break;
                default:
                    m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
                    break;
            }
        }

        /// <summary>
        /// Finish up at the end of the pass.
        /// </summary>
        public virtual void finish_pass()
        {
            /* no work in 1-pass case */
        }

        /// <summary>
        /// Switch to a new external colormap between output passes.
        /// Shouldn't get to this!
        /// </summary>
        public virtual void new_color_map()
        {
            m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_MODE_CHANGE);
        }

        /// <summary>
        /// Map some rows of pixels to the output colormapped representation.
        /// General case, no dithering.
        /// </summary>
        private void quantize(byte[][] input_buf, int in_row, byte[][] output_buf, int out_row, int num_rows)
        {
            int nc = m_cinfo.m_out_color_components;

            for (int row = 0; row < num_rows; row++)
            {
                int inIndex = 0;
                int inRow = in_row + row;

                int outIndex = 0;
                int outRow = out_row + row;

                for (int col = m_cinfo.m_output_width; col > 0; col--)
                {
                    int pixcode = 0;
                    for (int ci = 0; ci < nc; ci++)
                    {
                        pixcode += m_colorindex[ci][m_colorindexOffset[ci] + input_buf[inRow][inIndex]];
                        inIndex++;
                    }

                    output_buf[outRow][outIndex] = (byte)pixcode;
                    outIndex++;
                }
            }
        }

        /// <summary>
        /// Map some rows of pixels to the output colormapped representation.
        /// Fast path for out_color_components==3, no dithering
        /// </summary>
        private void quantize3(byte[][] input_buf, int in_row, byte[][] output_buf, int out_row, int num_rows)
        {
            int width = m_cinfo.m_output_width;

            for (int row = 0; row < num_rows; row++)
            {
                int inIndex = 0;
                int inRow = in_row + row;

                int outIndex = 0;
                int outRow = out_row + row;

                for (int col = width; col > 0; col--)
                {
                    int pixcode = m_colorindex[0][m_colorindexOffset[0] + input_buf[inRow][inIndex]];
                    inIndex++;

                    pixcode += m_colorindex[1][m_colorindexOffset[1] + input_buf[inRow][inIndex]];
                    inIndex++;

                    pixcode += m_colorindex[2][m_colorindexOffset[2] + input_buf[inRow][inIndex]];
                    inIndex++;

                    output_buf[outRow][outIndex] = (byte)pixcode;
                    outIndex++;
                }
            }
        }

        /// <summary>
        /// Map some rows of pixels to the output colormapped representation.
        /// General case, with ordered dithering.
        /// </summary>
        private void quantize_ord_dither(byte[][] input_buf, int in_row, byte[][] output_buf, int out_row, int num_rows)
        {
            int nc = m_cinfo.m_out_color_components;
            int width = m_cinfo.m_output_width;

            for (int row = 0; row < num_rows; row++)
            {
                /* Initialize output values to 0 so can process components separately */
                Array.Clear(output_buf[out_row + row], 0, width);

                int row_index = m_row_index;
                for (int ci = 0; ci < nc; ci++)
                {
                    int inputIndex = ci;
                    int outIndex = 0;
                    int outRow = out_row + row;

                    int col_index = 0;
                    for (int col = width; col > 0; col--)
                    {
                        /* Form pixel value + dither, range-limit to 0..MAXJSAMPLE,
                         * select output value, accumulate into output code for this pixel.
                         * Range-limiting need not be done explicitly, as we have extended
                         * the colorindex table to produce the right answers for out-of-range
                         * inputs.  The maximum dither is +- MAXJSAMPLE; this sets the
                         * required amount of padding.
                         */
                        output_buf[outRow][outIndex] += m_colorindex[ci][m_colorindexOffset[ci] + input_buf[in_row + row][inputIndex] + m_odither[ci][row_index][col_index]];
                        inputIndex += nc;
                        outIndex++;
                        col_index = (col_index + 1) & ODITHER_MASK;
                    }
                }

                /* Advance row index for next row */
                row_index = (row_index + 1) & ODITHER_MASK;
                m_row_index = row_index;
            }
        }

        /// <summary>
        /// Map some rows of pixels to the output colormapped representation.
        /// Fast path for out_color_components==3, with ordered dithering
        /// </summary>
        private void quantize3_ord_dither(byte[][] input_buf, int in_row, byte[][] output_buf, int out_row, int num_rows)
        {
            int width = m_cinfo.m_output_width;

            for (int row = 0; row < num_rows; row++)
            {
                int row_index = m_row_index;
                int inRow = in_row + row;
                int inIndex = 0;

                int outIndex = 0;
                int outRow = out_row + row;

                int col_index = 0;
                for (int col = width; col > 0; col--)
                {
                    int pixcode = m_colorindex[0][m_colorindexOffset[0] + input_buf[inRow][inIndex] + m_odither[0][row_index][col_index]];
                    inIndex++;

                    pixcode += m_colorindex[1][m_colorindexOffset[1] + input_buf[inRow][inIndex] + m_odither[1][row_index][col_index]];
                    inIndex++;

                    pixcode += m_colorindex[2][m_colorindexOffset[2] + input_buf[inRow][inIndex] + m_odither[2][row_index][col_index]];
                    inIndex++;

                    output_buf[outRow][outIndex] = (byte)pixcode;
                    outIndex++;

                    col_index = (col_index + 1) & ODITHER_MASK;
                }

                row_index = (row_index + 1) & ODITHER_MASK;
                m_row_index = row_index;
            }
        }

        /// <summary>
        /// Map some rows of pixels to the output colormapped representation.
        /// General case, with Floyd-Steinberg dithering
        /// </summary>
        private void quantize_fs_dither(byte[][] input_buf, int in_row, byte[][] output_buf, int out_row, int num_rows)
        {
            int nc = m_cinfo.m_out_color_components;
            int width = m_cinfo.m_output_width;

            byte[] limit = m_cinfo.m_sample_range_limit;
            int limitOffset = m_cinfo.m_sampleRangeLimitOffset;

            for (int row = 0; row < num_rows; row++)
            {
                /* Initialize output values to 0 so can process components separately */
                Array.Clear(output_buf[out_row + row], 0, width);

                for (int ci = 0; ci < nc; ci++)
                {
                    int inRow = in_row + row;
                    int inIndex = ci;

                    int outIndex = 0;
                    int outRow = out_row + row;

                    int errorIndex = 0;
                    int dir;            /* 1 for left-to-right, -1 for right-to-left */
                    if (m_on_odd_row)
                    {
                        /* work right to left in this row */
                        inIndex += (width - 1) * nc; /* so point to rightmost pixel */
                        outIndex += width - 1;
                        dir = -1;
                        errorIndex = width + 1; /* => entry after last column */
                    }
                    else
                    {
                        /* work left to right in this row */
                        dir = 1;
                        errorIndex = 0; /* => entry before first column */
                    }
                    int dirnc = dir * nc;

                    /* Preset error values: no error propagated to first pixel from left */
                    int cur = 0;
                    /* and no error propagated to row below yet */
                    int belowerr = 0;
                    int bpreverr = 0;

                    for (int col = width; col > 0; col--)
                    {
                        /* cur holds the error propagated from the previous pixel on the
                         * current line.  Add the error propagated from the previous line
                         * to form the complete error correction term for this pixel, and
                         * round the error term (which is expressed * 16) to an integer.
                         * RIGHT_SHIFT rounds towards minus infinity, so adding 8 is correct
                         * for either sign of the error value.
                         * Note: errorIndex is for *previous* column's array entry.
                         */
                        cur = JpegUtils.RIGHT_SHIFT(cur + m_fserrors[ci][errorIndex + dir] + 8, 4);

                        /* Form pixel value + error, and range-limit to 0..MAXJSAMPLE.
                         * The maximum error is +- MAXJSAMPLE; this sets the required size
                         * of the range_limit array.
                         */
                        cur += input_buf[inRow][inIndex];
                        cur = limit[limitOffset + cur];

                        /* Select output value, accumulate into output code for this pixel */
                        int pixcode = m_colorindex[ci][m_colorindexOffset[ci] + cur];
                        output_buf[outRow][outIndex] += (byte)pixcode;
                        
                        /* Compute actual representation error at this pixel */
                        /* Note: we can do this even though we don't have the final */
                        /* pixel code, because the colormap is orthogonal. */
                        cur -= m_sv_colormap[ci][pixcode];
                        
                        /* Compute error fractions to be propagated to adjacent pixels.
                         * Add these into the running sums, and simultaneously shift the
                         * next-line error sums left by 1 column.
                         */
                        int bnexterr = cur;
                        int delta = cur * 2;
                        cur += delta;       /* form error * 3 */
                        m_fserrors[ci][errorIndex + 0] = (short) (bpreverr + cur);
                        cur += delta;       /* form error * 5 */
                        bpreverr = belowerr + cur;
                        belowerr = bnexterr;
                        cur += delta;       /* form error * 7 */
                        
                        /* At this point cur contains the 7/16 error value to be propagated
                         * to the next pixel on the current line, and all the errors for the
                         * next line have been shifted over. We are therefore ready to move on.
                         */
                        inIndex += dirnc; /* advance input to next column */
                        outIndex += dir;  /* advance output to next column */
                        errorIndex += dir;    /* advance errorIndex to current column */
                    }

                    /* Post-loop cleanup: we must unload the final error value into the
                     * final fserrors[] entry.  Note we need not unload belowerr because
                     * it is for the dummy column before or after the actual array.
                     */
                    m_fserrors[ci][errorIndex + 0] = (short) bpreverr; /* unload prev err into array */
                }

                m_on_odd_row = (m_on_odd_row ? false : true);
            }
        }

        /// <summary>
        /// Create the colormap.
        /// </summary>
        private void create_colormap()
        {
            /* Select number of colors for each component */
            int total_colors = select_ncolors(m_Ncolors);

            /* Report selected color counts */
            if (m_cinfo.m_out_color_components == 3)
                m_cinfo.TRACEMS(1, J_MESSAGE_CODE.JTRC_QUANT_3_NCOLORS, total_colors, m_Ncolors[0], m_Ncolors[1], m_Ncolors[2]);
            else
                m_cinfo.TRACEMS(1, J_MESSAGE_CODE.JTRC_QUANT_NCOLORS, total_colors);

            /* Allocate and fill in the colormap. */
            /* The colors are ordered in the map in standard row-major order, */
            /* i.e. rightmost (highest-indexed) color changes most rapidly. */
            byte[][] colormap = jpeg_common_struct.AllocJpegSamples(total_colors, m_cinfo.m_out_color_components);

            /* blksize is number of adjacent repeated entries for a component */
            /* blkdist is distance between groups of identical entries for a component */
            int blkdist = total_colors;
            for (int i = 0; i < m_cinfo.m_out_color_components; i++)
            {
                /* fill in colormap entries for i'th color component */
                int nci = m_Ncolors[i]; /* # of distinct values for this color */
                int blksize = blkdist / nci;
                for (int j = 0; j < nci; j++)
                {
                    /* Compute j'th output value (out of nci) for component */
                    int val = output_value(j, nci - 1);

                    /* Fill in all colormap entries that have this value of this component */
                    for (int ptr = j * blksize; ptr < total_colors; ptr += blkdist)
                    {
                        /* fill in blksize entries beginning at ptr */
                        for (int k = 0; k < blksize; k++)
                            colormap[i][ptr + k] = (byte)val;
                    }
                }

                /* blksize of this color is blkdist of next */
                blkdist = blksize;
            }

            /* Save the colormap in private storage,
             * where it will survive color quantization mode changes.
             */
            m_sv_colormap = colormap;
            m_sv_actual = total_colors;
        }

        /// <summary>
        /// Create the color index table.
        /// </summary>
        private void create_colorindex()
        {
            /* For ordered dither, we pad the color index tables by MAXJSAMPLE in
             * each direction (input index values can be -MAXJSAMPLE .. 2*MAXJSAMPLE).
             * This is not necessary in the other dithering modes.  However, we
             * flag whether it was done in case user changes dithering mode.
             */
            int pad;
            if (m_cinfo.m_dither_mode == J_DITHER_MODE.JDITHER_ORDERED)
            {
                pad = JpegConstants.MAXJSAMPLE * 2;
                m_is_padded = true;
            }
            else
            {
                pad = 0;
                m_is_padded = false;
            }

            m_colorindex = jpeg_common_struct.AllocJpegSamples(JpegConstants.MAXJSAMPLE + 1 + pad, m_cinfo.m_out_color_components);
            m_colorindexOffset = new int[m_cinfo.m_out_color_components];

            /* blksize is number of adjacent repeated entries for a component */
            int blksize = m_sv_actual;
            for (int i = 0; i < m_cinfo.m_out_color_components; i++)
            {
                /* fill in colorindex entries for i'th color component */
                int nci = m_Ncolors[i]; /* # of distinct values for this color */
                blksize = blksize / nci;

                /* adjust colorindex pointers to provide padding at negative indexes. */
                if (pad != 0)
                    m_colorindexOffset[i] += JpegConstants.MAXJSAMPLE;

                /* in loop, val = index of current output value, */
                /* and k = largest j that maps to current val */
                int val = 0;
                int k = largest_input_value(0, nci - 1);
                for (int j = 0; j <= JpegConstants.MAXJSAMPLE; j++)
                {
                    while (j > k)
                    {
                        /* advance val if past boundary */
                        k = largest_input_value(++val, nci - 1);
                    }

                    /* premultiply so that no multiplication needed in main processing */
                    m_colorindex[i][m_colorindexOffset[i] + j] = (byte)(val * blksize);
                }

                /* Pad at both ends if necessary */
                if (pad != 0)
                {
                    for (int j = 1; j <= JpegConstants.MAXJSAMPLE; j++)
                    {
                        m_colorindex[i][m_colorindexOffset[i] + -j] = m_colorindex[i][m_colorindexOffset[i]];
                        m_colorindex[i][m_colorindexOffset[i] + JpegConstants.MAXJSAMPLE + j] = m_colorindex[i][m_colorindexOffset[i] + JpegConstants.MAXJSAMPLE];
                    }
                }
            }
        }

        /// <summary>
        /// Create the ordered-dither tables.
        /// Components having the same number of representative colors may 
        /// share a dither table.
        /// </summary>
        private void create_odither_tables()
        {
            for (int i = 0; i < m_cinfo.m_out_color_components; i++)
            {
                int nci = m_Ncolors[i]; /* # of distinct values for this color */

                /* search for matching prior component */
                int foundPos = -1;
                for (int j = 0; j < i; j++)
                {
                    if (nci == m_Ncolors[j])
                    {
                        foundPos = j;
                        break;
                    }
                }

                if (foundPos == -1)
                {
                    /* need a new table? */
                    m_odither[i] = make_odither_array(nci);
                }
                else
                    m_odither[i] = m_odither[foundPos];
            }
        }

        /// <summary>
        /// Allocate workspace for Floyd-Steinberg errors.
        /// </summary>
        private void alloc_fs_workspace()
        {
            for (int i = 0; i < m_cinfo.m_out_color_components; i++)
                m_fserrors[i] = new short[m_cinfo.m_output_width + 2];
        }

        /* 
         * Policy-making subroutines for create_colormap and create_colorindex.
         * These routines determine the colormap to be used.  The rest of the module
         * only assumes that the colormap is orthogonal.
         *
         *  * select_ncolors decides how to divvy up the available colors
         *    among the components.
         *  * output_value defines the set of representative values for a component.
         *  * largest_input_value defines the mapping from input values to
         *    representative values for a component.
         * Note that the latter two routines may impose different policies for
         * different components, though this is not currently done.
         */

        /// <summary>
        /// Return largest input value that should map to j'th output value
        /// Must have largest(j=0) >= 0, and largest(j=maxj) >= MAXJSAMPLE
        /// </summary>
        private static int largest_input_value(int j, int maxj)
        {
            /* Breakpoints are halfway between values returned by output_value */
            return (int)(((2 * j + 1) * JpegConstants.MAXJSAMPLE + maxj) / (2 * maxj));
        }

        /// <summary>
        /// Return j'th output value, where j will range from 0 to maxj
        /// The output values must fall in 0..MAXJSAMPLE in increasing order
        /// </summary>
        private static int output_value(int j, int maxj)
        {
            /* We always provide values 0 and MAXJSAMPLE for each component;
             * any additional values are equally spaced between these limits.
             * (Forcing the upper and lower values to the limits ensures that
             * dithering can't produce a color outside the selected gamut.)
             */
            return (int)((j * JpegConstants.MAXJSAMPLE + maxj / 2) / maxj);
        }

        /// <summary>
        /// Determine allocation of desired colors to components,
        /// and fill in Ncolors[] array to indicate choice.
        /// Return value is total number of colors (product of Ncolors[] values).
        /// </summary>
        private int select_ncolors(int[] Ncolors)
        {
            int nc = m_cinfo.m_out_color_components; /* number of color components */
            int max_colors = m_cinfo.m_desired_number_of_colors;
            
            /* We can allocate at least the nc'th root of max_colors per component. */
            /* Compute floor(nc'th root of max_colors). */
            int iroot = 1;
            long temp = 0;
            do
            {
                iroot++;
                temp = iroot;       /* set temp = iroot ** nc */
                for (int i = 1; i < nc; i++)
                    temp *= iroot;
            }
            while (temp <= max_colors); /* repeat till iroot exceeds root */

            /* now iroot = floor(root) */
            iroot--;

            /* Must have at least 2 color values per component */
            if (iroot < 2)
                m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_QUANT_FEW_COLORS, (int)temp);

            /* Initialize to iroot color values for each component */
            int total_colors = 1;
            for (int i = 0; i < nc; i++)
            {
                Ncolors[i] = iroot;
                total_colors *= iroot;
            }

            /* We may be able to increment the count for one or more components without
             * exceeding max_colors, though we know not all can be incremented.
             * Sometimes, the first component can be incremented more than once!
             * (Example: for 16 colors, we start at 2*2*2, go to 3*2*2, then 4*2*2.)
             * In RGB colorspace, try to increment G first, then R, then B.
             */
            bool changed = false;
            do
            {
                changed = false;
                for (int i = 0; i < nc; i++)
                {
                    int j = (m_cinfo.m_out_color_space == J_COLOR_SPACE.JCS_RGB ? RGB_order[i] : i);
                    /* calculate new total_colors if Ncolors[j] is incremented */
                    temp = total_colors / Ncolors[j];
                    temp *= Ncolors[j] + 1; /* done in long arith to avoid oflo */

                    if (temp > max_colors)
                        break;          /* won't fit, done with this pass */
                    
                    Ncolors[j]++;       /* OK, apply the increment */
                    total_colors = (int)temp;
                    changed = true;
                }
            }
            while (changed);

            return total_colors;
        }

        /// <summary>
        /// Create an ordered-dither array for a component having ncolors
        /// distinct output values.
        /// </summary>
        private static int[][] make_odither_array(int ncolors)
        {
            int[][] odither = new int[ODITHER_SIZE][];
            for (int i = 0; i < ODITHER_SIZE; i++)
                odither[i] = new int[ODITHER_SIZE];

            /* The inter-value distance for this color is MAXJSAMPLE/(ncolors-1).
             * Hence the dither value for the matrix cell with fill order f
             * (f=0..N-1) should be (N-1-2*f)/(2*N) * MAXJSAMPLE/(ncolors-1).
             * On 16-bit-int machine, be careful to avoid overflow.
             */
            int den = 2 * ODITHER_CELLS * (ncolors - 1);
            for (int j = 0; j < ODITHER_SIZE; j++)
            {
                for (int k = 0; k < ODITHER_SIZE; k++)
                {
                    int num = ((int)(ODITHER_CELLS - 1 - 2 * ((int)base_dither_matrix[j][k]))) * JpegConstants.MAXJSAMPLE;

                    /* Ensure round towards zero despite C's lack of consistency
                     * about rounding negative values in integer division...
                     */
                    odither[j][k] = num < 0 ? -((-num) / den) : num / den;
                }
            }

            return odither;
        }
    }
}
