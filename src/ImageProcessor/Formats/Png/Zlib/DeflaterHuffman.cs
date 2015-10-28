namespace ImageProcessor.Formats
{
    using System;

    /// <summary>
    /// This is the DeflaterHuffman class.
    ///
    /// This class is <i>not</i> thread safe.  This is inherent in the API, due
    /// to the split of Deflate and SetInput.
    ///
    /// author of the original java version : Jochen Hoenicke
    /// </summary>
    public class DeflaterHuffman
    {
        /// <summary>
        /// The buffer size.
        /// </summary>
        private const int Buffersize = 1 << (DeflaterConstants.DefaultMemLevel + 6);

        /// <summary>
        /// The number of literals.
        /// </summary>
        private const int LiteralCount = 286;

        /// <summary>
        /// Number of distance codes
        /// </summary>
        private const int DistanceCodeCount = 30;

        /// <summary>
        /// Number of codes used to transfer bit lengths
        /// </summary>
        private const int BitLengthCount = 19;

        /// <summary>
        /// Repeat previous bit length 3-6 times (2 bits of repeat count)
        /// </summary>
        private const int Repeat3To6 = 16;

        /// <summary>
        /// Repeat a zero length 3-10 times  (3 bits of repeat count)
        /// </summary>
        private const int Repeat3To10 = 17;

        /// <summary>
        /// Repeat a zero length 11-138 times  (7 bits of repeat count)
        /// </summary>
        private const int Repeat11To138 = 18;

        /// <summary>
        /// The end of file flag.
        /// </summary>
        private const int Eof = 256;

        /// <summary>
        /// The lengths of the bit length codes are sent in order of decreasing
        /// probability, to avoid transmitting the lengths for unused bit length codes.
        /// </summary>
        private static readonly int[] BitLengthOrder = { 16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15 };

        /// <summary>
        /// Bit data reversed.
        /// </summary>
        private static readonly byte[] Bit4Reverse = { 0, 8, 4, 12, 2, 10, 6, 14, 1, 9, 5, 13, 3, 11, 7, 15 };

        /// <summary>
        /// The literal codes.
        /// </summary>
        private static short[] staticLCodes;
        private static byte[] staticLLength;
        private static short[] staticDCodes;
        private static byte[] staticDLength;

        /// <summary>
        /// A binary tree, with the property, that the parent node is smaller than both child nodes.
        /// </summary>
        private class Tree
        {
            /// <summary>
            /// The minimum number of codes.
            /// </summary>
            private readonly int minimumNumberOfCodes;

            /// <summary>
            /// The array of codes.
            /// </summary>
            private short[] codes;
            private readonly int[] blCounts;
            private readonly int maxLength;
            private readonly DeflaterHuffman deflater;

            /// <summary>
            /// Initializes a new instance of the <see cref="Tree"/> class.
            /// </summary>
            /// <param name="huffman">The <see cref="DeflaterHuffman"/></param>
            /// <param name="elems">The elements.</param>
            /// <param name="minCodes">The minimum number of codes.</param>
            /// <param name="maxLength">The maximum length.</param>
            public Tree(DeflaterHuffman huffman, int elems, int minCodes, int maxLength)
            {
                this.deflater = huffman;
                this.minimumNumberOfCodes = minCodes;
                this.maxLength = maxLength;
                this.Frequencies = new short[elems];
                this.blCounts = new int[maxLength];
            }

            /// <summary>
            /// Gets the number of codes.
            /// </summary>
            public int NumberOfCodes { get; private set; }

            /// <summary>
            /// Gets the frequencies.
            /// </summary>
            public short[] Frequencies { get; }

            /// <summary>
            /// Gets or sets the length.
            /// </summary>
            public byte[] Length { get; private set; }

            /// <summary>
            /// Resets the internal state of the tree
            /// </summary>
            public void Reset()
            {
                for (int i = 0; i < this.Frequencies.Length; i++)
                {
                    this.Frequencies[i] = 0;
                }

                this.codes = null;
                this.Length = null;
            }

            /// <summary>
            /// Writes a code symbol.
            /// </summary>
            /// <param name="code">The code index.</param>
            public void WriteSymbol(int code)
            {
                this.deflater.pending.WriteBits(this.codes[code] & 0xffff, this.Length[code]);
            }

            /// <summary>
            /// Set static codes and length
            /// </summary>
            /// <param name="staticCodes">new codes</param>
            /// <param name="staticLengths">length for new codes</param>
            public void SetStaticCodes(short[] staticCodes, byte[] staticLengths)
            {
                this.codes = staticCodes;
                this.Length = staticLengths;
            }

            /// <summary>
            /// Build dynamic codes and lengths
            /// </summary>
            public void BuildCodes()
            {
                int numSymbols = this.Frequencies.Length;
                int[] nextCode = new int[this.maxLength];
                int code = 0;

                this.codes = new short[numSymbols];

                for (int bits = 0; bits < this.maxLength; bits++)
                {
                    nextCode[bits] = code;
                    code += this.blCounts[bits] << (15 - bits);
                }

                for (int i = 0; i < this.NumberOfCodes; i++)
                {
                    int bits = this.Length[i];
                    if (bits > 0)
                    {
                        this.codes[i] = BitReverse(nextCode[bits - 1]);
                        nextCode[bits - 1] += 1 << (16 - bits);
                    }
                }
            }

            public void BuildTree()
            {
                int numSymbols = this.Frequencies.Length;

                // Heap is a priority queue, sorted by frequency, least frequent
                // nodes first.  The heap is a binary tree, with the property, that
                // the parent node is smaller than both child nodes.  This assures
                // that the smallest node is the first parent.
                //
                // The binary tree is encoded in an array:  0 is root node and
                // the nodes 2//n+1, 2//n+2 are the child nodes of node n.
                int[] heap = new int[numSymbols];
                int heapLen = 0;
                int maxCode = 0;
                for (int n = 0; n < numSymbols; n++)
                {
                    int freq = this.Frequencies[n];
                    if (freq != 0)
                    {
                        // Insert n into heap
                        int pos = heapLen++;
                        int ppos;
                        while (pos > 0 && this.Frequencies[heap[ppos = (pos - 1) / 2]] > freq)
                        {
                            heap[pos] = heap[ppos];
                            pos = ppos;
                        }

                        heap[pos] = n;

                        maxCode = n;
                    }
                }

                // We could encode a single literal with 0 bits but then we
                // don't see the literals.  Therefore we force at least two
                // literals to avoid this case.  We don't care about order in
                // this case, both literals get a 1 bit code.
                while (heapLen < 2)
                {
                    int node = maxCode < 2 ? ++maxCode : 0;
                    heap[heapLen++] = node;
                }

                this.NumberOfCodes = Math.Max(maxCode + 1, this.minimumNumberOfCodes);

                int numLeafs = heapLen;
                int[] childs = new int[(4 * heapLen) - 2];
                int[] values = new int[(2 * heapLen) - 1];
                int numNodes = numLeafs;
                for (int i = 0; i < heapLen; i++)
                {
                    int node = heap[i];
                    childs[2 * i] = node;
                    childs[(2 * i) + 1] = -1;
                    values[i] = this.Frequencies[node] << 8;
                    heap[i] = i;
                }

                // Construct the Huffman tree by repeatedly combining the least two
                // frequent nodes.
                do
                {
                    int first = heap[0];
                    int last = heap[--heapLen];

                    // Propagate the hole to the leafs of the heap
                    int ppos = 0;
                    int path = 1;

                    while (path < heapLen)
                    {
                        if (path + 1 < heapLen && values[heap[path]] > values[heap[path + 1]])
                        {
                            path++;
                        }

                        heap[ppos] = heap[path];
                        ppos = path;
                        path = (path * 2) + 1;
                    }

                    // Now propagate the last element down along path.  Normally
                    // it shouldn't go too deep.
                    int lastVal = values[last];
                    while ((path = ppos) > 0 && values[heap[ppos = (path - 1) / 2]] > lastVal)
                    {
                        heap[path] = heap[ppos];
                    }

                    heap[path] = last;

                    int second = heap[0];

                    // Create a new node father of first and second
                    last = numNodes++;
                    childs[2 * last] = first;
                    childs[(2 * last) + 1] = second;
                    int mindepth = Math.Min(values[first] & 0xff, values[second] & 0xff);
                    values[last] = lastVal = values[first] + values[second] - mindepth + 1;

                    // Again, propagate the hole to the leafs
                    ppos = 0;
                    path = 1;

                    while (path < heapLen)
                    {
                        if (path + 1 < heapLen && values[heap[path]] > values[heap[path + 1]])
                        {
                            path++;
                        }

                        heap[ppos] = heap[path];
                        ppos = path;
                        path = (ppos * 2) + 1;
                    }

                    // Now propagate the new element down along path
                    while ((path = ppos) > 0 && values[heap[ppos = (path - 1) / 2]] > lastVal)
                    {
                        heap[path] = heap[ppos];
                    }

                    heap[path] = last;
                }
                while (heapLen > 1);

                if (heap[0] != (childs.Length / 2) - 1)
                {
                    throw new ImageFormatException("Heap invariant violated");
                }

                this.BuildLength(childs);
            }

            /// <summary>
            /// Get encoded length
            /// </summary>
            /// <returns>Encoded length, the sum of frequencies * lengths</returns>
            public int GetEncodedLength()
            {
                int len = 0;
                for (int i = 0; i < this.Frequencies.Length; i++)
                {
                    len += this.Frequencies[i] * this.Length[i];
                }

                return len;
            }

            /// <summary>
            /// Scan a literal or distance tree to determine the frequencies of the codes
            /// in the bit length tree.
            /// </summary>
            public void CalcBLFreq(Tree blTree)
            {
                int max_count;               /* max repeat count */
                int min_count;               /* min repeat count */
                int count;                   /* repeat count of the current code */
                int curlen = -1;             /* length of current code */

                int i = 0;
                while (i < this.NumberOfCodes)
                {
                    count = 1;
                    int nextlen = this.Length[i];
                    if (nextlen == 0)
                    {
                        max_count = 138;
                        min_count = 3;
                    }
                    else
                    {
                        max_count = 6;
                        min_count = 3;
                        if (curlen != nextlen)
                        {
                            blTree.Frequencies[nextlen]++;
                            count = 0;
                        }
                    }

                    curlen = nextlen;
                    i++;

                    while (i < this.NumberOfCodes && curlen == this.Length[i])
                    {
                        i++;
                        if (++count >= max_count)
                        {
                            break;
                        }
                    }

                    if (count < min_count)
                    {
                        blTree.Frequencies[curlen] += (short)count;
                    }
                    else if (curlen != 0)
                    {
                        blTree.Frequencies[Repeat3To6]++;
                    }
                    else if (count <= 10)
                    {
                        blTree.Frequencies[Repeat3To10]++;
                    }
                    else
                    {
                        blTree.Frequencies[Repeat11To138]++;
                    }
                }
            }

            /// <summary>
            /// Write tree values
            /// </summary>
            /// <param name="blTree">Tree to write</param>
            public void WriteTree(Tree blTree)
            {
                int max_count;               // max repeat count
                int min_count;               // min repeat count
                int count;                   // repeat count of the current code
                int curlen = -1;             // length of current code

                int i = 0;
                while (i < this.NumberOfCodes)
                {
                    count = 1;
                    int nextlen = this.Length[i];
                    if (nextlen == 0)
                    {
                        max_count = 138;
                        min_count = 3;
                    }
                    else
                    {
                        max_count = 6;
                        min_count = 3;
                        if (curlen != nextlen)
                        {
                            blTree.WriteSymbol(nextlen);
                            count = 0;
                        }
                    }

                    curlen = nextlen;
                    i++;

                    while (i < this.NumberOfCodes && curlen == this.Length[i])
                    {
                        i++;
                        if (++count >= max_count)
                        {
                            break;
                        }
                    }

                    if (count < min_count)
                    {
                        while (count-- > 0)
                        {
                            blTree.WriteSymbol(curlen);
                        }
                    }
                    else if (curlen != 0)
                    {
                        blTree.WriteSymbol(Repeat3To6);
                        this.deflater.pending.WriteBits(count - 3, 2);
                    }
                    else if (count <= 10)
                    {
                        blTree.WriteSymbol(Repeat3To10);
                        this.deflater.pending.WriteBits(count - 3, 3);
                    }
                    else
                    {
                        blTree.WriteSymbol(Repeat11To138);
                        this.deflater.pending.WriteBits(count - 11, 7);
                    }
                }
            }

            private void BuildLength(int[] childs)
            {
                this.Length = new byte[this.Frequencies.Length];
                int numNodes = childs.Length / 2;
                int numLeafs = (numNodes + 1) / 2;
                int overflow = 0;

                for (int i = 0; i < this.maxLength; i++)
                {
                    this.blCounts[i] = 0;
                }

                // First calculate optimal bit lengths
                int[] lengths = new int[numNodes];
                lengths[numNodes - 1] = 0;

                for (int i = numNodes - 1; i >= 0; i--)
                {
                    if (childs[(2 * i) + 1] != -1)
                    {
                        int bitLength = lengths[i] + 1;
                        if (bitLength > this.maxLength)
                        {
                            bitLength = this.maxLength;
                            overflow++;
                        }

                        lengths[childs[2 * i]] = lengths[childs[(2 * i) + 1]] = bitLength;
                    }
                    else
                    {
                        // A leaf node
                        int bitLength = lengths[i];
                        this.blCounts[bitLength - 1]++;
                        this.Length[childs[2 * i]] = (byte)lengths[i];
                    }
                }

                if (overflow == 0)
                {
                    return;
                }

                int incrBitLen = this.maxLength - 1;
                do
                {
                    // Find the first bit length which could increase:
                    while (this.blCounts[--incrBitLen] == 0)
                    {
                    }

                    // Move this node one down and remove a corresponding
                    // number of overflow nodes.
                    do
                    {
                        this.blCounts[incrBitLen]--;
                        this.blCounts[++incrBitLen]++;
                        overflow -= 1 << (this.maxLength - 1 - incrBitLen);
                    }
                    while (overflow > 0 && incrBitLen < this.maxLength - 1);
                }
                while (overflow > 0);

                // We may have overshot above.  Move some nodes from maxLength to
                // maxLength-1 in that case.
                this.blCounts[this.maxLength - 1] += overflow;
                this.blCounts[this.maxLength - 2] -= overflow;

                // Now recompute all bit lengths, scanning in increasing
                // frequency.  It is simpler to reconstruct all lengths instead of
                // fixing only the wrong ones. This idea is taken from 'ar'
                // written by Haruhiko Okumura.
                // The nodes were inserted with decreasing frequency into the childs
                // array.
                int nodePtr = 2 * numLeafs;
                for (int bits = this.maxLength; bits != 0; bits--)
                {
                    int n = this.blCounts[bits - 1];
                    while (n > 0)
                    {
                        int childPtr = 2 * childs[nodePtr++];
                        if (childs[childPtr + 1] == -1)
                        {
                            // We found another leaf
                            this.Length[childs[childPtr]] = (byte)bits;
                            n--;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Pending buffer to use
        /// </summary>
        public DeflaterPending pending;

        Tree literalTree;
        Tree distTree;
        Tree blTree;

        // Buffer for distances
        short[] d_buf;
        byte[] l_buf;
        int last_lit;
        int extra_bits;

        static DeflaterHuffman()
        {
            // See RFC 1951 3.2.6
            // Literal codes
            staticLCodes = new short[LiteralCount];
            staticLLength = new byte[LiteralCount];

            int i = 0;
            while (i < 144)
            {
                staticLCodes[i] = BitReverse((0x030 + i) << 8);
                staticLLength[i++] = 8;
            }

            while (i < 256)
            {
                staticLCodes[i] = BitReverse((0x190 - 144 + i) << 7);
                staticLLength[i++] = 9;
            }

            while (i < 280)
            {
                staticLCodes[i] = BitReverse((0x000 - 256 + i) << 9);
                staticLLength[i++] = 7;
            }

            while (i < LiteralCount)
            {
                staticLCodes[i] = BitReverse((0x0c0 - 280 + i) << 8);
                staticLLength[i++] = 8;
            }

            // Distance codes
            staticDCodes = new short[DistanceCodeCount];
            staticDLength = new byte[DistanceCodeCount];
            for (i = 0; i < DistanceCodeCount; i++)
            {
                staticDCodes[i] = BitReverse(i << 11);
                staticDLength[i] = 5;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeflaterHuffman"/> class with a pending buffer.
        /// </summary>
        /// <param name="pending">Pending buffer to use</param>
        public DeflaterHuffman(DeflaterPending pending)
        {
            this.pending = pending;

            this.literalTree = new Tree(this, LiteralCount, 257, 15);
            this.distTree = new Tree(this, DistanceCodeCount, 1, 15);
            this.blTree = new Tree(this, BitLengthCount, 4, 7);

            this.d_buf = new short[Buffersize];
            this.l_buf = new byte[Buffersize];
        }

        /// <summary>
        /// Reverse the bits of a 16 bit value.
        /// </summary>
        /// <param name="toReverse">Value to reverse bits</param>
        /// <returns>Value with bits reversed</returns>
        public static short BitReverse(int toReverse)
        {
            return (short)(Bit4Reverse[toReverse & 0xF] << 12 |
                            Bit4Reverse[(toReverse >> 4) & 0xF] << 8 |
                            Bit4Reverse[(toReverse >> 8) & 0xF] << 4 |
                            Bit4Reverse[toReverse >> 12]);
        }

        /// <summary>
        /// Resets the internal state
        /// </summary>
        public void Reset()
        {
            this.last_lit = 0;
            this.extra_bits = 0;
            this.literalTree.Reset();
            this.distTree.Reset();
            this.blTree.Reset();
        }

        /// <summary>
        /// Write all trees to pending buffer
        /// </summary>
        /// <param name="blTreeCodes">The number/rank of treecodes to send.</param>
        public void SendAllTrees(int blTreeCodes)
        {
            this.blTree.BuildCodes();
            this.literalTree.BuildCodes();
            this.distTree.BuildCodes();
            this.pending.WriteBits(this.literalTree.NumberOfCodes - 257, 5);
            this.pending.WriteBits(this.distTree.NumberOfCodes - 1, 5);
            this.pending.WriteBits(blTreeCodes - 4, 4);

            for (int rank = 0; rank < blTreeCodes; rank++)
            {
                this.pending.WriteBits(this.blTree.Length[BitLengthOrder[rank]], 3);
            }

            this.literalTree.WriteTree(this.blTree);
            this.distTree.WriteTree(this.blTree);
        }

        /// <summary>
        /// Compress current buffer writing data to pending buffer
        /// </summary>
        public void CompressBlock()
        {
            for (int i = 0; i < this.last_lit; i++)
            {
                int litlen = this.l_buf[i] & 0xff;
                int dist = this.d_buf[i];
                if (dist-- != 0)
                {
                    int lc = Lcode(litlen);
                    this.literalTree.WriteSymbol(lc);

                    int bits = (lc - 261) / 4;
                    if (bits > 0 && bits <= 5)
                    {
                        this.pending.WriteBits(litlen & ((1 << bits) - 1), bits);
                    }

                    int dc = Dcode(dist);
                    this.distTree.WriteSymbol(dc);

                    bits = (dc / 2) - 1;
                    if (bits > 0)
                    {
                        this.pending.WriteBits(dist & ((1 << bits) - 1), bits);
                    }
                }
                else
                {
                    this.literalTree.WriteSymbol(litlen);
                }
            }

            this.literalTree.WriteSymbol(Eof);
        }

        /// <summary>
        /// Flush block to output with no compression
        /// </summary>
        /// <param name="stored">Data to write</param>
        /// <param name="storedOffset">Index of first byte to write</param>
        /// <param name="storedLength">Count of bytes to write</param>
        /// <param name="lastBlock">True if this is the last block</param>
        public void FlushStoredBlock(byte[] stored, int storedOffset, int storedLength, bool lastBlock)
        {
            this.pending.WriteBits((DeflaterConstants.StoredBlock << 1) + (lastBlock ? 1 : 0), 3);
            this.pending.AlignToByte();
            this.pending.WriteShort(storedLength);
            this.pending.WriteShort(~storedLength);
            this.pending.WriteBlock(stored, storedOffset, storedLength);
            this.Reset();
        }

        /// <summary>
        /// Flush block to output with compression
        /// </summary>
        /// <param name="stored">Data to flush</param>
        /// <param name="storedOffset">Index of first byte to flush</param>
        /// <param name="storedLength">Count of bytes to flush</param>
        /// <param name="lastBlock">True if this is the last block</param>
        public void FlushBlock(byte[] stored, int storedOffset, int storedLength, bool lastBlock)
        {
            this.literalTree.Frequencies[Eof]++;

            // Build trees
            this.literalTree.BuildTree();
            this.distTree.BuildTree();

            // Calculate bitlen frequency
            this.literalTree.CalcBLFreq(this.blTree);
            this.distTree.CalcBLFreq(this.blTree);

            // Build bitlen tree
            this.blTree.BuildTree();

            int blTreeCodes = 4;
            for (int i = 18; i > blTreeCodes; i--)
            {
                if (this.blTree.Length[BitLengthOrder[i]] > 0)
                {
                    blTreeCodes = i + 1;
                }
            }

            int opt_len = 14 + (blTreeCodes * 3) + this.blTree.GetEncodedLength() +
                this.literalTree.GetEncodedLength() + this.distTree.GetEncodedLength() +
                this.extra_bits;

            int static_len = this.extra_bits;
            for (int i = 0; i < LiteralCount; i++)
            {
                static_len += this.literalTree.Frequencies[i] * staticLLength[i];
            }

            for (int i = 0; i < DistanceCodeCount; i++)
            {
                static_len += this.distTree.Frequencies[i] * staticDLength[i];
            }

            if (opt_len >= static_len)
            {
                // Force static trees
                opt_len = static_len;
            }

            if (storedOffset >= 0 && storedLength + 4 < opt_len >> 3)
            {
                // Store Block
                this.FlushStoredBlock(stored, storedOffset, storedLength, lastBlock);
            }
            else if (opt_len == static_len)
            {
                // Encode with static tree
                this.pending.WriteBits((DeflaterConstants.StaticTrees << 1) + (lastBlock ? 1 : 0), 3);
                this.literalTree.SetStaticCodes(staticLCodes, staticLLength);
                this.distTree.SetStaticCodes(staticDCodes, staticDLength);
                this.CompressBlock();
                this.Reset();
            }
            else
            {
                // Encode with dynamic tree
                this.pending.WriteBits((DeflaterConstants.DynTrees << 1) + (lastBlock ? 1 : 0), 3);
                this.SendAllTrees(blTreeCodes);
                this.CompressBlock();
                this.Reset();
            }
        }

        /// <summary>
        /// Get value indicating if internal buffer is full
        /// </summary>
        /// <returns>true if buffer is full</returns>
        public bool IsFull()
        {
            return this.last_lit >= Buffersize;
        }

        /// <summary>
        /// Add literal to buffer
        /// </summary>
        /// <param name="literal">Literal value to add to buffer.</param>
        /// <returns>Value indicating internal buffer is full</returns>
        public bool TallyLit(int literal)
        {
            this.d_buf[this.last_lit] = 0;
            this.l_buf[this.last_lit++] = (byte)literal;
            this.literalTree.Frequencies[literal]++;
            return this.IsFull();
        }

        /// <summary>
        /// Add distance code and length to literal and distance trees
        /// </summary>
        /// <param name="distance">Distance code</param>
        /// <param name="length">Length</param>
        /// <returns>Value indicating if internal buffer is full</returns>
        public bool TallyDist(int distance, int length)
        {
            this.d_buf[this.last_lit] = (short)distance;
            this.l_buf[this.last_lit++] = (byte)(length - 3);

            int lc = Lcode(length - 3);
            this.literalTree.Frequencies[lc]++;
            if (lc >= 265 && lc < 285)
            {
                this.extra_bits += (lc - 261) / 4;
            }

            int dc = Dcode(distance - 1);
            this.distTree.Frequencies[dc]++;
            if (dc >= 4)
            {
                this.extra_bits += (dc / 2) - 1;
            }

            return this.IsFull();
        }

        private static int Lcode(int length)
        {
            if (length == 255)
            {
                return 285;
            }

            int code = 257;
            while (length >= 8)
            {
                code += 4;
                length >>= 1;
            }

            return code + length;
        }

        private static int Dcode(int distance)
        {
            int code = 0;
            while (distance >= 4)
            {
                code += 2;
                distance >>= 1;
            }

            return code + distance;
        }
    }
}
