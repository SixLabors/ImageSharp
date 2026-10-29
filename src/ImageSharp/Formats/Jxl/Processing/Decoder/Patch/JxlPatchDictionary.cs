// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Blending;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Patch;

internal sealed class JxlPatchDictionary
{
    private const byte NumPatchBlendModes = (byte)JxlPatchBlendMode.AlphaWeightedAddBelow + 1;

    private JxlReferenceFrame[] referenceFrames = new JxlReferenceFrame[4];
    private readonly List<JxlPatchPosition> positions = [];
    private readonly List<JxlPatchReferencePosition> referencePositions = [];
    private readonly List<JxlPatchBlending> blendings = [];
    private int blendingsStride;
    private readonly List<PatchTreeNode> patchTree = [];
    private List<int> numPatches = [];
    private readonly List<SortedPatch> sortedPatchesY0 = [];
    private readonly List<SortedPatch> sortedPatchesY1 = [];

    public bool HasAny => this.positions.Count > 0;

    public void SetShared(JxlReferenceFrame[] referenceFrames) => this.referenceFrames = referenceFrames;

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

        this.ComputePatchTree();
    }

    private static bool UsesAlpha(JxlPatchBlendMode mode) =>
        mode is JxlPatchBlendMode.BlendAbove or
         JxlPatchBlendMode.BlendBelow or
         JxlPatchBlendMode.AlphaWeightedAddAbove or
         JxlPatchBlendMode.AlphaWeightedAddBelow;

    private static bool UsesClamp(JxlPatchBlendMode mode) =>
        UsesAlpha(mode) || mode == JxlPatchBlendMode.Multiply;

    public void ComputePatchTree()
    {
        this.patchTree.Clear();
        this.numPatches.Clear();
        this.sortedPatchesY0.Clear();
        this.sortedPatchesY1.Clear();

        if (this.positions.Count == 0)
        {
            return;
        }

        PatchInterval[] intervals = ArrayPool<PatchInterval>.Shared.Rent(this.positions.Count);

        for (int i = 0; i < this.positions.Count; i++)
        {
            JxlPatchPosition pos = this.positions[i];
            intervals[i].Index = i;
            intervals[i].Y0 = pos.Y;
            intervals[i].Y1 = pos.Y + this.referencePositions[pos.ReferencePositionIndex].YSize;
        }

        void SortByY0(int start, int end) => intervals.AsSpan()[start..end].Sort(static (i0, i1) => i0.Y0.CompareTo(i1.Y0));

        void SortByY1(int start, int end) => intervals.AsSpan()[start..end].Sort(static (i0, i1) => i0.Y1.CompareTo(i1.Y1));

        SortByY1(0, intervals.Length);

        // We have cleared the numPatches list at the start of this
        // method. We have to assign a new one.
        this.numPatches = new List<int>(intervals[^1].Y1);

        // Finding frequency of each patch within any
        // interval
        foreach (PatchInterval interval in intervals)
        {
            for (int y = interval.Y0; y < interval.Y1; y++)
            {
                this.numPatches[y]++;
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

            SortByY0(start, end);
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

            SortByY1(start, rightStart);
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

            SortByY0(leftEnd, rightStart);

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

                ref PatchTreeNode nextNode = ref CollectionsMarshal.AsSpan(this.patchTree)[next];
                nextNode.LeftChild = this.patchTree.Count;

                this.patchTree.Add(left);
            }

            if (rightStart < end)
            {
                PatchTreeNode right = default;
                right.Start = rightStart;
                right.Count = end - right.Start;

                ref PatchTreeNode nextNode = ref CollectionsMarshal.AsSpan(this.patchTree)[next];
                nextNode.RightChild = this.patchTree.Count;

                this.patchTree.Add(right);
            }

            next++;
        }

        ArrayPool<PatchInterval>.Shared.Return(intervals);
    }

    public IMemoryOwner<int> GetPatchesForRow(Configuration configuration, int y)
    {
        IMemoryOwner<int>? result = null;

        Span<SortedPatch> sortedPatchesY0 = CollectionsMarshal.AsSpan(this.sortedPatchesY0);
        Span<SortedPatch> sortedPatchesY1 = CollectionsMarshal.AsSpan(this.sortedPatchesY1);

        if (y < this.numPatches.Count && this.numPatches[y] > 0)
        {
            result = configuration.MemoryAllocator.Allocate<int>(this.numPatches[y]);
            Span<int> spanResult = result.GetSpan();

            // Instead of making result a List, we simply allocate
            // a fixed-length memory, get a Span to it, and track the
            // length separately.
            int resultPointer = 0;

            for (int treeIdx = 0; treeIdx != -1;)
            {
                // Index of the tree must be within the bounds of the patch tree
                if (treeIdx >= this.patchTree.Count)
                {
                    throw new InvalidOperationException("Tree index out of bounds");
                }

                // Recomputing CollectionsMarshal.AsSpan is important as we'll
                // be adding items to the backing list. Items should not be
                // added or removed to the original list while the Span<T>
                // returned by CollectionsMarshal.AsSpan is in use.
                Span<PatchTreeNode> patchTreeSpan = CollectionsMarshal.AsSpan(this.patchTree);

                ref PatchTreeNode node = ref patchTreeSpan[treeIdx];

                if (y <= node.YCenter)
                {
                    for (int i = 0; i < node.Count; ++i)
                    {
                        ref SortedPatch p = ref sortedPatchesY0[node.Start + i];

                        if (y < p.First)
                        {
                            break;
                        }

                        spanResult[resultPointer++] = p.Second;
                    }

                    treeIdx = y < node.YCenter ? (int)node.LeftChild : -1;
                }
                else
                {
                    for (int i = 0; i < node.Count; ++i)
                    {
                        ref SortedPatch p = ref sortedPatchesY1[node.Start + i];

                        if (y >= p.First)
                        {
                            break;
                        }

                        spanResult[resultPointer++] = p.Second;
                    }

                    treeIdx = (int)node.RightChild;
                }
            }

            // Once we collect patches, we must make sure
            // that the resulting patches are sorted.
            //
            // Span<T>.Sort() in theory should be faster
            // and more lightweight than an array's sort.
            spanResult.Sort();
        }

        return result
            ?? throw new InvalidOperationException("Y offset out of bounds");
    }

    public bool AddOneRow(
        Configuration configuration,
        Span<Memory<float>> inOut,
        int y,
        int x0,
        int width,
        List<JxlExtraChannelInfo> extraChannelInfos)
    {
        int numEc = extraChannelInfos.Count; // number of Extra Channels (ECs)

        if (numEc + 1 > this.blendingsStride)
        {
            // out of bounds extra channel
            return false;
        }

        using IMemoryOwner<int> patches = this.GetPatchesForRow(configuration, y);
        Span<int> patchesSpan = patches.GetSpan();

        Span<Memory<float>> foreground = new Memory<float>[3 + numEc];

        Span<JxlPatchPosition> patchPositions = CollectionsMarshal.AsSpan(this.positions);
        Span<JxlPatchReferencePosition> patchRefPositions = CollectionsMarshal.AsSpan(this.referencePositions);

        for (int i = 0; i < patchesSpan.Length; i++)
        {
            int posIdx = patchesSpan[i];
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

            if (bx >= x0 + width || bx + patchWidth < x0)
            {
                continue;
            }

            int patchX0 = Math.Max(bx, x0);
            int patchX1 = Math.Min(bx + patchWidth, x0 + width);

            for (int c = 0; c < 3; c++)
            {
                foreground[c] = this.referenceFrames[@ref].Frame.Color!.PlaneRowMemory(c, refPos.Y0 + iy)[(refPos.X0 + x0 - bx)..];
            }

            for (int j = 0; j < 3; j++)
            {
                foreground[3 + j] = this.referenceFrames[@ref].Frame.GetExtraChannel(j).GetRowMemory(refPos.Y0 + iy)[(refPos.X0 + x0 - bx)..];
            }

            JxlBlending.PerformBlending(
                configuration,
                inOut,
                foreground,
                inOut,
                patchX0 - x0,
                patchX1 - patchX0,
                this.blendings[blendingIdx],
                CollectionsMarshal.AsSpan(this.blendings)[(blendingIdx + 1)..],
                extraChannelInfos);
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
