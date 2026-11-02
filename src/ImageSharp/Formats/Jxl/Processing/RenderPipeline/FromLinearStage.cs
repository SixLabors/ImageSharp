// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

internal class FromLinearStage<TOperator> : RenderPipelineStageBase
    where TOperator : FromLinearStageOperators.IOperator
{
    private readonly FromLinearStageOperators.IOperator op;

    public FromLinearStage(Configuration configuration, JxlOutputEncodingInfo outputEncodingInfo)
        : base(configuration)
    {
        this.Settings = default;
        this.op = TOperator.Create(outputEncodingInfo);
    }

    public override string Name => "FromLinear";

    public override void ProcessRow(Buffer2D<Memory<float>> inputRows, Buffer2D<Memory<float>> outputRows, int xExtraLeft, int xExtraRight, int width, int xPos, int yPos)
    {
        if (xExtraLeft != 0 || xExtraRight != 0)
        {
            throw new InvalidOperationException("xExtraLeft, xExtraRight must both = 0");
        }

        Span<float> row0 = this.GetInputRow(inputRows, 0, 0);
        Span<float> row1 = this.GetInputRow(inputRows, 1, 0);
        Span<float> row2 = this.GetInputRow(inputRows, 2, 0);

        for (int x = 0; x < width; x += Vector<float>.Count)
        {
            Vector<float> r = Vector.Create<float>(row0[x..]);
            Vector<float> g = Vector.Create<float>(row1[x..]);
            Vector<float> b = Vector.Create<float>(row2[x..]);

            this.op.Transform(ref r, ref g, ref b);

            r.CopyTo(row0[x..]);
            g.CopyTo(row1[x..]);
            b.CopyTo(row2[x..]);
        }
    }

    public override RenderPipelineChannelMode GetChannelMode(int channel) => channel < 3 ? RenderPipelineChannelMode.InPlace : RenderPipelineChannelMode.Ignored;
}
