// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Blending;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Patch;

internal sealed class JxlPatchDictionary
{
    private const byte NumPatchBlendModes = (byte)JxlPatchBlendMode.AlphaWeightedAddBelow + 1;

    /// <summary>
    /// Borrows the codestream owner's reference slots. The owner binds them before decoding patches and retains their sample planes.
    /// </summary>
    private (JxlImageBundle Frame, bool ImageBundleIsInXyb)[] referenceFrames = [];
    private readonly List<JxlPatchPosition> positions = [];
    private readonly List<JxlPatchReferencePosition> referencePositions = [];
    private readonly List<JxlPatchBlending> blendings = [];
    private int blendingsStride;
    private readonly List<PatchTreeNode> patchTree = [];
    private List<int> numPatches = [];
    private readonly List<SortedPatch> sortedPatchesY0 = [];
    private readonly List<SortedPatch> sortedPatchesY1 = [];

    /// <summary>
    /// Gets the maximum number of patch indices needed for any row.
    /// </summary>
    public int MaximumPatchesPerRow { get; private set; }

    public bool HasAny => this.positions.Count > 0;

    /// <summary>
    /// Borrows the codestream's reference slots without copying slots or taking ownership of their sample planes.
    /// </summary>
    /// <param name="referenceFrames">The slots retained by the shared state for this codestream.</param>
    public void SetShared((JxlImageBundle Frame, bool ImageBundleIsInXyb)[] referenceFrames) => this.referenceFrames = referenceFrames;

    /// <summary>
    /// Clears the decoded dictionary and its row index before it is reused for another frame.
    /// </summary>
    public void Clear()
    {
        // Remove frame-specific entries while retaining list capacity for the next frame.
        this.positions.Clear();
        this.referencePositions.Clear();
        this.blendings.Clear();
        this.blendingsStride = 0;
        this.patchTree.Clear();
        this.numPatches.Clear();
        this.sortedPatchesY0.Clear();
        this.sortedPatchesY1.Clear();
        this.MaximumPatchesPerRow = 0;
    }

    public int GetReferences()
    {
        int result = 0;

        foreach (JxlPatchReferencePosition refPos in this.referencePositions)
        {
            result |= 1 << refPos.Ref;
        }

        return result;
    }

