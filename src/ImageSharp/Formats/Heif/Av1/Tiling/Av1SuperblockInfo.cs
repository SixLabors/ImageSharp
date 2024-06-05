// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Symbol;

internal class Av1SuperblockInfo
{
    private readonly Av1FrameBuffer frameBuffer;

    public Av1SuperblockInfo(Av1FrameBuffer frameBuffer, Point position)
    {
        this.Position = position;
        this.frameBuffer = frameBuffer;
    }

    public Point Position { get; }

    public ref int SuperblockDeltaQ => ref this.frameBuffer.GetDeltaQuantizationIndex(this.Position);

    public Av1BlockModeInfo SuperblockModeInfo => this.GetModeInfo(default);

    public Span<int> CoefficientsY => this.frameBuffer.GetCoefficientsY(this.Position);

    public Span<int> CoefficientsU => this.frameBuffer.GetCoefficientsU(this.Position);

    public Span<int> CoefficientsV => this.frameBuffer.GetCoefficientsV(this.Position);

    public Av1TransformInfo SuperblockTransformInfo => this.frameBuffer.GetTransformY(this.Position);

    public Span<int> CdefStrength => this.frameBuffer.GetCdefStrength(this.Position);

    public Span<int> SuperblockDeltaLoopFilter => this.frameBuffer.GetDeltaLoopFilter(this.Position);

    public Av1BlockModeInfo GetModeInfo(Point index) => this.frameBuffer.GetModeInfo(this.Position, index);
}
