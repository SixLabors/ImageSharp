// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Formats.WebP.BitWriter
{
    /// <summary>
    /// A bit writer for writing lossless webp streams.
    /// </summary>
    internal class Vp8BitWriter
    {
        private uint range;

        private uint value;

        /// <summary>
        /// Number of outstanding bits.
        /// </summary>
        private int run;

        /// <summary>
        /// Number of pending bits.
        /// </summary>
        private int nbBits;

        private byte[] buffer;

        private int pos;

        private int maxPos;

        private bool error;

        public Vp8BitWriter(int expectedSize)
        {
            this.range = 255 - 1;
            this.value = 0;
            this.run = 0;
            this.nbBits = -8;
            this.pos = 0;
            this.maxPos = 0;
            this.error = false;

            //BitWriterResize(expected_size);
        }
    }
}
