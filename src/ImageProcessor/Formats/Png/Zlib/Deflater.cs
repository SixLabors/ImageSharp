// <copyright file="Deflater.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Formats
{
    using System;

    /// <summary>
    /// This is the Deflater class.  The deflater class compresses input
    /// with the deflate algorithm described in RFC 1951.  It has several
    /// compression levels and three different strategies described below.
    ///
    /// This class is <i>not</i> thread safe.  This is inherent in the API, due
    /// to the split of deflate and setInput.
    ///
    /// author of the original java version : Jochen Hoenicke
    /// </summary>
    public class Deflater
    {
        /*
        * The Deflater can do the following state transitions:
        *
        * (1) -> INIT_STATE   ----> INIT_FINISHING_STATE ---.
        *        /  | (2)      (5)                          |
        *       /   v          (5)                          |
        *   (3)| SETDICT_STATE ---> SETDICT_FINISHING_STATE |(3)
        *       \   | (3)                 |        ,--------'
        *        |  |                     | (3)   /
        *        v  v          (5)        v      v
        * (1) -> BUSY_STATE   ----> FINISHING_STATE
        *                                | (6)
        *                                v
        *                           FINISHED_STATE
        *    \_____________________________________/
        *                    | (7)
        *                    v
        *               CLOSED_STATE
        *
        * (1) If we should produce a header we start in INIT_STATE, otherwise
        *     we start in BUSY_STATE.
        * (2) A dictionary may be set only when we are in INIT_STATE, then
        *     we change the state as indicated.
        * (3) Whether a dictionary is set or not, on the first call of deflate
        *     we change to BUSY_STATE.
        * (4) -- intentionally left blank -- :)
        * (5) FINISHING_STATE is entered, when flush() is called to indicate that
        *     there is no more INPUT.  There are also states indicating, that
        *     the header wasn't written yet.
        * (6) FINISHED_STATE is entered, when everything has been flushed to the
        *     internal pending output buffer.
        * (7) At any time (7)
        *
        */

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
        /// The is dictionary set flag.
        /// </summary>
        private const int IsSetdict = 0x01;

        /// <summary>
        /// Flags whether flushing.
        /// </summary>
        private const int IsFlushing = 0x04;

        /// <summary>
        /// Flags whether finishing.
        /// </summary>
        private const int IsFinishing = 0x08;

        /// <summary>
        /// The initial stat flag
        /// </summary>
        private const int InitState = 0x00;

        /// <summary>
        /// Flags setting the dictionary.
        /// </summary>
        private const int SetdictState = 0x01;

        /// <summary>
        /// The busy state flag.
        /// </summary>
        private const int BusyState = 0x10;

        /// <summary>
        /// The flushing state flag.
        /// </summary>
        private const int FlushingState = 0x14;

        /// <summary>
        /// The finishing state flag.
        /// </summary>
        private const int FinishingState = 0x1c;

        /// <summary>
        /// The finished state flag.
        /// </summary>
        private const int FinishedState = 0x1e;

        /// <summary>
        /// The closed state flag.
        /// </summary>
        private const int ClosedState = 0x7f;

        /// <summary>
        /// The pending output.
        /// </summary>
        private readonly DeflaterPending pending;

        /// <summary>
        /// If true no Zlib/RFC1950 headers or footers are generated
        /// </summary>
        private readonly bool noZlibHeaderOrFooter;

        /// <summary>
        /// The deflater engine.
        /// </summary>
        private readonly DeflaterEngine engine;

        /// <summary>
        /// Compression level.
        /// </summary>
        private int deflaterLevel;

        /// <summary>
        /// The current state.
        /// </summary>
        private int state;

        /// <summary>
        /// The total bytes of output written.
        /// </summary>
        private long totalOut;

        /// <summary>
        /// Initializes a new instance of the <see cref="Deflater"/> class with the default compression level.
        /// </summary>
        public Deflater()
            : this(DefaultCompression, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Deflater"/> class with the given compressin level.
        /// </summary>
        /// <param name="level">
        /// The compression level, a value between NoCompression and BestCompression, or DefaultCompression.
        /// </param>
        /// <exception cref="System.ArgumentOutOfRangeException">If level is out of range.</exception>
        public Deflater(int level)
            : this(level, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Deflater"/> class with the given compressin level.
        /// </summary>
        /// <param name="level">
        /// The compression level, a value between NoCompression and BestCompression, or DefaultCompression.
        /// </param>
        /// <param name="noZlibHeaderOrFooter">
        /// True, if we should suppress the Zlib/RFC1950 header at the
        /// beginning and the adler checksum at the end of the output.  This is
        /// useful for the GZIP/PKZIP formats.
        /// </param>
        /// <exception cref="System.ArgumentOutOfRangeException">if lvl is out of range.</exception>
        public Deflater(int level, bool noZlibHeaderOrFooter)
        {
            if (level == DefaultCompression)
            {
                level = 6;
            }
            else if (level < NoCompression || level > BestCompression)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }

            this.pending = new DeflaterPending();
            this.engine = new DeflaterEngine(this.pending);
            this.noZlibHeaderOrFooter = noZlibHeaderOrFooter;
            this.SetStrategy(DeflateStrategy.Default);
            this.SetLevel(level);
            this.Reset();
        }

        /// <summary>
        /// Gets the current adler checksum of the data that was processed so far.
        /// </summary>
        public int Adler => this.engine.Adler;

        /// <summary>
        /// Gets the number of input bytes processed so far.
        /// </summary>
        public long TotalIn => this.engine.TotalIn;

        /// <summary>
        /// Gets the number of output bytes so far.
        /// </summary>
        public long TotalOut => this.totalOut;

        /// <summary>
        /// Returns true if the stream was finished and no more output bytes
        /// are available.
        /// </summary>
        public bool IsFinished => (this.state == FinishedState) && this.pending.IsFlushed;

        /// <summary>
        /// Returns true, if the input buffer is empty.
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
        public void Reset()
        {
            this.state = this.noZlibHeaderOrFooter ? BusyState : InitState;
            this.totalOut = 0;
            this.pending.Reset();
            this.engine.Reset();
        }

        /// <summary>
        /// Flushes the current input block.  Further calls to deflate() will
        /// produce enough output to inflate everything in the current input
        /// block.  This is not part of Sun's JDK so I have made it package
        /// private.  It is used by DeflaterOutputStream to implement
        /// flush().
        /// </summary>
        public void Flush()
        {
            this.state |= IsFlushing;
        }

        /// <summary>
        /// Finishes the deflater with the current input block.  It is an error
        /// to give more input after this method was called.  This method must
        /// be called to force all bytes to be flushed.
        /// </summary>
        public void Finish()
        {
            this.state |= IsFlushing | IsFinishing;
        }

        /// <summary>
        /// Sets the data which should be compressed next.  This should be only
        /// called when needsInput indicates that more input is needed.
        /// If you call setInput when needsInput() returns false, the
        /// previous input that is still pending will be thrown away.
        /// The given byte array should not be changed, before needsInput() returns
        /// true again.
        /// This call is equivalent to <code>setInput(input, 0, input.length)</code>.
        /// </summary>
        /// <param name="input">
        /// the buffer containing the input data.
        /// </param>
        /// <exception cref="System.InvalidOperationException">
        /// if the buffer was finished() or ended().
        /// </exception>
        public void SetInput(byte[] input)
        {
            this.SetInput(input, 0, input.Length);
        }

        /// <summary>
        /// Sets the data which should be compressed next.  This should be
        /// only called when needsInput indicates that more input is needed.
        /// The given byte array should not be changed, before needsInput() returns
        /// true again.
        /// </summary>
        /// <param name="input">
        /// the buffer containing the input data.
        /// </param>
        /// <param name="offset">
        /// the start of the data.
        /// </param>
        /// <param name="count">
        /// the number of data bytes of input.
        /// </param>
        /// <exception cref="System.InvalidOperationException">
        /// if the buffer was Finish()ed or if previous input is still pending.
        /// </exception>
        public void SetInput(byte[] input, int offset, int count)
        {
            if ((this.state & IsFinishing) != 0)
            {
                throw new InvalidOperationException("Finish() already called");
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

            if (this.deflaterLevel != level)
            {
                this.deflaterLevel = level;
                this.engine.SetLevel(level);
            }
        }

        /// <summary>
        /// Get current compression level
        /// </summary>
        /// <returns>Returns the current compression level</returns>
        public int GetLevel()
        {
            return this.deflaterLevel;
        }

        /// <summary>
        /// Sets the compression strategy. Strategy is one of
        /// DEFAULT_STRATEGY, HUFFMAN_ONLY and FILTERED.  For the exact
        /// position where the strategy is changed, the same as for
        /// SetLevel() applies.
        /// </summary>
        /// <param name="strategy">
        /// The new compression strategy.
        /// </param>
        public void SetStrategy(DeflateStrategy strategy)
        {
            this.engine.Strategy = strategy;
        }

        /// <summary>
        /// Deflates the current input block with to the given array.
        /// </summary>
        /// <param name="output">
        /// The buffer where compressed data is stored
        /// </param>
        /// <returns>
        /// The number of compressed bytes added to the output, or 0 if either
        /// IsNeedingInput() or IsFinished returns true or length is zero.
        /// </returns>
        public int Deflate(byte[] output)
        {
            return this.Deflate(output, 0, output.Length);
        }

        /// <summary>
        /// Deflates the current input block to the given array.
        /// </summary>
        /// <param name="output">
        /// Buffer to store the compressed data.
        /// </param>
        /// <param name="offset">
        /// Offset into the output array.
        /// </param>
        /// <param name="length">
        /// The maximum number of bytes that may be stored.
        /// </param>
        /// <returns>
        /// The number of compressed bytes added to the output, or 0 if either
        /// needsInput() or finished() returns true or length is zero.
        /// </returns>
        /// <exception cref="System.InvalidOperationException">
        /// If Finish() was previously called.
        /// </exception>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// If offset or length don't match the array length.
        /// </exception>
        public int Deflate(byte[] output, int offset, int length)
        {
            int origLength = length;

            if (this.state == ClosedState)
            {
                throw new InvalidOperationException("Deflater closed");
            }

            if (this.state < BusyState)
            {
                // output header
                int header = (Deflated +
                    ((DeflaterConstants.MaxWbits - 8) << 4)) << 8;
                int levelFlags = (this.deflaterLevel - 1) >> 1;
                if (levelFlags < 0 || levelFlags > 3)
                {
                    levelFlags = 3;
                }

                header |= levelFlags << 6;
                if ((this.state & IsSetdict) != 0)
                {
                    // Dictionary was set
                    header |= DeflaterConstants.PresetDict;
                }

                header += 31 - (header % 31);

                this.pending.WriteShortMSB(header);
                if ((this.state & IsSetdict) != 0)
                {
                    int chksum = this.engine.Adler;
                    this.engine.ResetAdler();
                    this.pending.WriteShortMSB(chksum >> 16);
                    this.pending.WriteShortMSB(chksum & 0xffff);
                }

                this.state = BusyState | (this.state & (IsFlushing | IsFinishing));
            }

            for (; ;)
            {
                int count = this.pending.Flush(output, offset, length);
                offset += count;
                this.totalOut += count;
                length -= count;

                if (length == 0 || this.state == FinishedState)
                {
                    break;
                }

                if (!this.engine.Deflate((this.state & IsFlushing) != 0, (this.state & IsFinishing) != 0))
                {
                    if (this.state == BusyState)
                    {
                        // We need more input now
                        return origLength - length;
                    }
                    else if (this.state == FlushingState)
                    {
                        if (this.deflaterLevel != NoCompression)
                        {
                            /* We have to supply some lookahead.  8 bit lookahead
                             * is needed by the zlib inflater, and we must fill
                             * the next byte, so that all bits are flushed.
                             */
                            int neededbits = 8 + ((-this.pending.BitCount) & 7);
                            while (neededbits > 0)
                            {
                                /* write a static tree block consisting solely of
                                 * an EOF:
                                 */
                                this.pending.WriteBits(2, 10);
                                neededbits -= 10;
                            }
                        }

                        this.state = BusyState;
                    }
                    else if (this.state == FinishingState)
                    {
                        this.pending.AlignToByte();

                        // Compressed data is complete.  Write footer information if required.
                        if (!this.noZlibHeaderOrFooter)
                        {
                            int adler = this.engine.Adler;
                            this.pending.WriteShortMSB(adler >> 16);
                            this.pending.WriteShortMSB(adler & 0xffff);
                        }

                        this.state = FinishedState;
                    }
                }
            }

            return origLength - length;
        }

        /// <summary>
        /// Sets the dictionary which should be used in the deflate process.
        /// This call is equivalent to <code>setDictionary(dict, 0, dict.Length)</code>.
        /// </summary>
        /// <param name="dictionary">
        /// the dictionary.
        /// </param>
        /// <exception cref="System.InvalidOperationException">
        /// if SetInput () or Deflate () were already called or another dictionary was already set.
        /// </exception>
        public void SetDictionary(byte[] dictionary)
        {
            this.SetDictionary(dictionary, 0, dictionary.Length);
        }

        /// <summary>
        /// Sets the dictionary which should be used in the deflate process.
        /// The dictionary is a byte array containing strings that are
        /// likely to occur in the data which should be compressed.  The
        /// dictionary is not stored in the compressed output, only a
        /// checksum.  To decompress the output you need to supply the same
        /// dictionary again.
        /// </summary>
        /// <param name="dictionary">
        /// The dictionary data
        /// </param>
        /// <param name="index">
        /// The index where dictionary information commences.
        /// </param>
        /// <param name="count">
        /// The number of bytes in the dictionary.
        /// </param>
        /// <exception cref="System.InvalidOperationException">
        /// If SetInput () or Deflate() were already called or another dictionary was already set.
        /// </exception>
        public void SetDictionary(byte[] dictionary, int index, int count)
        {
            if (this.state != InitState)
            {
                throw new InvalidOperationException();
            }

            this.state = SetdictState;
            this.engine.SetDictionary(dictionary, index, count);
        }
    }
}
