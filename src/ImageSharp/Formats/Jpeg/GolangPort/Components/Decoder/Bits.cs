// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.GolangPort.Components.Decoder
{
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
        /// <param name="inputProcessor">The <see cref="InputProcessor"/></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureNBits(int n, ref InputProcessor inputProcessor)
        {
            GolangDecoderErrorCode errorCode = this.EnsureNBitsUnsafe(n, ref inputProcessor);
            errorCode.EnsureNoError();
        }

        /// <summary>
        /// Reads bytes from the byte buffer to ensure that bits.UnreadBits is at
        /// least n. For best performance (avoiding function calls inside hot loops),
        /// the caller is the one responsible for first checking that bits.UnreadBits &lt; n.
        /// This method does not throw. Returns <see cref="GolangDecoderErrorCode"/> instead.
        /// </summary>
        /// <param name="n">The number of bits to ensure.</param>
        /// <param name="inputProcessor">The <see cref="InputProcessor"/></param>
        /// <returns>Error code</returns>
        public GolangDecoderErrorCode EnsureNBitsUnsafe(int n, ref InputProcessor inputProcessor)
        {
            while (true)
            {
                GolangDecoderErrorCode errorCode = this.EnsureBitsStepImpl(ref inputProcessor);
                if (errorCode != GolangDecoderErrorCode.NoError || this.UnreadBits >= n)
                {
                    return errorCode;
                }
            }
        }

        /// <summary>
        /// Unrolled version of <see cref="EnsureNBitsUnsafe"/> for n==8
        /// </summary>
        /// <param name="inputProcessor">The <see cref="InputProcessor"/></param>
        /// <returns>A <see cref="GolangDecoderErrorCode"/></returns>
        public GolangDecoderErrorCode Ensure8BitsUnsafe(ref InputProcessor inputProcessor)
        {
            return this.EnsureBitsStepImpl(ref inputProcessor);
        }

        /// <summary>
        /// Unrolled version of <see cref="EnsureNBitsUnsafe"/> for n==1
        /// </summary>
        /// <param name="inputProcessor">The <see cref="InputProcessor"/></param>
        /// <returns>A <see cref="GolangDecoderErrorCode"/></returns>
        public GolangDecoderErrorCode Ensure1BitUnsafe(ref InputProcessor inputProcessor)
        {
            return this.EnsureBitsStepImpl(ref inputProcessor);
        }

        /// <summary>
        /// Receive extend
        /// </summary>
        /// <param name="t">Byte</param>
        /// <param name="inputProcessor">The <see cref="InputProcessor"/></param>
        /// <returns>Read bits value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReceiveExtend(int t, ref InputProcessor inputProcessor)
        {
            GolangDecoderErrorCode errorCode = this.ReceiveExtendUnsafe(t, ref inputProcessor, out int x);
            errorCode.EnsureNoError();
            return x;
        }

        /// <summary>
        /// Receive extend
        /// </summary>
        /// <param name="t">Byte</param>
        /// <param name="inputProcessor">The <see cref="InputProcessor"/></param>
        /// <param name="x">Read bits value</param>
        /// <returns>The <see cref="GolangDecoderErrorCode"/></returns>
        public GolangDecoderErrorCode ReceiveExtendUnsafe(int t, ref InputProcessor inputProcessor, out int x)
        {
            if (this.UnreadBits < t)
            {
                GolangDecoderErrorCode errorCode = this.EnsureNBitsUnsafe(t, ref inputProcessor);
                if (errorCode != GolangDecoderErrorCode.NoError)
                {
                    x = int.MaxValue;
                    return errorCode;
                }
            }

            this.UnreadBits -= t;
            this.Mask >>= t;
            int s = 1 << t;
            x = (this.Accumulator >> this.UnreadBits) & (s - 1);

            if (x < (s >> 1))
            {
                x += ((-1) << t) + 1;
            }

            return GolangDecoderErrorCode.NoError;
        }

        private GolangDecoderErrorCode EnsureBitsStepImpl(ref InputProcessor inputProcessor)
        {
            GolangDecoderErrorCode errorCode = inputProcessor.Bytes.ReadByteStuffedByteUnsafe(inputProcessor.InputStream, out int c);

            if (errorCode != GolangDecoderErrorCode.NoError)
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