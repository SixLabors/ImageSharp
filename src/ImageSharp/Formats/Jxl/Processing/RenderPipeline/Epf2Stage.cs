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
/// Edge Preserving Filter (type 2) stage
/// </summary>
internal sealed class Epf2Stage : RenderPipelineStageBase
{
    private readonly JxlLoopFilter loopFilter;
    private readonly JxlImageF sigma;

    public Epf2Stage(JxlLoopFilter loopFilter, JxlImageF sigma, Configuration configuration)
        : base(configuration)
    {
        this.loopFilter = loopFilter;
        this.sigma = sigma;
        this.Settings = RenderPipelineStageConfiguration.CreateSymmetricBorderOnly(2);
    }

    /// <inheritdoc />
    public override string Name => "EPF2";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> AbsoluteDifference(Vector256<float> x, Vector256<float> y) => Vector256.Abs(x - y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddPixel(
        int row,
        InlineArray3<InlineArray3<Memory<float>>> rows,
        int x,
        Vector256<float> rx,
        Vector256<float> ry,
        Vector256<float> rb,
        Vector256<float> inverseSigma,
        ref Vector256<float> X,
        ref Vector256<float> Y,
        ref Vector256<float> B,
        ref Vector256<float> w)
    {
        Vector256<float> cx = Vector256.Create((ReadOnlySpan<float>)rows[0][1 + row][x..].Span);
        Vector256<float> cy = Vector256.Create((ReadOnlySpan<float>)rows[1][1 + row][x..].Span);
        Vector256<float> cb = Vector256.Create((ReadOnlySpan<float>)rows[2][1 + row][x..].Span);

        Vector256<float> sad = AbsoluteDifference(cx, rx) * Vector256.Create(this.loopFilter.EpfChannelScale[0]);
        sad = (AbsoluteDifference(cy, ry) * Vector256.Create(this.loopFilter.EpfChannelScale[1])) + sad;
        sad = (AbsoluteDifference(cb, rb) * Vector256.Create(this.loopFilter.EpfChannelScale[2])) + sad;

        Vector256<float> weight = EpfUtils.Weight(sad, inverseSigma);
        w += weight;
        X = (weight * cx) + X;
        Y = (weight * cy) + Y;
        B = (weight * cb) + B;
    }

    public override void ProcessRow(Buffer2D<Memory<float>> inputRows, Buffer2D<Memory<float>> outputRows, int xExtraLeft, int xExtraRight, int width, int xPos, int yPos)
    {
        int xStart = -JxlMath.RoundUpTo(xExtraLeft, Vector256<float>.Count);
        int xEnd = width + xExtraRight;

        Span<float> rowSigma = this.sigma.GetRow((yPos / JxlFrameDimensions.BlockDimensions) + JxlDecoderCache.SigmaPadding);
        float sm = 1.65f;
        float bsm = sm * this.loopFilter.EpfBorderSadMul;

        Span<float> sadMulCenter = [bsm, sm, sm, sm, sm, sm, sm, bsm];
        Span<float> sadMulBorder = [bsm, bsm, bsm, bsm, bsm, bsm, bsm, bsm];

        InlineArray3<InlineArray3<Memory<float>>> rows = default;
        for (int c = 0; c < 3; c++)
        {
            for (int i = 0; i < 3; i++)
            {
                rows[c][i] = this.GetInputRowMemory(inputRows, c, i - 1);
            }
        }

        Span<float> sadMul = (yPos % JxlFrameDimensions.BlockDimensions is 0 or JxlFrameDimensions.BlockDimensions - 1)
            ? sadMulBorder
            : sadMulCenter;

        for (int x = xStart; x < xEnd; x += Vector256<float>.Count)
        {
            int bx = (x + xPos + (JxlDecoderCache.SigmaPadding * JxlFrameDimensions.BlockDimensions)) / JxlFrameDimensions.BlockDimensions;
            int ix = (x + xPos) % JxlFrameDimensions.BlockDimensions;

            if (rowSigma[bx] < JxlLoopFilter.MinimumSigma)
            {
                for (int c = 0; c < 3; c++)
                {
                    Vector256<float> px = Vector256.Create((ReadOnlySpan<float>)rows[c][1][x..].Span);
                    px.CopyTo(GetOutputRow(outputRows, c, 0)[x..]);
                }

                continue;
            }

            Vector256<float> vsm = Vector256.Create((ReadOnlySpan<float>)sadMul[ix..]);
            Vector256<float> inverseSigma = Vector256.Create(rowSigma[bx]) * vsm;

            Vector256<float> xCC = Vector256.Create((ReadOnlySpan<float>)rows[0][1 + 0][x..].Span);
            Vector256<float> yCC = Vector256.Create((ReadOnlySpan<float>)rows[1][1 + 0][x..].Span);
            Vector256<float> bCC = Vector256.Create((ReadOnlySpan<float>)rows[2][1 + 0][x..].Span);

            Vector256<float> w = Vector256<float>.One;
            Vector256<float> X = xCC;
            Vector256<float> Y = yCC;
            Vector256<float> B = bCC;

            // Top row
            this.AddPixel(-1, rows, x, xCC, yCC, bCC, inverseSigma, ref X, ref Y, ref B, ref w);

            // Center
            this.AddPixel(0, rows, x - 1, xCC, yCC, bCC, inverseSigma, ref X, ref Y, ref B, ref w);
            this.AddPixel(0, rows, x + 1, xCC, yCC, bCC, inverseSigma, ref X, ref Y, ref B, ref w);

            // Bottom
            this.AddPixel(1, rows, x, xCC, yCC, bCC, inverseSigma, ref X, ref Y, ref B, ref w);

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
