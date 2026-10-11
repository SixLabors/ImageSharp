// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Noise;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

internal class AddNoiseStage(
    Configuration configuration,
    JxlNoiseParameters noiseParams,
    JxlColorCorrelation colorCorrelation,
    int firstC)
    : RenderPipelineStageBase(configuration)
{
    public override string Name => "AddNoise";

    public override void ProcessRow(Buffer2D<Memory<float>> inputRows, Buffer2D<Memory<float>> outputRows, int xExtraLeft, int xExtraRight, int width, int xPos, int yPos)
    {
        if (!noiseParams.ContainsAny)
        {
            return;
        }

        if (xExtraLeft != 0 || xExtraRight != 0)
        {
            throw new InvalidOperationException("xExtraLeft and xExtraRight must both be 0");
        }

        StrengthEvaluationLut noiseModel = new(noiseParams);
        Vector<float> half = Vector.Create(0.5f);

        // With the prior subtract-random Laplacian approximation, rnd_* ranges were
        // about [-1.5, 1.6]; Laplacian3 about doubles this to [-3.6, 3.6], so the
        // normalizer is half of what it was before (0.5).
        Vector<float> normConst = Vector.Create(0.22f);

        float yToX = colorCorrelation.YToXRatio(0);
        float yToB = colorCorrelation.YToBRatio(0);

        Span<float> rowX = this.GetInputRow(inputRows, 0, 0);
        Span<float> rowY = this.GetInputRow(inputRows, 1, 0);
        Span<float> rowB = this.GetInputRow(inputRows, 2, 0);

        Span<float> rowRndR = this.GetInputRow(inputRows, firstC + 0, 0);
        Span<float> rowRndG = this.GetInputRow(inputRows, firstC + 1, 0);
        Span<float> rowRndC = this.GetInputRow(inputRows, firstC + 2, 0);

        for (int x = 0; x < width; x += Vector<float>.Count)
        {
            Vector<float> vx = Vector.Create<float>(rowX[x..]);
            Vector<float> vy = Vector.Create<float>(rowY[x..]);

            Vector<float> inputG = vy - vx;
            Vector<float> inputR = vy + vx;

            Vector<float> noiseStrengthG = NoiseStageUtils.NoiseStrength(ref noiseModel, inputG * half);
            Vector<float> noiseStrengthR = NoiseStageUtils.NoiseStrength(ref noiseModel, inputR * half);

            Vector<float> additionalRandomNoiseRed = Vector.Create<float>(rowRndR[x..]) * normConst;
            Vector<float> additionalRandomNoiseGreen = Vector.Create<float>(rowRndG[x..]) * normConst;
            Vector<float> additionalRandomNoiseCorrelated = Vector.Create<float>(rowRndC[x..]) * normConst;

            NoiseStageUtils.AddNoiseToRgb(
                additionalRandomNoiseRed,
                additionalRandomNoiseGreen,
                additionalRandomNoiseCorrelated,
                noiseStrengthG,
                noiseStrengthR,
                yToX,
                yToB,
                rowX[x..],
                rowY[x..],
                rowB[x..]);
        }
    }

    public override RenderPipelineChannelMode GetChannelMode(int channel)
    {
        if (channel >= firstC)
        {
            return RenderPipelineChannelMode.Input;
        }

        if (channel < 3)
        {
            return RenderPipelineChannelMode.InPlace;
        }

        return RenderPipelineChannelMode.Ignored;
    }
}
