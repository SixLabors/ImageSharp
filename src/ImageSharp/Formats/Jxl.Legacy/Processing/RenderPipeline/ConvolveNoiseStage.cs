// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

internal class ConvolveNoiseStage : RenderPipelineStageBase
{
    /// <summary>
    /// Offset of the first channel. (3 channels in total)
    /// </summary>
    private readonly int firstC;

    public ConvolveNoiseStage(Configuration configuration, int firstC)
        : base(configuration)
    {
        this.Settings = RenderPipelineStageConfiguration.CreateSymmetricBorderOnly(2);
        this.firstC = firstC;
    }

    public override string Name => "ConvNoise";

    public override void ProcessRow(Buffer2D<Memory<float>> inputRows, Buffer2D<Memory<float>> outputRows, int xExtraLeft, int xExtraRight, int width, int xPos, int yPos)
    {
        int xStart = -xExtraLeft;
        int xEnd = width + xExtraRight;

        for (int c = this.firstC; c < this.firstC + 3; c++)
        {
            Span<float> row0 = this.GetInputRow(inputRows, c, -2);
            Span<float> row1 = this.GetInputRow(inputRows, c, 1);
            Span<float> row2 = this.GetInputRow(inputRows, c, 0);
            Span<float> row3 = this.GetInputRow(inputRows, c, 1);
            Span<float> row4 = this.GetInputRow(inputRows, c, 2);

            Span<float> rowOut = GetOutputRow(outputRows, c, 0);

            for (int x = xStart; x < xEnd; x += Vector<float>.Count)
            {
                Vector<float> p00 = Vector.Create<float>(row2[x..]);
                Vector<float> others = Vector<float>.Zero;

                for (int i = -2; i <= 2; i++)
                {
                    others += Vector.Create<float>(row0[(x + i)..]);
                    others += Vector.Create<float>(row1[(x + i)..]);
                    others += Vector.Create<float>(row3[(x + i)..]);
                    others += Vector.Create<float>(row4[(x + i)..]);
                }

                others += Vector.Create<float>(row2[(x - 2)..]);
                others += Vector.Create<float>(row2[(x - 1)..]);
                others += Vector.Create<float>(row2[(x + 1)..]);
                others += Vector.Create<float>(row2[(x + 2)..]);

                Vector<float> pixels = Vector.FusedMultiplyAdd(others, Vector.Create(0.16f), p00 * Vector.Create(-3.84f));
                pixels.CopyTo(rowOut[x..]);
            }
        }
    }

    public override RenderPipelineChannelMode GetChannelMode(int channel) => channel >= this.firstC ? RenderPipelineChannelMode.InOut : RenderPipelineChannelMode.Ignored;
}
