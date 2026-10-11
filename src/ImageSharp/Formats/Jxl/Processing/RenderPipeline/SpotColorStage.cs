// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

internal sealed class SpotColorStage(Configuration configuration, int spotColorOffset, Memory<float> spotColor)
    : RenderPipelineStageBase(configuration)
{
    private readonly int spotC = 3 + spotColorOffset;

    public override string Name => "Spot";

    public override void ProcessRow(Buffer2D<Memory<float>> inputRows, Buffer2D<Memory<float>> outputRows, int xExtraLeft, int xExtraRight, int width, int xPos, int yPos)
    {
        Span<float> spotColors = spotColor.Span;

        float scale = 0;
        for (int c = 0; c < 3; c++)
        {
            Span<float> p = this.GetInputRow(inputRows, c, 0);
            Span<float> s = this.GetInputRow(inputRows, this.spotC, 0);

            for (int x = 0; x < width; x++)
            {
                float mix = scale * s[x];
                p[x] = (mix * spotColors[c]) + ((1.0f - mix) * p[x]);
            }
        }
    }

    public override RenderPipelineChannelMode GetChannelMode(int channel)
        => channel < 3 ? RenderPipelineChannelMode.InPlace
            : channel == this.spotC ? RenderPipelineChannelMode.Input
            : RenderPipelineChannelMode.Ignored;
}
