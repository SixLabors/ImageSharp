// <copyright file="Bits.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpg
{
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Holds the unprocessed bits that have been taken from the byte-stream.
    /// The n least significant bits of a form the unread bits, to be read in MSB to
    /// LSB order.
    /// </summary>
    internal struct Bits
    {
        /// <summary>
        /// Gets or sets the accumulator.
        /// </summary>
        public uint Accumulator;

        /// <summary>
        /// Gets or sets the mask.
        /// <![CDATA[mask==1<<(unreadbits-1) when unreadbits>0, with mask==0 when unreadbits==0.]]>
        /// </summary>
        public uint Mask;

        /// <summary>
        /// Gets or sets the  number of unread bits in the accumulator.
        /// </summary>
        public int UnreadBits;

        /// <summary>
        /// Reads bytes from the byte buffer to ensure that bits.UnreadBits is at
        /// least n. For best performance (avoiding function calls inside hot loops),
        /// the caller is the one responsible for first checking that bits.UnreadBits &lt; n.
        /// </summary>
        /// <param name="n">The number of bits to ensure.</param>
        /// <param name="decoder">Jpeg decoder</param>
        /// <returns>Error code</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal JpegDecoderCore.ErrorCodes EnsureNBits(int n, JpegDecoderCore decoder)
        {
            while (true)
            {
                JpegDecoderCore.ErrorCodes errorCode;

                // Grab the decode bytes, use them and then set them
                // back on the decoder.
                Bytes decoderBytes = decoder.Bytes;
                byte c = decoderBytes.ReadByteStuffedByte(decoder.InputStream, out errorCode);
                decoder.Bytes = decoderBytes;

                if (errorCode != JpegDecoderCore.ErrorCodes.NoError)
                {
                    return errorCode;
                }

                this.Accumulator = (this.Accumulator << 8) | c;
                this.UnreadBits += 8;
                if (this.Mask == 0)
                {
                    this.Mask = 1 << 7;
                }
                else
                {
                    this.Mask <<= 8;
                }

                if (this.UnreadBits >= n)
                {
                    return JpegDecoderCore.ErrorCodes.NoError;
                }
            }
        }

        /// <summary>
        /// Receive extend
        /// </summary>
        /// <param name="t">Byte</param>
        /// <param name="decoder">Jpeg decoder</param>
        /// <returns>Read bits value</returns>
        internal int ReceiveExtend(byte t, JpegDecoderCore decoder)
        {
            if (this.UnreadBits < t)
            {
                JpegDecoderCore.ErrorCodes errorCode = this.EnsureNBits(t, decoder);
                if (errorCode != JpegDecoderCore.ErrorCodes.NoError)
                {
                    throw new JpegDecoderCore.MissingFF00Exception();
                }
            }

            this.UnreadBits -= t;
            this.Mask >>= t;
            int s = 1 << t;
            int x = (int)((this.Accumulator >> this.UnreadBits) & (s - 1));

            if (x < (s >> 1))
            {
                x += ((-1) << t) + 1;
            }

            return x;
        }
    }
}