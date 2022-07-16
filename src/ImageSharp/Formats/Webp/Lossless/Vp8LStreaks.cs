// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless
{
    internal class Vp8LStreaks
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8LStreaks"/> class.
        /// </summary>
        public Vp8LStreaks()
        {
            this.Counts = new int[2];
            this.Streaks = new int[2][];
            this.Streaks[0] = new int[2];
            this.Streaks[1] = new int[2];
        }

        /// <summary>
        /// Gets the streak count.
        /// index: 0=zero streak, 1=non-zero streak.
        /// </summary>
        public int[] Counts { get; }

        /// <summary>
        /// Gets the streaks.
        /// [zero/non-zero][streak &lt; 3 / streak >= 3].
        /// </summary>
        public int[][] Streaks { get; }

        public void Clear()
        {
            this.Counts.AsSpan().Clear();
            this.Streaks[0].AsSpan().Clear();
            this.Streaks[1].AsSpan().Clear();
        }

        public double FinalHuffmanCost()
        {
            // The constants in this function are experimental and got rounded from
            // their original values in 1/8 when switched to 1/1024.
            double retval = InitialHuffmanCost();

            // Second coefficient: Many zeros in the histogram are covered efficiently
            // by a run-length encode. Originally 2/8.
            retval += (this.Counts[0] * 1.5625) + (0.234375 * this.Streaks[0][1]);

            // Second coefficient: Constant values are encoded less efficiently, but still
            // RLE'ed. Originally 6/8.
            retval += (this.Counts[1] * 2.578125) + (0.703125 * this.Streaks[1][1]);

            // 0s are usually encoded more efficiently than non-0s.
            // Originally 15/8.
            retval += 1.796875 * this.Streaks[0][0];

            // Originally 26/8.
            retval += 3.28125 * this.Streaks[1][0];

            return retval;
        }

        private static double InitialHuffmanCost()
        {
            // Small bias because Huffman code length is typically not stored in full length.
            int huffmanCodeOfHuffmanCodeSize = WebpConstants.CodeLengthCodes * 3;
            double smallBias = 9.1;
            return huffmanCodeOfHuffmanCodeSize - smallBias;
        }
    }
}