    /// <summary>
    /// Reads patch references, positions, and blending settings from their entropy stream.
    /// </summary>
    /// <param name="configuration">The configuration that owns entropy and patch-tree storage.</param>
    /// <param name="br">The encoded patch fields.</param>
    /// <param name="xsize">The destination width.</param>
    /// <param name="ysize">The destination height.</param>
    /// <param name="numExtraChannels">The available extra-channel count.</param>
    /// <param name="usesExtraChannels">Whether decoded blending settings use extra channels.</param>
    public void Decode(
        Configuration configuration,
        JxlBitReader br,
        ulong xsize,
        ulong ysize,
        ulong numExtraChannels,
        ref bool usesExtraChannels)
    {
        this.Clear();

        // The image metadata header bounds extra channels to 4096. Each patch adds one color blending entry.
        this.blendingsStride = (int)numExtraChannels + 1;

        List<byte> contextMap = [];
        JxlAnsCode code = new();

        JxlAnsReader.DecodeHistograms(configuration, br, (int)JxlPatchContext.NumPatchDictionaryContexts, code, ref contextMap, false);

        // Patch fields use direct LZ77 distances; multiplier zero selects that native stream rule.
        using JxlAnsSymbolReader decoder = JxlAnsSymbolReader.Create(configuration, code, br, 0);

        // References and positions share one entropy stream. Borrow its map and LZ77 window before either loop.
        ReadOnlySpan<byte> contexts = CollectionsMarshal.AsSpan(contextMap);
        Span<int> window = decoder.Lz77Window;

        uint numRefPatch = (uint)decoder.ReadHybridUInt((int)JxlPatchContext.NumRefPatch, br, contexts, window);

        ulong numPixels = xsize * ysize;
        ulong maxRefPatches = 1024 + (numPixels / 4);
        ulong maxPatches = maxRefPatches * 4;
        ulong maxBlendingInfos = maxPatches * 4;

        // Retain the format's pixel-based quotas, but reject counts that cannot fit the managed lists.
        // Bounding patch count by the blending stride also keeps every capacity multiplication within that limit.
        maxRefPatches = Math.Min(maxRefPatches, (ulong)Array.MaxLength);
        maxBlendingInfos = Math.Min(maxBlendingInfos, (ulong)Array.MaxLength);
        maxPatches = Math.Min(maxPatches, maxBlendingInfos / (uint)this.blendingsStride);

        // The patch tree stores rectangle ends as Int32 values. Both the frame and that storage must contain each complete patch.
        ulong destinationWidth = Math.Min(xsize, (ulong)int.MaxValue);
        ulong destinationHeight = Math.Min(ysize, (ulong)int.MaxValue);

        if (numRefPatch > maxRefPatches)
        {
            throw new InvalidOperationException("Too many patches in dictionary");
        }

        ulong totalPatches = 0;
        ulong nextSize = 1;

        for (uint id = 0; id < numRefPatch; id++)
        {
            uint reference = (uint)decoder.ReadHybridUInt((int)JxlPatchContext.ReferenceFrame, br, contexts, window);
            if (reference >= JxlShared.MaximumNumberOfReferenceFrames || this.referenceFrames[(int)reference].Frame.XSize == 0)
            {
                throw new InvalidOperationException("Invalid reference frame ID");
            }

            if (!this.referenceFrames[(int)reference].ImageBundleIsInXyb)
            {
                throw new InvalidOperationException("Patches cannot use frames saved post color transforms");
            }

            JxlImageBundle ib = this.referenceFrames[(int)reference].Frame;
            ulong referenceX = (uint)decoder.ReadHybridUInt((int)JxlPatchContext.PatchReferencePosition, br, contexts, window);
            ulong referenceY = (uint)decoder.ReadHybridUInt((int)JxlPatchContext.PatchReferencePosition, br, contexts, window);

            // A size field stores size minus one. Widen before adding one, then validate the complete reference rectangle.
            ulong width = (ulong)(uint)decoder.ReadHybridUInt((int)JxlPatchContext.PatchSize, br, contexts, window) + 1;
            ulong height = (ulong)(uint)decoder.ReadHybridUInt((int)JxlPatchContext.PatchSize, br, contexts, window) + 1;

            if (referenceX + width > (ulong)ib.XSize || referenceY + height > (ulong)ib.YSize)
            {
                throw new InvalidOperationException("Invalid position specified in reference frame");
            }

            JxlPatchReferencePosition refPos = new()
            {
                Ref = (int)reference,
                X0 = (int)referenceX,
                Y0 = (int)referenceY,
                XSize = (int)width,
                YSize = (int)height
            };

            ulong idCount = (ulong)(uint)decoder.ReadHybridUInt((int)JxlPatchContext.PatchCount, br, contexts, window) + 1;

            // Check against the remaining quota before accumulating. The wire's UInt32 maximum plus one must not wrap.
            if (idCount > maxPatches - totalPatches)
            {
                throw new InvalidOperationException("Too many patches in dictionary");
            }

            totalPatches += idCount;

            if (nextSize < totalPatches)
            {
                nextSize = Math.Min(Math.Max(totalPatches, nextSize * 2), maxPatches);
            }

            _ = this.positions.EnsureCapacity((int)nextSize);
            _ = this.blendings.EnsureCapacity((int)(nextSize * (uint)this.blendingsStride));

            bool chooseAlpha = numExtraChannels > 1;

            for (ulong i = 0; i < idCount; i++)
            {
                long x;
                long y;

                if (i == 0)
                {
                    x = (uint)decoder.ReadHybridUInt((int)JxlPatchContext.PatchPosition, br, contexts, window);
                    y = (uint)decoder.ReadHybridUInt((int)JxlPatchContext.PatchPosition, br, contexts, window);
                }
                else
                {
                    // Delta codes cover the complete signed Int32 range. Add in Int64 so neither direction wraps before validation.
                    long deltaX = JxlPackSigned.UnpackSigned((uint)decoder.ReadHybridUInt((int)JxlPatchContext.PatchOffset, br, contexts, window));
                    x = this.positions[^1].X + deltaX;
                    long deltaY = JxlPackSigned.UnpackSigned((uint)decoder.ReadHybridUInt((int)JxlPatchContext.PatchOffset, br, contexts, window));
                    y = this.positions[^1].Y + deltaY;
                }

                if (x < 0 || (ulong)x + width > destinationWidth)
                {
                    throw new InvalidOperationException($"Invalid patch x: {x} + {width} > {xsize}");
                }

                if (y < 0 || (ulong)y + height > destinationHeight)
                {
                    throw new InvalidOperationException($"Invalid patch y: {y} + {height} > {ysize}");
                }

                JxlPatchPosition pos = new()
                {
                    X = (int)x,
                    Y = (int)y,
                    ReferencePositionIndex = this.referencePositions.Count
                };

                for (int j = 0; j < this.blendingsStride; j++)
                {
                    uint blendMode = (uint)decoder.ReadHybridUInt((int)JxlPatchContext.PatchBlendMode, br, contexts, window);

                    if (blendMode >= NumPatchBlendModes)
                    {
                        throw new InvalidOperationException($"Invalid patch blend mode: {blendMode}");
                    }

                    JxlPatchBlending info = new()
                    {
                        Mode = (JxlPatchBlendMode)blendMode
                    };

                    if (UsesAlpha(info.Mode))
                    {
                        usesExtraChannels = true;
                    }

                    if (info.Mode != JxlPatchBlendMode.None && j > 0)
                    {
                        usesExtraChannels = true;
                    }

                    if (UsesAlpha(info.Mode) && chooseAlpha)
                    {
                        uint alphaChannel = (uint)decoder.ReadHybridUInt((int)JxlPatchContext.PatchAlphaChannel, br, contexts, window);

                        if (alphaChannel >= numExtraChannels)
                        {
                            throw new InvalidOperationException($"Invalid alpha channel for blending: {alphaChannel} out of {numExtraChannels}");
                        }

                        info.AlphaChannel = (int)alphaChannel;
                    }
                    else
                    {
                        info.AlphaChannel = 0;
                    }

                    if (UsesClamp(info.Mode))
                    {
                        info.Clamp = (uint)decoder.ReadHybridUInt((int)JxlPatchContext.PatchClamp, br, contexts, window) != 0;
                    }
                    else
                    {
                        info.Clamp = false;
                    }

                    this.blendings.Add(info);
                }

                this.positions.Add(pos);
            }

            this.referencePositions.Add(refPos);
        }

        if (!decoder.CheckAnsFinalState())
        {
            throw new InvalidOperationException("ANS checksum failure.");
        }

        this.ComputePatchTree(configuration);
    }

