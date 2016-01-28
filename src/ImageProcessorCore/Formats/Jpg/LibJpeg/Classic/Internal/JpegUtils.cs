/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * Copyright (C) 1994-1996, Thomas G. Lane.
 * This file is part of the Independent JPEG Group's software.
 * For conditions of distribution and use, see the accompanying README file.
 *
 */

/*
 * This file contains tables and miscellaneous utility routines needed
 * for both compression and decompression.
 * Note we prefix all global names with "j" to minimize conflicts with
 * a surrounding application.
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace BitMiracle.LibJpeg.Classic.Internal
{
    class JpegUtils
    {
        /*
        * jpeg_natural_order[i] is the natural-order position of the i'th element
        * of zigzag order.
        *
        * When reading corrupted data, the Huffman decoders could attempt
        * to reference an entry beyond the end of this array (if the decoded
        * zero run length reaches past the end of the block).  To prevent
        * wild stores without adding an inner-loop test, we put some extra
        * "63"s after the real entries.  This will cause the extra coefficient
        * to be stored in location 63 of the block, not somewhere random.
        * The worst case would be a run-length of 15, which means we need 16
        * fake entries.
        */
        public static int[] jpeg_natural_order = 
        { 
            0, 1, 8, 16, 9, 2, 3, 10, 17, 24, 32, 25, 18, 11, 4, 5, 12,
            19, 26, 33, 40, 48, 41, 34, 27, 20, 13, 6, 7, 14, 21, 28, 35,
            42, 49, 56, 57, 50, 43, 36, 29, 22, 15, 23, 30, 37, 44, 51,
            58, 59, 52, 45, 38, 31, 39, 46, 53, 60, 61, 54, 47, 55, 62,
            63, 63, 63, 63, 63, 63, 63, 63, 63,
            /* extra entries for safety in decoder */
            63, 63, 63, 63, 63, 63, 63, 63 
        };

        /* We assume that right shift corresponds to signed division by 2 with
        * rounding towards minus infinity.  This is correct for typical "arithmetic
        * shift" instructions that shift in copies of the sign bit.
        * RIGHT_SHIFT provides a proper signed right shift of an int quantity.
        * It is only applied with constant shift counts.  SHIFT_TEMPS must be
        * included in the variables of any routine using RIGHT_SHIFT.
        */
        public static int RIGHT_SHIFT(int x, int shft)
        {
            return (x >> shft);
        }
        
        /* Descale and correctly round an int value that's scaled by N bits.
        * We assume RIGHT_SHIFT rounds towards minus infinity, so adding
        * the fudge factor is correct for either sign of X.
        */
        public static int DESCALE(int x, int n)
        {
            return RIGHT_SHIFT(x + (1 << (n - 1)), n);
        }

        //////////////////////////////////////////////////////////////////////////
        // Arithmetic utilities

        /// <summary>
        /// Compute a/b rounded up to next integer, ie, ceil(a/b)
        /// Assumes a >= 0, b > 0
        /// </summary>
        public static int jdiv_round_up(int a, int b)
        {
            return (a + b - 1) / b;
        }

        /// <summary>
        /// Compute a rounded up to next multiple of b, ie, ceil(a/b)*b
        /// Assumes a >= 0, b > 0
        /// </summary>
        public static int jround_up(int a, int b)
        {
            a += b - 1;
            return a - (a % b);
        }

        /// <summary>
        /// Copy some rows of samples from one place to another.
        /// num_rows rows are copied from input_array[source_row++]
        /// to output_array[dest_row++]; these areas may overlap for duplication.
        /// The source and destination arrays must be at least as wide as num_cols.
        /// </summary>
        public static void jcopy_sample_rows(ComponentBuffer input_array, int source_row, byte[][] output_array, int dest_row, int num_rows, int num_cols)
        {
            for (int row = 0; row < num_rows; row++)
                Buffer.BlockCopy(input_array[source_row + row], 0, output_array[dest_row + row], 0, num_cols);
        }

        public static void jcopy_sample_rows(ComponentBuffer input_array, int source_row, ComponentBuffer output_array, int dest_row, int num_rows, int num_cols)
        {
            for (int row = 0; row < num_rows; row++)
                Buffer.BlockCopy(input_array[source_row + row], 0, output_array[dest_row + row], 0, num_cols);
        }

        public static void jcopy_sample_rows(byte[][] input_array, int source_row, byte[][] output_array, int dest_row, int num_rows, int num_cols)
        {
            for (int row = 0; row < num_rows; row++)
                Buffer.BlockCopy(input_array[source_row++], 0, output_array[dest_row++], 0, num_cols);
        }
    }
}
