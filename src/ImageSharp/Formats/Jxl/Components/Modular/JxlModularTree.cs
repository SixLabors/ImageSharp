// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Components.Entropy;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Modular;

/// <summary>
/// Owns a modular prediction tree and the entropy tables selected by its leaves.
/// </summary>
internal sealed class JxlModularTree : IDisposable
{
    /// <summary>
    /// The six entropy contexts encode split values, properties, predictors, offsets and two multiplier fields.
    /// </summary>
    private const int TreeContextCount = 6;

    /// <summary>
    /// The entropy context for signed split thresholds.
    /// </summary>
    private const int SplitValueContext = 0;

    /// <summary>
    /// The entropy context for a split property plus one, or zero for a leaf.
    /// </summary>
    private const int PropertyContext = 1;

    /// <summary>
    /// The entropy context for a leaf's predictor code.
    /// </summary>
    private const int PredictorContext = 2;

    /// <summary>
    /// The entropy context for signed predictor offsets.
    /// </summary>
    private const int OffsetContext = 3;

    /// <summary>
    /// The entropy context for the residual multiplier's base-two shift.
    /// </summary>
    private const int MultiplierLogContext = 4;

    /// <summary>
    /// The entropy context for the residual multiplier's unshifted factor minus one.
    /// </summary>
    private const int MultiplierBitsContext = 5;

    /// <summary>
    /// The largest validation stack accepted by libjxl's modular tree reader.
    /// </summary>
    private const int MaximumDepth = 2048;

    /// <summary>
    /// The initial node capacity. Larger trees grow only while their metadata is parsed.
    /// </summary>
    private const int InitialCapacity = 64;

    /// <summary>
    /// The interval lookup's first representable property value, as defined by jxl-rs LUT_MIN_SPLITVAL.
    /// </summary>
    private const int MinimumLookupValue = -1024;

    /// <summary>
    /// The interval lookup's last representable property value, as defined by jxl-rs LUT_MAX_SPLITVAL.
    /// </summary>
    private const int MaximumLookupValue = 1023;

    /// <summary>
    /// The number of signed property values represented by each interval table.
    /// </summary>
    private const int LookupLength = MaximumLookupValue - MinimumLookupValue + 1;

    /// <summary>
    /// The largest candidate-leaf count accepted by the reference bitset representation.
    /// </summary>
    private const int MaximumLookupLeaves = 256;

    /// <summary>
    /// The largest distinct property count accepted by the reference bitset representation.
    /// </summary>
    private const int MaximumLookupProperties = 8;

    /// <summary>
    /// The largest property ID represented by the lookup's 64-bit requirement mask.
    /// </summary>
    private const int MaximumLookupProperty = 63;

    /// <summary>
    /// The largest channel sample count that retains direct traversal to avoid lookup preparation cost.
    /// </summary>
    private const int SmallChannelSampleCount = 64;

    private readonly IMemoryOwner<Node> nodes;
    private readonly int nodeCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlModularTree"/> class and takes ownership of its completed storage.
    /// </summary>
    /// <param name="nodes">The allocator-owned nodes in breadth-first order.</param>
    /// <param name="nodeCount">The number of initialized nodes.</param>
    /// <param name="codes">The sample entropy tables indexed by leaf context.</param>
    private JxlModularTree(IMemoryOwner<Node> nodes, int nodeCount, JxlEntropyDecoder codes)
    {
        this.nodes = nodes;
        this.nodeCount = nodeCount;
        this.Codes = codes;
    }

    /// <summary>
    /// Gets the initialized tree nodes. Completed leaves name sample entropy clusters; splits name adjacent child nodes.
    /// </summary>
    public ReadOnlySpan<Node> Nodes => this.nodes.Memory.Span[..this.nodeCount];

    /// <summary>
    /// Gets the sample entropy tables. Completed leaf cluster IDs select these tables directly.
    /// </summary>
    public JxlEntropyDecoder Codes { get; }