    private static bool UsesAlpha(JxlPatchBlendMode mode) =>
        mode is JxlPatchBlendMode.BlendAbove or
         JxlPatchBlendMode.BlendBelow or
         JxlPatchBlendMode.AlphaWeightedAddAbove or
         JxlPatchBlendMode.AlphaWeightedAddBelow;

    private static bool UsesClamp(JxlPatchBlendMode mode) =>
        UsesAlpha(mode) || mode == JxlPatchBlendMode.Multiply;

    /// <summary>
    /// Builds the row-interval index for the decoded patches.
    /// </summary>
    /// <param name="configuration">The configuration that owns temporary interval storage.</param>
    public void ComputePatchTree(Configuration configuration)
    {
        this.patchTree.Clear();
        this.numPatches.Clear();
        this.sortedPatchesY0.Clear();
        this.sortedPatchesY1.Clear();
        this.MaximumPatchesPerRow = 0;

        if (this.positions.Count == 0)
        {
            return;
        }

        // Only decoded intervals participate in sorting. Allocator padding must never become a patch.
        using IMemoryOwner<PatchInterval> intervalStorage = configuration.MemoryAllocator.Allocate<PatchInterval>(this.positions.Count);
        Span<PatchInterval> intervals = intervalStorage.GetSpan()[..this.positions.Count];

        for (int i = 0; i < this.positions.Count; i++)
        {
            JxlPatchPosition pos = this.positions[i];
            intervals[i].Index = i;
            intervals[i].Y0 = pos.Y;
            intervals[i].Y1 = pos.Y + this.referencePositions[pos.ReferencePositionIndex].YSize;
        }

        intervals.Sort(static (left, right) => left.Y1.CompareTo(right.Y1));

        // List capacity does not create count entries. Materialize and clear the row counters before incrementing them.
        CollectionsMarshal.SetCount(this.numPatches, intervals[^1].Y1);
        Span<int> rowPatchCounts = CollectionsMarshal.AsSpan(this.numPatches);
        rowPatchCounts.Clear();

        foreach (PatchInterval interval in intervals)
        {
            for (int y = interval.Y0; y < interval.Y1; y++)
            {
                this.MaximumPatchesPerRow = Math.Max(this.MaximumPatchesPerRow, ++rowPatchCounts[y]);
            }
        }

        PatchTreeNode root = default;
        root.Start = 0;
        root.Count = intervals.Length;
        this.patchTree.Add(root);

        int next = 0;
        while (next < this.patchTree.Count)
        {
            PatchTreeNode node = this.patchTree[next];
            int start = node.Start;
            int end = node.Start + node.Count;

            intervals[start..end].Sort(static (left, right) => left.Y0.CompareTo(right.Y0));
            int middleIndex = start + (node.Count / 2);
            node.YCenter = intervals[middleIndex].Y0;

            // Divide the intervals in [start, end) into three groups:
            // - those completely to the right of YCenter: [rightStart, end)
            // - those overlapping YCenter: [leftEnd, rightStart)
            // - those completely to the left of YCenter: [start, leftEnd)
            int rightStart = middleIndex;
            while (rightStart < end && intervals[rightStart].Y0 == node.YCenter)
            {
                rightStart++;
            }

            intervals[start..rightStart].Sort(static (left, right) => left.Y1.CompareTo(right.Y1));
            int leftEnd = rightStart;

            while (leftEnd > start && intervals[leftEnd - 1].Y1 > node.YCenter)
            {
                leftEnd--;
            }

            node.Count = rightStart - leftEnd;
            node.Start = this.sortedPatchesY0.Count;

            for (int i = rightStart - 1; i >= leftEnd; i--)
            {
                this.sortedPatchesY1.Add(new SortedPatch(intervals[i].Y1, intervals[i].Index));
            }

            intervals[leftEnd..rightStart].Sort(static (left, right) => left.Y0.CompareTo(right.Y0));

            for (int i = leftEnd; i < rightStart; i++)
            {
                this.sortedPatchesY0.Add(new SortedPatch(intervals[i].Y0, intervals[i].Index));
            }

            node.LeftChild = node.RightChild = -1;

            if (leftEnd > start)
            {
                PatchTreeNode left = default;
                left.Start = start;
                left.Count = leftEnd - left.Start;

                node.LeftChild = this.patchTree.Count;

                this.patchTree.Add(left);
            }

            if (rightStart < end)
            {
                PatchTreeNode right = default;
                right.Start = rightStart;
                right.Count = end - right.Start;

                node.RightChild = this.patchTree.Count;

                this.patchTree.Add(right);
            }

            // node is a value copy. Store its center, ranges, and child links after list growth has finished.
            this.patchTree[next] = node;
            next++;
        }
    }

