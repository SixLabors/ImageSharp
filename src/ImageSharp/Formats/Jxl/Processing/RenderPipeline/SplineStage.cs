// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

internal sealed class SplineStage(Configuration configuration, JxlSplines splines) : RenderPipelineStageBase(configuration)
{
    public override string Name => "Splines";

    public override void ProcessRow(Buffer2D<Memory<float>> inputRows, Buffer2D<Memory<float>> outputRows, int xExtraLeft, int xExtraRight, int width, int xPos, int yPos)
    {
        Span<float> rowX = this.GetInputRow(inputRows, 0, 0, xExtraLeft);
        Span<float> rowY = this.GetInputRow(inputRows, 1, 0, xExtraLeft);
        Span<float> rowB = this.GetInputRow(inputRows, 2, 0, xExtraLeft);
        splines.AddToRow(rowX, rowY, rowB, yPos, xPos - xExtraLeft, xPos + width + xExtraRight);
    }

    public override RenderPipelineChannelMode GetChannelMode(int channel) => channel < 3 ? RenderPipelineChannelMode.InPlace : RenderPipelineChannelMode.Ignored;
}
