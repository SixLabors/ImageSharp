// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Compression;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Entropy;

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
    private readonly IMemoryOwner<byte> contextMap;
    private readonly IMemoryOwner<Cluster> clusters;
    private readonly IMemoryOwner<HuffmanCode>? prefixCodes;
    private readonly IMemoryOwner<JxlAnsDecoder.Bucket>? ansBuckets;

    // ANS divides a twelve-bit probability index into an alphabet bucket and a position within it.
    // These table-wide values are copied into each reader once; prefix-coded owners keep both at zero.
    private readonly int ansBucketBits;
    private readonly uint ansBucketMask;

    private readonly uint minimumCopySymbol;
    private readonly uint minimumCopyLength;
    private readonly JxlHybridUint copyLength;

    /// <summary>
    /// Indicates whether this owner has released its entropy tables.
    /// </summary>
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlEntropyDecoder"/> class with its completed tables.
    /// </summary>
    /// <param name="contextMap">The validated context-to-cluster map.</param>
    /// <param name="clusters">The owned integer configurations and table offsets, one per cluster.</param>
    /// <param name="prefixCodes">The owned prefix tables, or null for ANS.</param>
    /// <param name="ansBuckets">The owned ANS tables, or null for prefix coding.</param>
    /// <param name="ansBucketBits">The bit count of one ANS bucket's probability range, or zero for prefix coding.</param>
    /// <param name="ansBucketMask">The mask for that within-bucket position, or zero for prefix coding.</param>
    /// <param name="minimumCopySymbol">The first LZ77 token, or uint.MaxValue without LZ77.</param>
    /// <param name="minimumCopyLength">The minimum copy length.</param>
    /// <param name="copyLength">The copy-length configuration, unused without LZ77.</param>
    /// <param name="maximumValueBits">The conservative hybrid-integer width represented by the completed symbol tables.</param>
    /// <param name="canUseAns420">Whether every cluster uses hybrid widths 4/2/0 with ANS and no LZ77.</param>
    private JxlEntropyDecoder(
        IMemoryOwner<byte> contextMap,
        IMemoryOwner<Cluster> clusters,
        IMemoryOwner<HuffmanCode>? prefixCodes,
        IMemoryOwner<JxlAnsDecoder.Bucket>? ansBuckets,
        int ansBucketBits,
        uint ansBucketMask,
        uint minimumCopySymbol,
        uint minimumCopyLength,
        JxlHybridUint copyLength,
        int maximumValueBits,
        bool canUseAns420)
    {
        this.contextMap = contextMap;
        this.clusters = clusters;
        this.prefixCodes = prefixCodes;
        this.ansBuckets = ansBuckets;
        this.ansBucketBits = ansBucketBits;
        this.ansBucketMask = ansBucketMask;
        this.minimumCopySymbol = minimumCopySymbol;
        this.minimumCopyLength = minimumCopyLength;
        this.copyLength = copyLength;
        this.MaximumValueBits = maximumValueBits;
        this.CanUseAns420 = canUseAns420;
    }

    /// <summary>
    /// Selects one completed entropy table mode for a repeated value traversal.
    /// </summary>
    public interface IEntropyOperator
    {
        /// <summary>
        /// Gets a value indicating whether this mode maintains LZ77 copy state and history.
        /// </summary>
        static abstract bool UsesLz77 { get; }

        /// <summary>
        /// Reads one unexpanded symbol from the selected cluster's lookup table.
        /// </summary>
        /// <param name="entropy">The current section's entropy state.</param>
        /// <param name="reader">The bounded bit reader.</param>
        /// <param name="clusterIndex">The validated cluster ID. The mode resolves its own lookup-table range.</param>
        /// <param name="integer">The selected cluster's hybrid widths, or default when the mode fixes those widths.</param>
        /// <returns>The entropy-coded token before hybrid-integer expansion.</returns>
        static abstract uint ReadCode(ref Reader entropy, ref JxlBitReader reader, int clusterIndex, out JxlHybridUint integer);

        /// <summary>
        /// Expands a token with the integer arithmetic selected for the complete traversal.
        /// </summary>
        /// <param name="reader">The bounded reader for literal middle bits.</param>
        /// <param name="token">The entropy-coded token.</param>
        /// <param name="integer">The cluster's validated hybrid widths, unused when the operator fixes those widths.</param>
        /// <returns>The expanded unsigned value, with its reconstructed width validated.</returns>
        static abstract uint Expand(ref JxlBitReader reader, uint token, in JxlHybridUint integer);
    }

    /// <summary>
    /// Gets a value indicating whether this entropy section permits LZ77 copy tokens.
    /// </summary>
    public bool UsesLz77 => this.minimumCopySymbol != uint.MaxValue;

    /// <summary>
    /// Gets a value indicating whether the section uses prefix codes rather than ANS.
    /// </summary>
    public bool UsesPrefixCodes => this.prefixCodes is not null;

    /// <summary>
    /// Gets the original syntax-context to cluster map. Borrow it once while this table owner remains alive.
    /// </summary>
    public ReadOnlySpan<byte> ContextMap => this.contextMap.Memory.Span;

    /// <summary>
    /// Gets a value indicating whether ANS tokens can use fixed hybrid integer widths 4/2/0 without LZ77 state.
    /// </summary>
    /// <remarks>
    /// The split exponent is four, two leading value bits are retained in each expanded token, and no trailing bits are retained.
    /// Qualification includes every cluster and is accumulated while the existing configurations are parsed.
    /// </remarks>
    public bool CanUseAns420 { get; }

    /// <summary>
    /// Gets the conservative hybrid-integer width represented by the symbol tables, including LZ77 length and distance symbols.
    /// </summary>
    /// <remarks>
    /// Direct tokens use their configured split width. A zero-valued singleton can therefore have a nonzero bound.
    /// A cluster shared by literals and distances can also give a bound greater than 32 when it includes copy tokens.
    /// Such a bound selects wider coefficient storage; it does not mean that the stream produces integers of that width.
    /// Consumers use this metadata to select coefficient storage; actual decoded values still receive normal width validation.
    /// </remarks>
    public int MaximumValueBits { get; }

    /// <summary>
    /// Reads entropy descriptions. Retained lookup data and table scratch use the configured allocator.
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
        JxlHybridUint length = default;
        if (lz77)
        {
            // Literal contexts gain one final context for copy distances. Copy lengths have a separate
            // hybrid-integer configuration and share no value history with the entropy tables themselves.
            minimumSymbol = reader.ReadU32((0, 224), (0, 512), (0, 4096), (15, 8));
            minimumLength = reader.ReadU32((0, 3), (0, 4), (2, 5), (8, 9));
            length = JxlHybridUint.Read(ref reader, 8);
            contextCount++;
        }

        IMemoryOwner<byte> mapOwner = allocator.Allocate<byte>(contextCount);
        IMemoryOwner<Cluster>? clusterOwner = null;
        IMemoryOwner<HuffmanCode>? prefixOwner = null;
        IMemoryOwner<JxlAnsDecoder.Bucket>? ansOwner = null;
        try
        {
            Span<byte> map = mapOwner.Memory.Span;
            if (contextCount > 1)
            {
                ReadContextMap(ref reader, allocator, map);
            }
            else
            {
                // A one-context stream omits its map. Allocator memory is not zero-initialized,
                // so write the implicit cluster zero before any reader can borrow this span.
                map[0] = 0;
            }

            int clusterCount = 1;
            foreach (byte cluster in map)
            {
                // Cluster 255 represents a 256th table. Keep this count wider than the byte-valued map.
                clusterCount = Math.Max(clusterCount, cluster + 1);
            }

            bool prefix = reader.ReadBoolean();
            int alphabetBits = prefix ? HuffmanTableBuilder.MaximumCodeLength : (int)reader.ReadBits(2) + 5;
            int maximumValueBits = 0;
            int distanceCluster = lz77 ? map[^1] : -1;
            clusterOwner = allocator.Allocate<Cluster>(clusterCount);
            Span<Cluster> clusters = clusterOwner.Memory.Span;

            // Each cluster's integer configuration and lookup offset have the same lifetime.
            // Store them together in one allocator buffer. ANS has fixed-size tables.
            bool canUseAns420 = !prefix && !lz77;
            for (int i = 0; i < clusterCount; i++)
            {
                JxlHybridUint integer = JxlHybridUint.Read(ref reader, alphabetBits);
                canUseAns420 = canUseAns420 && integer.Is420;
                clusters[i] = new Cluster(integer, prefix ? 0 : i << alphabetBits);
            }

            if (!prefix)
            {
                // All clusters share one table buffer. Cache its span before filling the fixed-size ranges.
                ansOwner = allocator.Allocate<JxlAnsDecoder.Bucket>(clusterCount << alphabetBits);
                Span<JxlAnsDecoder.Bucket> buckets = ansOwner.Memory.Span;
                for (int i = 0; i < clusterCount; i++)
                {
                    Span<JxlAnsDecoder.Bucket> table = buckets.Slice(clusters[i].TableOffset, 1 << alphabetBits);
                    JxlAnsDecoder.ReadDistribution(ref reader, table);
                    for (int symbol = 0; symbol < table.Length; symbol++)
                    {
                        // Alias buckets retain each original symbol's frequency. Only nonzero support can emit a token.
                        if (table[symbol].Frequency != 0)
                        {
                            int bits = GetSymbolBitCount((uint)symbol, clusters[i].Integer, minimumSymbol, length, i == distanceCluster);
                            maximumValueBits = Math.Max(maximumValueBits, bits);
                        }
                    }
                }

                // The serialized two-bit field gives alphabet widths 5..8. The remaining 7..4 bits
                // locate a position inside each bucket's share of the fixed twelve-bit probability range.
                int bucketBits = JxlAnsDecoder.ProbabilityBits - alphabetBits;
                uint bucketMask = (1U << bucketBits) - 1;

                return new JxlEntropyDecoder(
                    mapOwner, clusterOwner, null, ansOwner, bucketBits, bucketMask, minimumSymbol, minimumLength, length, maximumValueBits, canUseAns420);
            }

            // Cluster numbers are bytes, so at most 256 alphabet sizes are needed. This bounded
            // stack buffer exists only during table construction; the decoder retains no scratch offsets.
            InlineArray256<int> alphabetStorage = default;
            Span<int> alphabetSizes = alphabetStorage[..clusterCount];
            int largestAlphabet = 0;
            int totalAlphabetSize = 0;

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
                alphabetSizes[i] = alphabetSize;
                totalAlphabetSize += alphabetSize;
            }

            // Alphabet sizes precede their code lengths. Retain those lengths until all tables have
            // been validated and measured, then rent one exact-sized final table. Scratch returns on exit.
            using IMemoryOwner<int> codeLengths = allocator.Allocate<int>(totalAlphabetSize);
            using IMemoryOwner<int> sorted = allocator.Allocate<int>(largestAlphabet);
            Span<int> lengths = codeLengths.Memory.Span;
            Span<int> workspace = sorted.Memory.Span;
            int alphabetOffset = 0;
            int tableSize = 0;

            for (int i = 0; i < clusterCount; i++)
            {
                Span<int> current = lengths.Slice(alphabetOffset, alphabetSizes[i]);
                JxlHuffmanDecoder.ReadCodeLengths(ref reader, current);
                for (int symbol = 0; symbol < current.Length; symbol++)
                {
                    if (current[symbol] != 0)
                    {
                        int bits = GetSymbolBitCount((uint)symbol, clusters[i].Integer, minimumSymbol, length, i == distanceCluster);
                        maximumValueBits = Math.Max(maximumValueBits, bits);
                    }
                }

                int count = HuffmanTableBuilder.Build([], 8, current, workspace);
                if (count == 0)
                {
                    throw new InvalidImageContentException("The JPEG XL prefix table is invalid.");
                }

                clusters[i] = new Cluster(clusters[i].Integer, tableSize);
                alphabetOffset += current.Length;
                tableSize += count;
            }

            prefixOwner = allocator.Allocate<HuffmanCode>(tableSize);
            Span<HuffmanCode> storage = prefixOwner.Memory.Span;
            alphabetOffset = 0;

            for (int i = 0; i < clusterCount; i++)
            {
                int tableEnd = i + 1 < clusterCount ? clusters[i + 1].TableOffset : storage.Length;
                Span<HuffmanCode> table = storage[clusters[i].TableOffset..tableEnd];
                ReadOnlySpan<int> current = lengths.Slice(alphabetOffset, alphabetSizes[i]);

                // Measurement already validated these unchanged code lengths and fixed each table's size.
                // Building fills that exact range; its returned entry count repeats the measured value.
                _ = HuffmanTableBuilder.Build(table, 8, current, workspace);
                alphabetOffset += current.Length;
            }

            return new JxlEntropyDecoder(
                mapOwner, clusterOwner, prefixOwner, null, 0, 0, minimumSymbol, minimumLength, length, maximumValueBits, canUseAns420);
        }
        catch
        {
            // Ownership transfers only when the completed decoder is returned. Release every retained
            // buffer acquired before a parsing or allocation failure; using declarations own the scratch.
            prefixOwner?.Dispose();
            ansOwner?.Dispose();
            clusterOwner?.Dispose();
            mapOwner.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Borrows table spans once for the section. Symbol loops do not access Memory.Span.
    /// </summary>
    /// <param name="reader">The bit reader positioned at the section's initial ANS state, when present.</param>
    /// <returns>The section reader.</returns>
    public Reader CreateReader(ref JxlBitReader reader) => new(this, this.ansBuckets is null ? FinalAnsState : reader.ReadBits(32));

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        this.prefixCodes?.Dispose();
        this.ansBuckets?.Dispose();
        this.clusters.Dispose();
        this.contextMap.Dispose();
        this.isDisposed = true;
    }

    /// <summary>
    /// Reads the simple or entropy-coded context map and validates that all cluster numbers are used.
    /// </summary>
    /// <param name="reader">The bounded codestream reader.</param>
    /// <param name="allocator">The decoder's allocator.</param>
    /// <param name="map">The nonempty destination sized by the consuming syntax before this call.</param>
    /// <remarks>
    /// This method validates dense cluster numbering. The caller validates any smaller cluster limit required by its own syntax.
    /// </remarks>
    public static void ReadContextMap(ref JxlBitReader reader, MemoryAllocator allocator, Span<byte> map)
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
    /// Calculates the configured width for a literal, copy length or copy distance token.
    /// </summary>
    /// <param name="symbol">A symbol with nonzero probability in its table.</param>
    /// <param name="integer">The cluster's normal hybrid configuration.</param>
    /// <param name="minimumCopySymbol">The first copy token, or uint.MaxValue without LZ77.</param>
    /// <param name="copyLength">The copy-length hybrid configuration.</param>
    /// <param name="distanceCluster">Whether this cluster supplies copy distances.</param>
    /// <returns>The conservative width used by the native decoder to select coefficient storage.</returns>
    private static int GetSymbolBitCount(uint symbol, JxlHybridUint integer, uint minimumCopySymbol, JxlHybridUint copyLength, bool distanceCluster)
    {
        // Copy tokens use the length configuration outside the distance cluster. Distance symbols retain
        // their normal integer configuration, even when a context map shares that cluster with literals.
        return !distanceCluster && symbol >= minimumCopySymbol
            ? copyLength.GetBitCount(symbol - minimumCopySymbol)
            : integer.GetBitCount(symbol);
    }

    /// <summary>
    /// Reads ANS symbols without LZ77 copy tokens.
    /// </summary>
    public readonly struct AnsOperator : IEntropyOperator
    {
        /// <inheritdoc/>
        public static bool UsesLz77 => false;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadCode(ref Reader entropy, ref JxlBitReader reader, int clusterIndex, out JxlHybridUint integer)
        {
            // ANS selects its table directly. Read widths afterward so they do not stay live across input refills.
            uint token = entropy.ReadAnsCode(ref reader, clusterIndex);
            integer = entropy.GetIntegerConfiguration(clusterIndex);
            return token;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Expand(ref JxlBitReader reader, uint token, in JxlHybridUint integer)
            => integer.Expand(ref reader, token);
    }

    /// <summary>
    /// Reads ANS symbols without LZ77 using hybrid integer widths 4/2/0 selected for every cluster.
    /// </summary>
    /// <remarks>
    /// Arithmetic follows jxl-rs entropy_coding/hybrid_uint.rs read_config_420 at revision
    /// d7a831fae15b903f1b56a76668030c19ac07bb6f. Invalid widths retain this decoder's existing rejection rule.
    /// </remarks>
    public readonly struct Ans420Operator : IEntropyOperator
    {
        /// <inheritdoc/>
        public static bool UsesLz77 => false;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadCode(ref Reader entropy, ref JxlBitReader reader, int clusterIndex, out JxlHybridUint integer)
        {
            // Fixed widths need no cluster configuration. Expand ignores this result, so no Cluster is borrowed here.
            integer = default;
            return entropy.ReadAnsCode(ref reader, clusterIndex);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Expand(ref JxlBitReader reader, uint token, in JxlHybridUint integer)
        {
            // A split exponent of four stores 0..15 directly. Above that range, two token bits
            // hold the leading mantissa and there are no trailing bits. The implicit leading one is bit two.
            if (token < 16)
            {
                return token;
            }

            // The general equation is 4-2+((token-16)>>2), which reduces to (token>>2)-2.
            // Two explicit high bits plus their implicit leading one permit at most 29 literal bits.
            // Reject invalid widths before C# can mask the shift count; do not copy the reference's masking behavior.
            uint bits = (token >> 2) - 2;
            if (bits >= 30)
            {
                throw new InvalidImageContentException("The JPEG XL hybrid integer exceeds 32 bits.");
            }

            return (((token & 3) | 4) << (int)bits) | reader.ReadBits((int)bits);
        }
    }

    /// <summary>
    /// Reads ANS symbols with LZ77 copy tokens.
    /// </summary>
    public readonly struct AnsLz77Operator : IEntropyOperator
    {
        /// <inheritdoc/>
        public static bool UsesLz77 => true;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadCode(ref Reader entropy, ref JxlBitReader reader, int clusterIndex, out JxlHybridUint integer)
        {
            // ANS selects its table directly. Read widths afterward so they do not stay live across input refills.
            uint token = entropy.ReadAnsCode(ref reader, clusterIndex);
            integer = entropy.GetIntegerConfiguration(clusterIndex);
            return token;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Expand(ref JxlBitReader reader, uint token, in JxlHybridUint integer)
            => integer.Expand(ref reader, token);
    }

    /// <summary>
    /// Reads prefix symbols without LZ77 copy tokens.
    /// </summary>
    public readonly struct PrefixOperator : IEntropyOperator
    {
        /// <inheritdoc/>
        public static bool UsesLz77 => false;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadCode(ref Reader entropy, ref JxlBitReader reader, int clusterIndex, out JxlHybridUint integer)
            => entropy.ReadPrefixCode(ref reader, clusterIndex, out integer);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Expand(ref JxlBitReader reader, uint token, in JxlHybridUint integer)
            => integer.Expand(ref reader, token);
    }

    /// <summary>
    /// Reads prefix symbols with LZ77 copy tokens.
    /// </summary>
    public readonly struct PrefixLz77Operator : IEntropyOperator
    {
        /// <inheritdoc/>
        public static bool UsesLz77 => true;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadCode(ref Reader entropy, ref JxlBitReader reader, int clusterIndex, out JxlHybridUint integer)
            => entropy.ReadPrefixCode(ref reader, clusterIndex, out integer);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Expand(ref JxlBitReader reader, uint token, in JxlHybridUint integer)
            => integer.Expand(ref reader, token);
    }

    /// <summary>
    /// Borrows completed tables and holds the state for one entropy-coded section.
    /// </summary>
    public ref struct Reader
    {
        private readonly JxlEntropyDecoder code;
        private readonly ReadOnlySpan<byte> contextMap;
        private readonly ReadOnlySpan<Cluster> clusters;
        private readonly ReadOnlySpan<HuffmanCode> prefix;
        private readonly ReadOnlySpan<JxlAnsDecoder.Bucket> buckets;

        // Cache the table-wide index decomposition once with the table spans. ANS alphabet widths 5..8
        // give probability bucket widths 7..4. These shifts and the mask apply to literals and LZ77 distances.
        // Prefix coding does not use these values; its table offsets come from the borrowed cluster metadata.
        private readonly int bucketBits;
        private readonly uint bucketMask;
        private readonly int alphabetBits;

        private uint state;
        private uint remainingCopy;
        private int copyDistance;

        /// <summary>
        /// Initializes a new instance of the <see cref="Reader"/> struct and borrows the owner's table spans.
        /// </summary>
        /// <param name="code">The table owner, which must outlive this reader.</param>
        /// <param name="initialState">The section's decoded ANS state, or the unused final-state constant for prefix coding.</param>
        public Reader(JxlEntropyDecoder code, uint initialState)
        {
            // Cache borrowed spans once. The owner outlives this stack-only reader and returns its storage
            // only after decoding finishes; no per-symbol rent or Memory.Span access is required.
            this.code = code;
            this.contextMap = code.contextMap.Memory.Span;
            this.clusters = code.clusters.Memory.Span;
            this.prefix = code.prefixCodes is null ? [] : code.prefixCodes.Memory.Span;
            this.buckets = code.ansBuckets is null ? [] : code.ansBuckets.Memory.Span;
            this.bucketBits = code.ansBucketBits;
            this.bucketMask = code.ansBucketMask;
            this.alphabetBits = JxlAnsDecoder.ProbabilityBits - code.ansBucketBits;
            this.state = initialState;
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
        public byte ReadByte(ref JxlBitReader reader, int context, ReadOnlySpan<byte> decoded)
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
        public uint ReadUInt32(ref JxlBitReader reader, int context, Span<uint> history, uint decodedCount)
            => this.ReadUInt32(ref reader, context, history, decodedCount, 0);

        /// <summary>
        /// Reads a value with linear history or modular image distances expressed relative to a row width.
        /// </summary>
        /// <param name="reader">The bounded bit reader.</param>
        /// <param name="context">The context selected by the consuming syntax.</param>
        /// <param name="history">The caller-owned power-of-two residual history, or an empty span without LZ77.</param>
        /// <param name="decodedCount">The number of preceding residual values in this stream.</param>
        /// <param name="distanceMultiplier">Zero for linear distances, or the maximum encoded channel width for modular samples.</param>
        /// <returns>The decoded residual before prediction. Nonempty history receives the returned value.</returns>
        public uint ReadUInt32(ref JxlBitReader reader, int context, Span<uint> history, uint decodedCount, int distanceMultiplier)
        {
            // General consumers select the completed table mode here. Repeated consumers can select
            // the same operator once outside their loop through the constrained overload below.
            if (this.code.UsesPrefixCodes)
            {
                return this.code.UsesLz77
                    ? this.ReadUInt32<PrefixLz77Operator>(ref reader, context, history, decodedCount, distanceMultiplier)
                    : this.ReadUInt32<PrefixOperator>(ref reader, context, history, decodedCount, distanceMultiplier);
            }

            return this.code.UsesLz77
                ? this.ReadUInt32<AnsLz77Operator>(ref reader, context, history, decodedCount, distanceMultiplier)
                : this.ReadUInt32<AnsOperator>(ref reader, context, history, decodedCount, distanceMultiplier);
        }

        /// <summary>
        /// Reads a context-selected value using the entropy mode fixed for the consuming traversal.
        /// </summary>
        /// <typeparam name="TOperator">The completed tables' symbol and LZ77 mode.</typeparam>
        /// <param name="reader">The bounded bit reader.</param>
        /// <param name="context">The consuming syntax's original context.</param>
        /// <param name="history">The caller-owned power-of-two history, empty without LZ77.</param>
        /// <param name="decodedCount">The number of preceding values in this section.</param>
        /// <param name="distanceMultiplier">Zero for linear history, or the modular channel width.</param>
        /// <returns>The literal or copied value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadUInt32<TOperator>(
            ref JxlBitReader reader,
            int context,
            Span<uint> history,
            uint decodedCount,
            int distanceMultiplier)
            where TOperator : struct, IEntropyOperator
        {
            // An active copy needs neither a token nor its context. Preserve that path before
            // resolving the original syntax context.
            if (TOperator.UsesLz77 && this.remainingCopy != 0)
            {
                return this.ContinueCopy(history, decodedCount);
            }

            return this.ReadClusteredUInt32<TOperator>(ref reader, this.contextMap[context], history, decodedCount, distanceMultiplier);
        }

        /// <summary>
        /// Reads a value from a cluster already selected by prepared metadata, sharing all literal and LZ77 handling.
        /// </summary>
        /// <typeparam name="TOperator">The completed tables' symbol and LZ77 mode.</typeparam>
        /// <param name="reader">The bounded bit reader.</param>
        /// <param name="clusterIndex">The validated entropy cluster ID, with no further context mapping.</param>
        /// <param name="history">The caller-owned power-of-two history, empty without LZ77.</param>
        /// <param name="decodedCount">The number of preceding values in this section.</param>
        /// <param name="distanceMultiplier">Zero for linear history, or the modular channel width.</param>
        /// <returns>The literal or copied value. Nonempty history receives that value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadClusteredUInt32<TOperator>(
            ref JxlBitReader reader,
            int clusterIndex,
            Span<uint> history,
            uint decodedCount,
            int distanceMultiplier)
            where TOperator : struct, IEntropyOperator
        {
            // Both operator properties and calls specialize at JIT/AOT compilation. Prepared tree
            // leaves already supply clusters, so their residuals need no dependent context-map load.
            // Each mode resolves only the metadata it needs. Fixed 4/2/0 ANS needs neither a stored
            // table offset nor a hybrid configuration, so do not borrow a Cluster before those calls.
            if (!TOperator.UsesLz77)
            {
                uint token = TOperator.ReadCode(ref this, ref reader, clusterIndex, out JxlHybridUint integer);
                return TOperator.Expand(ref reader, token, integer);
            }

            if (this.remainingCopy != 0)
            {
                return this.ContinueCopy(history, decodedCount);
            }

            uint value = this.ReadClusteredToken<TOperator>(ref reader, clusterIndex, out uint copyCount);
            if (copyCount != 0)
            {
                if (decodedCount == 0)
                {
                    throw new InvalidImageContentException("The JPEG XL LZ77 copy precedes the first decoded value.");
                }

                // Copies can overlap their own output. A fixed backward distance and a circular write position
                // reproduce that sequence one value at a time, including after the window wraps.
                // Image streams map the first 120 symbols to two-dimensional offsets. ICC and tree
                // streams use plain distances. All variants cap distance to the window and available history.
                ulong distance = distanceMultiplier == 0
                    ? (ulong)value + 1
                    : value < Lz77DistanceCodes.Count
                        ? (uint)Lz77DistanceCodes.GetDistance((int)value, distanceMultiplier)
                        : (ulong)value - Lz77DistanceCodes.Count + 1;

                this.copyDistance = (int)Math.Min(Math.Min(distance, MaximumLz77Distance), decodedCount);
                this.remainingCopy = copyCount - 1;
                value = history[(int)((decodedCount - (uint)this.copyDistance) & (uint)(history.Length - 1))];
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
        /// <param name="context">The consuming syntax's original context.</param>
        /// <param name="copyCount">Zero for a literal, or the decoded copy length.</param>
        /// <returns>The literal or distance symbol. Image consumers apply their own two-dimensional distance mapping.</returns>
        public uint ReadToken(ref JxlBitReader reader, int context, out uint copyCount)
        {
            int clusterIndex = this.contextMap[context];

            if (this.code.UsesPrefixCodes)
            {
                return this.code.UsesLz77
                    ? this.ReadClusteredToken<PrefixLz77Operator>(ref reader, clusterIndex, out copyCount)
                    : this.ReadClusteredToken<PrefixOperator>(ref reader, clusterIndex, out copyCount);
            }

            return this.code.UsesLz77
                ? this.ReadClusteredToken<AnsLz77Operator>(ref reader, clusterIndex, out copyCount)
                : this.ReadClusteredToken<AnsOperator>(ref reader, clusterIndex, out copyCount);
        }

        /// <summary>
        /// Reads and expands a literal or copy token using the selected symbol decoder and the owner's copy configuration.
        /// </summary>
        /// <typeparam name="TOperator">The completed tables' symbol and LZ77 mode.</typeparam>
        /// <param name="reader">The bounded bit reader.</param>
        /// <param name="clusterIndex">The already-resolved entropy cluster.</param>
        /// <param name="copyCount">Zero for a literal, or the decoded copy length.</param>
        /// <returns>The literal or copy-distance value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint ReadClusteredToken<TOperator>(ref JxlBitReader reader, int clusterIndex, out uint copyCount)
            where TOperator : struct, IEntropyOperator
        {
            uint token = TOperator.ReadCode(ref this, ref reader, clusterIndex, out JxlHybridUint integer);
            if (token < this.code.minimumCopySymbol)
            {
                copyCount = 0;
                return TOperator.Expand(ref reader, token, integer);
            }

            uint length = this.code.copyLength.Expand(ref reader, token - this.code.minimumCopySymbol);
            if (length > uint.MaxValue - this.code.minimumCopyLength)
            {
                throw new InvalidImageContentException("The JPEG XL LZ77 copy length overflows.");
            }

            copyCount = length + this.code.minimumCopyLength;

            // Copy distance always uses the extra final context, not the context that selected the copy token.
            int distanceCluster = this.contextMap[^1];
            uint distance = TOperator.ReadCode(ref this, ref reader, distanceCluster, out JxlHybridUint distanceInteger);
            return distanceInteger.Expand(ref reader, distance);
        }

        /// <summary>
        /// Validates the final ANS state after the consumer has read its required values.
        /// </summary>
        public readonly void CheckFinalState()
        {
            // A consumer can stop within the final LZ77 copy. Its unused values require no more entropy symbols.
            // Both reference decoders validate the ANS state without requiring that copy to finish.
            if (this.state != FinalAnsState)
            {
                throw new InvalidImageContentException("The JPEG XL entropy section has an invalid final state.");
            }
        }

        /// <summary>
        /// Decodes one prefix symbol from the selected cluster's cached lookup table.
        /// </summary>
        /// <param name="reader">The bounded bit reader.</param>
        /// <param name="clusterIndex">The validated cluster ID whose variable-sized prefix table supplies the symbol.</param>
        /// <param name="integer">The same cluster's validated hybrid configuration for subsequent expansion.</param>
        /// <returns>The unexpanded symbol.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadPrefixCode(ref JxlBitReader reader, int clusterIndex, out JxlHybridUint integer)
        {
            // Prefix table sizes vary. Borrow the cluster once for both its table offset and its hybrid widths.
            // The owner keeps this immutable reference valid across refills. Read widths only after the token,
            // so the decoder retains one reference rather than four configuration values during bit reading.
            ref readonly Cluster cluster = ref this.clusters[clusterIndex];
            int tableOffset = cluster.TableOffset;

            // The first eight bits select the cluster's root entry. A longer entry points into a
            // second-level table relative to that root position; only then consume the root bits separately.
            int position = tableOffset + (int)reader.PeekBits(8);
            HuffmanCode entry = this.prefix[position];
            if (entry.BitsUsed > 8)
            {
                reader.ConsumeBits(8);
                position += (int)entry.Value + (int)reader.PeekBits(entry.BitsUsed - 8);
                entry = this.prefix[position];
            }

            reader.ConsumeBits(entry.BitsUsed);
            integer = cluster.Integer;
            return entry.Value;
        }

        /// <summary>
        /// Decodes one ANS symbol and updates this section's ANS state.
        /// </summary>
        /// <param name="reader">The bounded bit reader.</param>
        /// <param name="clusterIndex">The validated cluster ID whose fixed-size alias table supplies the symbol.</param>
        /// <returns>The unexpanded symbol.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadAnsCode(ref JxlBitReader reader, int clusterIndex)
        {
            // Split the state's low twelve bits into a bucket number and a position within that bucket.
            // The alias entry supplies the alternate frequency and cumulative offset without a 4096-entry table.
            uint index = this.state & (JxlAnsDecoder.ProbabilityTotal - 1);
            int bucketIndex = (int)(index >> this.bucketBits);
            uint positionInBucket = index & this.bucketMask;

            // Every ANS cluster has 2^alphabetBits buckets. The low twelve state bits give bucketIndex
            // in 0..2^alphabetBits-1, so its bits do not overlap the shifted, validated cluster ID.
            // Prefix tables have variable sizes and resolve their offsets separately in ReadPrefixCode.
            JxlAnsDecoder.Bucket bucket = this.buckets[(clusterIndex << this.alphabetBits) | bucketIndex];

            // Unsigned subtraction maps a zero selector to zero and a one selector to all one bits.
            // Each AND either clears a complete field or retains it unchanged. The complementary mask keeps
            // only the direct bucket index. This keeps field selection off the multiplication chain used by the ANS state.
            uint selectAlias = positionInBucket >= bucket.Cutoff ? 1U : 0U;
            uint aliasMask = 0U - selectAlias;
            uint frequency = bucket.Frequency ^ (bucket.AliasFrequencyXor & aliasMask);
            uint offset = positionInBucket + (bucket.AliasOffset & aliasMask);
            uint symbol = (bucket.AliasSymbol & aliasMask) | ((uint)bucketIndex & ~aliasMask);
            this.state = ((this.state >> JxlAnsDecoder.ProbabilityBits) * frequency) + offset;

            // ANS consumes sixteen bits only when the updated state falls below its normalization threshold.
            // The normalized frequencies ensure that one sixteen-bit refill restores the required state range.
            if (this.state < 1 << 16)
            {
                this.state = (this.state << 16) | reader.ReadBits(16);
            }

            return symbol;
        }

        /// <summary>
        /// Gets the selected cluster's validated hybrid-integer configuration for a general ANS mode.
        /// </summary>
        /// <param name="clusterIndex">The validated cluster ID in the borrowed configuration span.</param>
        /// <returns>The configuration copied from the table owner, which outlives this reader.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly JxlHybridUint GetIntegerConfiguration(int clusterIndex) => this.clusters[clusterIndex].Integer;

        /// <summary>
        /// Continues an active copy through the same caller-owned circular history for both reader entry points.
        /// </summary>
        /// <param name="history">The nonempty power-of-two history already used by the copy's first value.</param>
        /// <param name="decodedCount">The current logical history position.</param>
        /// <returns>The copied value, also retained at the current history position.</returns>
        private uint ContinueCopy(Span<uint> history, uint decodedCount)
        {
            // A copy already read its first value from this window. The source read therefore requires
            // nonempty history, and overlapping writes reproduce runs without another token or context lookup.
            this.remainingCopy--;
            uint value = history[(int)((decodedCount - (uint)this.copyDistance) & (uint)(history.Length - 1))];
            history[(int)(decodedCount & (uint)(history.Length - 1))] = value;
            return value;
        }
    }

    /// <summary>
    /// Stores one entropy table's integer configuration and offset in the shared lookup buffer.
    /// </summary>
    private readonly struct Cluster
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Cluster"/> struct.
        /// </summary>
        /// <param name="integer">The integer configuration for this cluster's symbols.</param>
        /// <param name="tableOffset">The first entry of its Huffman or ANS table.</param>
        public Cluster(JxlHybridUint integer, int tableOffset)
        {
            this.Integer = integer;
            this.TableOffset = tableOffset;
        }

        /// <summary>
        /// Gets the integer configuration for this cluster's symbols.
        /// </summary>
        public JxlHybridUint Integer { get; }

        /// <summary>
        /// Gets the first entry of this cluster's table in the shared lookup buffer.
        /// </summary>
        public int TableOffset { get; }
    }
}