    /// <summary>
    /// Reads and validates one modular tree followed by its sample entropy tables.
    /// </summary>
    /// <param name="reader">The bounded reader positioned at the tree entropy description.</param>
    /// <param name="allocator">The decoder's allocator for all retained and temporary storage.</param>
    /// <param name="maximumNodes">The image-derived node limit, from one through the format limit of 4194304.</param>
    /// <returns>The owner of the completed tree and sample entropy tables.</returns>
    public static JxlModularTree Read(ref JxlBitReader reader, MemoryAllocator allocator, int maximumNodes)
    {
        IMemoryOwner<Node> nodeOwner = allocator.Allocate<Node>(Math.Min(maximumNodes, InitialCapacity));
        JxlEntropyDecoder? sampleCodes = null;
        try
        {
            int count = 0;
            int leaves = 0;
            int propertyCount = 0;
            Span<Node> nodes = nodeOwner.Memory.Span;
            using (JxlEntropyDecoder treeCodes = JxlEntropyDecoder.Read(ref reader, allocator, TreeContextCount, true))
            {
                // A leaf uses five values and a split uses two. This bounds the complete history before parsing begins.
                // Tree distances are linear, unlike modular sample distances. Retain a ring only when LZ77 is enabled.
                int historyLength = treeCodes.UsesLz77
                    ? (int)BitOperations.RoundUpToPowerOf2((uint)Math.Min((long)maximumNodes * 5, JxlEntropyDecoder.MaximumLz77Distance))
                    : 0;

                using IMemoryOwner<uint>? historyOwner = historyLength == 0 ? null : allocator.Allocate<uint>(historyLength);
                Span<uint> history = historyOwner is null ? [] : historyOwner.Memory.Span;
                JxlEntropyDecoder.Reader symbols = treeCodes.CreateReader(ref reader);
                uint decodedCount = 0;
                int pending = 1;

                // Wire order is breadth-first. Pending nodes already have reserved positions after the current node.
                // Every split appends two adjacent children to that queue; each leaf receives the next sample context.
                while (pending != 0)
                {
                    if (count == nodes.Length)
                    {
                        int capacity = Math.Min(maximumNodes, nodes.Length * 2);
                        IMemoryOwner<Node> replacement = allocator.Allocate<Node>(capacity);
                        Span<Node> replacementNodes = replacement.Memory.Span;
                        nodes[..count].CopyTo(replacementNodes);

                        // The larger capacity already holds every initialized node. No copy can exceed that span.
                        // An allocation failure leaves the previous owner available to the enclosing cleanup.
                        nodeOwner.Dispose();
                        nodeOwner = replacement;
                        nodes = replacementNodes;
                    }

                    pending--;
                    uint property = ReadValue(ref reader, ref symbols, PropertyContext, history, ref decodedCount);
                    if (property > 256)
                    {
                        throw new InvalidImageContentException("The JPEG XL modular tree property is invalid.");
                    }

                    if (property != 0)
                    {
                        // The current node and all pending nodes must fit before two children can be appended.
                        // The caller's format-bounded limit also keeps queue indices and capacity doubling within int.
                        if (count + pending + 3 > maximumNodes)
                        {
                            throw new InvalidImageContentException("The JPEG XL modular tree exceeds its node limit.");
                        }

                        uint split = ReadValue(ref reader, ref symbols, SplitValueContext, history, ref decodedCount);
                        int value = (int)(split >> 1) ^ -(int)(split & 1);
                        nodes[count] = new Node((int)property - 1, value, count + pending + 1, 0, 1);
                        propertyCount = Math.Max(propertyCount, (int)property);
                        pending += 2;
                    }
                    else
                    {
                        uint predictor = ReadValue(ref reader, ref symbols, PredictorContext, history, ref decodedCount);
                        uint offset = ReadValue(ref reader, ref symbols, OffsetContext, history, ref decodedCount);
                        uint logarithm = ReadValue(ref reader, ref symbols, MultiplierLogContext, history, ref decodedCount);
                        uint bits = ReadValue(ref reader, ref symbols, MultiplierBitsContext, history, ref decodedCount);
                        if (predictor > 13 || logarithm >= 31 || bits >= (1U << (31 - (int)logarithm)) - 1U)
                        {
                            throw new InvalidImageContentException("The JPEG XL modular tree leaf is invalid.");
                        }

                        // Signed residuals and offsets use the same zigzag mapping: 0, -1, 1, -2, ... .
                        // The residual multiplier must be positive and below 2^31.
                        // Widen before its construction so malformed fields cannot wrap during validation.
                        int value = (int)(offset >> 1) ^ -(int)(offset & 1);
                        uint multiplier = (uint)(((ulong)bits + 1) << (int)logarithm);
                        nodes[count] = new Node(-1, value, leaves++, (byte)predictor, multiplier);
                    }

                    count++;
                }

                symbols.CheckFinalState();
            }

            Validate(nodes[..count], propertyCount, allocator);

            // The tree's entropy state ends before these descriptions. Sample symbols use one context per leaf,
            // with their own reader and LZ77 history; tree tokens never enter the sample history.
            sampleCodes = JxlEntropyDecoder.Read(ref reader, allocator, leaves, true);

            // Each parsed leaf initially names one sample context. Resolve that immutable map once,
            // after its tables exist and before publishing any nodes. All channel pruning and lookup
            // forms then retain the cluster ID directly, including a one-leaf tree with no prepared-node allocation.
            ReadOnlySpan<byte> contextMap = sampleCodes.ContextMap;
            for (int i = 0; i < count; i++)
            {
                ref readonly Node node = ref nodes[i];
                if (node.Property < 0)
                {
                    nodes[i] = new Node(node.Property, node.Value, contextMap[node.FirstChild], node.Predictor, node.Multiplier);
                }
            }

            return new JxlModularTree(nodeOwner, count, sampleCodes);
        }
        catch
        {
            sampleCodes?.Dispose();
            nodeOwner.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Prepares a bounded interval lookup for mixed-predictor trees, or returns an empty view for the ordinary tree walk.
    /// </summary>
    /// <param name="nodes">The statically reduced, breadth-first tree. Its storage remains unchanged during lookup.</param>
    /// <param name="sampleCount">The number of samples in this channel.</param>
    /// <param name="workspace">The frame-owned table storage and temporary metadata scratch.</param>
    /// <returns>A borrowed lookup whose tables remain valid until the next channel is prepared, or an empty view.</returns>
    /// <remarks>
    /// Eligibility follows jxl-rs specialized_trees/mod.rs and bitset_tree.rs at revision
    /// d7a831fae15b903f1b56a76668030c19ac07bb6f. Small channels retain the ordinary walk to avoid table setup cost.
    /// At most 256 leaves, eight properties below 64 and thresholds from -1024 through 1022 can use this representation.
    /// Other trees retain their full property and threshold ranges through the existing walk.
    /// </remarks>
    public static Lookup PrepareLookup(ReadOnlySpan<Node> nodes, long sampleCount, JxlModularWorkspace workspace)
    {
        if (sampleCount <= SmallChannelSampleCount || nodes.Length <= 1 || nodes.Length > (2 * MaximumLookupLeaves) - 1)
        {
            return default;
        }

        ulong propertyMask = 0;
        int depth = 0;
        int levelEnd = 1;
        int nextLevelEnd = 1;
        for (int i = 0; i < nodes.Length; i++)
        {
            if (i == levelEnd)
            {
                depth++;
                levelEnd = nextLevelEnd;
            }

            ref readonly Node node = ref nodes[i];
            if (node.Property >= 0)
            {
                // The last table value is 1023. A threshold of 1023 would leave its greater branch
                // outside the table, so it must use the unrestricted fallback instead.
                if (node.Property > MaximumLookupProperty || node.Value < MinimumLookupValue || node.Value >= MaximumLookupValue)
                {
                    return default;
                }

                propertyMask |= 1UL << node.Property;
                nextLevelEnd = Math.Max(nextLevelEnd, node.FirstChild + 2);
            }
        }

        int propertyCount = BitOperations.PopCount(propertyMask);
        int minimumDepth = propertyCount == 1 ? 1 : propertyCount == 2 ? 3 : 5;
        if (propertyCount > MaximumLookupProperties || depth < minimumDepth)
        {
            return default;
        }

        int leafCount = (nodes.Length + 1) / 2;
        int splitCount = leafCount - 1;
        int frameLength = 1 + (2 * propertyCount);

        // Setup uses the existing integer reference scratch before any row references are borrowed.
        // A depth-first pending stack has at most leafCount+1 frames. Each frame stores its source
        // node and one half-open value interval per property. Its storage does not escape this method.
        Span<int> scratch = workspace.PrepareReferences(Math.Max(splitCount, (leafCount + 1) * frameLength));
        Span<int> splits = scratch[..splitCount];
        int splitIndex = 0;
        foreach (ref readonly Node node in nodes)
        {
            if (node.Property >= 0)
            {
                int ordinal = BitOperations.PopCount(propertyMask & ((1UL << node.Property) - 1));
                splits[splitIndex++] = (ordinal * LookupLength) + node.Value + 1 - MinimumLookupValue;
            }
        }

        // Sorting at most 255 metadata thresholds identifies the actual intervals. Repeated thresholds
        // share a mask entry; table allocation therefore follows this tree rather than the format maximum.
        splits.Sort();
        InlineArray8<int> intervalCounts = default;
        Span<int> counts = intervalCounts;
        counts[..propertyCount].Fill(1);
        int previousSplit = -1;
        foreach (int split in splits)
        {
            if (split != previousSplit)
            {
                counts[split / LookupLength]++;
                previousSplit = split;
            }
        }

        int bucketStride = leafCount <= 64 ? 1 : leafCount <= 128 ? 2 : 4;
        int intervalCount = 0;
        foreach (int count in counts[..propertyCount])
        {
            intervalCount += count;
        }

        // Eight bytes hold the property IDs. Align the offset metadata and masks to eight bytes;
        // all pixel-time spans then point into this single tracked owner. A one-property tree maps
        // directly to leaf IDs and needs neither offsets nor masks.
        int offsetBytes = propertyCount == 1 ? 0 : ((propertyCount * sizeof(int)) + 7) & ~7;
        int maskLength = propertyCount == 1 ? 0 : intervalCount * bucketStride;
        int byteLength = 8 + (propertyCount * LookupLength) + offsetBytes + (maskLength * sizeof(ulong)) + (leafCount * sizeof(int));
        Span<byte> bytes = MemoryMarshal.AsBytes(workspace.PrepareTreeLookup((byteLength + 7) / 8));
        Span<byte> propertyIds = bytes[..propertyCount];
        Span<byte> tables = bytes.Slice(8, propertyCount * LookupLength);
        int position = 8 + tables.Length;
        Span<int> offsets = MemoryMarshal.Cast<byte, int>(bytes.Slice(position, offsetBytes));
        position += offsetBytes;
        Span<ulong> masks = MemoryMarshal.Cast<byte, ulong>(bytes.Slice(position, maskLength * sizeof(ulong)));
        position += maskLength * sizeof(ulong);
        Span<int> leaves = MemoryMarshal.Cast<byte, int>(bytes.Slice(position, leafCount * sizeof(int)));
        InlineArray64<byte> ordinalStorage = default;
        Span<byte> ordinals = ordinalStorage;
        ulong remainingProperties = propertyMask;
        for (int i = 0; i < propertyCount; i++)
        {
            byte property = (byte)BitOperations.TrailingZeroCount(remainingProperties);
            propertyIds[i] = property;
            ordinals[property] = (byte)i;
            remainingProperties &= remainingProperties - 1;
        }

        if (propertyCount != 1)
        {
            tables.Clear();
            foreach (int split in splits)
            {
                tables[split] = 1;
            }

            int maskOffset = 0;
            for (int p = 0; p < propertyCount; p++)
            {
                offsets[p] = maskOffset;
                maskOffset += counts[p] * bucketStride;
                Span<byte> table = tables.Slice(p * LookupLength, LookupLength);
                int interval = 0;
                for (int value = 0; value < table.Length; value++)
                {
                    interval += table[value];
                    table[value] = (byte)interval;
                }
            }

            masks.Clear();
        }

        // The sorted thresholds are no longer needed. Reuse their scratch for pending node ranges.
        Span<int> stack = scratch;
        stack[..frameLength].Clear();
        for (int p = 0; p < propertyCount; p++)
        {
            stack[2 + (2 * p)] = LookupLength;
        }

        int pending = 1;
        int leaf = 0;
        while (pending != 0)
        {
            Span<int> frame = stack.Slice(--pending * frameLength, frameLength);
            int nodeIndex = frame[0];
            ref readonly Node node = ref nodes[nodeIndex];
            if (node.Property >= 0)
            {
                int property = ordinals[node.Property];
                int range = 1 + (2 * property);
                int cut = node.Value + 1 - MinimumLookupValue;

                // Tree validation already requires two nonempty branches. The accepted thresholds also
                // lie inside the lookup range, so this cut lies strictly inside its inherited interval.
                Span<int> first = stack.Slice(pending * frameLength, frameLength);
                Span<int> second = stack.Slice((pending + 1) * frameLength, frameLength);
                frame.CopyTo(second);
                first[0] = node.FirstChild;
                first[range] = cut;
                second[0] = node.FirstChild + 1;
                second[range + 1] = cut;
                pending += 2;
                continue;
            }

            leaves[leaf] = nodeIndex;
            if (propertyCount == 1)
            {
                tables[frame[1]..frame[2]].Fill((byte)leaf);
            }
            else
            {
                int bucket = leaf >> 6;
                ulong bit = 1UL << (leaf & 63);
                for (int p = 0; p < propertyCount; p++)
                {
                    int range = 1 + (2 * p);
                    ReadOnlySpan<byte> table = tables.Slice(p * LookupLength, LookupLength);
                    int first = table[frame[range]];
                    int last = table[frame[range + 1] - 1];
                    for (int interval = first; interval <= last; interval++)
                    {
                        masks[offsets[p] + (interval * bucketStride) + bucket] |= bit;
                    }
                }
            }

            leaf++;
        }

        return new Lookup(propertyMask, propertyIds, tables, offsets[..(propertyCount == 1 ? 0 : propertyCount)], masks, leaves, bucketStride);
    }

    /// <summary>
    /// Combines two tree levels per split so sample traversal selects one of four adjacent descendants.
    /// </summary>
    /// <param name="nodes">The validated, statically reduced tree in breadth-first order.</param>
    /// <param name="workspace">The frame-owned storage reused between channels.</param>
    /// <returns>The completed flat tree, borrowed until the next channel is prepared.</returns>
    /// <remarks>
    /// This follows jxl-rs flat_tree.rs at revision d7a831fae15b903f1b56a76668030c19ac07bb6f.
    /// Leaves retain their complete entropy and prediction fields inside the flat storage, without a second source-node lookup.
    /// </remarks>
    public static ReadOnlySpan<FlatNode> PrepareFlatTree(ReadOnlySpan<Node> nodes, JxlModularWorkspace workspace)
    {
        int depth = 0;
        int levelEnd = 1;
        int nextLevelEnd = 1;
        int splits = 0;
        for (int i = 0; i < nodes.Length; i++)
        {
            if (i == levelEnd)
            {
                depth++;
                levelEnd = nextLevelEnd;
            }

            ref readonly Node node = ref nodes[i];
            if (node.Property >= 0)
            {
                nextLevelEnd = Math.Max(nextLevelEnd, node.FirstChild + 2);
                splits += (depth & 1) == 0 ? 1 : 0;
            }
        }

        // Every even-depth split emits four descendants. A leaf at the intervening level is emitted
        // twice, so selecting either of its two descendants reaches the same original leaf. These
        // duplicates are included in the exact 4*splits+1 length before the only allocation is prepared.
        Span<FlatNode> flat = workspace.PrepareFlatTree((4 * splits) + 1);
        flat[0] = new FlatNode(0);
        int pending = 1;
        for (int i = 0; i < pending; i++)
        {
            int sourceIndex = flat[i].FirstChild;
            ref readonly Node node = ref nodes[sourceIndex];
            if (node.Property < 0)
            {
                // Replace the queued source index with its complete payload before publishing the tree.
                // The copied Property=-1 also supplies the leaf tag through the shared first four bytes.
                flat[i] = new FlatNode(in node);
                continue;
            }

            ref readonly Node greater = ref nodes[node.FirstChild];
            ref readonly Node lesser = ref nodes[node.FirstChild + 1];
            int greaterFirst = greater.Property < 0 ? node.FirstChild : greater.FirstChild;
            int greaterSecond = greater.Property < 0 ? greaterFirst : greaterFirst + 1;
            int lesserFirst = lesser.Property < 0 ? node.FirstChild + 1 : lesser.FirstChild;
            int lesserSecond = lesser.Property < 0 ? lesserFirst : lesserFirst + 1;
            flat[pending] = new FlatNode(greaterFirst);
            flat[pending + 1] = new FlatNode(greaterSecond);
            flat[pending + 2] = new FlatNode(lesserFirst);
            flat[pending + 3] = new FlatNode(lesserSecond);

            // Unprocessed entries hold source indices in FirstChild. The current entry is replaced
            // only after its descendants are queued. No queue allocation or source-tree mutation is needed.
            flat[i] = new FlatNode(
                (byte)node.Property,
                node.Value,
                greater.Property < 0 ? (byte)0 : (byte)greater.Property,
                greater.Value,
                lesser.Property < 0 ? (byte)0 : (byte)lesser.Property,
                lesser.Value,
                pending);

            pending += 4;
        }

        return flat;
    }

    /// <summary>
    /// Selects a retained leaf through a completed flat tree without restricting property values or tree size.
    /// </summary>
    /// <param name="nodes">The nonempty result of PrepareFlatTree, unchanged until this method returns.</param>
    /// <param name="properties">The fixed 256-entry property array. Validated byte indices cover its complete domain.</param>
    /// <returns>The matching leaf, borrowed until the flat workspace is prepared for another channel.</returns>
    public static ref readonly Node FindFlatLeaf(ReadOnlySpan<FlatNode> nodes, ref InlineArray256<int> properties)
    {
        ref FlatNode first = ref MemoryMarshal.GetReference(nodes);
        ref readonly FlatNode node = ref first;
        while (!node.IsLeaf)
        {
            // Byte property IDs always address the fixed 256-entry inline array. Their bound is
            // independent of the compiled node indices and needs no further property-range validation.
            int parentLow = properties[node.Property] <= node.Value ? 1 : 0;
            int greaterLow = properties[node.GreaterProperty] <= node.GreaterValue ? 1 : 0;
            int lesserLow = properties[node.LesserProperty] <= node.LesserValue ? 1 : 0;

            // PrepareFlatTree writes all four children through bounds-checked span indexing before publication.
            // FirstChild + 3 is therefore inside the completed span, and these comparisons select offsets 0..3.
            // Children also follow their parent, so traversal advances to a leaf through valid managed references.
            // Each comparison is zero or one. The parent bit selects pair 0..1 or pair 2..3; AND masks retain
            // only that pair's low bit. This forms the same child offset without a data-dependent branch.
            int low = (greaterLow & (parentLow ^ 1)) | (lesserLow & parentLow);
            int child = node.FirstChild + ((parentLow << 1) | low);
            node = ref Unsafe.Add(ref first, child);
        }

        return ref node.Leaf;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Codes.Dispose();
        this.nodes.Dispose();
    }

    /// <summary>
    /// Reads a tree value and retains its position in the shared linear LZ77 history.
    /// </summary>
    /// <param name="reader">The bounded tree bit reader.</param>
    /// <param name="symbols">The entropy state shared by all six tree contexts.</param>
    /// <param name="context">The field context, from zero through five.</param>
    /// <param name="history">The circular value history, or an empty span without LZ77.</param>
    /// <param name="decodedCount">The number of preceding tree values.</param>
    /// <returns>The decoded unsigned field.</returns>
    private static uint ReadValue(
        ref JxlBitReader reader, ref JxlEntropyDecoder.Reader symbols, int context, Span<uint> history, ref uint decodedCount)
    {
        uint value = symbols.ReadUInt32(ref reader, context, history, decodedCount);
        decodedCount++;
        return value;
    }

    /// <summary>
    /// Rejects branches with empty property ranges and bounds the depth of subsequent tree traversal.
    /// </summary>
    /// <param name="nodes">The complete tree in breadth-first order.</param>
    /// <param name="propertyCount">The number of property indices used by its splits.</param>
    /// <param name="allocator">The allocator for temporary property ranges and the traversal stack.</param>
    private static void Validate(ReadOnlySpan<Node> nodes, int propertyCount, MemoryAllocator allocator)
    {
        if (nodes[0].Property == -1)
        {
            return;
        }

        using IMemoryOwner<int> rangeOwner = allocator.Allocate<int>(propertyCount * 2);
        Span<int> ranges = rangeOwner.Memory.Span;
        Span<int> lower = ranges[..propertyCount];
        Span<int> upper = ranges.Slice(propertyCount, propertyCount);
        lower.Fill(int.MinValue);
        upper.Fill(int.MaxValue);

        using IMemoryOwner<ValidationFrame> stackOwner = allocator.Allocate<ValidationFrame>(Math.Min(nodes.Length, MaximumDepth));
        Span<ValidationFrame> stack = stackOwner.Memory.Span;
        stack[0] = new ValidationFrame(0);
        int depth = 1;

        // Keep ranges for the current root-to-node path. Each stack frame saves only the range changed by its split.
        // Greater values follow the first child; lesser or equal values follow the adjacent second child.
        while (depth != 0)
        {
            if (depth >= MaximumDepth)
            {
                throw new InvalidImageContentException("The JPEG XL modular tree exceeds its depth limit.");
            }

            ref ValidationFrame frame = ref stack[depth - 1];
            ref readonly Node node = ref nodes[frame.NodeIndex];
            int property = node.Property;
            if (property == -1)
            {
                depth--;
            }
            else if (frame.Stage == 0)
            {
                if (lower[property] > node.Value || upper[property] <= node.Value)
                {
                    throw new InvalidImageContentException("The JPEG XL modular tree splits an empty property range.");
                }

                frame.OriginalLower = lower[property];
                frame.OriginalUpper = upper[property];
                frame.Stage = 1;

                // The range check proves Value is below int.MaxValue, so its greater-than endpoint cannot overflow.
                lower[property] = node.Value + 1;
                stack[depth++] = new ValidationFrame(node.FirstChild);
            }
            else if (frame.Stage == 1)
            {
                lower[property] = frame.OriginalLower;
                upper[property] = node.Value;
                frame.Stage = 2;
                stack[depth++] = new ValidationFrame(node.FirstChild + 1);
            }
            else
            {
                upper[property] = frame.OriginalUpper;
                depth--;
            }
        }
    }

    /// <summary>
    /// Stores one parent split with both child splits, or one complete retained leaf.
    /// </summary>
    /// <remarks>
    /// Both representations occupy twenty bytes. A split stores three byte property IDs and a zero tag,
    /// followed by three thresholds and its first compiled child. A leaf overlaps the unchanged sequential Node layout.
    /// Its Property=-1 writes all four leading bytes as 255, so byte three distinguishes leaves from splits on every byte order.
    /// No managed references overlap. Only the representation selected by the tag can be read after compilation.
    /// </remarks>
    [StructLayout(LayoutKind.Explicit, Size = 20)]
    public readonly struct FlatNode
    {
        /// <summary>
        /// The complete leaf payload, valid only when IsLeaf is true.
        /// </summary>
        [FieldOffset(0)]
        public readonly Node Leaf;

        /// <summary>
        /// The parent's validated property ID for a completed split.
        /// </summary>
        [FieldOffset(0)]
        public readonly byte Property;

        /// <summary>
        /// The greater child's property ID, ignored when both descendants contain the same leaf.
        /// </summary>
        [FieldOffset(1)]
        public readonly byte GreaterProperty;

        /// <summary>
        /// The lesser child's property ID, ignored when both descendants contain the same leaf.
        /// </summary>
        [FieldOffset(2)]
        public readonly byte LesserProperty;

        /// <summary>
        /// The parent's signed threshold for a completed split.
        /// </summary>
        [FieldOffset(4)]
        public readonly int Value;

        /// <summary>
        /// The greater child's signed threshold for a completed split.
        /// </summary>
        [FieldOffset(8)]
        public readonly int GreaterValue;

        /// <summary>
        /// The lesser child's signed threshold for a completed split.
        /// </summary>
        [FieldOffset(12)]
        public readonly int LesserValue;

        /// <summary>
        /// The first compiled descendant for a completed split, or a temporary source index during compilation.
        /// </summary>
        [FieldOffset(16)]
        public readonly int FirstChild;

        /// <summary>
        /// Zero for a split or queued entry, or 255 from a completed leaf's Property=-1.
        /// </summary>
        [FieldOffset(3)]
        private readonly byte tag;

        /// <summary>
        /// Initializes a new instance of the <see cref="FlatNode"/> struct for a queued source node.
        /// </summary>
        /// <param name="sourceIndex">The original reduced-tree node index.</param>
        public FlatNode(int sourceIndex)
        {
            this = default;
            this.FirstChild = sourceIndex;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FlatNode"/> struct with a completed source leaf.
        /// </summary>
        /// <param name="leaf">The validated leaf, whose Property is minus one and whose entropy ID is already a cluster.</param>
        public FlatNode(in Node leaf)
        {
            this = default;
            this.Leaf = leaf;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FlatNode"/> struct for a completed two-level split.
        /// </summary>
        /// <param name="property">The parent's validated property index.</param>
        /// <param name="value">The parent's signed threshold.</param>
        /// <param name="greaterProperty">The greater child's property, or zero when that child is a duplicated leaf.</param>
        /// <param name="greaterValue">The greater child's signed threshold.</param>
        /// <param name="lesserProperty">The lesser child's property, or zero when that child is a duplicated leaf.</param>
        /// <param name="lesserValue">The lesser child's signed threshold.</param>
        /// <param name="firstChild">The first of four adjacent compiled descendants.</param>
        public FlatNode(byte property, int value, byte greaterProperty, int greaterValue, byte lesserProperty, int lesserValue, int firstChild)
        {
            this = default;
            this.Property = property;
            this.GreaterProperty = greaterProperty;
            this.LesserProperty = lesserProperty;
            this.Value = value;
            this.GreaterValue = greaterValue;
            this.LesserValue = lesserValue;
            this.FirstChild = firstChild;
        }

        /// <summary>
        /// Gets a value indicating whether this completed entry contains a retained leaf payload.
        /// </summary>
        public bool IsLeaf => this.tag != 0;
    }

    /// <summary>
    /// Borrows a prepared mixed-tree selector without taking ownership of its tables or source nodes.
    /// </summary>
    public readonly ref struct Lookup
    {
        private readonly ReadOnlySpan<byte> properties;
        private readonly ReadOnlySpan<byte> tables;
        private readonly ReadOnlySpan<int> offsets;
        private readonly ReadOnlySpan<ulong> masks;
        private readonly ReadOnlySpan<int> leaves;
        private readonly int bucketStride;

        /// <summary>
        /// Initializes a new instance of the <see cref="Lookup"/> struct from one frame-owned table allocation.
        /// </summary>
        /// <param name="propertyMask">The distinct selected property indices.</param>
        /// <param name="properties">The selected property IDs in ascending order.</param>
        /// <param name="tables">The 2048-entry interval or leaf lookup for each selected property.</param>
        /// <param name="offsets">The first mask word for each property's actual intervals.</param>
        /// <param name="masks">Candidate-leaf masks, with one, two or four words per interval.</param>
        /// <param name="leaves">The reduced-tree node index for each candidate leaf.</param>
        /// <param name="bucketStride">The mask words per interval. A fourth word pads three-bucket trees for SIMD loading.</param>
        public Lookup(
            ulong propertyMask,
            ReadOnlySpan<byte> properties,
            ReadOnlySpan<byte> tables,
            ReadOnlySpan<int> offsets,
            ReadOnlySpan<ulong> masks,
            ReadOnlySpan<int> leaves,
            int bucketStride)
        {
            this.PropertyMask = propertyMask;
            this.properties = properties;
            this.tables = tables;
            this.offsets = offsets;
            this.masks = masks;
            this.leaves = leaves;
            this.bucketStride = bucketStride;
        }

        /// <summary>
        /// Gets a value indicating whether the unrestricted binary tree must be used instead.
        /// </summary>
        public bool IsEmpty => this.properties.IsEmpty;

        /// <summary>
        /// Gets the distinct property requirements, with bit n representing property n.
        /// </summary>
        public ulong PropertyMask { get; }

        /// <summary>
        /// Gets the required property IDs in ascending order. Property eight therefore precedes property nine.
        /// </summary>
        public ReadOnlySpan<byte> PropertyIndices => this.properties;

        /// <summary>
        /// Selects the exact source-tree leaf by intersecting the intervals satisfied by the current properties.
        /// </summary>
        /// <param name="values">The current properties at their format-defined indices.</param>
        /// <returns>The matching leaf's index in the unchanged reduced tree.</returns>
        public int FindLeaf(ReadOnlySpan<int> values)
        {
            // Saturate only the lookup index. All thresholds lie inside this range, so a value outside
            // it selects exactly the same branches as its nearest table endpoint. Prediction values do not change.
            if (this.properties.Length == 1)
            {
                int index = Math.Clamp(values[this.properties[0]], MinimumLookupValue, MaximumLookupValue) - MinimumLookupValue;
                return this.leaves[this.tables[index]];
            }

            if (this.bucketStride == 1)
            {
                ulong candidates = ulong.MaxValue;
                for (int p = 0; p < this.properties.Length; p++)
                {
                    int value = Math.Clamp(values[this.properties[p]], MinimumLookupValue, MaximumLookupValue) - MinimumLookupValue;
                    int interval = this.tables[(p * LookupLength) + value];
                    candidates &= this.masks[this.offsets[p] + interval];
                }

                return this.leaves[BitOperations.TrailingZeroCount(candidates)];
            }

            ref ulong masks = ref MemoryMarshal.GetReference(this.masks);
            if (this.bucketStride == 2)
            {
                // Two lanes cover 128 leaves. Each property loads one contiguous candidate mask;
                // lane-wise AND retains the single leaf satisfying all property intervals.
                Vector128<ulong> candidates = Vector128.Create(ulong.MaxValue);
                for (int p = 0; p < this.properties.Length; p++)
                {
                    int value = Math.Clamp(values[this.properties[p]], MinimumLookupValue, MaximumLookupValue) - MinimumLookupValue;
                    int interval = this.tables[(p * LookupLength) + value];
                    nuint offset = (nuint)(this.offsets[p] + (interval * 2));
                    candidates &= Vector128.LoadUnsafe(ref masks, offset);
                }

                ulong first = candidates.GetElement(0);
                int leaf = first != 0 ? BitOperations.TrailingZeroCount(first) : 64 + BitOperations.TrailingZeroCount(candidates.GetElement(1));
                return this.leaves[leaf];
            }

            // Four lanes cover 256 leaves. A three-bucket tree has a zero fourth lane from setup.
            // Vector256 also retains exact lane-wise behavior on 128-bit and scalar fallback runtimes.
            Vector256<ulong> remaining = Vector256.Create(ulong.MaxValue);
            for (int p = 0; p < this.properties.Length; p++)
            {
                int value = Math.Clamp(values[this.properties[p]], MinimumLookupValue, MaximumLookupValue) - MinimumLookupValue;
                int interval = this.tables[(p * LookupLength) + value];
                nuint offset = (nuint)(this.offsets[p] + (interval * 4));
                remaining &= Vector256.LoadUnsafe(ref masks, offset);
            }

            for (int bucket = 0; bucket < 3; bucket++)
            {
                ulong candidates = remaining.GetElement(bucket);
                if (candidates != 0)
                {
                    return this.leaves[(bucket * 64) + BitOperations.TrailingZeroCount(candidates)];
                }
            }

            return this.leaves[192 + BitOperations.TrailingZeroCount(remaining.GetElement(3))];
        }
    }

    /// <summary>
    /// Stores one split or leaf without references so the allocator can own contiguous node storage.
    /// </summary>
    /// <remarks>
    /// Field order is also the compiled flat leaf layout: three integers, one predictor byte and one unsigned multiplier.
    /// Property must remain the first integer. Its minus-one leaf value supplies the overlapping flat entry's byte tag.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Node
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Node"/> struct from validated tree fields.
        /// </summary>
        /// <param name="property">The property index, or minus one for a leaf.</param>
        /// <param name="value">The split threshold or leaf predictor offset.</param>
        /// <param name="firstChild">The first split child, or a leaf entropy ID. Read maps raw leaf contexts to clusters before publication.</param>
        /// <param name="predictor">The leaf predictor code, from zero through thirteen.</param>
        /// <param name="multiplier">The positive leaf residual multiplier.</param>
        public Node(int property, int value, int firstChild, byte predictor, uint multiplier)
        {
            this.Property = property;
            this.Value = value;
            this.FirstChild = firstChild;
            this.Predictor = predictor;
            this.Multiplier = multiplier;
        }

        /// <summary>
        /// Gets the split property index, or minus one for a leaf.
        /// </summary>
        public int Property { get; }

        /// <summary>
        /// Gets the split threshold or leaf predictor offset.
        /// </summary>
        public int Value { get; }

        /// <summary>
        /// Gets the first adjacent split child index, or the sample entropy cluster for a completed leaf.
        /// </summary>
        public int FirstChild { get; }

        /// <summary>
        /// Gets the leaf predictor code, from zero through thirteen.
        /// </summary>
        public byte Predictor { get; }

        /// <summary>
        /// Gets the positive residual multiplier used after signed residual decoding.
        /// </summary>
        public uint Multiplier { get; }
    }

    /// <summary>
    /// Retains one split's traversal stage and the property range restored after its children.
    /// </summary>
    private struct ValidationFrame
    {
        /// <summary>
        /// The node examined by this traversal frame.
        /// </summary>
        public int NodeIndex;

        /// <summary>
        /// The property's lower bound before entering the first child.
        /// </summary>
        public int OriginalLower;

        /// <summary>
        /// The property's upper bound before entering the first child.
        /// </summary>
        public int OriginalUpper;

        /// <summary>
        /// Zero enters the node, one visits its second child and two restores the parent's range.
        /// </summary>
        public byte Stage;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationFrame"/> struct before visiting its first child.
        /// </summary>
        /// <param name="nodeIndex">The node to inspect.</param>
        public ValidationFrame(int nodeIndex)
        {
            this.NodeIndex = nodeIndex;
            this.OriginalLower = 0;
            this.OriginalUpper = 0;
            this.Stage = 0;
        }
    }
}
