// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

internal sealed class JxlPatchDictionary
{
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
    }

    private readonly JxlReferenceFrame[] referenceFrames = new JxlReferenceFrame[4];
    private readonly List<JxlPatchPosition> positions = [];
    private readonly List<JxlPatchReferencePosition> referencePositions = [];
    private readonly List<JxlPatchBlending> blendings = [];
    private int blendingsStride;
    private readonly List<PatchTreeNode> patchTree = [];
    private readonly List<int> numPatches = [];
    private readonly List<SortedPatch> sortedPatchesY0 = [];
    private readonly List<SortedPatch> sortedPatchesY1 = [];

    public bool HasAny => this.positions.Count > 0;

    public void Clear()
    {
        this.positions.Clear();
        ComputePatchTree();
    }

    public void Decode(
        JxlMemoryManager memoryManager,
        JxlBitReader br,
        ulong xsize,
        ulong ysize,
        ulong numExtraChannels,
        ref bool usesExtraChannels)
    {
        this.positions.Clear();
        this.blendingsStride = (int)(numExtraChannels + 1);

        List<byte> contextMap = [];
        var code = new JxlAnsCode();

        var status = DecodeHistograms(
            memoryManager,
            br,
            PatchDictionaryContexts,
            code,
            contextMap);

        JxlAnsSymbolReader decoder = JxlAnsSymbolReader.Create(code, br);

        ulong ReadNum(int context)
            => decoder.ReadHybridUint(context, br, contextMap);

        ulong numRefPatch = ReadNum(kNumRefPatchContext);

        ulong numPixels = xsize * ysize;
        ulong maxRefPatches = 1024 + (numPixels / 4);
        ulong maxPatches = maxRefPatches * 4;
        ulong maxBlendingInfos = maxPatches * 4;

        if (numRefPatch > maxRefPatches)
        {
            throw new InvalidOperationException("Too many patches in dictionary");
        }

        ulong totalPatches = 0;
        ulong nextSize = 1;

        for (ulong id = 0; id < numRefPatch; id++)
        {
            JxlPatchReferencePosition refPos = new()
            {
                Ref = ReadNum(kReferenceFrameContext)
            };

            if (refPos.Ref >= kMaxNumReferenceFrames || this.referenceFrames[(int)refPos.Ref].Frame.XSize == 0)
            {
                throw new InvalidOperationException("Invalid reference frame ID");
            }

            if (!this.referenceFrames[refPos.Ref].IsInXYB)
            {
                throw new InvalidOperationException("Patches cannot use frames saved post color transforms");
            }

            JxlImageBundle ib = this.referenceFrames[refPos.Ref].Frame;

            refPos.X0 = ReadNum(kPatchReferencePositionContext);
            refPos.Y0 = ReadNum(kPatchReferencePositionContext);
            refPos.XSize = ReadNum(kPatchSizeContext) + 1;
            refPos.YSize = ReadNum(kPatchSizeContext) + 1;

            if (refPos.X0 + refPos.XSize > ib.XSize)
            {
                throw new InvalidOperationException("Invalid position specified in reference frame");
            }

            if (refPos.Y0 + refPos.YSize > ib.YSize)
            {
                throw new InvalidOperationException("Invalid position specified in reference frame");
            }

            ulong idCount = ReadNum(kPatchCountContext);

            if (idCount > maxPatches)
            {
                throw new InvalidOperationException("Too many patches in dictionary");
            }

            idCount++;

            totalPatches += idCount;

            if (totalPatches > maxPatches)
            {
                throw new InvalidOperationException("Too many patches in dictionary");
            }

            if (nextSize < totalPatches)
            {
                nextSize *= 2;
                nextSize = Math.Min(nextSize, maxPatches);
            }

            if (nextSize * (ulong)this.blendingsStride > maxBlendingInfos)
            {
                throw new InvalidOperationException("Too many patches in dictionary");
            }

            _ = this.blendings.EnsureCapacity((int)nextSize);
            _ = this.blendings.EnsureCapacity((int)(nextSize * (ulong)this.blendingsStride));

            bool chooseAlpha = numExtraChannels > 1;

            for (ulong i = 0; i < idCount; i++)
            {
                JxlPatchPosition pos = new()
                {
                    ReferencePositionIndex = this.referencePositions.Count
                };

                if (i == 0)
                {
                    pos.X = ReadNum(kPatchPositionContext);
                    pos.Y = ReadNum(kPatchPositionContext);
                }
                else
                {
                    long deltaX = JxlPackSigned.UnpackSigned(ReadNum(kPatchOffsetContext));

                    if (deltaX < 0 && (int)(-deltaX) > this.positions[^1].X)
                    {
                        throw new InvalidOperationException($"Invalid patch: negative x coordinate ({this.positions[^1].X}, delta {deltaX})");
                    }

                    pos.X = (int)(this.positions[^1].X + deltaX);

                    long deltaY = JxlPackSigned.UnpackSigned(ReadNum(kPatchOffsetContext));

                    if (deltaY < 0 && (int)(-deltaY) > this.positions[^1].Y)
                    {
                        throw new InvalidOperationException($"Invalid patch: negative y coordinate ({this.positions[^1].Y}, delta {deltaY})");
                    }

                    pos.Y = (int)(this.positions[^1].Y + deltaY);
                }

                if (pos.X + refPos.XSize > (int)xsize)
                {
                    throw new InvalidOperationException($"Invalid patch x: {pos.X} + {refPos.XSize} > {xsize}");
                }

                if (pos.Y + refPos.YSize > (int)ysize)
                {
                    throw new InvalidOperationException($"Invalid patch y: {pos.Y} + {refPos.YSize} > {ysize}");
                }

                for (int j = 0; j < this.blendingsStride; j++)
                {
                    uint blendMode = (uint)ReadNum(kPatchBlendModeContext);

                    if (blendMode >= kNumPatchBlendModes)
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
                        info.AlphaChannel = (uint)ReadNum(kPatchAlphaChannelContext);

                        if (info.AlphaChannel >= (int)numExtraChannels)
                        {
                            throw new InvalidOperationException($"Invalid alpha channel for blending: {info.AlphaChannel} out of {numExtraChannels}");
                        }
                    }
                    else
                    {
                        info.AlphaChannel = 0;
                    }

                    if (UsesClamp(info.Mode))
                    {
                        info.Clamp = ReadNum(kPatchClampContext) != 0;
                    }
                    else
                    {
                        info.Clamp = false;
                    }

                    this.blendings.Add(info);
                }

                this.positions.Add(pos);
            }

            this.positions.Add(refPos);
        }

        this.positions.TrimExcess();

        if (!decoder.CheckAnsFinalState())
        {
            throw new InvalidOperationException("ANS checksum failure.");
        }

        this.ComputePatchTree();
    }
}
