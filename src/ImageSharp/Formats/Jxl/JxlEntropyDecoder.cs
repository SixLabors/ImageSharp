// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Compression;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Owns JPEG XL entropy tables. Readers borrow their spans for one entropy-coded section.
/// </summary>
/// <remarks>
/// Context maps, hybrid integers, prefix codes and ANS are shared by ICC and image decoding.
/// LZ77 copies refer to values already retained by the consumer; the decoder does not allocate another history buffer.
/// </remarks>
internal sealed class JxlEntropyDecoder : IDisposable
{
    /// <summary>
    /// The maximum backward distance, measured in decoded values, specified by JPEG XL LZ77 coding.
    /// </summary>
    public const int MaximumLz77Distance = 1 << 20;

    private const uint FinalAnsState = 0x130000;
    private readonly byte[] contextMap;
    private readonly HybridUint[] integers;
    private readonly int[] tableOffsets;
    private readonly IMemoryOwner<HuffmanCode>? prefixCodes;
    private readonly IMemoryOwner<JxlAnsDecoder.Bucket>? ansBuckets;
    private readonly int alphabetBits;
    private readonly uint minimumCopySymbol;
    private readonly uint minimumCopyLength;
    private readonly HybridUint copyLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlEntropyDecoder"/> class with its completed tables.
    /// </summary>
    /// <param name="contextMap">The validated context-to-cluster map.</param>
    /// <param name="integers">The hybrid integer configuration for each cluster.</param>
    /// <param name="tableOffsets">The prefix-table offsets, or an empty array for ANS.</param>
    /// <param name="prefixCodes">The owned prefix tables, or null for ANS.</param>
    /// <param name="ansBuckets">The owned ANS tables, or null for prefix coding.</param>
    /// <param name="alphabetBits">The ANS alphabet width, or 15 for prefix coding.</param>
    /// <param name="minimumCopySymbol">The first LZ77 token, or uint.MaxValue without LZ77.</param>
    /// <param name="minimumCopyLength">The minimum copy length.</param>
    /// <param name="copyLength">The copy-length configuration, unused without LZ77.</param>
    private JxlEntropyDecoder(
        byte[] contextMap,
        HybridUint[] integers,
        int[] tableOffsets,
        IMemoryOwner<HuffmanCode>? prefixCodes,
        IMemoryOwner<JxlAnsDecoder.Bucket>? ansBuckets,
        int alphabetBits,
        uint minimumCopySymbol,
        uint minimumCopyLength,
        HybridUint copyLength)
    {
        this.contextMap = contextMap;
        this.integers = integers;
        this.tableOffsets = tableOffsets;
        this.prefixCodes = prefixCodes;
        this.ansBuckets = ansBuckets;
        this.alphabetBits = alphabetBits;
        this.minimumCopySymbol = minimumCopySymbol;
        this.minimumCopyLength = minimumCopyLength;
        this.copyLength = copyLength;
    }

    /// <summary>
    /// Gets a value indicating whether this entropy section permits LZ77 copy tokens.
    /// </summary>
    public bool UsesLz77 => this.minimumCopySymbol != uint.MaxValue;

