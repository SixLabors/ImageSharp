// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Png.Zlib
{
    /// <summary>
    /// This class compresses input with the deflate algorithm described in RFC 1951.
    /// It has several compression levels and three different strategies described below.
    /// </summary>
    internal sealed class Deflater : IDisposable
    {
        /// <summary>
        /// The best and slowest compression level.  This tries to find very
        /// long and distant string repetitions.
        /// </summary>
        public const int BestCompression = 9;

        /// <summary>
        /// The worst but fastest compression level.
        /// </summary>
        public const int BestSpeed = 1;

        /// <summary>
        /// The default compression level.
        /// </summary>
        public const int DefaultCompression = -1;

        /// <summary>
        /// This level won't compress at all but output uncompressed blocks.
        /// </summary>
        public const int NoCompression = 0;

        /// <summary>
        /// The compression method.  This is the only method supported so far.
        /// There is no need to use this constant at all.
        /// </summary>
        public const int Deflated = 8;

        /// <summary>
        /// Compression level.
        /// </summary>
        private int level;

        /// <summary>
        /// The current state.
        /// </summary>
        private int state;

        private DeflaterEngine engine;
        private bool isDisposed;

        private const int IsFlushing = 0x04;
        private const int IsFinishing = 0x08;
        private const int BusyState = 0x10;
        private const int FlushingState = 0x14;
        private const int FinishingState = 0x1c;
        private const int FinishedState = 0x1e;
        private const int ClosedState = 0x7f;

        /// <summary>
        /// Initializes a new instance of the <see cref="Deflater"/> class.
        /// </summary>
        /// <param name="memoryAllocator">The memory allocator to use for buffer allocations.</param>
        /// <param name="level">The compression level, a value between NoCompression and BestCompression.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">if level is out of range.</exception>
        public Deflater(MemoryAllocator memoryAllocator, int level)
        {
            if (level == DefaultCompression)
            {
                level = 6;
            }
            else if (level < NoCompression || level > BestCompression)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }

            // TODO: Possibly provide DeflateStrategy as an option.
            this.engine = new DeflaterEngine(memoryAllocator, DeflateStrategy.Default);

            this.SetLevel(level);
            this.Reset();
        }

        /// <summary>
        /// Compression Level as an enum for safer use
        /// </summary>
        public enum CompressionLevel
        {
            /// <summary>
            /// The best and slowest compression level.  This tries to find very
            /// long and distant string repetitions.
            /// </summary>
            BestCompression = Deflater.BestCompression,

            /// <summary>
            /// The worst but fastest compression level.
            /// </summary>
            BestSpeed = Deflater.BestSpeed,

            /// <summary>
            /// The default compression level.
            /// </summary>
            DefaultCompression = Deflater.DefaultCompression,

            /// <summary>
            /// This level won't compress at all but output uncompressed blocks.
            /// </summary>
            NoCompression = Deflater.NoCompression,

            /// <summary>
            /// The compression method.  This is the only method supported so far.
            /// There is no need to use this constant at all.
            /// </summary>
            Deflated = Deflater.Deflated
        }

        /// <summary>
        /// Gets a value indicating whetherthe stream was finished and no more output bytes
        /// are available.
        /// </summary>
        public bool IsFinished => (this.state == FinishedState) && this.engine.Pending.IsFlushed;

        /// <summary>
        /// Gets a value indicating whether the input buffer is empty.
        /// You should then call setInput().
        /// NOTE: This method can also return true when the stream
        /// was finished.
        /// </summary>
        public bool IsNeedingInput => this.engine.NeedsInput();

        /// <summary>
        /// Resets the deflater.  The deflater acts afterwards as if it was
        /// just created with the same compression level and strategy as it
        /// had before.
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void Reset()
        {
            this.state = BusyState;
            this.engine.Pending.Reset();
            this.engine.Reset();
        }

        /// <summary>
        /// Flushes the current input block. Further calls to Deflate() will
        /// produce enough output to inflate everything in the current input
        /// block. It is used by DeflaterOutputStream to implement Flush().
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void Flush() => this.state |= IsFlushing;

        /// <summary>
        /// Finishes the deflater with the current input block. It is an error
        /// to give more input after this method was called. This method must
        /// be called to force all bytes to be flushed.
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void Finish() => this.state |= IsFlushing | IsFinishing;

        /// <summary>
        /// Sets the data which should be compressed next.  This should be
        /// only called when needsInput indicates that more input is needed.
        /// The given byte array should not be changed, before needsInput() returns
        /// true again.
        /// </summary>
        /// <param name="input">The buffer containing the input data.</param>
        /// <param name="offset">The start of the data.</param>
        /// <param name="count">The number of data bytes of input.</param>
        /// <exception cref="InvalidOperationException">
        /// if the buffer was finished or if previous input is still pending.
        /// </exception>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void SetInput(byte[] input, int offset, int count)
        {
            if ((this.state & IsFinishing) != 0)
            {
                DeflateThrowHelper.ThrowAlreadyFinished();
            }

            this.engine.SetInput(input, offset, count);
        }

        /// <summary>
        /// Sets the compression level.  There is no guarantee of the exact
        /// position of the change, but if you call this when needsInput is
        /// true the change of compression level will occur somewhere near
        /// before the end of the so far given input.
        /// </summary>
        /// <param name="level">
        /// the new compression level.
        /// </param>
        public void SetLevel(int level)
        {
            if (level == DefaultCompression)
            {
                level = 6;
            }
            else if (level < NoCompression || level > BestCompression)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }

            if (this.level != level)
            {
                this.level = level;
                this.engine.SetLevel(level);
            }
        }

        /// <summary>
        /// Deflates the current input block to the given array.
        /// </summary>
        /// <param name="output">Buffer to store the compressed data.</param>
        /// <param name="offset">Offset into the output array.</param>
        /// <param name="length">The maximum number of bytes that may be stored.</param>
        /// <returns>
        /// The number of compressed bytes added to the output, or 0 if either
        /// <see cref="IsNeedingInput"/> or <see cref="IsFinished"/> returns true or length is zero.
        /// </returns>
        public int Deflate(byte[] output, int offset, int length)
        {
            int origLength = length;

            if (this.state == ClosedState)
            {
                DeflateThrowHelper.ThrowAlreadyClosed();
            }

            while (true)
            {
                int count = this.engine.Pending.Flush(output, offset, length);
                offset += count;
                length -= count;

                if (length == 0 || this.state == FinishedState)
                {
                    break;
                }

                if (!this.engine.Deflate((this.state & IsFlushing) != 0, (this.state & IsFinishing) != 0))
                {
                    switch (this.state)
                    {
                        case BusyState:
                            // We need more input now
                            return origLength - length;

                        case FlushingState:
                            if (this.level != NoCompression)
                            {
                                // We have to supply some lookahead.  8 bit lookahead
                                // is needed by the zlib inflater, and we must fill
                                // the next byte, so that all bits are flushed.
                                int neededbits = 8 + ((-this.engine.Pending.BitCount) & 7);
                                while (neededbits > 0)
                                {
                                    // Write a static tree block consisting solely of an EOF:
                                    this.engine.Pending.WriteBits(2, 10);
                                    neededbits -= 10;
                                }
                            }

                            this.state = BusyState;
                            break;

                        case FinishingState:
                            this.engine.Pending.AlignToByte();
                            this.state = FinishedState;
                            break;
                    }
                }
            }

            return origLength - length;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!this.isDisposed)
            {
                this.engine.Dispose();
                this.engine = null;
                this.isDisposed = true;
            }
        }
    }
}
