// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Png.Zlib
{
    /// <summary>
    /// Computes Adler32 checksum for a stream of data. An Adler32
    /// checksum is not as reliable as a CRC32 checksum, but a lot faster to
    /// compute.
    /// </summary>
    /// <remarks>
    /// The specification for Adler32 may be found in RFC 1950.
    /// ZLIB Compressed Data Format Specification version 3.3)
    ///
    ///
    /// From that document:
    ///
    ///      "ADLER32 (Adler-32 checksum)
    ///       This contains a checksum value of the uncompressed data
    ///       (excluding any dictionary data) computed according to Adler-32
    ///       algorithm. This algorithm is a 32-bit extension and improvement
    ///       of the Fletcher algorithm, used in the ITU-T X.224 / ISO 8073
    ///       standard.
    ///
    ///       Adler-32 is composed of two sums accumulated per byte: s1 is
    ///       the sum of all bytes, s2 is the sum of all s1 values. Both sums
    ///       are done modulo 65521. s1 is initialized to 1, s2 to zero.  The
    ///       Adler-32 checksum is stored as s2*65536 + s1 in most-
    ///       significant-byte first (network) order."
    ///
    ///  "8.2. The Adler-32 algorithm
    ///
    ///    The Adler-32 algorithm is much faster than the CRC32 algorithm yet
    ///    still provides an extremely low probability of undetected errors.
    ///
    ///    The modulo on unsigned long accumulators can be delayed for 5552
    ///    bytes, so the modulo operation time is negligible.  If the bytes
    ///    are a, b, c, the second sum is 3a + 2b + c + 3, and so is position
    ///    and order sensitive, unlike the first sum, which is just a
    ///    checksum.  That 65521 is prime is important to avoid a possible
    ///    large class of two-byte errors that leave the check unchanged.
    ///    (The Fletcher checksum uses 255, which is not prime and which also
    ///    makes the Fletcher check insensitive to single byte changes 0 -
    ///    255.)
    ///
    ///    The sum s1 is initialized to 1 instead of zero to make the length
    ///    of the sequence part of s2, so that the length does not have to be
    ///    checked separately. (Any sequence of zeroes has a Fletcher
    ///    checksum of zero.)"
    /// </remarks>
    /// <see cref="ZlibInflateStream"/>
    /// <see cref="ZlibDeflateStream"/>
    internal sealed class Adler32 : IChecksum
    {
        /// <summary>
        /// largest prime smaller than 65536
        /// </summary>
        private const uint Base = 65521;

        /// <summary>
        /// The checksum calculated to far.
        /// </summary>
        private uint checksum;

        /// <summary>
        /// Initializes a new instance of the <see cref="Adler32"/> class.
        /// The checksum starts off with a value of 1.
        /// </summary>
        public Adler32()
        {
            this.Reset();
        }

        /// <inheritdoc/>
        public long Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.checksum;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            this.checksum = 1;
        }

        /// <summary>
        /// Updates the checksum with a byte value.
        /// </summary>
        /// <param name="value">
        /// The data value to add. The high byte of the int is ignored.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(int value)
        {
            // We could make a length 1 byte array and call update again, but I
            // would rather not have that overhead
            uint s1 = this.checksum & 0xFFFF;
            uint s2 = this.checksum >> 16;

            s1 = (s1 + ((uint)value & 0xFF)) % Base;
            s2 = (s1 + s2) % Base;

            this.checksum = (s2 << 16) + s1;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(ReadOnlySpan<byte> data)
        {
            // (By Per Bothner)
            uint s1 = this.checksum & 0xFFFF;
            uint s2 = this.checksum >> 16;

            int count = data.Length;
            int offset = 0;

            while (count > 0)
            {
                // We can defer the modulo operation:
                // s1 maximally grows from 65521 to 65521 + 255 * 3800
                // s2 maximally grows by 3800 * median(s1) = 2090079800 < 2^31
                int n = 3800;
                if (n > count)
                {
                    n = count;
                }

                count -= n;
                while (--n >= 0)
                {
                    s1 = s1 + (uint)(data[offset++] & 0xff);
                    s2 = s2 + s1;
                }

                s1 %= Base;
                s2 %= Base;
            }

            this.checksum = (s2 << 16) | s1;
        }
    }
}