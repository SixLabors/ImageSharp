// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal class Av1SuperblockInfo
{
    private readonly Av1FrameInfo frameInfo;

    public Av1SuperblockInfo(Av1FrameInfo frameInfo, Point position)
    {
        this.Position = position;
        this.frameInfo = frameInfo;
    }

    /// <summary>
    /// Gets the position of this superblock inside the tile, counted in superblocks.
    /// </summary>
    public Point Position { get; }

    public ref int SuperblockDeltaQ => ref this.frameInfo.GetDeltaQuantizationIndex(this.Position);

    public Av1BlockModeInfo SuperblockModeInfo => this.GetModeInfo(new Point(0, 0));

    public Span<int> CoefficientsY => this.frameInfo.GetCoefficientsY(this.Position);

    public Span<int> CoefficientsU => this.frameInfo.GetCoefficientsU(this.Position);

    public Span<int> CoefficientsV => this.frameInfo.GetCoefficientsV(this.Position);

    public Span<int> CdefStrength => this.frameInfo.GetCdefStrength(this.Position);

    public Span<int> SuperblockDeltaLoopFilter => this.frameInfo.GetDeltaLoopFilter(this.Position);

    public int TransformInfoIndexY { get; internal set; }

    public int TransformInfoIndexUv { get; internal set; }

    public int BlockCount { get; internal set; }

    public ref Av1TransformInfo GetTransformInfoY() => ref this.frameInfo.GetSuperblockTransformY(this.Position);

    public ref Av1TransformInfo GetTransformInfoUv() => ref this.frameInfo.GetSuperblockTransformUv(this.Position);

    public Av1BlockModeInfo GetModeInfo(Point index) => this.frameInfo.GetModeInfo(this.Position, index);
}
