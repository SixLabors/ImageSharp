// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Png.Zlib
{
    /// <summary>
    /// Strategies for deflater
    /// </summary>
    internal enum DeflateStrategy
    {
        /// <summary>
        /// The default strategy
        /// </summary>
        Default = 0,

        /// <summary>
        /// This strategy will only allow longer string repetitions.  It is
        /// useful for random data with a small character set.
        /// </summary>
        Filtered = 1,

        /// <summary>
        /// This strategy will not look for string repetitions at all.  It
        /// only encodes with Huffman trees (which means, that more common
        /// characters get a smaller encoding.
        /// </summary>
        HuffmanOnly = 2
    }

    // DEFLATE ALGORITHM:
    //
    // The uncompressed stream is inserted into the window array.  When
    // the window array is full the first half is thrown away and the
    // second half is copied to the beginning.
    //
    // The head array is a hash table.  Three characters build a hash value
    // and they the value points to the corresponding index in window of
    // the last string with this hash.  The prev array implements a
    // linked list of matches with the same hash: prev[index & WMASK] points
    // to the previous index with the same hash.
    //

    /// <summary>
    /// Low level compression engine for deflate algorithm which uses a 32K sliding window
    /// with secondary compression from Huffman/Shannon-Fano codes.
    /// </summary>
    internal sealed unsafe class DeflaterEngine : IDisposable
    {
        private const int TooFar = 4096;

        // Hash index of string to be inserted
        private int insertHashIndex;

        private int matchStart;

        // Length of best match
        private int matchLen;

        // Set if previous match exists
        private bool prevAvailable;

        private int blockStart;

        /// <summary>
        /// Points to the current character in the window.
        /// </summary>
        private int strstart;

        /// <summary>
        /// lookahead is the number of characters starting at strstart in
        /// window that are valid.
        /// So window[strstart] until window[strstart+lookahead-1] are valid
        /// characters.
        /// </summary>
        private int lookahead;

        /// <summary>
        /// The current compression function.
        /// </summary>
        private int compressionFunction;

        /// <summary>
        /// The input data for compression.
        /// </summary>
        private byte[] inputBuf;

        /// <summary>
        /// The offset into inputBuf, where input data starts.
        /// </summary>
        private int inputOff;

        /// <summary>
        /// The end offset of the input data.
        /// </summary>
        private int inputEnd;

        private readonly DeflateStrategy strategy;
        private DeflaterHuffman huffman;
        private bool isDisposed;

        /// <summary>
        /// Hashtable, hashing three characters to an index for window, so
        /// that window[index]..window[index+2] have this hash code.
        /// Note that the array should really be unsigned short, so you need
        /// to and the values with 0xFFFF.
        /// </summary>
        private IMemoryOwner<short> headMemoryOwner;
        private MemoryHandle headMemoryHandle;
        private readonly Memory<short> head;
        private readonly short* pinnedHeadPointer;

        /// <summary>
        /// <code>prev[index &amp; WMASK]</code> points to the previous index that has the
        /// same hash code as the string starting at index.  This way
        /// entries with the same hash code are in a linked list.
        /// Note that the array should really be unsigned short, so you need
        /// to and the values with 0xFFFF.
        /// </summary>
        private IMemoryOwner<short> prevMemoryOwner;
        private MemoryHandle prevMemoryHandle;
        private readonly Memory<short> prev;
        private readonly short* pinnedPrevPointer;

        /// <summary>
        /// This array contains the part of the uncompressed stream that
        /// is of relevance. The current character is indexed by strstart.
        /// </summary>
        private IManagedByteBuffer windowMemoryOwner;
        private MemoryHandle windowMemoryHandle;
        private readonly byte[] window;
        private readonly byte* pinnedWindowPointer;

        private int maxChain;
        private int maxLazy;
        private int niceLength;
        private int goodLength;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeflaterEngine"/> class.
        /// </summary>
        /// <param name="memoryAllocator">The memory allocator to use for buffer allocations.</param>
        /// <param name="strategy">The deflate strategy to use.</param>
        public DeflaterEngine(MemoryAllocator memoryAllocator, DeflateStrategy strategy)
        {
            this.huffman = new DeflaterHuffman(memoryAllocator);
            this.Pending = this.huffman.Pending;
            this.strategy = strategy;

            // Create pinned pointers to the various buffers to allow indexing
            // without bounds checks.
            this.windowMemoryOwner = memoryAllocator.AllocateManagedByteBuffer(2 * DeflaterConstants.WSIZE);
            this.window = this.windowMemoryOwner.Array;
            this.windowMemoryHandle = this.windowMemoryOwner.Memory.Pin();
            this.pinnedWindowPointer = (byte*)this.windowMemoryHandle.Pointer;

            this.headMemoryOwner = memoryAllocator.Allocate<short>(DeflaterConstants.HASH_SIZE);
            this.head = this.headMemoryOwner.Memory;
            this.headMemoryHandle = this.headMemoryOwner.Memory.Pin();
            this.pinnedHeadPointer = (short*)this.headMemoryHandle.Pointer;

            this.prevMemoryOwner = memoryAllocator.Allocate<short>(DeflaterConstants.WSIZE);
            this.prev = this.prevMemoryOwner.Memory;
            this.prevMemoryHandle = this.prevMemoryOwner.Memory.Pin();
            this.pinnedPrevPointer = (short*)this.prevMemoryHandle.Pointer;

            // We start at index 1, to avoid an implementation deficiency, that
            // we cannot build a repeat pattern at index 0.
            this.blockStart = this.strstart = 1;
        }

        /// <summary>
        /// Gets the pending buffer to use.
        /// </summary>
        public DeflaterPendingBuffer Pending { get; }

        /// <summary>
        /// Deflate drives actual compression of data
        /// </summary>
        /// <param name="flush">True to flush input buffers</param>
        /// <param name="finish">Finish deflation with the current input.</param>
        /// <returns>Returns true if progress has been made.</returns>
        public bool Deflate(bool flush, bool finish)
        {
            bool progress = false;
            do
            {
                this.FillWindow();
                bool canFlush = flush && (this.inputOff == this.inputEnd);

                switch (this.compressionFunction)
                {
                    case DeflaterConstants.DEFLATE_STORED:
                        progress = this.DeflateStored(canFlush, finish);
                        break;

                    case DeflaterConstants.DEFLATE_FAST:
                        progress = this.DeflateFast(canFlush, finish);
                        break;

                    case DeflaterConstants.DEFLATE_SLOW:
                        progress = this.DeflateSlow(canFlush, finish);
                        break;

                    default:
                        DeflateThrowHelper.ThrowUnknownCompression();
                        break;
                }
            }
            while (this.Pending.IsFlushed && progress); // repeat while we have no pending output and progress was made
            return progress;
        }

        /// <summary>
        /// Sets input data to be deflated.  Should only be called when <see cref="NeedsInput"/>
        /// returns true
        /// </summary>
        /// <param name="buffer">The buffer containing input data.</param>
        /// <param name="offset">The offset of the first byte of data.</param>
        /// <param name="count">The number of bytes of data to use as input.</param>
        public void SetInput(byte[] buffer, int offset, int count)
        {
            if (buffer is null)
            {
                DeflateThrowHelper.ThrowNull(nameof(buffer));
            }

            if (offset < 0)
            {
                DeflateThrowHelper.ThrowOutOfRange(nameof(offset));
            }

            if (count < 0)
            {
                DeflateThrowHelper.ThrowOutOfRange(nameof(count));
            }

            if (this.inputOff < this.inputEnd)
            {
                DeflateThrowHelper.ThrowNotProcessed();
            }

            int end = offset + count;

            // We want to throw an ArgumentOutOfRangeException early.
            // The check is very tricky: it also handles integer wrap around.
            if ((offset > end) || (end > buffer.Length))
            {
                DeflateThrowHelper.ThrowOutOfRange(nameof(count));
            }

            this.inputBuf = buffer;
            this.inputOff = offset;
            this.inputEnd = end;
        }

        /// <summary>
        /// Determines if more <see cref="SetInput">input</see> is needed.
        /// </summary>
        /// <returns>Return true if input is needed via <see cref="SetInput">SetInput</see></returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool NeedsInput() => this.inputEnd == this.inputOff;

        /// <summary>
        /// Reset internal state
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void Reset()
        {
            this.huffman.Reset();
            this.blockStart = this.strstart = 1;
            this.lookahead = 0;
            this.prevAvailable = false;
            this.matchLen = DeflaterConstants.MIN_MATCH - 1;
            this.head.Span.Slice(0, DeflaterConstants.HASH_SIZE).Clear();
            this.prev.Span.Slice(0, DeflaterConstants.WSIZE).Clear();
        }

        /// <summary>
        /// Set the deflate level (0-9)
        /// </summary>
        /// <param name="level">The value to set the level to.</param>
        public void SetLevel(int level)
        {
            if ((level < 0) || (level > 9))
            {
                DeflateThrowHelper.ThrowOutOfRange(nameof(level));
            }

            this.goodLength = DeflaterConstants.GOOD_LENGTH[level];
            this.maxLazy = DeflaterConstants.MAX_LAZY[level];
            this.niceLength = DeflaterConstants.NICE_LENGTH[level];
            this.maxChain = DeflaterConstants.MAX_CHAIN[level];

            if (DeflaterConstants.COMPR_FUNC[level] != this.compressionFunction)
            {
                switch (this.compressionFunction)
                {
                    case DeflaterConstants.DEFLATE_STORED:
                        if (this.strstart > this.blockStart)
                        {
                            this.huffman.FlushStoredBlock(this.window, this.blockStart, this.strstart - this.blockStart, false);
                            this.blockStart = this.strstart;
                        }

                        this.UpdateHash();
                        break;

                    case DeflaterConstants.DEFLATE_FAST:
                        if (this.strstart > this.blockStart)
                        {
                            this.huffman.FlushBlock(this.window, this.blockStart, this.strstart - this.blockStart, false);
                            this.blockStart = this.strstart;
                        }

                        break;

                    case DeflaterConstants.DEFLATE_SLOW:
                        if (this.prevAvailable)
                        {
                            this.huffman.TallyLit(this.pinnedWindowPointer[this.strstart - 1] & 0xFF);
                        }

                        if (this.strstart > this.blockStart)
                        {
                            this.huffman.FlushBlock(this.window, this.blockStart, this.strstart - this.blockStart, false);
                            this.blockStart = this.strstart;
                        }

                        this.prevAvailable = false;
                        this.matchLen = DeflaterConstants.MIN_MATCH - 1;
                        break;
                }

                this.compressionFunction = DeflaterConstants.COMPR_FUNC[level];
            }
        }

        /// <summary>
        /// Fill the window
        /// </summary>
        public void FillWindow()
        {
            // If the window is almost full and there is insufficient lookahead,
            // move the upper half to the lower one to make room in the upper half.
            if (this.strstart >= DeflaterConstants.WSIZE + DeflaterConstants.MAX_DIST)
            {
                this.SlideWindow();
            }

            // If there is not enough lookahead, but still some input left, read in the input.
            if (this.lookahead < DeflaterConstants.MIN_LOOKAHEAD && this.inputOff < this.inputEnd)
            {
                int more = (2 * DeflaterConstants.WSIZE) - this.lookahead - this.strstart;

                if (more > this.inputEnd - this.inputOff)
                {
                    more = this.inputEnd - this.inputOff;
                }

                Buffer.BlockCopy(this.inputBuf, this.inputOff, this.window, this.strstart + this.lookahead, more);

                this.inputOff += more;
                this.lookahead += more;
            }

            if (this.lookahead >= DeflaterConstants.MIN_MATCH)
            {
                this.UpdateHash();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!this.isDisposed)
            {
                this.huffman.Dispose();

                this.windowMemoryHandle.Dispose();
                this.windowMemoryOwner.Dispose();

                this.headMemoryHandle.Dispose();
                this.headMemoryOwner.Dispose();

                this.prevMemoryHandle.Dispose();
                this.prevMemoryOwner.Dispose();

                this.windowMemoryOwner = null;
                this.headMemoryOwner = null;
                this.prevMemoryOwner = null;
                this.huffman = null;

                this.isDisposed = true;
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private void UpdateHash()
        {
            byte* pinned = this.pinnedWindowPointer;
            this.insertHashIndex = (pinned[this.strstart] << DeflaterConstants.HASH_SHIFT) ^ pinned[this.strstart + 1];
        }

        /// <summary>
        /// Inserts the current string in the head hash and returns the previous
        /// value for this hash.
        /// </summary>
        /// <returns>The previous hash value</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        private int InsertString()
        {
            short match;
            int hash = ((this.insertHashIndex << DeflaterConstants.HASH_SHIFT) ^ this.pinnedWindowPointer[this.strstart + (DeflaterConstants.MIN_MATCH - 1)]) & DeflaterConstants.HASH_MASK;

            short* pinnedHead = this.pinnedHeadPointer;
            this.pinnedPrevPointer[this.strstart & DeflaterConstants.WMASK] = match = pinnedHead[hash];
            pinnedHead[hash] = unchecked((short)this.strstart);
            this.insertHashIndex = hash;
            return match & 0xFFFF;
        }

        private void SlideWindow()
        {
            Unsafe.CopyBlockUnaligned(ref this.window[0], ref this.window[DeflaterConstants.WSIZE], DeflaterConstants.WSIZE);
            this.matchStart -= DeflaterConstants.WSIZE;
            this.strstart -= DeflaterConstants.WSIZE;
            this.blockStart -= DeflaterConstants.WSIZE;

            // Slide the hash table (could be avoided with 32 bit values
            // at the expense of memory usage).
            short* pinnedHead = this.pinnedHeadPointer;
            for (int i = 0; i < DeflaterConstants.HASH_SIZE; ++i)
            {
                int m = pinnedHead[i] & 0xFFFF;
                pinnedHead[i] = (short)(m >= DeflaterConstants.WSIZE ? (m - DeflaterConstants.WSIZE) : 0);
            }

            // Slide the prev table.
            short* pinnedPrev = this.pinnedPrevPointer;
            for (int i = 0; i < DeflaterConstants.WSIZE; i++)
            {
                int m = pinnedPrev[i] & 0xFFFF;
                pinnedPrev[i] = (short)(m >= DeflaterConstants.WSIZE ? (m - DeflaterConstants.WSIZE) : 0);
            }
        }

        /// <summary>
        /// <para>
        /// Find the best (longest) string in the window matching the
        /// string starting at strstart.
        /// </para>
        /// <para>
        /// Preconditions:
        /// <code>
        /// strstart + DeflaterConstants.MAX_MATCH &lt;= window.length.</code>
        /// </para>
        /// </summary>
        /// <param name="curMatch">The current match.</param>
        /// <returns>True if a match greater than the minimum length is found</returns>
        [MethodImpl(InliningOptions.HotPath)]
        private bool FindLongestMatch(int curMatch)
        {
            int match;
            int scan = this.strstart;

            // scanMax is the highest position that we can look at
            int scanMax = scan + Math.Min(DeflaterConstants.MAX_MATCH, this.lookahead) - 1;
            int limit = Math.Max(scan - DeflaterConstants.MAX_DIST, 0);

            int chainLength = this.maxChain;
            int niceLength = Math.Min(this.niceLength, this.lookahead);

            int matchStrt = this.matchStart;
            this.matchLen = Math.Max(this.matchLen, DeflaterConstants.MIN_MATCH - 1);
            int matchLength = this.matchLen;

            if (scan + matchLength > scanMax)
            {
                return false;
            }

            byte* pinnedWindow = this.pinnedWindowPointer;
            int scanStart = this.strstart;
            byte scanEnd1 = pinnedWindow[scan + matchLength - 1];
            byte scanEnd = pinnedWindow[scan + matchLength];

            // Do not waste too much time if we already have a good match:
            if (matchLength >= this.goodLength)
            {
                chainLength >>= 2;
            }

            short* pinnedPrev = this.pinnedPrevPointer;
            do
            {
                match = curMatch;
                scan = scanStart;

                if (pinnedWindow[match + matchLength] != scanEnd
                 || pinnedWindow[match + matchLength - 1] != scanEnd1
                 || pinnedWindow[match] != pinnedWindow[scan]
                 || pinnedWindow[++match] != pinnedWindow[++scan])
                {
                    continue;
                }

                // scan is set to strstart+1 and the comparison passed, so
                // scanMax - scan is the maximum number of bytes we can compare.
                // below we compare 8 bytes at a time, so first we compare
                // (scanMax - scan) % 8 bytes, so the remainder is a multiple of 8
                // n & (8 - 1) == n % 8.
                switch ((scanMax - scan) & 7)
                {
                    case 1:
                        if (pinnedWindow[++scan] == pinnedWindow[++match])
                        {
                            break;
                        }

                        break;

                    case 2:
                        if (pinnedWindow[++scan] == pinnedWindow[++match]
                            && pinnedWindow[++scan] == pinnedWindow[++match])
                        {
                            break;
                        }

                        break;

                    case 3:
                        if (pinnedWindow[++scan] == pinnedWindow[++match]
                            && pinnedWindow[++scan] == pinnedWindow[++match]
                            && pinnedWindow[++scan] == pinnedWindow[++match])
                        {
                            break;
                        }

                        break;

                    case 4:
                        if (pinnedWindow[++scan] == pinnedWindow[++match]
                            && pinnedWindow[++scan] == pinnedWindow[++match]
                            && pinnedWindow[++scan] == pinnedWindow[++match]
                            && pinnedWindow[++scan] == pinnedWindow[++match])
                        {
                            break;
                        }

                        break;

                    case 5:
                        if (pinnedWindow[++scan] == pinnedWindow[++match]
                            && pinnedWindow[++scan] == pinnedWindow[++match]
                            && pinnedWindow[++scan] == pinnedWindow[++match]
                            && pinnedWindow[++scan] == pinnedWindow[++match]
                            && pinnedWindow[++scan] == pinnedWindow[++match])
                        {
                            break;
                        }

                        break;

                    case 6:
                        if (pinnedWindow[++scan] == pinnedWindow[++match]
                            && pinnedWindow[++scan] == pinnedWindow[++match]
                            && pinnedWindow[++scan] == pinnedWindow[++match]
                            && pinnedWindow[++scan] == pinnedWindow[++match]
                            && pinnedWindow[++scan] == pinnedWindow[++match]
                            && pinnedWindow[++scan] == pinnedWindow[++match])
                        {
                            break;
                        }

                        break;

                    case 7:
                        if (pinnedWindow[++scan] == pinnedWindow[++match]
                            && pinnedWindow[++scan] == pinnedWindow[++match]
                            && pinnedWindow[++scan] == pinnedWindow[++match]
                            && pinnedWindow[++scan] == pinnedWindow[++match]
                            && pinnedWindow[++scan] == pinnedWindow[++match]
                            && pinnedWindow[++scan] == pinnedWindow[++match]
                            && pinnedWindow[++scan] == pinnedWindow[++match])
                        {
                            break;
                        }

                        break;
                }

                if (pinnedWindow[scan] == pinnedWindow[match])
                {
                    // We check for insufficient lookahead only every 8th comparison;
                    // the 256th check will be made at strstart + 258 unless lookahead is
                    // exhausted first.
                    do
                    {
                        if (scan == scanMax)
                        {
                            ++scan; // advance to first position not matched
                            ++match;

                            break;
                        }
                    }
                    while (pinnedWindow[++scan] == pinnedWindow[++match]
                           && pinnedWindow[++scan] == pinnedWindow[++match]
                           && pinnedWindow[++scan] == pinnedWindow[++match]
                           && pinnedWindow[++scan] == pinnedWindow[++match]
                           && pinnedWindow[++scan] == pinnedWindow[++match]
                           && pinnedWindow[++scan] == pinnedWindow[++match]
                           && pinnedWindow[++scan] == pinnedWindow[++match]
                           && pinnedWindow[++scan] == pinnedWindow[++match]);
                }

                if (scan - scanStart > matchLength)
                {
                    matchStrt = curMatch;
                    matchLength = scan - scanStart;

                    if (matchLength >= niceLength)
                    {
                        break;
                    }

                    scanEnd1 = pinnedWindow[scan - 1];
                    scanEnd = pinnedWindow[scan];
                }
            }
            while ((curMatch = pinnedPrev[curMatch & DeflaterConstants.WMASK] & 0xFFFF) > limit && --chainLength != 0);

            this.matchStart = matchStrt;
            this.matchLen = matchLength;
            return matchLength >= DeflaterConstants.MIN_MATCH;
        }

        private bool DeflateStored(bool flush, bool finish)
        {
            if (!flush && (this.lookahead == 0))
            {
                return false;
            }

            this.strstart += this.lookahead;
            this.lookahead = 0;

            int storedLength = this.strstart - this.blockStart;

            if ((storedLength >= DeflaterConstants.MAX_BLOCK_SIZE) || // Block is full
                (this.blockStart < DeflaterConstants.WSIZE && storedLength >= DeflaterConstants.MAX_DIST) || // Block may move out of window
                flush)
            {
                bool lastBlock = finish;
                if (storedLength > DeflaterConstants.MAX_BLOCK_SIZE)
                {
                    storedLength = DeflaterConstants.MAX_BLOCK_SIZE;
                    lastBlock = false;
                }

                this.huffman.FlushStoredBlock(this.window, this.blockStart, storedLength, lastBlock);
                this.blockStart += storedLength;
                return !(lastBlock || storedLength == 0);
            }

            return true;
        }

        private bool DeflateFast(bool flush, bool finish)
        {
            if (this.lookahead < DeflaterConstants.MIN_LOOKAHEAD && !flush)
            {
                return false;
            }

            while (this.lookahead >= DeflaterConstants.MIN_LOOKAHEAD || flush)
            {
                if (this.lookahead == 0)
                {
                    // We are flushing everything
                    this.huffman.FlushBlock(this.window, this.blockStart, this.strstart - this.blockStart, finish);
                    this.blockStart = this.strstart;
                    return false;
                }

                if (this.strstart > (2 * DeflaterConstants.WSIZE) - DeflaterConstants.MIN_LOOKAHEAD)
                {
                    // slide window, as FindLongestMatch needs this.
                    // This should only happen when flushing and the window
                    // is almost full.
                    this.SlideWindow();
                }

                int hashHead;
                if (this.lookahead >= DeflaterConstants.MIN_MATCH &&
                    (hashHead = this.InsertString()) != 0 &&
                    this.strategy != DeflateStrategy.HuffmanOnly &&
                    this.strstart - hashHead <= DeflaterConstants.MAX_DIST &&
                    this.FindLongestMatch(hashHead))
                {
                    // longestMatch sets matchStart and matchLen
                    bool full = this.huffman.TallyDist(this.strstart - this.matchStart, this.matchLen);

                    this.lookahead -= this.matchLen;
                    if (this.matchLen <= this.maxLazy && this.lookahead >= DeflaterConstants.MIN_MATCH)
                    {
                        while (--this.matchLen > 0)
                        {
                            ++this.strstart;
                            this.InsertString();
                        }

                        ++this.strstart;
                    }
                    else
                    {
                        this.strstart += this.matchLen;
                        if (this.lookahead >= DeflaterConstants.MIN_MATCH - 1)
                        {
                            this.UpdateHash();
                        }
                    }

                    this.matchLen = DeflaterConstants.MIN_MATCH - 1;
                    if (!full)
                    {
                        continue;
                    }
                }
                else
                {
                    // No match found
                    this.huffman.TallyLit(this.pinnedWindowPointer[this.strstart] & 0xff);
                    ++this.strstart;
                    --this.lookahead;
                }

                if (this.huffman.IsFull())
                {
                    bool lastBlock = finish && (this.lookahead == 0);
                    this.huffman.FlushBlock(this.window, this.blockStart, this.strstart - this.blockStart, lastBlock);
                    this.blockStart = this.strstart;
                    return !lastBlock;
                }
            }

            return true;
        }

        private bool DeflateSlow(bool flush, bool finish)
        {
            if (this.lookahead < DeflaterConstants.MIN_LOOKAHEAD && !flush)
            {
                return false;
            }

            while (this.lookahead >= DeflaterConstants.MIN_LOOKAHEAD || flush)
            {
                if (this.lookahead == 0)
                {
                    if (this.prevAvailable)
                    {
                        this.huffman.TallyLit(this.pinnedWindowPointer[this.strstart - 1] & 0xff);
                    }

                    this.prevAvailable = false;

                    // We are flushing everything
                    this.huffman.FlushBlock(this.window, this.blockStart, this.strstart - this.blockStart, finish);
                    this.blockStart = this.strstart;
                    return false;
                }

                if (this.strstart >= (2 * DeflaterConstants.WSIZE) - DeflaterConstants.MIN_LOOKAHEAD)
                {
                    // slide window, as FindLongestMatch needs this.
                    // This should only happen when flushing and the window
                    // is almost full.
                    this.SlideWindow();
                }

                int prevMatch = this.matchStart;
                int prevLen = this.matchLen;
                if (this.lookahead >= DeflaterConstants.MIN_MATCH)
                {
                    int hashHead = this.InsertString();

                    if (this.strategy != DeflateStrategy.HuffmanOnly &&
                        hashHead != 0 &&
                        this.strstart - hashHead <= DeflaterConstants.MAX_DIST &&
                        this.FindLongestMatch(hashHead))
                    {
                        // longestMatch sets matchStart and matchLen
                        // Discard match if too small and too far away
                        if (this.matchLen <= 5 && (this.strategy == DeflateStrategy.Filtered || (this.matchLen == DeflaterConstants.MIN_MATCH && this.strstart - this.matchStart > TooFar)))
                        {
                            this.matchLen = DeflaterConstants.MIN_MATCH - 1;
                        }
                    }
                }

                // previous match was better
                if ((prevLen >= DeflaterConstants.MIN_MATCH) && (this.matchLen <= prevLen))
                {
                    this.huffman.TallyDist(this.strstart - 1 - prevMatch, prevLen);
                    prevLen -= 2;
                    do
                    {
                        this.strstart++;
                        this.lookahead--;
                        if (this.lookahead >= DeflaterConstants.MIN_MATCH)
                        {
                            this.InsertString();
                        }
                    }
                    while (--prevLen > 0);

                    this.strstart++;
                    this.lookahead--;
                    this.prevAvailable = false;
                    this.matchLen = DeflaterConstants.MIN_MATCH - 1;
                }
                else
                {
                    if (this.prevAvailable)
                    {
                        this.huffman.TallyLit(this.pinnedWindowPointer[this.strstart - 1] & 0xff);
                    }

                    this.prevAvailable = true;
                    this.strstart++;
                    this.lookahead--;
                }

                if (this.huffman.IsFull())
                {
                    int len = this.strstart - this.blockStart;
                    if (this.prevAvailable)
                    {
                        len--;
                    }

                    bool lastBlock = finish && (this.lookahead == 0) && !this.prevAvailable;
                    this.huffman.FlushBlock(this.window, this.blockStart, len, lastBlock);
                    this.blockStart += len;
                    return !lastBlock;
                }
            }

            return true;
        }
    }
}