    /// <summary>
    /// Reads entropy descriptions. All large table storage uses the configured allocator.
    /// </summary>
    /// <param name="reader">The bounded codestream reader.</param>
    /// <param name="allocator">The decoder's allocator.</param>
    /// <param name="contextCount">The context count fixed by the consuming syntax.</param>
    /// <param name="allowLz77">Whether this syntax permits copy tokens.</param>
    /// <returns>The table owner. Dispose it after all borrowed readers finish.</returns>
    public static JxlEntropyDecoder Read(ref JxlBitReader reader, MemoryAllocator allocator, int contextCount, bool allowLz77)
    {
        bool lz77 = reader.ReadBoolean();
        if (lz77 && !allowLz77)
        {
            throw new InvalidImageContentException("JPEG XL LZ77 coding is not permitted in this context map.");
        }

        uint minimumSymbol = uint.MaxValue;
        uint minimumLength = 0;
        HybridUint length = default;
        if (lz77)
        {
            // Literal contexts gain one final context for copy distances. Copy lengths have a separate
            // hybrid-integer configuration and share no value history with the entropy tables themselves.
            minimumSymbol = reader.ReadU32((0, 224), (0, 512), (0, 4096), (15, 8));
            minimumLength = reader.ReadU32((0, 3), (0, 4), (2, 5), (8, 9));
            length = HybridUint.Read(ref reader, 8);
            contextCount++;
        }

        byte[] map = new byte[contextCount];
        if (contextCount > 1)
        {
            ReadContextMap(ref reader, allocator, map);
        }

        int clusterCount = 1;
        foreach (byte cluster in map)
        {
            // Cluster 255 represents a 256th table. Keep this count wider than the byte-valued map.
            clusterCount = Math.Max(clusterCount, cluster + 1);
        }

        bool prefix = reader.ReadBoolean();
        int alphabetBits = prefix ? HuffmanTableBuilder.MaximumCodeLength : (int)reader.ReadBits(2) + 5;
        HybridUint[] integers = new HybridUint[clusterCount];

        for (int i = 0; i < integers.Length; i++)
        {
            integers[i] = HybridUint.Read(ref reader, alphabetBits);
        }

        if (!prefix)
        {
            // All clusters share one allocator-owned table buffer. Each cluster occupies 2^alphabetBits
            // consecutive buckets; the section reader can index that range without retrieving Memory.Span per symbol.
            IMemoryOwner<JxlAnsDecoder.Bucket> buckets = allocator.Allocate<JxlAnsDecoder.Bucket>(clusterCount << alphabetBits);
            try
            {
                Span<JxlAnsDecoder.Bucket> storage = buckets.Memory.Span;
                for (int i = 0; i < clusterCount; i++)
                {
                    JxlAnsDecoder.ReadDistribution(ref reader, storage.Slice(i << alphabetBits, 1 << alphabetBits));
                }

                return new JxlEntropyDecoder(map, integers, [], null, buckets, alphabetBits, minimumSymbol, minimumLength, length);
            }
            catch
            {
                // Ownership transfers only when the completed decoder is returned. A malformed distribution
                // must return the buffer here because no decoder instance exists to dispose it.
                buckets.Dispose();
                throw;
            }
        }

        // Alphabet sizes precede all prefix descriptions. Retain the code lengths until each table is measured,
        // then allocate one exact-sized table buffer. The scratch buffers are returned before symbol decoding.
        int[] alphabetOffsets = new int[clusterCount + 1];
        int largestAlphabet = 0;

        for (int i = 0; i < clusterCount; i++)
        {
            int alphabetSize = 1;
            if (reader.ReadBoolean())
            {
                int bits = (int)reader.ReadBits(4);
                alphabetSize += (1 << bits) + (int)reader.ReadBits(bits);
            }

            if (alphabetSize > 1 << HuffmanTableBuilder.MaximumCodeLength)
            {
                throw new InvalidImageContentException("The JPEG XL prefix alphabet is too large.");
            }

            largestAlphabet = Math.Max(largestAlphabet, alphabetSize);
            alphabetOffsets[i + 1] = alphabetOffsets[i] + alphabetSize;
        }

        using IMemoryOwner<int> codeLengths = allocator.Allocate<int>(alphabetOffsets[^1]);
        using IMemoryOwner<int> sorted = allocator.Allocate<int>(largestAlphabet);
        Span<int> lengths = codeLengths.Memory.Span;
        Span<int> workspace = sorted.Memory.Span;
        int[] tableOffsets = new int[clusterCount + 1];

        for (int i = 0; i < clusterCount; i++)
        {
            Span<int> current = lengths[alphabetOffsets[i]..alphabetOffsets[i + 1]];
            JxlHuffmanDecoder.ReadCodeLengths(ref reader, current);
            int count = HuffmanTableBuilder.Build([], 8, current, workspace);
            if (count == 0)
            {
                throw new InvalidImageContentException("The JPEG XL prefix table is invalid.");
            }

            tableOffsets[i + 1] = tableOffsets[i] + count;
        }

        IMemoryOwner<HuffmanCode> codes = allocator.Allocate<HuffmanCode>(tableOffsets[^1]);
        try
        {
            Span<HuffmanCode> storage = codes.Memory.Span;
            for (int i = 0; i < clusterCount; i++)
            {
                HuffmanTableBuilder.Build(storage[tableOffsets[i]..tableOffsets[i + 1]], 8, lengths[alphabetOffsets[i]..alphabetOffsets[i + 1]], workspace);
            }

            return new JxlEntropyDecoder(map, integers, tableOffsets, codes, null, alphabetBits, minimumSymbol, minimumLength, length);
        }
        catch
        {
            // The code-length and sorting owners leave scope automatically. This final table has not
            // transferred to a decoder on failure, so it must be returned separately.
            codes.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Borrows table spans once for the section. Symbol loops do not access Memory.Span.
    /// </summary>
    /// <param name="reader">The bit reader positioned at the section's initial ANS state, when present.</param>
    /// <returns>The section reader.</returns>
    public Reader CreateReader(scoped ref JxlBitReader reader) => new(this, ref reader);

    /// <inheritdoc/>
    public void Dispose()
    {
        this.prefixCodes?.Dispose();
        this.ansBuckets?.Dispose();
    }

    /// <summary>
    /// Reads the simple or entropy-coded context map and validates that all cluster numbers are used.
    /// </summary>
    /// <param name="reader">The bounded codestream reader.</param>
    /// <param name="allocator">The decoder's allocator.</param>
    /// <param name="map">The destination fixed by the consuming syntax.</param>
    private static void ReadContextMap(ref JxlBitReader reader, MemoryAllocator allocator, Span<byte> map)
    {
        if (reader.ReadBoolean())
        {
            int bits = (int)reader.ReadBits(2);
            for (int i = 0; i < map.Length; i++)
            {
                map[i] = (byte)reader.ReadBits(bits);
            }
        }
        else
        {
            // The map is itself a one-context entropy stream. Its decoded bytes supply any copy history,
            // and the optional move-to-front step runs only after its entropy stream reaches a valid final state.
            bool moveToFront = reader.ReadBoolean();
            using JxlEntropyDecoder codes = Read(ref reader, allocator, 1, map.Length > 2);
            Reader symbols = codes.CreateReader(ref reader);
            for (int i = 0; i < map.Length; i++)
            {
                map[i] = symbols.ReadByte(ref reader, 0, map[..i]);
            }

            symbols.CheckFinalState();
            if (moveToFront)
            {
                Span<byte> order = stackalloc byte[256];
                for (int i = 0; i < order.Length; i++)
                {
                    order[i] = (byte)i;
                }

                foreach (ref byte entry in map)
                {
                    // Interpret the byte as a rank in the current alphabet, then promote that symbol.
                    // Span.CopyTo supports the overlap when shifting earlier symbols one place to the right.
                    int index = entry;
                    entry = order[index];
                    order[..index].CopyTo(order[1..]);
                    order[0] = entry;
                }
            }
        }

        // Cluster identifiers must form a dense range starting at zero. This fixed byte set validates
        // that rule without allocating another collection before the table buffers are sized.
        Span<byte> used = stackalloc byte[256];
        used.Clear();
        int maximum = 0;
        foreach (byte cluster in map)
        {
            used[cluster] = 1;
            maximum = Math.Max(maximum, cluster);
        }

        if (used[..(maximum + 1)].Contains((byte)0))
        {
            throw new InvalidImageContentException("The JPEG XL context map skips a cluster number.");
        }
    }

    /// <summary>
    /// Borrows completed tables and holds the state for one entropy-coded section.
    /// </summary>
    public ref struct Reader
    {
        private readonly JxlEntropyDecoder code;
        private readonly ReadOnlySpan<HuffmanCode> prefix;
        private readonly ReadOnlySpan<JxlAnsDecoder.Bucket> buckets;
        private uint state;
        private uint remainingCopy;
        private int copyDistance;

        /// <summary>
        /// Initializes a new instance of the <see cref="Reader"/> struct and borrows the owner's table spans.
        /// </summary>
        /// <param name="code">The table owner, which must outlive this reader.</param>
        /// <param name="reader">The section bit reader.</param>
        public Reader(JxlEntropyDecoder code, scoped ref JxlBitReader reader)
        {
            // Cache borrowed spans once. The owner outlives this stack-only reader and returns its storage
            // only after decoding finishes; no per-symbol rent or Memory.Span access is required.
            this.code = code;
            this.prefix = code.prefixCodes is null ? [] : code.prefixCodes.Memory.Span;
            this.buckets = code.ansBuckets is null ? [] : code.ansBuckets.Memory.Span;
            this.state = this.buckets.IsEmpty ? FinalAnsState : reader.ReadBits(32);
            this.remainingCopy = 0;
            this.copyDistance = 0;
        }

        /// <summary>
        /// Reads a byte-valued symbol or continues an LZ77 copy from the consumer's retained output.
        /// </summary>
        /// <param name="reader">The bounded bit reader.</param>
        /// <param name="context">The context fixed by the consumer's syntax or preceding decoded bytes.</param>
        /// <param name="decoded">All preceding bytes in this entropy section.</param>
        /// <returns>The next byte.</returns>
        public byte ReadByte(ref JxlBitReader reader, int context, scoped ReadOnlySpan<byte> decoded)
        {
            if (this.remainingCopy != 0)
            {
                this.remainingCopy--;
                return decoded[^this.copyDistance];
            }

            uint value = this.ReadToken(ref reader, context, out uint copyCount);
            if (copyCount == 0)
            {
                if (value > byte.MaxValue)
                {
                    throw new InvalidImageContentException("The JPEG XL byte stream contains a value above 255.");
                }

                return (byte)value;
            }

            if (decoded.IsEmpty)
            {
                throw new InvalidImageContentException("The JPEG XL LZ77 copy precedes the first decoded value.");
            }

            // The format limits backward distance to the one-megavalue window and available history.
            // Widen before adding one so an encoded uint.MaxValue still selects the maximum valid distance.
            this.copyDistance = (int)Math.Min(Math.Min((ulong)value + 1, MaximumLz77Distance), (ulong)decoded.Length);
            this.remainingCopy = copyCount - 1;
            return decoded[^this.copyDistance];
        }

        /// <summary>
        /// Reads an unsigned value using caller-owned LZ77 history when the section permits copies.
        /// </summary>
        /// <param name="reader">The bounded bit reader.</param>
        /// <param name="context">The context selected by the consuming syntax.</param>
        /// <param name="history">
        /// The circular history. Its power-of-two length must cover the smaller of this section's total value count
        /// and <see cref="MaximumLz77Distance"/>. It must contain preceding values at their masked logical positions.
        /// Use an empty span when LZ77 is disabled or before the first value.
        /// </param>
        /// <param name="decodedCount">The number of preceding values, including values already copied.</param>
        /// <returns>The next literal or copied value. Nonempty history receives the returned value.</returns>
        public uint ReadUInt32(ref JxlBitReader reader, int context, scoped Span<uint> history, uint decodedCount)
        {
            // The consumer bounds the total value count and supplies a power-of-two ring only for LZ77.
            // Mask logical positions into that ring; history remains caller-owned and is reused through the section.
            uint value;
            if (this.remainingCopy != 0)
            {
                this.remainingCopy--;
                value = history[(int)((decodedCount - (uint)this.copyDistance) & (uint)(history.Length - 1))];
            }
            else
            {
                value = this.ReadToken(ref reader, context, out uint copyCount);
                if (copyCount != 0)
                {
                    if (decodedCount == 0)
                    {
                        throw new InvalidImageContentException("The JPEG XL LZ77 copy precedes the first decoded value.");
                    }

                    // Copies can overlap their own output. A fixed backward distance and a circular write position
                    // reproduce that sequence one value at a time, including after the window wraps.
                    this.copyDistance = (int)Math.Min(Math.Min((ulong)value + 1, MaximumLz77Distance), decodedCount);
                    this.remainingCopy = copyCount - 1;
                    value = history[(int)((decodedCount - (uint)this.copyDistance) & (uint)(history.Length - 1))];
                }
            }

            if (!history.IsEmpty)
            {
                history[(int)(decodedCount & (uint)(history.Length - 1))] = value;
            }

            return value;
        }

        /// <summary>
        /// Reads a literal value, or a copy-distance symbol with a nonzero copy length.
        /// </summary>
        /// <param name="reader">The bounded bit reader.</param>
        /// <param name="context">The consuming syntax's context.</param>
        /// <param name="copyCount">Zero for a literal, or the decoded copy length.</param>
        /// <returns>The literal or distance symbol. Image consumers apply their own two-dimensional distance mapping.</returns>
        public uint ReadToken(ref JxlBitReader reader, int context, out uint copyCount)
        {
            int cluster = this.code.contextMap[context];
            uint token = this.ReadCode(ref reader, cluster);
            if (token < this.code.minimumCopySymbol)
            {
                copyCount = 0;
                return this.code.integers[cluster].Expand(ref reader, token);
            }

            uint length = this.code.copyLength.Expand(ref reader, token - this.code.minimumCopySymbol);
            if (length > uint.MaxValue - this.code.minimumCopyLength)
            {
                throw new InvalidImageContentException("The JPEG XL LZ77 copy length overflows.");
            }

            copyCount = length + this.code.minimumCopyLength;

            // Copy distance always uses the extra final context, not the context that selected the copy token.
            int distanceCluster = this.code.contextMap[^1];
            uint distance = this.ReadCode(ref reader, distanceCluster);
            return this.code.integers[distanceCluster].Expand(ref reader, distance);
        }

        /// <summary>
        /// Validates the final ANS state and rejects copies that extend past the declared value count.
        /// </summary>
        public readonly void CheckFinalState()
        {
            if (this.state != FinalAnsState || this.remainingCopy != 0)
            {
                throw new InvalidImageContentException("The JPEG XL entropy section has an invalid final state.");
            }
        }

        /// <summary>
        /// Decodes one prefix or ANS symbol from the cached table spans.
        /// </summary>
        /// <param name="reader">The bounded bit reader.</param>
        /// <param name="cluster">The validated cluster index.</param>
        /// <returns>The unexpanded symbol.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint ReadCode(ref JxlBitReader reader, int cluster)
        {
            if (!this.prefix.IsEmpty)
            {
                // The first eight bits select the cluster's root entry. A longer entry points into a
                // second-level table relative to that root position; only then consume the root bits separately.
                int position = this.code.tableOffsets[cluster] + (int)reader.PeekBits(8);
                HuffmanCode entry = this.prefix[position];
                if (entry.BitsUsed > 8)
                {
                    reader.ConsumeBits(8);
                    position += (int)entry.Value + (int)reader.PeekBits(entry.BitsUsed - 8);
                    entry = this.prefix[position];
                }

                reader.ConsumeBits(entry.BitsUsed);
                return entry.Value;
            }

            // Split the state's low twelve bits into a bucket number and a position within that bucket.
            // The alias entry supplies the alternate frequency and cumulative offset without a 4096-entry table.
            int bucketBits = JxlAnsDecoder.ProbabilityBits - this.code.alphabetBits;
            uint index = this.state & (JxlAnsDecoder.ProbabilityTotal - 1);
            int bucketIndex = (int)(index >> bucketBits);
            uint positionInBucket = index & ((1U << bucketBits) - 1);
            JxlAnsDecoder.Bucket bucket = this.buckets[(cluster << this.code.alphabetBits) + bucketIndex];
            bool alias = positionInBucket >= bucket.Cutoff;
            uint frequency = alias ? (uint)(bucket.Frequency ^ bucket.AliasFrequencyXor) : bucket.Frequency;
            uint offset = positionInBucket + (alias ? bucket.AliasOffset : 0U);
            this.state = ((this.state >> JxlAnsDecoder.ProbabilityBits) * frequency) + offset;

            // ANS consumes sixteen bits only when the updated state falls below its normalization threshold.
            // The normalized frequencies ensure that one sixteen-bit refill restores the required state range.
            if (this.state < 1 << 16)
            {
                this.state = (this.state << 16) | reader.ReadBits(16);
            }

            return alias ? bucket.AliasSymbol : (uint)bucketIndex;
        }
    }

    /// <summary>
    /// Splits an unsigned integer between an entropy token and following literal bits.
    /// </summary>
    private readonly struct HybridUint
    {
        private readonly int splitExponent;
        private readonly int highBits;
        private readonly int lowBits;

        /// <summary>
        /// Initializes a new instance of the <see cref="HybridUint"/> struct from validated field widths.
        /// </summary>
        /// <param name="splitExponent">The threshold exponent.</param>
        /// <param name="highBits">The leading bits retained in the token.</param>
        /// <param name="lowBits">The trailing bits retained in the token.</param>
        private HybridUint(int splitExponent, int highBits, int lowBits)
        {
            this.splitExponent = splitExponent;
            this.highBits = highBits;
            this.lowBits = lowBits;
        }

        /// <summary>
        /// Reads and validates one hybrid integer configuration.
        /// </summary>
        /// <param name="reader">The bounded bit reader.</param>
        /// <param name="alphabetBits">The entropy alphabet's maximum bit width.</param>
        /// <returns>The validated configuration.</returns>
        public static HybridUint Read(ref JxlBitReader reader, int alphabetBits)
        {
            // A split at the alphabet width encodes values directly. Smaller splits also describe
            // leading and trailing token bits; their combined width cannot exceed the split exponent.
            int split = (int)reader.ReadBits(32 - BitOperations.LeadingZeroCount((uint)alphabetBits));
            if (split > alphabetBits)
            {
                throw new InvalidImageContentException("The JPEG XL hybrid integer split exceeds its alphabet.");
            }

            int high = 0;
            int low = 0;
            if (split != alphabetBits)
            {
                high = (int)reader.ReadBits(32 - BitOperations.LeadingZeroCount((uint)split));
                if (high > split)
                {
                    throw new InvalidImageContentException("The JPEG XL hybrid integer leading width is invalid.");
                }

                low = (int)reader.ReadBits(32 - BitOperations.LeadingZeroCount((uint)(split - high)));
                if (high + low > split)
                {
                    throw new InvalidImageContentException("The JPEG XL hybrid integer trailing width is invalid.");
                }
            }

            return new HybridUint(split, high, low);
        }

        /// <summary>
        /// Reconstructs one unsigned value without allowing malformed widths to wrap a machine shift.
        /// </summary>
        /// <param name="reader">The bounded bit reader.</param>
        /// <param name="token">The entropy-coded token.</param>
        /// <returns>The reconstructed unsigned value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint Expand(ref JxlBitReader reader, uint token)
        {
            uint split = 1U << this.splitExponent;
            if (token < split)
            {
                return token;
            }

            // Above the direct-value range, the token stores a length index plus leading and trailing
            // value bits. Insert the implicit leading one and read only the middle bits from the stream.
            // Validate the reconstructed width first because C# masks an out-of-range shift count.
            int tokenBits = this.highBits + this.lowBits;
            uint bits = (uint)(this.splitExponent - tokenBits) + ((token - split) >> tokenBits);
            if (bits + (uint)tokenBits >= 32)
            {
                throw new InvalidImageContentException("The JPEG XL hybrid integer exceeds 32 bits.");
            }

            uint low = token & ((1U << this.lowBits) - 1);
            uint high = ((token >> this.lowBits) & ((1U << this.highBits) - 1)) | (1U << this.highBits);
            return (((high << (int)bits) | reader.ReadBits((int)bits)) << this.lowBits) | low;
        }
    }
}
