// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Components.Entropy;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Modular;

/// <summary>
/// Owns modular channel shapes, frame samples and reusable entropy workspace across global and group streams.
/// </summary>
/// <remarks>
/// Each stream uses the same transform parser and prediction traversal on one thread.
/// Global transforms remain pending until every group has populated its encoded frame regions.
/// </remarks>
internal sealed class JxlModularDecoder : IDisposable
{
    // Transform tags are serialized in two bits. Tag three has no defined transform.
    private const byte ColorTransform = 0;
    private const byte PaletteTransform = 1;
    private const byte SqueezeTransform = 2;

    // The modular channel limit includes metadata and residual channels introduced by transforms.
    private const int MaximumChannels = 1 << 16;

    // Properties 0..15 describe this channel. Each earlier matching channel supplies four further properties.
    // The tree's eight-bit property range therefore requires at most 240 planes of 64 integer samples.
    private const int ReferenceBlockLength = 64;
    private const int PropertiesPerReference = 4;
    private const int FirstReferenceProperty = 16;
    private const int MaximumFirstPreviewSize = 8;

    private readonly MemoryAllocator allocator;
    private readonly JxlModularWorkspace workspace;
    private readonly List<JxlModularChannel> channels;
    private readonly JxlModularChannelLayout[] originalLayouts;
    private readonly List<JxlModularChannelLayout> layouts = [];
    private readonly int groupSize;
    private readonly int bitDepth;
    private readonly int originalChannelCount;
    private readonly List<JxlModularChannel> groupChannels = [];
    private readonly List<JxlModularChannel> groupTargets = [];
    private readonly List<Transform> groupTransforms = [];
    private readonly List<Transform> globalTransforms = [];
    private JxlModularTree? globalTree;
    private JxlWeightedPredictor globalPredictor;
    private int globalPrefixCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlModularDecoder"/> class with original channel layouts and no sample allocations.
    /// </summary>
    /// <param name="image">The image-wide sample and extra-channel declarations.</param>
    /// <param name="frame">The frame geometry and sampling layout.</param>
    /// <param name="allocator">The tracked allocator for samples and scratch.</param>
    private JxlModularDecoder(JxlHeader image, JxlFrameHeader frame, MemoryAllocator allocator)
    {
        this.allocator = allocator;
        this.workspace = new JxlModularWorkspace(allocator);
        this.groupSize = frame.GroupSize;
        this.bitDepth = image.Metadata.BitDepth.BitsPerSample;
        int colorCount = image.ProfileColorType == JxlColorType.Luminance && !image.UsesXybTransform && !frame.YCbCr ? 1 : 3;
        this.originalChannelCount = colorCount + image.Metadata.ExtraChannels.Count;

        // VarDCT reconstructs its color coefficients in separate modular streams. The frame-wide modular image
        // contains only extra channels. Its global tree limit still counts the original color channels.
        if (!frame.Modular)
        {
            colorCount = 0;
        }

        this.channels = new List<JxlModularChannel>(colorCount + image.Metadata.ExtraChannels.Count);
        Size size = frame.CodingSize;
        ReadOnlySpan<byte> horizontalShifts = frame.HorizontalSamplingShifts;
        ReadOnlySpan<byte> verticalShifts = frame.VerticalSamplingShifts;

        for (int c = 0; c < colorCount; c++)
        {
            int horizontal = horizontalShifts[c];
            int vertical = verticalShifts[c];
            Size channelSize = new(((size.Width - 1) >> horizontal) + 1, ((size.Height - 1) >> vertical) + 1);
            this.layouts.Add(new JxlModularChannelLayout(channelSize, horizontal, vertical));
        }

        for (int c = 0; c < image.Metadata.ExtraChannels.Count; c++)
        {
            // Effective extra-channel upsampling already includes its image-wide dimension shift.
            // Express its reduction relative to the color coding size; successive ceilings equal one combined ceiling.
            int shift = BitOperations.Log2(frame.ExtraChannelUpsampling[c]) - BitOperations.Log2(frame.Upsampling);
            Size channelSize = new(((size.Width - 1) >> shift) + 1, ((size.Height - 1) >> shift) + 1);
            this.layouts.Add(new JxlModularChannelLayout(channelSize, shift, shift));
        }

        // Retain the original layouts independently of samples. Reuse the layout list for each stream's complete
        // transform parse; no owned or borrowed sample descriptor needs an unallocated state.
        this.originalLayouts = this.layouts.ToArray();
        this.layouts.Clear();
    }

    /// <summary>
    /// Gets one completed native output channel. Complete must run before the caller borrows these samples.
    /// </summary>
    /// <param name="index">The original output channel index.</param>
    /// <returns>The region owned by this modular frame.</returns>
    public Buffer2DRegion<int> GetChannel(int index) => this.channels[index].Region;