    /// <summary>
    /// Collects intersecting patch indices in their encoded application order.
    /// </summary>
    /// <param name="y">The frame row.</param>
    /// <param name="destination">Caller-owned storage with at least MaximumPatchesPerRow entries.</param>
    /// <returns>The written indices, or an empty span for a row without patches.</returns>
    public Span<int> GetPatchesForRow(int y, Span<int> destination)
    {
        if (y >= this.numPatches.Count || this.numPatches[y] == 0)
        {
            return destination[..0];
        }

        ReadOnlySpan<SortedPatch> starts = CollectionsMarshal.AsSpan(this.sortedPatchesY0);
        ReadOnlySpan<SortedPatch> ends = CollectionsMarshal.AsSpan(this.sortedPatchesY1);
        ReadOnlySpan<PatchTreeNode> tree = CollectionsMarshal.AsSpan(this.patchTree);
        int count = 0;

        for (int nodeIndex = 0; nodeIndex != -1;)
        {
            PatchTreeNode node = tree[nodeIndex];

            if (y <= node.YCenter)
            {
                // Starts are ascending. Every remaining interval starts after this row and cannot intersect it.
                for (int i = 0; i < node.Count; i++)
                {
                    SortedPatch patch = starts[node.Start + i];

                    if (y < patch.First)
                    {
                        break;
                    }

                    destination[count++] = patch.Second;
                }

                nodeIndex = y < node.YCenter ? (int)node.LeftChild : -1;
            }
            else
            {
                // Ends are descending and exclusive. At the first expired interval, all remaining intervals have expired too.
                for (int i = 0; i < node.Count; i++)
                {
                    SortedPatch patch = ends[node.Start + i];

                    if (y >= patch.First)
                    {
                        break;
                    }

                    destination[count++] = patch.Second;
                }

                nodeIndex = (int)node.RightChild;
            }
        }

        // Overlapping patches must compose in encoded order. Tree traversal order is not application order.
        Span<int> result = destination[..count];
        result.Sort();
        return result;
    }

