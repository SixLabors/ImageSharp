// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal class Av1SuperblockInfo
{
    private readonly Av1FrameBuffer frameBuffer;

    public Av1SuperblockInfo(Av1FrameBuffer frameBuffer, Point position)
    {
        this.Position = position;
        this.frameBuffer = frameBuffer;
    }

    /// <summary>
    /// Gets the position of this superblock inside the tile, counted in superblocks.
    /// </summary>
    public Point Position { get; }

    public ref int SuperblockDeltaQ => ref this.frameBuffer.GetDeltaQuantizationIndex(this.Position);

    public Av1BlockModeInfo SuperblockModeInfo => this.GetModeInfo(new Point(0, 0));

    public Span<int> CoefficientsY => this.frameBuffer.GetCoefficientsY(this.Position);

    public Span<int> CoefficientsU => this.frameBuffer.GetCoefficientsU(this.Position);

    public Span<int> CoefficientsV => this.frameBuffer.GetCoefficientsV(this.Position);

    public Span<int> CdefStrength => this.frameBuffer.GetCdefStrength(this.Position);

    public Span<int> SuperblockDeltaLoopFilter => this.frameBuffer.GetDeltaLoopFilter(this.Position);

    public int TransformInfoIndexY { get; internal set; }

    public int TransformInfoIndexUv { get; internal set; }

    public Span<Av1TransformInfo> GetTransformInfoY() => this.frameBuffer.GetSuperblockTransformY(this.Position);

    public Span<Av1TransformInfo> GetTransformInfoUv() => this.frameBuffer.GetSuperblockTransformUv(this.Position);

    public Av1BlockModeInfo GetModeInfo(Point index) => this.frameBuffer.GetModeInfo(this.Position, index);
}
