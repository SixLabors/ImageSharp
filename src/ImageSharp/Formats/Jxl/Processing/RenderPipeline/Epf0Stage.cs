// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

/// <summary>
/// Edge Preserving Filter (type 0) stage
/// </summary>
internal sealed class Epf0Stage : RenderPipelineStageBase
{
    private static readonly int[][] SadOffsets =
    [
        [-2, 0], [-1, -1], [-1, 0], [-1, 1], [0, -2], [0, -1],
        [0, 1], [0, 2], [1, -1], [1, 0], [1, 1], [2, 0]
    ];

    private static readonly int[][] PlusOffsets =
    [
        [0, 0], [-1, 0], [0, -1], [1, 0], [0, 1]
    ];

    private readonly JxlLoopFilter loopFilter;
    private readonly JxlImageF sigma;

    public Epf0Stage(JxlLoopFilter loopFilter, JxlImageF sigma, Configuration configuration) : base(configuration)
    {
        this.loopFilter = loopFilter;
        this.sigma = sigma;
        this.Settings = RenderPipelineStageConfiguration.CreateSymmetricBorderOnly(3);
    }

    /// <inheritdoc />
    public override string Name => "EPF0";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddPixel(
        int row,
        InlineArray3<InlineArray7<Memory<float>>> rows,
        int x,
        Vector256<float> sad,
        Vector256<float> inverseSigma,
        ref Vector256<float> xOut,
        ref Vector256<float> yOut,
        ref Vector256<float> bOut,
        ref Vector256<float> wOut)
    {
        int rowPlus3 = row + 3;
        Vector256<float> cx = Vector256.Create<float>(rows[0][rowPlus3].Span[x..]);
        Vector256<float> cy = Vector256.Create<float>(rows[1][rowPlus3].Span[x..]);
        Vector256<float> cb = Vector256.Create<float>(rows[2][rowPlus3].Span[x..]);
        Vector256<float> weight = EpfUtils.Weight(sad, inverseSigma);
        wOut += weight;
        xOut += (weight * cx) + xOut;
        yOut += (weight * cy) + yOut;
        bOut += (weight * cb) + bOut;
    }

    /// <inheritdoc />
    public override void ProcessRow(Buffer2D<Memory<float>> inputRows, Buffer2D<Memory<float>> outputRows, int xExtraLeft, int xExtraRight, int width, int xPos, int yPos)
    {
        Span<Vector256<float>> sads = stackalloc Vector256<float>[16].Slice(0, 12);
        sads.Clear();

        int xStart = -JxlMath.RoundUpTo(xExtraLeft, Vector256<float>.Count);
        int xEnd = width + xExtraRight;
        Span<float> rowSigma = this.sigma.GetRow((yPos / JxlFrameDimensions.BlockDimensions) + JxlDecoderCache.SigmaPadding);

        float sm = this.loopFilter.EpfPass0SigmaScale * 1.65f;
        float bsm = sm * this.loopFilter.EpfBorderSadMul;

        Span<float> sadMulCenter = [bsm, sm, sm, sm, sm, sm, sm, bsm];
        Span<float> sadMulBorder = [bsm, bsm, bsm, bsm, bsm, bsm, bsm, bsm];

        int yPosModBlockDim = yPos % JxlFrameDimensions.BlockDimensions;
        Span<float> sadMul = yPosModBlockDim is 0 or JxlFrameDimensions.BlockDimensions - 1
            ? sadMulBorder
            : sadMulCenter;

        InlineArray3<InlineArray7<Memory<float>>> rows = default;
        for (int c = 0; c < 3; c++)
        {
            for (int i = 0; i < 7; i++)
            {
                rows[c][i] = this.GetInputRowMemory(inputRows, c, i - 3);
            }
        }

        for (int x = xStart; x < xEnd; x += Vector256<float>.Count)
        {
            int xPlusXpos = x + xPos;

            int bx = (xPlusXpos + (JxlDecoderCache.SigmaPadding * JxlFrameDimensions.BlockDimensions)) / JxlFrameDimensions.BlockDimensions;
            int ix = xPlusXpos % JxlFrameDimensions.BlockDimensions;

            if (rowSigma[bx] < JxlLoopFilter.MinimumSigma)
            {
                for (int c = 0; c < 3; c++)
                {
                    Vector256<float> px = Vector256.Create<float>(rows[c][3].Span[x..]);
                    px.CopyTo(GetOutputRow(outputRows, c, 0)[x..]);
                }

                continue;
            }

            Vector256<float> vsm = Vector256.Create<float>(sadMul[ix..]);
            Vector256<float> inverseSigma = Vector256.Create<float>(rowSigma[bx]) * vsm;

            sads.Clear();

            for (int c = 0; c < 3; c++)
            {
                Vector256<float> scale = Vector256.Create(this.loopFilter.EpfChannelScale[c]);

                for (int i = 0; i < 12; i++)
                {
                    Vector256<float> sad = Vector256<float>.Zero;

                    foreach (Span<int> offset in PlusOffsets)
                    {
                        Vector256<float> r11 = Vector256.Create((ReadOnlySpan<float>)rows[c][3 + offset[0]][(x + offset[1])..].Span);
                        Vector256<float> c11 = Vector256.Create((ReadOnlySpan<float>)rows[c][3 + SadOffsets[i][0] + offset[0]][(x + SadOffsets[i][1] + offset[1])..].Span);
                        sad += Vector256.Abs(r11 - c11);
                    }

                    sads[i] = (sad * scale) + sads[i];
                }
            }

            Vector256<float> xCC = Vector256.Create((ReadOnlySpan<float>)rows[0][3 + 0][x..].Span);
            Vector256<float> yCC = Vector256.Create((ReadOnlySpan<float>)rows[1][3 + 0][x..].Span);
            Vector256<float> bCC = Vector256.Create((ReadOnlySpan<float>)rows[2][3 + 0][x..].Span);

            Vector256<float> w = Vector256<float>.One;
            Vector256<float> X = xCC;
            Vector256<float> Y = yCC;
            Vector256<float> B = bCC;

            for (int i = 0; i < 12; i++)
            {
                AddPixel(SadOffsets[i][0], rows, x + SadOffsets[i][1], sads[i], inverseSigma, ref X, ref Y, ref B, ref w);
            }

            Vector256<float> inverseW = Vector256<float>.One / w;

            (X * inverseW).CopyTo(GetOutputRow(outputRows, 0, 0)[x..]);
            (Y * inverseW).CopyTo(GetOutputRow(outputRows, 1, 0)[x..]);
            (B * inverseW).CopyTo(GetOutputRow(outputRows, 2, 0)[x..]);
        }
    }

    /// <inheritdoc />
    public override RenderPipelineChannelMode GetChannelMode(int channel)
    {
        if (channel < 3)
        {
            return RenderPipelineChannelMode.InOut;
        }
        else
        {
            return RenderPipelineChannelMode.Ignored;
        }
    }
}