    /// <summary>
    /// Reads global modular metadata and its eligible channel prefix, retaining larger channels for later group streams.
    /// </summary>
    /// <param name="reader">The reader at the global-tree flag, after the frame's DC factors and any VarDCT global fields.</param>
    /// <param name="image">The image-wide sample declarations.</param>
    /// <param name="frame">The retained frame header.</param>
    /// <param name="allocator">The allocator for all samples and reusable scratch.</param>
    /// <param name="cancellationToken">The token checked at metadata and row boundaries.</param>
    /// <returns>The frame owner with pending global transforms.</returns>
    public static JxlModularDecoder Read(
        ref JxlBitReader reader, JxlHeader image, JxlFrameHeader frame, MemoryAllocator allocator, CancellationToken cancellationToken)
    {
        JxlModularDecoder result = new(image, frame, allocator);
        try
        {
            if (reader.ReadBoolean())
            {
                int limit = GetGlobalTreeLimit(frame.CodingSize, result.originalChannelCount);
                result.globalTree = JxlModularTree.Read(ref reader, allocator, limit);
            }

            // A VarDCT frame without extra channels can still define the shared tree above. It has no global
            // modular channels, so no modular header or sample stream follows that tree.
            if (result.originalLayouts.Length != 0)
            {
                result.ReadStream(
                    ref reader, result.channels, result.globalTransforms, result.groupSize, 0, true, out result.globalPredictor, cancellationToken);
            }

            result.globalPrefixCount = GetEligiblePrefix(result.layouts, result.groupSize);
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Decodes one group into selected frame regions and reverses any local transforms before returning.
    /// </summary>
    /// <param name="reader">The bounded group-section reader.</param>
    /// <param name="rectangle">The full group grid rectangle in coding-pixel coordinates, before cropping at image edges.</param>
    /// <param name="minimumShift">The inclusive minimum horizontal/vertical resolution reduction.</param>
    /// <param name="maximumShift">The inclusive maximum horizontal/vertical resolution reduction.</param>
    /// <param name="streamId">The format-defined modular stream identifier used by tree property one.</param>
    /// <param name="cancellationToken">The token checked between channels, transforms and rows.</param>
    public void ReadGroup(
        ref JxlBitReader reader, Rectangle rectangle, int minimumShift, int maximumShift, int streamId, CancellationToken cancellationToken)
    {
        // One decoder processes groups sequentially. Reuse descriptor capacity without sharing ownership of frame samples.
        List<JxlModularChannel> local = this.groupChannels;
        List<JxlModularChannel> targets = this.groupTargets;

        local.Clear();
        targets.Clear();
        for (int c = this.globalPrefixCount; c < this.channels.Count; c++)
        {
            JxlModularChannel channel = this.channels[c];
            int shift = Math.Min(channel.HorizontalShift, channel.VerticalShift);
            if (shift < minimumShift || shift > maximumShift)
            {
                continue;
            }

            // Shift the full grid extent before cropping. Cropping an odd image edge first would lose its last reduced sample.
            int x = rectangle.X >> channel.HorizontalShift;
            int y = rectangle.Y >> channel.VerticalShift;
            int width = Math.Min(rectangle.Width >> channel.HorizontalShift, Math.Max(channel.Width - x, 0));
            int height = Math.Min(rectangle.Height >> channel.VerticalShift, Math.Max(channel.Height - y, 0));
            if (width != 0 && height != 0)
            {
                JxlModularChannel borrowed = channel.Borrow(new Rectangle(x, y, width, height));
                local.Add(borrowed);
                targets.Add(borrowed);
            }
        }

        this.ReadSelectedChannels(ref reader, streamId, cancellationToken);
    }

    /// <summary>
    /// Decodes a modular stream into caller-owned channel regions and reverses its local transforms.
    /// </summary>
    /// <param name="reader">The reader bounded to the containing frame section.</param>
    /// <param name="targets">The allocated output layouts in encoded channel order. The caller retains ownership of every region.</param>
    /// <param name="streamId">The format-defined identifier used by tree property one.</param>
    /// <param name="cancellationToken">The token checked between channels, transforms and rows.</param>
    /// <remarks>
    /// VarDCT DC samples and AC metadata use the same modular syntax as image groups, with explicitly declared channel layouts.
    /// Completed samples remain in the supplied regions. Temporary transformed samples are released before this method returns.
    /// </remarks>
    public void ReadChannels(ref JxlBitReader reader, ReadOnlySpan<JxlModularChannel> targets, int streamId, CancellationToken cancellationToken)
    {
        this.groupChannels.Clear();
        this.groupTargets.Clear();
        foreach (JxlModularChannel target in targets)
        {
            // Borrow the complete requested region without copying its owner. Empty layouts retain their channel
            // positions because tree property zero and transform ranges refer to the full encoded channel sequence.
            JxlModularChannel borrowed = target.Borrow(new Rectangle(Point.Empty, target.Size));
            this.groupChannels.Add(borrowed);
            this.groupTargets.Add(borrowed);
        }

        this.ReadSelectedChannels(ref reader, streamId, cancellationToken);
    }

    /// <summary>
    /// Reconstructs the prepared borrowed channels through one shared stream, transform and export path.
    /// </summary>
    /// <param name="reader">The reader bounded to the containing section.</param>
    /// <param name="streamId">The selected modular stream identifier.</param>
    /// <param name="cancellationToken">The token checked during reconstruction and export.</param>
    private void ReadSelectedChannels(ref JxlBitReader reader, int streamId, CancellationToken cancellationToken)
    {
        List<JxlModularChannel> local = this.groupChannels;
        List<JxlModularChannel> targets = this.groupTargets;

        // Empty group selections have no modular header or entropy stream.
        if (local.Count == 0)
        {
            return;
        }

        try
        {
            List<Transform> transforms = this.groupTransforms;
            this.ReadStream(ref reader, local, transforms, int.MaxValue, streamId, false, out JxlWeightedPredictor predictor, cancellationToken);

            this.UndoTransforms(local, transforms, predictor, cancellationToken);

            // Parsing no longer needs the encoded layouts. Reuse that list to validate the restored channels
            // against their retained borrowed targets without creating a per-group layout array.
            this.layouts.Clear();
            foreach (JxlModularChannel target in targets)
            {
                this.layouts.Add(target.Layout);
            }

            ValidateShapes(local, CollectionsMarshal.AsSpan(this.layouts));
            if (transforms.Count != 0)
            {
                // Only transformed groups need separate encoded storage. Ordinary groups already decoded directly into borrowed frame regions.
                for (int c = 0; c < local.Count; c++)
                {
                    for (int y = 0; y < local[c].Height; y++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        local[c].Region.DangerousGetRowSpan(y).CopyTo(targets[c].Region.DangerousGetRowSpan(y));
                    }
                }
            }
        }
        finally
        {
            DisposeChannels(local);
            local.Clear();
            targets.Clear();
        }
    }

    /// <summary>
    /// Reverses global transforms after all selected group streams have populated their frame regions.
    /// </summary>
    /// <param name="cancellationToken">The token checked between transforms and rows.</param>
    public void Complete(CancellationToken cancellationToken)
    {
        this.UndoTransforms(this.channels, this.globalTransforms, this.globalPredictor, cancellationToken);
        ValidateShapes(this.channels, this.originalLayouts);
        this.globalTransforms.Clear();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        DisposeChannels(this.groupChannels);
        DisposeChannels(this.channels);
        this.globalTree?.Dispose();
        this.workspace.Dispose();
    }

    /// <summary>
    /// Parses one modular header, applies its shape transforms and decodes its eligible prefix through the shared sample path.
    /// </summary>
    /// <param name="reader">The bounded stream reader.</param>
    /// <param name="channels">The ready borrowed targets, or the initially empty global sample list.</param>
    /// <param name="transforms">The retained transform list reused for this stream.</param>
    /// <param name="maximumSize">The largest normal channel dimension decoded here; metadata always belongs to the prefix.</param>
    /// <param name="streamId">The property-one modular stream identifier.</param>
    /// <param name="allocateAll">Whether original frame layouts supply this initially empty global sample list.</param>
    /// <param name="weightedPredictor">The header's predictor coefficients retained for inverse palette operations.</param>
    /// <param name="cancellationToken">The operation's cancellation token.</param>
    private void ReadStream(
        ref JxlBitReader reader,
        List<JxlModularChannel> channels,
        List<Transform> transforms,
        int maximumSize,
        int streamId,
        bool allocateAll,
        out JxlWeightedPredictor weightedPredictor,
        CancellationToken cancellationToken)
    {
        List<JxlModularChannelLayout> layouts = this.layouts;
        layouts.Clear();
        if (allocateAll)
        {
            layouts.AddRange(this.originalLayouts);
        }
        else
        {
            foreach (JxlModularChannel channel in channels)
            {
                layouts.Add(channel.Layout);
            }
        }

        bool useGlobalTree = reader.ReadBoolean();
        weightedPredictor = JxlWeightedPredictor.Read(ref reader);
        ReadTransforms(ref reader, layouts, transforms, cancellationToken);

        if (allocateAll)
        {
            ValidateGroupDimensions(layouts, this.groupSize);
        }

        if (allocateAll || transforms.Count != 0)
        {
            // Parsing changes layouts only. Borrowed targets retain their ready regions until every encoded shape
            // has passed validation. Reserve descriptor capacity before renting samples so Add cannot strand an owner.
            channels.EnsureCapacity(layouts.Count);
            channels.Clear();
            foreach (JxlModularChannelLayout layout in layouts)
            {
                channels.Add(JxlModularChannel.Allocate(this.allocator, layout));
            }
        }

        int count = GetEligiblePrefix(layouts, maximumSize);
        long samples = GetSampleCount(channels, count, out _);
        if (samples == 0)
        {
            // A header can leave every normal channel for later groups. No local tree or sample entropy follows in that case.
            return;
        }

        JxlModularTree tree = useGlobalTree
            ? this.globalTree ?? throw new InvalidImageContentException("The JPEG XL modular stream requests a missing global tree.")
            : JxlModularTree.Read(ref reader, this.allocator, GetLocalTreeLimit(channels, count));

        using JxlModularTree? localTree = useGlobalTree ? null : tree;
        this.DecodeSamples(ref reader, channels, count, samples, streamId, tree, weightedPredictor, cancellationToken);
    }

    /// <summary>
    /// Parses transforms and updates logical shapes without allocating or copying sample planes.
    /// </summary>
    /// <param name="reader">The modular header reader.</param>
    /// <param name="channels">The shapes changed in wire order.</param>
    /// <param name="transforms">The reusable list receiving RCT, palette and flattened squeeze records in wire order.</param>
    /// <param name="cancellationToken">The token checked between transform records.</param>
    private static void ReadTransforms(
        ref JxlBitReader reader, List<JxlModularChannelLayout> channels, List<Transform> transforms, CancellationToken cancellationToken)
    {
        int count = (int)reader.ReadU32((0, 0), (0, 1), (4, 2), (8, 18));
        transforms.Clear();
        transforms.EnsureCapacity(count);
        int metadataCount = GetMetadataCount(channels);
        for (int i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte type = (byte)reader.ReadBits(2);
            if (type == ColorTransform)
            {
                int begin = (int)reader.ReadU32((3, 0), (6, 8), (10, 72), (13, 1096));
                int operation = (int)reader.ReadU32((0, 6), (2, 0), (4, 2), (6, 10));
                if (operation >= 42)
                {
                    throw new InvalidImageContentException("The JPEG XL reversible color transform is invalid.");
                }

                CheckChannelRange(channels, begin, 3, metadataCount, true);
                transforms.Add(new Transform(type, begin, 3, operation, 0, 0, false, false));
            }
            else if (type == PaletteTransform)
            {
                int begin = (int)reader.ReadU32((3, 0), (6, 8), (10, 72), (13, 1096));
                int number = (int)reader.ReadU32((0, 1), (0, 3), (0, 4), (13, 1));
                int colors = (int)reader.ReadU32((8, 0), (10, 256), (12, 1280), (16, 5376));
                int deltas = (int)reader.ReadU32((0, 0), (8, 1), (10, 257), (16, 1281));
                byte predictor = (byte)reader.ReadBits(4);
                if (predictor > 13)
                {
                    throw new InvalidImageContentException("The JPEG XL palette predictor is invalid.");
                }

                CheckChannelRange(channels, begin, number, metadataCount, true);
                CheckChannelCount(channels.Count - number + 2);
                if (begin < metadataCount)
                {
                    metadataCount += 2 - number;
                }
                else
                {
                    metadataCount++;
                }

                // One index channel replaces the selected channels. Palette values form a new leading metadata plane.
                // A zero width is legal and must survive later shape validation even though no sample buffer exists.
                channels.RemoveRange(begin + 1, number - 1);
                channels.Insert(0, new JxlModularChannelLayout(new Size(colors + deltas, number), -1, -1));
                transforms.Add(new Transform(type, begin, number, 0, deltas, predictor, false, false));
            }
            else if (type == SqueezeTransform)
            {
                int squeezes = (int)reader.ReadU32((0, 0), (4, 1), (6, 9), (8, 41));
                if (squeezes == 0)
                {
                    AddDefaultSqueezes(channels, transforms, ref metadataCount);
                }
                else
                {
                    for (int s = 0; s < squeezes; s++)
                    {
                        bool horizontal = reader.ReadBoolean();
                        bool inPlace = reader.ReadBoolean();
                        int begin = (int)reader.ReadU32((3, 0), (6, 8), (10, 72), (13, 1096));
                        int number = (int)reader.ReadU32((0, 1), (0, 2), (0, 3), (4, 4));
                        AddSqueeze(channels, transforms, begin, number, horizontal, inPlace, ref metadataCount);
                    }
                }
            }
            else
            {
                throw new InvalidImageContentException("The JPEG XL modular transform type is invalid.");
            }
        }
    }

    /// <summary>
    /// Adds the format's implicit squeeze schedule, including the optional chroma preview reduction.
    /// </summary>
    /// <param name="channels">The current logical channel list.</param>
    /// <param name="transforms">The flattened reverse-transform records.</param>
    /// <param name="metadataCount">The current leading metadata channel count.</param>
    private static void AddDefaultSqueezes(List<JxlModularChannelLayout> channels, List<Transform> transforms, ref int metadataCount)
    {
        int normalCount = channels.Count - metadataCount;
        int width = channels[metadataCount].Width;
        int height = channels[metadataCount].Height;
        if (normalCount > 2 && channels[metadataCount + 1].Size == new Size(width, height))
        {
            AddSqueeze(channels, transforms, metadataCount + 1, 2, true, false, ref metadataCount);
            AddSqueeze(channels, transforms, metadataCount + 1, 2, false, false, ref metadataCount);
        }

        // The first normal channel determines the preview schedule. Only its original normal-channel prefix
        // is squeezed again; appended residuals remain available for later resolution groups.
        if (width <= height && height > MaximumFirstPreviewSize)
        {
            AddSqueeze(channels, transforms, metadataCount, normalCount, false, true, ref metadataCount);
            height = (height / 2) + (height & 1);
        }

        while (width > MaximumFirstPreviewSize || height > MaximumFirstPreviewSize)
        {
            if (width > MaximumFirstPreviewSize)
            {
                AddSqueeze(channels, transforms, metadataCount, normalCount, true, true, ref metadataCount);
                width = (width / 2) + (width & 1);
            }

            if (height > MaximumFirstPreviewSize)
            {
                AddSqueeze(channels, transforms, metadataCount, normalCount, false, true, ref metadataCount);
                height = (height / 2) + (height & 1);
            }
        }
    }

    /// <summary>
    /// Applies one squeeze to logical shapes and appends its inverse record without allocating sample storage.
    /// </summary>
    /// <param name="channels">The current shape list.</param>
    /// <param name="transforms">The flattened inverse records.</param>
    /// <param name="begin">The first selected channel.</param>
    /// <param name="number">The number of selected channels.</param>
    /// <param name="horizontal">Whether the reduction divides columns rather than rows.</param>
    /// <param name="inPlace">Whether residuals follow the selected channels instead of the complete channel list.</param>
    /// <param name="metadataCount">The leading metadata count updated by an in-place metadata squeeze.</param>
    private static void AddSqueeze(
        List<JxlModularChannelLayout> channels,
        List<Transform> transforms,
        int begin,
        int number,
        bool horizontal,
        bool inPlace,
        ref int metadataCount)
    {
        CheckChannelRange(channels, begin, number, metadataCount, false);
        CheckChannelCount(channels.Count + number);
        if (begin < metadataCount)
        {
            if (!inPlace)
            {
                throw new InvalidImageContentException("The JPEG XL metadata squeeze requires adjacent residuals.");
            }

            metadataCount += number;
        }

        int residualStart = inPlace ? begin + number : channels.Count;
        for (int c = 0; c < number; c++)
        {
            JxlModularChannelLayout channel = channels[begin + c];
            if (channel.IsEmpty || channel.HorizontalShift > 30 || channel.VerticalShift > 30)
            {
                throw new InvalidImageContentException("The JPEG XL squeeze channel is empty or its resolution shift is too large.");
            }

            int horizontalShift = channel.HorizontalShift;
            int verticalShift = channel.VerticalShift;
            if (horizontal && horizontalShift >= 0)
            {
                horizontalShift++;
            }
            else if (!horizontal && verticalShift >= 0)
            {
                verticalShift++;
            }

            int halfWidth = horizontal ? (channel.Width / 2) + (channel.Width & 1) : channel.Width;
            int halfHeight = horizontal ? channel.Height : (channel.Height / 2) + (channel.Height & 1);
            Size averages = new(halfWidth, halfHeight);
            Size residuals = horizontal ? new Size(channel.Width - halfWidth, channel.Height) : new Size(channel.Width, channel.Height - halfHeight);
            channels[begin + c] = new JxlModularChannelLayout(averages, horizontalShift, verticalShift);
            channels.Insert(residualStart + c, new JxlModularChannelLayout(residuals, horizontalShift, verticalShift));
        }

        transforms.Add(new Transform(SqueezeTransform, begin, number, 0, 0, 0, horizontal, inPlace));
    }

    /// <summary>
    /// Decodes a stream's eligible channels with one entropy state, residual history and reusable prediction scratch.
    /// </summary>
    /// <param name="reader">The bounded sample reader.</param>
    /// <param name="channels">The allocated encoded channel layouts.</param>
    /// <param name="count">The eligible prefix length, including empty channels for property numbering.</param>
    /// <param name="samples">The number of nonempty samples in this stream.</param>
    /// <param name="streamId">The static stream property.</param>
    /// <param name="tree">The selected tree and sample entropy tables.</param>
    /// <param name="weightedPredictor">The current group's weighted coefficients.</param>
    /// <param name="cancellationToken">The token checked before each reconstructed row.</param>
    private void DecodeSamples(
        ref JxlBitReader reader,
        List<JxlModularChannel> channels,
        int count,
        long samples,
        int streamId,
        JxlModularTree tree,
        JxlWeightedPredictor weightedPredictor,
        CancellationToken cancellationToken)
    {
        ReadOnlySpan<JxlModularChannel> descriptors = CollectionsMarshal.AsSpan(channels);
        ReadOnlySpan<JxlModularTree.Node> nodes = tree.Nodes;
        int propertyCount = FirstReferenceProperty;
        bool usesWeighted = false;
        foreach (ref readonly JxlModularTree.Node node in nodes)
        {
            propertyCount = Math.Max(propertyCount, node.Property + 1);
            usesWeighted |= node.Predictor == 6 || node.Property == 15;
        }

        GetSampleCount(channels, count, out int maximumWidth);
        int historyLength = tree.Codes.UsesLz77
            ? (int)BitOperations.RoundUpToPowerOf2((uint)Math.Min(samples, JxlEntropyDecoder.MaximumLz77Distance))
            : 0;

        // Width belongs to the complete entropy stream, not the current channel. Palette metadata can be its widest channel.
        Span<uint> history = this.workspace.PrepareHistory(historyLength);
        Span<int> errors = usesWeighted ? this.workspace.PrepareErrors(maximumWidth) : [];
        int referenceProperties = ((propertyCount - FirstReferenceProperty + 3) / PropertiesPerReference) * PropertiesPerReference;
        Span<int> referenceValues = this.workspace.PrepareReferences(referenceProperties * ReferenceBlockLength);
        InlineArray256<int> propertyStorage = default;
        InlineArray64<int> referenceStorage = default;
        Span<int> properties = propertyStorage;
        Span<int> referenceIndices = referenceStorage;
        JxlEntropyDecoder.Reader symbols = tree.Codes.CreateReader(ref reader);
        uint decoded = 0;

        for (int channelIndex = 0; channelIndex < count; channelIndex++)
        {
            JxlModularChannel channel = descriptors[channelIndex];
            if (channel.IsEmpty)
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            properties.Clear();
            properties[0] = channelIndex;
            properties[1] = streamId;
            if (usesWeighted)
            {
                weightedPredictor.Reset(errors, channel.Width);
            }

            // Earlier channels are considered newest first. Only equal dimensions and resolution shifts can supply reference properties.
            // Missing references retain their initialized zero properties; no padded reference image is allocated.
            int references = 0;
            for (int previous = channelIndex - 1; previous >= 0 && references * PropertiesPerReference < referenceProperties; previous--)
            {
                if (channel.HasSameShape(descriptors[previous]))
                {
                    referenceIndices[references++] = previous;
                }
            }

            int root = 0;
            while (nodes[root].Property is 0 or 1)
            {
                ref readonly JxlModularTree.Node split = ref nodes[root];
                root = split.FirstChild + (properties[split.Property] > split.Value ? 0 : 1);
            }

            for (int y = 0; y < channel.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Span<int> row = channel.Region.DangerousGetRowSpan(y);
                ReadOnlySpan<int> topRow = y == 0 ? row : channel.Region.DangerousGetRowSpan(y - 1);
                ReadOnlySpan<int> aboveRow = y < 2 ? topRow : channel.Region.DangerousGetRowSpan(y - 2);
                properties[9] = 0;

                // Bound reference scratch to 64 columns. Borrow each matching source row before its SIMD preparation,
                // then pass only spans through the pixel loop. Every property occupies one contiguous block plane.
                for (int start = 0; start < row.Length; start += ReferenceBlockLength)
                {
                    int length = Math.Min(ReferenceBlockLength, row.Length - start);
                    for (int r = 0; r < references; r++)
                    {
                        JxlModularChannel reference = descriptors[referenceIndices[r]];
                        ReadOnlySpan<int> source = reference.Region.DangerousGetRowSpan(y);
                        ReadOnlySpan<int> top = y == 0 ? source : reference.Region.DangerousGetRowSpan(y - 1);
                        int offset = r * PropertiesPerReference * ReferenceBlockLength;
                        JxlModularPredictor.PrepareReferences(
                            source,
                            top,
                            start,
                            y != 0,
                            referenceValues.Slice(offset, length),
                            referenceValues.Slice(offset + ReferenceBlockLength, length),
                            referenceValues.Slice(offset + (2 * ReferenceBlockLength), length),
                            referenceValues.Slice(offset + (3 * ReferenceBlockLength), length));
                    }

                    for (int column = 0; column < length; column++)
                    {
                        int x = start + column;
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
                            weighted = weightedPredictor.Predict(errors, x, y, left, top, topLeft, topRight, topTop, out int errorProperty);
                            properties[15] = errorProperty;
                        }

                        int leaf = root;
                        if (nodes[leaf].Property >= 0)
                        {
                            // Tree properties wrap in signed 32-bit units. The predictor itself uses the existing wide integer equations.
                            properties[2] = y;
                            properties[3] = x;
                            properties[4] = top < 0 ? unchecked(-top) : top;
                            properties[5] = left < 0 ? unchecked(-left) : left;
                            properties[6] = top;
                            properties[7] = left;
                            properties[8] = unchecked(left - properties[9]);
                            properties[9] = unchecked(left + top - topLeft);
                            properties[10] = unchecked(left - topLeft);
                            properties[11] = unchecked(topLeft - top);
                            properties[12] = unchecked(top - topRight);
                            properties[13] = unchecked(top - topTop);
                            properties[14] = unchecked(left - leftLeft);
                            for (int p = 0; p < references * PropertiesPerReference; p++)
                            {
                                properties[FirstReferenceProperty + p] = referenceValues[(p * ReferenceBlockLength) + column];
                            }

                            while (nodes[leaf].Property >= 0)
                            {
                                ref readonly JxlModularTree.Node split = ref nodes[leaf];
                                leaf = split.FirstChild + (properties[split.Property] > split.Value ? 0 : 1);
                            }
                        }

                        ref readonly JxlModularTree.Node node = ref nodes[leaf];
                        uint residual = symbols.ReadUInt32(ref reader, node.FirstChild, history, decoded++, maximumWidth);
                        int signed = (int)(residual >> 1) ^ -(int)(residual & 1);
                        long prediction = node.Predictor == 6
                            ? weighted
                            : JxlModularPredictor.Predict(node.Predictor, left, top, topLeft, topRight, leftLeft, topTop, topRightRight);

                        row[x] = unchecked((int)(prediction + node.Value + ((long)signed * node.Multiplier)));
                        if (usesWeighted)
                        {
                            // Weighted errors advance even when this tree leaf selected another predictor.
                            weightedPredictor.Update(errors, x, y, row[x]);
                        }
                    }
                }
            }
        }

        symbols.CheckFinalState();
    }

    /// <summary>
    /// Reverses shape transforms in reverse order, releasing consumed owned planes at their transform boundary.
    /// </summary>
    /// <param name="channels">The decoded stream channels, updated to their original layouts.</param>
    /// <param name="transforms">The flattened records in forward wire order.</param>
    /// <param name="weightedPredictor">The group coefficients used by delta palettes.</param>
    /// <param name="cancellationToken">The token checked between transforms and rows.</param>
    private void UndoTransforms(
        List<JxlModularChannel> channels, List<Transform> transforms, JxlWeightedPredictor weightedPredictor, CancellationToken cancellationToken)
    {
        for (int i = transforms.Count - 1; i >= 0; i--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Transform transform = transforms[i];
            if (transform.Kind == ColorTransform)
            {
                JxlModularChannel first = channels[transform.Begin];
                JxlModularChannel second = channels[transform.Begin + 1];
                JxlModularChannel third = channels[transform.Begin + 2];
                if (!first.IsEmpty)
                {
                    for (int y = 0; y < first.Height; y++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        JxlModularColorTransform.Apply(
                            transform.ColorOperation % 7,
                            first.Region.DangerousGetRowSpan(y),
                            second.Region.DangerousGetRowSpan(y),
                            third.Region.DangerousGetRowSpan(y));
                    }
                }

                // Move descriptors, including ownership, instead of copying permuted channel images.
                int permutation = transform.ColorOperation / 7;
                channels[transform.Begin + (permutation % 3)] = first;
                channels[transform.Begin + ((permutation + 1 + (permutation / 3)) % 3)] = second;
                channels[transform.Begin + ((permutation + 2 - (permutation / 3)) % 3)] = third;
            }
            else if (transform.Kind == PaletteTransform)
            {
                int begin = transform.Begin + 1;
                JxlModularChannel index = channels[begin];
                for (int c = 1; c < transform.ChannelCount; c++)
                {
                    JxlModularChannel output = JxlModularChannel.Allocate(this.allocator, index.Layout);
                    try
                    {
                        channels.Insert(begin + c, output);
                    }
                    catch
                    {
                        output.Dispose();
                        throw;
                    }
                }

                // Region descriptors contain references, so they cannot use the unmanaged allocator. Common RGB/RGBA palettes
                // fit the existing inline array; only larger component counts need a managed descriptor array.
                InlineArray4<Buffer2DRegion<int>> outputStorage = default;
                Span<Buffer2DRegion<int>> inlineOutputs = outputStorage;
                Span<Buffer2DRegion<int>> outputs = transform.ChannelCount <= inlineOutputs.Length
                    ? inlineOutputs[..transform.ChannelCount]
                    : new Buffer2DRegion<int>[transform.ChannelCount];

                for (int c = 0; c < outputs.Length; c++)
                {
                    outputs[c] = channels[begin + c].Region;
                }

                Span<int> errors = transform.Predictor == 6 && !index.IsEmpty ? this.workspace.PrepareErrors(index.Width) : [];
                JxlModularPalette.Apply(
                    index.Region,
                    channels[0].Region,
                    outputs,
                    transform.DeltaCount,
                    transform.Predictor,
                    this.bitDepth,
                    weightedPredictor,
                    errors,
                    cancellationToken);

                JxlModularChannel palette = channels[0];
                palette.Dispose();
                channels.RemoveAt(0);
            }
            else
            {
                int residualStart = transform.InPlace ? transform.Begin + transform.ChannelCount : channels.Count - transform.ChannelCount;
                Span<JxlModularChannel> descriptors = CollectionsMarshal.AsSpan(channels);
                for (int c = 0; c < transform.ChannelCount; c++)
                {
                    ref JxlModularChannel averages = ref descriptors[transform.Begin + c];
                    ref JxlModularChannel residuals = ref descriptors[residualStart + c];
                    Size size = transform.Horizontal
                        ? new Size(averages.Width + residuals.Width, averages.Height)
                        : new Size(averages.Width, averages.Height + residuals.Height);

                    int horizontal = averages.HorizontalShift;
                    int vertical = averages.VerticalShift;
                    if (transform.Horizontal && horizontal >= 0)
                    {
                        horizontal--;
                    }
                    else if (!transform.Horizontal && vertical >= 0)
                    {
                        vertical--;
                    }

                    JxlModularChannel output = JxlModularChannel.Allocate(this.allocator, new JxlModularChannelLayout(size, horizontal, vertical));
                    try
                    {
                        if (!output.IsEmpty)
                        {
                            JxlModularSqueeze.Apply(averages.Region, residuals.Region, output.Region, transform.Horizontal, cancellationToken);
                        }
                    }
                    catch
                    {
                        output.Dispose();
                        throw;
                    }

                    // Clear ownership in both consumed descriptors before the next allocation can fail.
                    // Cleanup can then traverse the whole list without returning a residual buffer twice.
                    averages.Dispose();
                    residuals.Dispose();
                    averages = output;
                }

                channels.RemoveRange(residualStart, transform.ChannelCount);
            }
        }
    }

    /// <summary>
    /// Checks a transform range before indexing channels, including the metadata/normal-channel boundary.
    /// </summary>
    /// <param name="channels">The current logical layouts.</param>
    /// <param name="begin">The first selected channel.</param>
    /// <param name="number">The positive selected count from the encoded syntax.</param>
    /// <param name="metadataCount">The number of leading metadata channels.</param>
    /// <param name="equalShapes">Whether the transform requires equal dimensions and shifts.</param>
    private static void CheckChannelRange(List<JxlModularChannelLayout> channels, int begin, int number, int metadataCount, bool equalShapes)
    {
        if (begin + number > channels.Count || (begin < metadataCount && begin + number > metadataCount))
        {
            throw new InvalidImageContentException("The JPEG XL transform channel range is invalid.");
        }

        if (equalShapes)
        {
            JxlModularChannelLayout first = channels[begin];
            for (int c = begin + 1; c < begin + number; c++)
            {
                if (!first.HasSameShape(channels[c]))
                {
                    throw new InvalidImageContentException("The JPEG XL transform mixes different channel shapes.");
                }
            }
        }
    }

    /// <summary>
    /// Bounds transformed descriptor counts before later metadata or scratch indexing.
    /// </summary>
    /// <param name="count">The shape count after a transform.</param>
    private static void CheckChannelCount(int count)
    {
        if (count > MaximumChannels)
        {
            throw new InvalidImageContentException("The JPEG XL modular channel count exceeds the supported format limit.");
        }
    }

    /// <summary>
    /// Counts the leading palette metadata channels from their negative resolution shifts.
    /// </summary>
    /// <param name="channels">The channel layouts.</param>
    /// <returns>The leading metadata count.</returns>
    private static int GetMetadataCount(List<JxlModularChannelLayout> channels)
    {
        int count = 0;
        while (count < channels.Count && channels[count].IsMetadata)
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// Locates the first normal channel too large for a stream's global prefix.
    /// </summary>
    /// <param name="channels">The transformed layouts in encoded order.</param>
    /// <param name="maximumSize">The normal-channel dimension limit.</param>
    /// <returns>The number of channels eligible for this stream.</returns>
    private static int GetEligiblePrefix(List<JxlModularChannelLayout> channels, int maximumSize)
    {
        int count = 0;
        while (count < channels.Count)
        {
            JxlModularChannelLayout channel = channels[count];
            if (!channel.IsMetadata && (channel.Width > maximumSize || channel.Height > maximumSize))
            {
                break;
            }

            count++;
        }

        return count;
    }

    /// <summary>
    /// Counts encoded samples and the single distance multiplier required by their shared entropy stream.
    /// </summary>
    /// <param name="channels">The transformed layouts.</param>
    /// <param name="count">The eligible prefix count.</param>
    /// <param name="maximumWidth">The largest width among nonempty decoded channels.</param>
    /// <returns>The number of encoded residual values.</returns>
    private static long GetSampleCount(List<JxlModularChannel> channels, int count, out int maximumWidth)
    {
        long samples = 0;
        maximumWidth = 0;
        for (int c = 0; c < count; c++)
        {
            JxlModularChannel channel = channels[c];
            if (!channel.IsEmpty)
            {
                samples += (long)channel.Width * channel.Height;
                maximumWidth = Math.Max(maximumWidth, channel.Width);
            }
        }

        return samples;
    }

    /// <summary>
    /// Calculates the native global tree bound from the coding area before channel subsampling.
    /// </summary>
    /// <param name="size">The complete frame coding dimensions.</param>
    /// <param name="channelCount">The original color and extra-channel count.</param>
    /// <returns>The native maximum of four million nodes, or the smaller image-derived bound.</returns>
    private static int GetGlobalTreeLimit(Size size, int channelCount)
    {
        const int Maximum = 1 << 22;
        const long SampleBudget = (Maximum - 1024L) * 16;

        // Native code uses coding area times the original channel count, including reduced-resolution channels.
        // Cap the area first: the capped product times the format's channel limit fits in long.
        long area = Math.Min((long)size.Width * size.Height, SampleBudget);
        long samples = Math.Min(area * channelCount, SampleBudget);
        return 1024 + (int)(samples / 16);
    }

    /// <summary>
    /// Calculates the native local tree bound without overflowing unrestricted image-area products.
    /// </summary>
    /// <param name="channels">The transformed local layouts.</param>
    /// <param name="count">The applicable prefix length.</param>
    /// <returns>The bounded maximum number of tree nodes.</returns>
    private static int GetLocalTreeLimit(List<JxlModularChannel> channels, int count)
    {
        const int Maximum = 1 << 20;
        const long Budget = Maximum - 1024;
        long samples = 0;
        for (int c = 0; c < count && samples < Budget; c++)
        {
            // One int-sized width/height product fits in long. Saturate before summing channels because only the capped tree bound is needed.
            samples += Math.Min((long)channels[c].Width * channels[c].Height, Budget - samples);
        }

        return 1024 + (int)samples;
    }

    /// <summary>
    /// Rejects encoded normal channels whose shifts leave no sample in their assigned DC or AC grid tile.
    /// </summary>
    /// <param name="channels">The complete transformed frame layouts.</param>
    /// <param name="groupSize">The native coding-group dimension in pixels.</param>
    private static void ValidateGroupDimensions(List<JxlModularChannelLayout> channels, int groupSize)
    {
        int prefix = GetEligiblePrefix(channels, groupSize);
        for (int c = prefix; c < channels.Count; c++)
        {
            JxlModularChannelLayout channel = channels[c];
            if (channel.IsEmpty)
            {
                continue;
            }

            int minimumShift = Math.Min(channel.HorizontalShift, channel.VerticalShift);
            int maximumShift = Math.Max(channel.HorizontalShift, channel.VerticalShift);
            int dimension = minimumShift >= 3 ? groupSize * 8 : groupSize;
            if ((dimension >> maximumShift) == 0)
            {
                throw new InvalidImageContentException("The JPEG XL transformed channel does not fit its resolution-group grid.");
            }
        }
    }

    /// <summary>
    /// Validates restored output layouts before exporting local samples or exposing completed frame channels.
    /// </summary>
    /// <param name="channels">The reconstructed output descriptors.</param>
    /// <param name="expected">The original frame layouts or the layouts of retained borrowed group targets.</param>
    private static void ValidateShapes(List<JxlModularChannel> channels, ReadOnlySpan<JxlModularChannelLayout> expected)
    {
        if (channels.Count != expected.Length)
        {
            throw new InvalidImageContentException("The JPEG XL inverse transforms changed the output channel count.");
        }

        for (int c = 0; c < channels.Count; c++)
        {
            if (!channels[c].Layout.HasSameShape(expected[c]))
            {
                throw new InvalidImageContentException("The JPEG XL inverse transforms changed the output channel dimensions.");
            }
        }
    }

    /// <summary>
    /// Releases only buffers owned by these descriptors; borrowed frame regions remain with their frame owner.
    /// </summary>
    /// <param name="channels">The current live descriptor list.</param>
    private static void DisposeChannels(List<JxlModularChannel> channels)
    {
        Span<JxlModularChannel> descriptors = CollectionsMarshal.AsSpan(channels);
        foreach (ref JxlModularChannel channel in descriptors)
        {
            channel.Dispose();
        }
    }

    /// <summary>
    /// Retains one validated RCT, palette or flattened squeeze instruction until its samples can be restored.
    /// </summary>
    private readonly struct Transform
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Transform"/> struct from validated header fields.
        /// </summary>
        /// <param name="kind">The encoded transform kind.</param>
        /// <param name="begin">The first selected channel before this transform.</param>
        /// <param name="channelCount">The selected count.</param>
        /// <param name="colorOperation">The RCT operation, or zero for another transform kind.</param>
        /// <param name="deltaCount">The number of explicit prediction-delta entries for a palette.</param>
        /// <param name="predictor">The palette's predictor code.</param>
        /// <param name="horizontal">Whether a squeeze divides columns.</param>
        /// <param name="inPlace">Whether squeeze residuals follow the selected channels.</param>
        public Transform(byte kind, int begin, int channelCount, int colorOperation, int deltaCount, byte predictor, bool horizontal, bool inPlace)
        {
            this.Kind = kind;
            this.Begin = begin;
            this.ChannelCount = channelCount;
            this.ColorOperation = colorOperation;
            this.DeltaCount = deltaCount;
            this.Predictor = predictor;
            this.Horizontal = horizontal;
            this.InPlace = inPlace;
        }

        /// <summary>
        /// Gets the encoded transform kind.
        /// </summary>
        public byte Kind { get; }

        /// <summary>
        /// Gets the first selected channel.
        /// </summary>
        public int Begin { get; }

        /// <summary>
        /// Gets the selected count.
        /// </summary>
        public int ChannelCount { get; }

        /// <summary>
        /// Gets the RCT operation, from zero through 41.
        /// </summary>
        public int ColorOperation { get; }

        /// <summary>
        /// Gets the palette's explicit delta count.
        /// </summary>
        public int DeltaCount { get; }

        /// <summary>
        /// Gets the palette predictor code.
        /// </summary>
        public byte Predictor { get; }

        /// <summary>
        /// Gets a value indicating whether a squeeze divides columns.
        /// </summary>
        public bool Horizontal { get; }

        /// <summary>
        /// Gets a value indicating whether squeeze residuals follow the selected channels.
        /// </summary>
        public bool InPlace { get; }
    }
}