    /// <summary>
    /// Applies the ordered patches that intersect one row segment using caller-owned row storage.
    /// </summary>
    /// <param name="inOut">The row segment, indexed by color and extra channel.</param>
    /// <param name="foreground">The reusable table that receives borrowed reference rows.</param>
    /// <param name="scratch">The reusable blend results, with one row per channel.</param>
    /// <param name="patchIndices">Reusable storage for at least MaximumPatchesPerRow patch indices.</param>
    /// <param name="y">The frame row.</param>
    /// <param name="startX">The frame column of the first sample in inOut.</param>
    /// <param name="width">The number of samples in the row segment.</param>
    /// <param name="extraChannelInfos">The extra-channel types and alpha representations.</param>
    /// <returns>Whether each patch can be applied to the row segment.</returns>
    public bool AddOneRow(
        Span<Memory<float>> inOut,
        Span<Memory<float>> foreground,
        JxlPlane<float> scratch,
        Span<int> patchIndices,
        int y,
        int startX,
        int width,
        List<JxlExtraChannelInfo> extraChannelInfos)
    {
        int extraChannelCount = extraChannelInfos.Count;

        if (extraChannelCount + 1 > this.blendingsStride)
        {
            // out of bounds extra channel
            return false;
        }

        Span<int> patches = this.GetPatchesForRow(y, patchIndices);

        Span<JxlPatchPosition> patchPositions = CollectionsMarshal.AsSpan(this.positions);
        Span<JxlPatchReferencePosition> patchRefPositions = CollectionsMarshal.AsSpan(this.referencePositions);

        for (int i = 0; i < patches.Length; i++)
        {
            int posIdx = patches[i];
            int blendingIdx = posIdx * this.blendingsStride;

            ref JxlPatchPosition pos = ref patchPositions[posIdx];
            ref JxlPatchReferencePosition refPos = ref patchRefPositions[pos.ReferencePositionIndex];

            int by = pos.Y;
            int bx = pos.X;
            int patchWidth = refPos.XSize;

            if (y < by || y >= by + refPos.YSize)
            {
                return false;
            }

            int iy = y - by;
            int @ref = refPos.Ref;

            if (bx >= startX + width || bx + patchWidth < startX)
            {
                continue;
            }

            int patchX0 = Math.Max(bx, startX);
            int patchX1 = Math.Min(bx + patchWidth, startX + width);

            // Slice at the actual overlap. The reference patch can start to the right of this row segment;
            // computing a row origin before the reference allocation would be invalid even if a later offset cancels it.
            int referenceStartX = refPos.X0 + patchX0 - bx;

            for (int c = 0; c < 3; c++)
            {
                foreground[c] = this.referenceFrames[@ref].Frame.Color!.PlaneRowMemory(c, refPos.Y0 + iy)[referenceStartX..];
            }

            for (int j = 0; j < extraChannelCount; j++)
            {
                foreground[3 + j] = this.referenceFrames[@ref].Frame.GetExtraChannel(j).GetRowMemory(refPos.Y0 + iy)[referenceStartX..];
            }

            JxlBlending.PerformBlending(
                inOut,
                foreground,
                inOut,
                patchX0 - startX,
                patchX1 - patchX0,
                this.blendings[blendingIdx],
                CollectionsMarshal.AsSpan(this.blendings)[(blendingIdx + 1)..],
                extraChannelInfos,
                scratch);
        }

        return true;
    }

    private struct PatchTreeNode
    {
        public long LeftChild;
        public long RightChild;
        public int YCenter;
        public int Start;
        public int Count;
    }

    private struct SortedPatch
    {
        public int First;
        public int Second;

        public SortedPatch(int first, int second)
        {
            this.First = first;
            this.Second = second;
        }
    }

    private struct PatchInterval
    {
        public int Index;
        public int Y0;
        public int Y1;
    }
}
