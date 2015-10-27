namespace ImageProcessor.Formats
{
    using System;

    /// <summary>
    /// Low level compression engine for deflate algorithm which uses a 32K sliding window
    /// with secondary compression from Huffman/Shannon-Fano codes.
    /// </summary>
    /// <remarks>
    /// DEFLATE ALGORITHM:
    ///
    /// The uncompressed stream is inserted into the window array.  When
    /// the window array is full the first half is thrown away and the
    /// second half is copied to the beginning.
    ///
    /// The head array is a hash table.  Three characters build a hash value
    /// and they the value points to the corresponding index in window of
    /// the last string with this hash.  The prev array implements a
    /// linked list of matches with the same hash: prev[index &amp; WMASK] points
    /// to the previous index with the same hash.
    /// </remarks>
    public class DeflaterEngine : DeflaterConstants
    {
        /// <summary>
        /// ne more than the maximum upper bounds.
        /// </summary>
        private const int TooFar = 4096;

        /// <summary>
        /// <code>prev[index &amp; WMASK]</code> points to the previous index that has the
        /// same hash code as the string starting at index.  This way
        /// entries with the same hash code are in a linked list.
        /// Note that the array should really be unsigned short, so you need
        /// to and the values with 0xffff.
        /// </summary>
        private readonly short[] previousIndex;

        /// <summary>
        /// Hashtable, hashing three characters to an index for window, so
        /// that window[index]..window[index+2] have this hash code.
        /// Note that the array should really be unsigned short, so you need
        /// to and the values with 0xffff.
        /// </summary>
        private readonly short[] head;

        /// <summary>
        /// Hash index of string to be inserted.
        /// </summary>
        private int insertHashIndex;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeflaterEngine"/> class with a pending buffer.
        /// </summary>
        /// <param name="pending">The pending buffer to use</param>>
        public DeflaterEngine(DeflaterPending pending)
        {
            this.pending = pending;
            this.huffman = new DeflaterHuffman(pending);
            this.adler = new Adler32();

            this.window = new byte[2 * WSIZE];
            this.head = new short[HASH_SIZE];
            this.previousIndex = new short[WSIZE];

            // We start at index 1, to avoid an implementation deficiency, that
            // we cannot build a repeat pattern at index 0.
            this.blockStart = this.strstart = 1;
        }

        /// <summary>
        /// Get current value of Adler checksum
        /// </summary>
        public int Adler => unchecked((int)this.adler.Value);

        /// <summary>
        /// Total data processed
        /// </summary>
        public long TotalIn => this.totalIn;

        /// <summary>
        /// Get or sets the <see cref="DeflateStrategy"/>
        /// </summary>
        public DeflateStrategy Strategy { get; set; }

        /// <summary>
        /// Deflate drives actual compression of data
        /// </summary>
        /// <param name="flush">True to flush input buffers</param>
        /// <param name="finish">Finish deflation with the current input.</param>
        /// <returns>Returns true if progress has been made.</returns>
        public bool Deflate(bool flush, bool finish)
        {
            bool progress;
            do
            {
                this.FillWindow();
                bool canFlush = flush && (this.inputOff == this.inputEnd);

                switch (this.compressionFunction)
                {
                    case DEFLATE_STORED:
                        progress = this.DeflateStored(canFlush, finish);
                        break;
                    case DEFLATE_FAST:
                        progress = this.DeflateFast(canFlush, finish);
                        break;
                    case DEFLATE_SLOW:
                        progress = this.DeflateSlow(canFlush, finish);
                        break;
                    default:
                        throw new InvalidOperationException("unknown compressionFunction");
                }
            }
            while (this.pending.IsFlushed && progress); // repeat while we have no pending output and progress was made
            return progress;
        }

        /// <summary>
        /// Sets input data to be deflated.  Should only be called when <code>NeedsInput()</code>
        /// returns true
        /// </summary>
        /// <param name="buffer">The buffer containing input data.</param>
        /// <param name="offset">The offset of the first byte of data.</param>
        /// <param name="count">The number of bytes of data to use as input.</param>
        public void SetInput(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (this.inputOff < this.inputEnd)
            {
                throw new InvalidOperationException("Old input was not completely processed");
            }

            int end = offset + count;

            // We want to throw an ArrayIndexOutOfBoundsException early.  The
            // check is very tricky: it also handles integer wrap around.
            if ((offset > end) || (end > buffer.Length))
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            this.inputBuf = buffer;
            this.inputOff = offset;
            this.inputEnd = end;
        }

        /// <summary>
        /// Determines if more <see cref="SetInput">input</see> is needed.
        /// </summary>
        /// <returns>Return true if input is needed via <see cref="SetInput">SetInput</see></returns>
        public bool NeedsInput()
        {
            return this.inputEnd == this.inputOff;
        }

        /// <summary>
        /// Set compression dictionary
        /// </summary>
        /// <param name="buffer">The buffer containing the dictionary data</param>
        /// <param name="offset">The offset in the buffer for the first byte of data</param>
        /// <param name="length">The length of the dictionary data.</param>
        public void SetDictionary(byte[] buffer, int offset, int length)
        {
            this.adler.Update(buffer, offset, length);
            if (length < MIN_MATCH)
            {
                return;
            }

            if (length > MAX_DIST)
            {
                offset += length - MAX_DIST;
                length = MAX_DIST;
            }

            Array.Copy(buffer, offset, this.window, this.strstart, length);

            this.UpdateHash();
            --length;
            while (--length > 0)
            {
                this.InsertString();
                this.strstart++;
            }

            this.strstart += 2;
            this.blockStart = this.strstart;
        }

        /// <summary>
        /// Reset internal state
        /// </summary>
        public void Reset()
        {
            this.huffman.Reset();
            this.adler.Reset();
            this.blockStart = this.strstart = 1;
            this.lookahead = 0;
            this.totalIn = 0;
            this.prevAvailable = false;
            this.matchLen = MIN_MATCH - 1;

            for (int i = 0; i < HASH_SIZE; i++)
            {
                this.head[i] = 0;
            }

            for (int i = 0; i < WSIZE; i++)
            {
                this.previousIndex[i] = 0;
            }
        }

        /// <summary>
        /// Reset Adler checksum
        /// </summary>
        public void ResetAdler()
        {
            this.adler.Reset();
        }

        /// <summary>
        /// Set the deflate level (0-9)
        /// </summary>
        /// <param name="level">The value to set the level to.</param>
        public void SetLevel(int level)
        {
            if ((level < 0) || (level > 9))
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }

            this.goodLength = GOOD_LENGTH[level];
            this.maxLazy = MAX_LAZY[level];
            this.niceLength = NICE_LENGTH[level];
            this.maxChain = MAX_CHAIN[level];

            if (COMPR_FUNC[level] != this.compressionFunction)
            {
                switch (this.compressionFunction)
                {
                    case DEFLATE_STORED:
                        if (this.strstart > this.blockStart)
                        {
                            this.huffman.FlushStoredBlock(this.window, this.blockStart, this.strstart - this.blockStart, false);
                            this.blockStart = this.strstart;
                        }

                        this.UpdateHash();
                        break;

                    case DEFLATE_FAST:
                        if (this.strstart > this.blockStart)
                        {
                            this.huffman.FlushBlock(this.window, this.blockStart, this.strstart - this.blockStart,
                                false);
                            this.blockStart = this.strstart;
                        }

                        break;

                    case DEFLATE_SLOW:
                        if (this.prevAvailable)
                        {
                            this.huffman.TallyLit(this.window[this.strstart - 1] & 0xff);
                        }

                        if (this.strstart > this.blockStart)
                        {
                            this.huffman.FlushBlock(this.window, this.blockStart, this.strstart - this.blockStart, false);
                            this.blockStart = this.strstart;
                        }

                        this.prevAvailable = false;
                        this.matchLen = MIN_MATCH - 1;
                        break;
                }

                this.compressionFunction = COMPR_FUNC[level];
            }
        }

        /// <summary>
        /// Fill the window
        /// </summary>
        public void FillWindow()
        {
            // If the window is almost full and there is insufficient lookahead,
            // move the upper half to the lower one to make room in the upper half.
            if (this.strstart >= WSIZE + MAX_DIST)
            {
                this.SlideWindow();
            }

            // If there is not enough lookahead, but still some input left,
            // read in the input
            while (this.lookahead < MIN_LOOKAHEAD && this.inputOff < this.inputEnd)
            {
                int more = (2 * WSIZE) - this.lookahead - this.strstart;

                if (more > this.inputEnd - this.inputOff)
                {
                    more = this.inputEnd - this.inputOff;
                }

                Array.Copy(this.inputBuf, this.inputOff, this.window, this.strstart + this.lookahead, more);
                this.adler.Update(this.inputBuf, this.inputOff, more);

                this.inputOff += more;
                this.totalIn += more;
                this.lookahead += more;
            }

            if (this.lookahead >= MIN_MATCH)
            {
                this.UpdateHash();
            }
        }

        private void UpdateHash()
        {
            this.insertHashIndex = (this.window[this.strstart] << HASH_SHIFT) ^ this.window[this.strstart + 1];
        }

        /// <summary>
        /// Inserts the current string in the head hash and returns the previous
        /// value for this hash.
        /// </summary>
        /// <returns>The previous hash value</returns>
        private int InsertString()
        {
            short match;
            int hash = ((this.insertHashIndex << HASH_SHIFT) ^ this.window[this.strstart + (MIN_MATCH - 1)]) & HASH_MASK;

            this.previousIndex[this.strstart & WMASK] = match = this.head[hash];
            this.head[hash] = unchecked((short)this.strstart);
            this.insertHashIndex = hash;
            return match & 0xffff;
        }

        private void SlideWindow()
        {
            Array.Copy(this.window, WSIZE, this.window, 0, WSIZE);
            this.matchStart -= WSIZE;
            this.strstart -= WSIZE;
            this.blockStart -= WSIZE;

            // Slide the hash table (could be avoided with 32 bit values
            // at the expense of memory usage).
            for (int i = 0; i < HASH_SIZE; ++i)
            {
                int m = this.head[i] & 0xffff;
                this.head[i] = (short)(m >= WSIZE ? (m - WSIZE) : 0);
            }

            // Slide the prev table.
            for (int i = 0; i < WSIZE; i++)
            {
                int m = this.previousIndex[i] & 0xffff;
                this.previousIndex[i] = (short)(m >= WSIZE ? (m - WSIZE) : 0);
            }
        }

        /// <summary>
        /// Find the best (longest) string in the window matching the
        /// string starting at strstart.
        ///
        /// Preconditions:
        /// <code>
        /// strstart + MAX_MATCH &lt;= window.length.</code>
        /// </summary>
        /// <param name="curMatch">The current match.</param>
        /// <returns>True if a match greater than the minimum length is found</returns>
        private bool FindLongestMatch(int curMatch)
        {
            int chainLength = this.maxChain;
            int length = this.niceLength;
            short[] previous = this.previousIndex;
            int scan = this.strstart;
            int bestEnd = this.strstart + this.matchLen;
            int bestLength = Math.Max(this.matchLen, MIN_MATCH - 1);

            int limit = Math.Max(this.strstart - MAX_DIST, 0);

            int strend = this.strstart + MAX_MATCH - 1;
            byte scanEnd1 = this.window[bestEnd - 1];
            byte scanEnd = this.window[bestEnd];

            // Do not waste too much time if we already have a good match:
            if (bestLength >= this.goodLength)
            {
                chainLength >>= 2;
            }

            // Do not look for matches beyond the end of the input. This is necessary
            // to make deflate deterministic.
            if (length > this.lookahead)
            {
                length = this.lookahead;
            }

            do
            {
                if (this.window[curMatch + bestLength] != scanEnd ||
                    this.window[curMatch + bestLength - 1] != scanEnd1 ||
                    this.window[curMatch] != this.window[scan] ||
                    this.window[curMatch + 1] != this.window[scan + 1])
                {
                    continue;
                }

                int match = curMatch + 2;
                scan += 2;

                // We check for insufficient lookahead only every 8th comparison;
                // the 256th check will be made at strstart + 258.
                while (
                    this.window[++scan] == this.window[++match] &&
                    this.window[++scan] == this.window[++match] &&
                    this.window[++scan] == this.window[++match] &&
                    this.window[++scan] == this.window[++match] &&
                    this.window[++scan] == this.window[++match] &&
                    this.window[++scan] == this.window[++match] &&
                    this.window[++scan] == this.window[++match] &&
                    this.window[++scan] == this.window[++match] &&
                    (scan < strend))
                {
                    // Do nothing
                }

                if (scan > bestEnd)
                {
                    this.matchStart = curMatch;
                    bestEnd = scan;
                    bestLength = scan - this.strstart;

                    if (bestLength >= length)
                    {
                        break;
                    }

                    scanEnd1 = this.window[bestEnd - 1];
                    scanEnd = this.window[bestEnd];
                }

                scan = this.strstart;
            } while ((curMatch = previous[curMatch & WMASK] & 0xffff) > limit && --chainLength != 0);

            this.matchLen = Math.Min(bestLength, this.lookahead);
            return this.matchLen >= MIN_MATCH;
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

            if ((storedLength >= MAX_BLOCK_SIZE) || // Block is full
                (this.blockStart < WSIZE && storedLength >= MAX_DIST) || // Block may move out of window
                flush)
            {
                bool lastBlock = finish;
                if (storedLength > MAX_BLOCK_SIZE)
                {
                    storedLength = MAX_BLOCK_SIZE;
                    lastBlock = false;
                }

                this.huffman.FlushStoredBlock(this.window, this.blockStart, storedLength, lastBlock);
                this.blockStart += storedLength;
                return !lastBlock;
            }

            return true;
        }

        private bool DeflateFast(bool flush, bool finish)
        {
            if (this.lookahead < MIN_LOOKAHEAD && !flush)
            {
                return false;
            }

            while (this.lookahead >= MIN_LOOKAHEAD || flush)
            {
                if (this.lookahead == 0)
                {
                    // We are flushing everything
                    this.huffman.FlushBlock(this.window, this.blockStart, this.strstart - this.blockStart, finish);
                    this.blockStart = this.strstart;
                    return false;
                }

                if (this.strstart > (2 * WSIZE) - MIN_LOOKAHEAD)
                {
                    /* slide window, as FindLongestMatch needs this.
                     * This should only happen when flushing and the window
                     * is almost full.
                     */
                    this.SlideWindow();
                }

                int hashHead;
                if (this.lookahead >= MIN_MATCH &&
                    (hashHead = this.InsertString()) != 0 &&
                    this.Strategy != DeflateStrategy.HuffmanOnly &&
                    this.strstart - hashHead <= MAX_DIST &&
                    this.FindLongestMatch(hashHead))
                {
                    // longestMatch sets matchStart and matchLen
                    bool full = this.huffman.TallyDist(this.strstart - this.matchStart, this.matchLen);

                    this.lookahead -= this.matchLen;
                    if (this.matchLen <= this.maxLazy && this.lookahead >= MIN_MATCH)
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
                        if (this.lookahead >= MIN_MATCH - 1)
                        {
                            this.UpdateHash();
                        }
                    }

                    this.matchLen = MIN_MATCH - 1;
                    if (!full)
                    {
                        continue;
                    }
                }
                else
                {
                    // No match found
                    this.huffman.TallyLit(this.window[this.strstart] & 0xff);
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
            if (this.lookahead < MIN_LOOKAHEAD && !flush)
            {
                return false;
            }

            while (this.lookahead >= MIN_LOOKAHEAD || flush)
            {
                if (this.lookahead == 0)
                {
                    if (this.prevAvailable)
                    {
                        this.huffman.TallyLit(this.window[this.strstart - 1] & 0xff);
                    }

                    this.prevAvailable = false;

                    // We are flushing everything
                    this.huffman.FlushBlock(this.window, this.blockStart, this.strstart - this.blockStart,
                        finish);
                    this.blockStart = this.strstart;
                    return false;
                }

                if (this.strstart >= (2 * WSIZE) - MIN_LOOKAHEAD)
                {
                    // slide window, as FindLongestMatch needs this.
                    // This should only happen when flushing and the window
                    // is almost full.
                    this.SlideWindow();
                }

                int prevMatch = this.matchStart;
                int prevLen = this.matchLen;
                if (this.lookahead >= MIN_MATCH)
                {

                    int hashHead = this.InsertString();

                    if (this.Strategy != DeflateStrategy.HuffmanOnly &&
                        hashHead != 0 &&
                        this.strstart - hashHead <= MAX_DIST &&
                        this.FindLongestMatch(hashHead))
                    {

                        // longestMatch sets matchStart and matchLen

                        // Discard match if too small and too far away
                        if (this.matchLen <= 5 && (this.Strategy == DeflateStrategy.Filtered || (this.matchLen == MIN_MATCH && this.strstart - this.matchStart > TooFar)))
                        {
                            this.matchLen = MIN_MATCH - 1;
                        }
                    }
                }

                // previous match was better
                if ((prevLen >= MIN_MATCH) && (this.matchLen <= prevLen))
                {
                    this.huffman.TallyDist(this.strstart - 1 - prevMatch, prevLen);
                    prevLen -= 2;
                    do
                    {
                        this.strstart++;
                        this.lookahead--;
                        if (this.lookahead >= MIN_MATCH)
                        {
                            this.InsertString();
                        }
                    } while (--prevLen > 0);

                    this.strstart++;
                    this.lookahead--;
                    this.prevAvailable = false;
                    this.matchLen = MIN_MATCH - 1;
                }
                else
                {
                    if (this.prevAvailable)
                    {
                        this.huffman.TallyLit(this.window[this.strstart - 1] & 0xff);
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
        /// This array contains the part of the uncompressed stream that
        /// is of relevance.  The current character is indexed by strstart.
        /// </summary>
        private byte[] window;

        private int maxChain;

        private int maxLazy;

        private int niceLength;

        private int goodLength;

        /// <summary>
        /// The current compression function.
        /// </summary>
        private int compressionFunction;

        /// <summary>
        /// The input data for compression.
        /// </summary>
        private byte[] inputBuf;

        /// <summary>
        /// The total bytes of input read.
        /// </summary>
        private long totalIn;

        /// <summary>
        /// The offset into inputBuf, where input data starts.
        /// </summary>
        private int inputOff;

        /// <summary>
        /// The end offset of the input data.
        /// </summary>
        private int inputEnd;

        private DeflaterPending pending;

        private DeflaterHuffman huffman;

        /// <summary>
        /// The adler checksum
        /// </summary>
        private Adler32 adler;
    }
}
