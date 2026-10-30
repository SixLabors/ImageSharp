// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Components.Entropy;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Modular;

/// <summary>
/// Reconstructs modular integer channels and owns their frame-local sample storage.
/// </summary>
/// <remarks>
/// Prediction and entropy decoding follow wire order on one thread. Independent inverse color arithmetic uses SIMD.
/// Channel buffers use the shared segmented allocator and row-span cache. No sample loop rents memory.
/// </remarks>
internal sealed class JxlModularDecoder : IDisposable
{
    private readonly Buffer2D<int>[] channels;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlModularDecoder"/> class and takes ownership of completed channel allocations.
    /// </summary>
    /// <param name="channels">The native channels, in current modular transform order.</param>
    private JxlModularDecoder(Buffer2D<int>[] channels) => this.channels = channels;

    /// <summary>
    /// Gets one reconstructed native channel. The owner must outlive every borrowed row.
    /// </summary>
    /// <param name="index">The decoded channel index.</param>
    /// <returns>The allocator-owned channel buffer.</returns>
    public Buffer2D<int> GetChannel(int index) => this.channels[index];

    /// <summary>
    /// Reads the modular DC-global data and reconstructs the channels held in its combined single-group section.
    /// </summary>
    /// <param name="reader">The reader bounded to the combined section.</param>
    /// <param name="image">The image-wide sample declarations.</param>
    /// <param name="frame">The retained frame header.</param>
    /// <param name="allocator">The allocator that owns all sample and temporary storage.</param>
    /// <param name="cancellationToken">The token checked between channels and rows.</param>
    /// <returns>The owner of the reconstructed native channels.</returns>
    public static JxlModularDecoder Read(
        ref JxlBitReader reader, JxlHeader image, JxlFrameHeader frame, MemoryAllocator allocator, CancellationToken cancellationToken)
    {
        // DC dequantization precedes modular data even though modular reconstruction does not use its factors.
        // The native DecodeDC syntax divides three optional F16 values by 128 and requires positive factors.
        if (!reader.ReadBoolean())
        {
            for (int i = 0; i < 3; i++)
            {
                if (reader.ReadHalf() / 128F < 1E-8F)
                {
                    throw new InvalidImageContentException("The JPEG XL DC dequantization factor is invalid.");
                }
            }
        }

        int colorCount = image.ProfileColorType == JxlColorType.Luminance ? 1 : 3;
        int channelCount = colorCount + image.Metadata.ExtraChannels.Count;
        long sampleCount = (long)frame.CodingSize.Width * frame.CodingSize.Height * channelCount;
        int globalLimit = (int)Math.Min(1 << 22, 1024 + (sampleCount / 16));
        using JxlModularTree? globalTree = reader.ReadBoolean() ? JxlModularTree.Read(ref reader, allocator, globalLimit) : null;

        bool useGlobalTree = reader.ReadBoolean();

        // The weighted header is present even when the selected tree does not use weighted prediction.
        JxlWeightedPredictor weightedPredictor = JxlWeightedPredictor.Read(ref reader);

        int transformCount = (int)reader.ReadU32((0, 0), (0, 1), (4, 2), (8, 18));
        using IMemoryOwner<ReversibleTransform>? transformOwner = transformCount == 0 ? null : allocator.Allocate<ReversibleTransform>(transformCount);
        Span<ReversibleTransform> transforms = transformOwner is null ? [] : transformOwner.Memory.Span;

        for (int i = 0; i < transforms.Length; i++)
        {
            uint type = reader.ReadBits(2);
            if (type == 3)
            {
                throw new InvalidImageContentException("The JPEG XL modular transform type is invalid.");
            }

            if (type != 0)
            {
                throw new NotSupportedException("JPEG XL palette and squeeze reconstruction is not yet supported.");
            }

            uint begin = reader.ReadU32((3, 0), (6, 8), (10, 72), (13, 1096));
            uint operation = reader.ReadU32((0, 6), (2, 0), (4, 2), (6, 10));
            if (begin + 2 >= channelCount || operation >= 42)
            {
                throw new InvalidImageContentException("The JPEG XL reversible color transform is invalid.");
            }

            transforms[i] = new ReversibleTransform((int)begin, (int)operation);
        }

        int localLimit = (int)Math.Min(1 << 20, 1024 + sampleCount);
        JxlModularTree tree = useGlobalTree
            ? globalTree ?? throw new InvalidImageContentException("The JPEG XL modular group requests a missing global tree.")
            : JxlModularTree.Read(ref reader, allocator, localLimit);

        using JxlModularTree? localTree = useGlobalTree ? null : tree;

        ReadOnlySpan<JxlModularTree.Node> nodes = tree.Nodes;
        foreach (ref readonly JxlModularTree.Node node in nodes)
        {
            if (node.Property >= 16)
            {
                throw new NotSupportedException("JPEG XL cross-channel prediction is not yet supported.");
            }
        }

        // Buffer descriptors are small managed metadata. Every sample allocation belongs to the shared allocator.
        // Prepare them before sample traversal; different channel dimensions remain independent buffer owners.
        Buffer2D<int>[] channels = new Buffer2D<int>[channelCount];
        int allocated = 0;
        try
        {
            for (; allocated < channels.Length; allocated++)
            {
                channels[allocated] = allocator.Allocate2D<int>(frame.CodingSize, AllocationOptions.Clean);
            }

            DecodeChannels(ref reader, tree, channels, allocator, sampleCount, weightedPredictor, cancellationToken);

            // RCT does not change channel dimensions. Undo its arithmetic first, then permute descriptors;
            // ownership moves without copying channel images or introducing another full-image buffer.
            for (int i = transforms.Length - 1; i >= 0; i--)
            {
                ReversibleTransform transform = transforms[i];
                Buffer2D<int> first = channels[transform.Begin];
                Buffer2D<int> second = channels[transform.Begin + 1];
                Buffer2D<int> third = channels[transform.Begin + 2];
                for (int y = 0; y < first.Height; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    JxlModularColorTransform.Apply(
                        transform.Type % 7, first.DangerousGetRowSpan(y), second.DangerousGetRowSpan(y), third.DangerousGetRowSpan(y));
                }

                int permutation = transform.Type / 7;
                channels[transform.Begin + (permutation % 3)] = first;
                channels[transform.Begin + ((permutation + 1 + (permutation / 3)) % 3)] = second;
                channels[transform.Begin + ((permutation + 2 - (permutation / 3)) % 3)] = third;
            }

            return new JxlModularDecoder(channels);
        }
        catch
        {
            for (int i = 0; i < allocated; i++)
            {
                channels[i].Dispose();
            }

            throw;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (Buffer2D<int> channel in this.channels)
        {
            channel.Dispose();
        }
    }

    /// <summary>
    /// Decodes residuals in channel order while retaining one shared entropy state and circular residual history.
    /// </summary>
    /// <param name="reader">The bounded modular reader.</param>
    /// <param name="tree">The selected prediction tree and sample entropy descriptions.</param>
    /// <param name="channels">The initialized native channel buffers.</param>
    /// <param name="allocator">The owner of the optional LZ77 history.</param>
    /// <param name="sampleCount">The complete number of residual values in this stream.</param>
    /// <param name="weightedPredictor">The group's retained weighted prediction coefficients.</param>
    /// <param name="cancellationToken">The token checked before each row.</param>
    private static void DecodeChannels(
        ref JxlBitReader reader,
        JxlModularTree tree,
        Buffer2D<int>[] channels,
        MemoryAllocator allocator,
        long sampleCount,
        JxlWeightedPredictor weightedPredictor,
        CancellationToken cancellationToken)
    {
        int historyLength = tree.Codes.UsesLz77
            ? (int)BitOperations.RoundUpToPowerOf2((uint)Math.Min(sampleCount, JxlEntropyDecoder.MaximumLz77Distance))
            : 0;

        using IMemoryOwner<uint>? historyOwner = historyLength == 0 ? null : allocator.Allocate<uint>(historyLength);
        Span<uint> history = historyOwner is null ? [] : historyOwner.Memory.Span;
        ReadOnlySpan<JxlModularTree.Node> nodes = tree.Nodes;
        bool usesWeighted = false;
        foreach (ref readonly JxlModularTree.Node node in nodes)
        {
            usesWeighted |= node.Predictor == 6 || node.Property == 15;
        }

        // One buffer holds both error rows for every weighted predictor. Reuse it between channels.
        // Ordinary trees rent no weighted state, and sample loops receive the cached span directly.
        using IMemoryOwner<int>? errorOwner = usesWeighted ? allocator.Allocate<int>(JxlWeightedPredictor.GetStorageLength(channels[0].Width)) : null;
        Span<int> errors = errorOwner is null ? [] : errorOwner.Memory.Span;
        JxlEntropyDecoder.Reader symbols = tree.Codes.CreateReader(ref reader);
        uint decoded = 0;
        InlineArray16<int> propertyStorage = default;
        Span<int> properties = propertyStorage;

        for (int channelIndex = 0; channelIndex < channels.Length; channelIndex++)
        {
            Buffer2D<int> channel = channels[channelIndex];
            if (usesWeighted)
            {
                weightedPredictor.Reset(errors, channel.Width);
            }

            properties.Clear();
            properties[0] = channelIndex;
            properties[1] = 0; // Global modular stream ID. Group streams have their own IDs.
            int root = 0;
            while (nodes[root].Property is 0 or 1)
            {
                ref readonly JxlModularTree.Node split = ref nodes[root];
                root = split.FirstChild + (properties[split.Property] > split.Value ? 0 : 1);
            }

            for (int y = 0; y < channel.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Borrow row spans once. Prediction reads only reconstructed neighbors; LZ77 instead copies
                // unsigned residuals from history, before prediction and the signed multiplier are applied.
                Span<int> row = channel.DangerousGetRowSpan(y);
                ReadOnlySpan<int> topRow = y == 0 ? row : channel.DangerousGetRowSpan(y - 1);
                ReadOnlySpan<int> aboveRow = y < 2 ? topRow : channel.DangerousGetRowSpan(y - 2);
                properties[9] = 0;
                for (int x = 0; x < row.Length; x++)
                {
                    int left = x != 0 ? row[x - 1] : y != 0 ? topRow[0] : 0;
                    int top = y != 0 ? topRow[x] : left;
                    int topLeft = x != 0 && y != 0 ? topRow[x - 1] : left;
                    int topRight = y != 0 && x + 1 < row.Length ? topRow[x + 1] : top;
                    int leftLeft = x > 1 ? row[x - 2] : left;
                    int topTop = y > 1 ? aboveRow[x] : top;
                    int topRightRight = y != 0 && x + 2 < row.Length ? topRow[x + 2] : topRight;
                    long weighted = 0;
                    if (usesWeighted)
                    {
                        // Property 15 and predictor 6 share this calculation. Update errors for every
                        // reconstructed sample, including leaves that choose an unweighted predictor.
                        weighted = weightedPredictor.Predict(errors, x, y, left, top, topLeft, topRight, topTop, out int errorProperty);
                        properties[15] = errorProperty;
                    }

                    int leaf = root;
                    if (nodes[leaf].Property >= 0)
                    {
                        // Properties use native signed 32-bit wrap. Predictor arithmetic below widens sums
                        // so averaging divides signed values toward zero, as required by the modular syntax.
                        properties[2] = y;
                        properties[3] = x;
                        properties[4] = top == int.MinValue ? int.MinValue : Math.Abs(top);
                        properties[5] = left == int.MinValue ? int.MinValue : Math.Abs(left);
                        properties[6] = top;
                        properties[7] = left;
                        properties[8] = unchecked(left - properties[9]);
                        properties[9] = unchecked(left + top - topLeft);
                        properties[10] = unchecked(left - topLeft);
                        properties[11] = unchecked(topLeft - top);
                        properties[12] = unchecked(top - topRight);
                        properties[13] = unchecked(top - topTop);
                        properties[14] = unchecked(left - leftLeft);
                        while (nodes[leaf].Property >= 0)
                        {
                            ref readonly JxlModularTree.Node split = ref nodes[leaf];
                            leaf = split.FirstChild + (properties[split.Property] > split.Value ? 0 : 1);
                        }
                    }

                    ref readonly JxlModularTree.Node node = ref nodes[leaf];
                    uint residual = symbols.ReadUInt32(ref reader, node.FirstChild, history, decoded++, channel.Width);
                    int signed = (int)(residual >> 1) ^ -(int)(residual & 1);
                    long prediction = node.Predictor == 6 ? weighted : Predict(node.Predictor, left, top, topLeft, topRight, leftLeft, topTop, topRightRight);
                    row[x] = unchecked((int)(prediction + node.Value + ((long)signed * node.Multiplier)));
                    if (usesWeighted)
                    {
                        weightedPredictor.Update(errors, x, y, row[x]);
                    }
                }
            }
        }

        symbols.CheckFinalState();
    }

    /// <summary>
    /// Calculates one nonweighted modular predictor from already reconstructed signed neighbors.
    /// </summary>
    /// <param name="predictor">The validated predictor code.</param>
    /// <param name="left">The preceding column, with the native edge fallback.</param>
    /// <param name="top">The preceding row at this column.</param>
    /// <param name="topLeft">The preceding row and column.</param>
    /// <param name="topRight">The preceding row and following column.</param>
    /// <param name="leftLeft">Two columns before the current sample.</param>
    /// <param name="topTop">Two rows before the current sample.</param>
    /// <param name="topRightRight">The preceding row and second following column.</param>
    /// <returns>The signed prediction before offset and residual reconstruction.</returns>
    private static long Predict(byte predictor, long left, long top, long topLeft, long topRight, long leftLeft, long topTop, long topRightRight)
        => predictor switch
        {
            0 => 0,
            1 => left,
            2 => top,
            3 => (left + top) / 2,
            4 => Math.Abs(top - topLeft) < Math.Abs(left - topLeft) ? left : top,
            5 => Math.Clamp(left + top - topLeft, Math.Min(left, top), Math.Max(left, top)),
            7 => topRight,
            8 => topLeft,
            9 => leftLeft,
            10 => (left + topLeft) / 2,
            11 => (top + topLeft) / 2,
            12 => (top + topRight) / 2,
            _ => ((6 * top) - (2 * topTop) + (7 * left) + leftLeft + topRightRight + (3 * topRight) + 8) / 16
        };

    /// <summary>
    /// Retains one dimension-preserving reversible color transform until sample reconstruction is complete.
    /// </summary>
    private readonly struct ReversibleTransform
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReversibleTransform"/> struct.
        /// </summary>
        /// <param name="begin">The first of three consecutive channels.</param>
        /// <param name="type">The permutation and arithmetic code, from zero through 41.</param>
        public ReversibleTransform(int begin, int type)
        {
            this.Begin = begin;
            this.Type = type;
        }

        /// <summary>
        /// Gets the first channel index.
        /// </summary>
        public int Begin { get; }

        /// <summary>
        /// Gets the permutation and arithmetic code.
        /// </summary>
        public int Type { get; }
    }
}
