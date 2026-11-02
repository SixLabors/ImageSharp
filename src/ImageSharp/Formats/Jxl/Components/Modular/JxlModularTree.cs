// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
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
    /// Gets the initialized tree nodes. Borrow this span once before decoding samples.
    /// </summary>
    public ReadOnlySpan<Node> Nodes => this.nodes.Memory.Span[..this.nodeCount];

    /// <summary>
    /// Gets the sample entropy tables. Leaf contexts select these tables through their context map.
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
            return new JxlModularTree(nodeOwner, count, sampleCodes);
        }
        catch
        {
            sampleCodes?.Dispose();
            nodeOwner.Dispose();
            throw;
        }
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
        ref JxlBitReader reader, ref JxlEntropyDecoder.Reader symbols, int context, scoped Span<uint> history, ref uint decodedCount)
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
    /// Stores one split or leaf without references so the allocator can own contiguous node storage.
    /// </summary>
    public readonly struct Node
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Node"/> struct from validated tree fields.
        /// </summary>
        /// <param name="property">The property index, or minus one for a leaf.</param>
        /// <param name="value">The split threshold or leaf predictor offset.</param>
        /// <param name="firstChild">The first adjacent child index, or the leaf's sample entropy context.</param>
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
        /// Gets the first adjacent child index or the leaf's sample entropy context.
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
