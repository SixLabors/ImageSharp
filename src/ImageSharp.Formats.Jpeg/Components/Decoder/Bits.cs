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
        public int Accumulator;

        /// <summary>
        /// Gets or sets the mask.
        /// <![CDATA[mask==1<<(unreadbits-1) when unreadbits>0, with mask==0 when unreadbits==0.]]>
        /// </summary>
        public int Mask;

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
        /// <param name="bufferProcessor">The <see cref="BufferProcessor"/></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureNBits(int n, ref BufferProcessor bufferProcessor)
        {
            DecoderErrorCode errorCode = this.EnsureNBitsUnsafe(n, ref bufferProcessor);
            errorCode.EnsureNoError();
        }

        /// <summary>
        /// Reads bytes from the byte buffer to ensure that bits.UnreadBits is at
        /// least n. For best performance (avoiding function calls inside hot loops),
        /// the caller is the one responsible for first checking that bits.UnreadBits &lt; n.
        /// This method does not throw. Returns <see cref="DecoderErrorCode"/> instead.
        /// </summary>
        /// <param name="n">The number of bits to ensure.</param>
        /// <param name="bufferProcessor">The <see cref="BufferProcessor"/></param>
        /// <returns>Error code</returns>
        public DecoderErrorCode EnsureNBitsUnsafe(int n, ref BufferProcessor bufferProcessor)
        {
            while (true)
            {
                DecoderErrorCode errorCode = this.EnsureBitsStepImpl(ref bufferProcessor);
                if (errorCode != DecoderErrorCode.NoError || this.UnreadBits >= n)
                {
                    return errorCode;
                }
            }
        }

        /// <summary>
        /// Unrolled version of <see cref="EnsureNBitsUnsafe"/> for n==8
        /// </summary>
        /// <param name="bufferProcessor">The <see cref="BufferProcessor"/></param>
        /// <returns>A <see cref="DecoderErrorCode"/></returns>
        public DecoderErrorCode Ensure8BitsUnsafe(ref BufferProcessor bufferProcessor)
        {
            return this.EnsureBitsStepImpl(ref bufferProcessor);
        }

        /// <summary>
        /// Unrolled version of <see cref="EnsureNBitsUnsafe"/> for n==1
        /// </summary>
        /// <param name="bufferProcessor">The <see cref="BufferProcessor"/></param>
        /// <returns>A <see cref="DecoderErrorCode"/></returns>
        public DecoderErrorCode Ensure1BitUnsafe(ref BufferProcessor bufferProcessor)
        {
            return this.EnsureBitsStepImpl(ref bufferProcessor);
        }

        /// <summary>
        /// Receive extend
        /// </summary>
        /// <param name="t">Byte</param>
        /// <param name="bufferProcessor">The <see cref="BufferProcessor"/></param>
        /// <returns>Read bits value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReceiveExtend(int t, ref BufferProcessor bufferProcessor)
        {
            int x;
            DecoderErrorCode errorCode = this.ReceiveExtendUnsafe(t, ref bufferProcessor, out x);
            errorCode.EnsureNoError();
            return x;
        }

        /// <summary>
        /// Receive extend
        /// </summary>
        /// <param name="t">Byte</param>
        /// <param name="bufferProcessor">The <see cref="BufferProcessor"/></param>
        /// <param name="x">Read bits value</param>
        /// <returns>The <see cref="DecoderErrorCode"/></returns>
        public DecoderErrorCode ReceiveExtendUnsafe(int t, ref BufferProcessor bufferProcessor, out int x)
        {
            if (this.UnreadBits < t)
            {
                DecoderErrorCode errorCode = this.EnsureNBitsUnsafe(t, ref bufferProcessor);
                if (errorCode != DecoderErrorCode.NoError)
                {
                    x = int.MaxValue;
                    return errorCode;
                }
            }

            this.UnreadBits -= t;
            this.Mask >>= t;
            int s = 1 << t;
            x = (int)((this.Accumulator >> this.UnreadBits) & (s - 1));

            if (x < (s >> 1))
            {
                x += ((-1) << t) + 1;
            }

            return DecoderErrorCode.NoError;
        }

        private DecoderErrorCode EnsureBitsStepImpl(ref BufferProcessor bufferProcessor)
        {
            int c;
            DecoderErrorCode errorCode = bufferProcessor.Bytes.ReadByteStuffedByteUnsafe(bufferProcessor.InputStream, out c);

            if (errorCode != DecoderErrorCode.NoError)
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

            return errorCode;
        }
    }
}