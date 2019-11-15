// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.WebP
{
    internal class Vp8BitReader
    {
        /// <summary>
        /// Current value.
        /// </summary>
        private long value;

        /// <summary>
        /// Current range minus 1. In [127, 254] interval.
        /// </summary>
        private int range;

        /// <summary>
        /// Number of valid bits left.
        /// </summary>
        private int bits;

        /// <summary>
        /// The next byte to be read.
        /// </summary>
        private byte buf;

        /// <summary>
        /// End of read buffer.
        /// </summary>
        private byte bufEnd;

        /// <summary>
        /// Max packed-read position on buffer.
        /// </summary>
        private byte bufMax;

        /// <summary>
        /// True if input is exhausted.
        /// </summary>
        private bool eof;

        /// <summary>
        /// Reads the specified number of bits from read buffer.
        /// Flags an error in case end_of_stream or n_bits is more than the allowed limit
        /// of VP8L_MAX_NUM_BIT_READ (inclusive).
        /// Flags eos_ if this read attempt is going to cross the read buffer.
        /// </summary>
        /// <param name="nBits">The number of bits to read.</param>
        public int ReadBits(int nBits)
        {
            throw new NotImplementedException();
        }
    }
}
