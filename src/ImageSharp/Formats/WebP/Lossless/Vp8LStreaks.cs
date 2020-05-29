// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Formats.WebP.Lossless
{
    internal class Vp8LStreaks
    {
        public Vp8LStreaks()
        {
            this.Counts = new int[2];
            this.Streaks = new int[2][];
            this.Streaks[0] = new int[2];
            this.Streaks[1] = new int[2];
        }

        /// <summary>
        /// index: 0=zero streak, 1=non-zero streak.
        /// </summary>
        public int[] Counts { get; }

        /// <summary>
        /// [zero/non-zero][streak < 3 / streak >= 3].
        /// </summary>
        public int[][] Streaks { get; }
    }
}
