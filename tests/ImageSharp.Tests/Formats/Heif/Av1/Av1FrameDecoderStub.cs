// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

internal class Av1FrameDecoderStub : IAv1FrameDecoder
{
    private readonly List<Av1SuperblockInfo> superblocks = [];
    private readonly int[] nextTransform = new int[3];
    private readonly int[] transformEnd = new int[3];
    private int blocksInCurrentSuperblock;

    public int BlockCount { get; private set; }

    public int TransformCount { get; private set; }

    public int RemainingTransforms =>
        this.transformEnd[0] - this.nextTransform[0] +
        this.transformEnd[1] - this.nextTransform[1] +
        this.transformEnd[2] - this.nextTransform[2];

    public int SuperblockCount => this.superblocks.Count;

    public void BeginSuperblock(Av1SuperblockInfo superblockInfo)
    {
        Assert.Equal(0, this.RemainingTransforms);
        Assert.Equal(0, superblockInfo.BlockCount);
        this.blocksInCurrentSuperblock = 0;
        this.superblocks.Add(superblockInfo);
    }

    public void BeginBlock(ref Av1PartitionInfo partitionInfo, Av1TileInfo tileInfo)
    {
        Assert.Equal(0, this.RemainingTransforms);
        Av1SuperblockInfo superblockInfo = partitionInfo.SuperblockInfo;
        ref Av1BlockModeInfo modeInfo = ref partitionInfo.ModeInfo;
        Point modeInfoPosition = new(partitionInfo.ColumnIndex, partitionInfo.RowIndex);

        // The current record must be published, but no later block in this superblock may have
        // been parsed yet. Checking inside the callback distinguishes interleaving from a later walk.
        this.blocksInCurrentSuperblock++;
        Assert.Equal(this.blocksInCurrentSuperblock, superblockInfo.BlockCount);
        Assert.Equal(modeInfo.ModeInfoIndex, superblockInfo.GetModeInfoAt(modeInfoPosition).ModeInfoIndex);
        Assert.Equal(superblockInfo.ModeInfoPosition + (Size)modeInfo.PositionInSuperblock, modeInfoPosition);
        this.BlockCount++;

        // Geometry has been parsed, but residual metadata has not. Poison only EOB, which residual
        // parsing overwrites before reconstruction. Later transform descriptors must retain the poison
        // until their own callback; this detects reading all coefficients before visiting transforms.
        for (int plane = 0; plane < 3; plane++)
        {
            Av1Plane selectedPlane = (Av1Plane)plane;
            int count = modeInfo.GetTransformUnitCount(selectedPlane);
            int start = modeInfo.GetFirstTransformLocation(selectedPlane);
            if (plane == 2)
            {
                start += count;
            }

            this.nextTransform[plane] = start;
            this.transformEnd[plane] = start + count;
            Span<Av1TransformInfo> transforms = superblockInfo.GetTransformInfo(plane).Slice(start, count);
            foreach (ref Av1TransformInfo transform in transforms)
            {
                transform.EndOfBlock = ushort.MaxValue;
            }
        }
    }

    public void EndBlock(ref Av1PartitionInfo partitionInfo) => Assert.Equal(0, this.RemainingTransforms);

    public void DecodeTransform(ref Av1PartitionInfo partitionInfo, int plane, ref Av1TransformInfo transformInfo, Av1TileInfo tileInfo)
    {
        Assert.NotEqual(ushort.MaxValue, transformInfo.EndOfBlock);
        Av1TransformInfo expected = partitionInfo.SuperblockInfo.GetTransformInfo(plane)[this.nextTransform[plane]];
        Assert.Equal(expected.Size, transformInfo.Size);
        Assert.Equal(expected.OffsetX, transformInfo.OffsetX);
        Assert.Equal(expected.OffsetY, transformInfo.OffsetY);
        this.nextTransform[plane]++;
        this.TransformCount++;

        for (int nextPlane = 0; nextPlane < 3; nextPlane++)
        {
            Span<Av1TransformInfo> transforms = partitionInfo.SuperblockInfo.GetTransformInfo(nextPlane);
            for (int index = this.nextTransform[nextPlane]; index < this.transformEnd[nextPlane]; index++)
            {
                Assert.Equal(ushort.MaxValue, transforms[index].EndOfBlock);
            }
        }
    }
}
