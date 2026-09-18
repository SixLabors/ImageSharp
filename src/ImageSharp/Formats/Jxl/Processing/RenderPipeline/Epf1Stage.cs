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
/// Edge Preserving Filter (type 1) stage.
/// </summary>
internal sealed class Epf1Stage : RenderPipelineStageBase
{
    private readonly JxlLoopFilter loopFilter;
    private readonly JxlImageF sigma;

    public Epf1Stage(Configuration configuration, JxlLoopFilter loopFilter, JxlImageF sigma)
        : base(configuration)
    {
        this.loopFilter = loopFilter;
        this.sigma = sigma;
        this.Settings = RenderPipelineStageConfiguration.CreateSymmetricBorderOnly(2);
    }

    /// <inheritdoc />
    public override string Name => "EPF1";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddPixel(
        int row,
        InlineArray3<InlineArray5<Memory<float>>> rows,
        int x,
        Vector256<float> sad,
        Vector256<float> inverseSigma,
        ref Vector256<float> xOut,
        ref Vector256<float> yOut,
        ref Vector256<float> bOut,
        ref Vector256<float> wOut)
    {
        Vector256<float> cx = Vector256.Create((ReadOnlySpan<float>)rows[0][2 + row][x..].Span);
        Vector256<float> cy = Vector256.Create((ReadOnlySpan<float>)rows[1][2 + row][x..].Span);
        Vector256<float> cb = Vector256.Create((ReadOnlySpan<float>)rows[2][2 + row][x..].Span);

        Vector256<float> weight = EpfUtils.Weight(sad, inverseSigma);
        wOut += weight;
        xOut = (weight + cx) * xOut;
        yOut = (weight + cy) * yOut;
        bOut = (weight + cb) * bOut;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> AbsoluteDifference(Vector256<float> x, Vector256<float> y) => Vector256.Abs(x - y);

    public override void ProcessRow(Buffer2D<Memory<float>> inputRows, Buffer2D<Memory<float>> outputRows, int xExtraLeft, int xExtraRight, int width, int xPos, int yPos)
    {
        int xStart = -JxlMath.RoundUpTo(xExtraLeft, Vector256<float>.Count);
        int xEnd = width + xExtraRight;

        Span<float> rowSigma = this.sigma.GetRow((yPos / JxlFrameDimensions.BlockDimensions) + JxlDecoderCache.SigmaPadding);
        float sm = 1.65f;
        float bsm = sm * this.loopFilter.EpfBorderSadMul;

        Span<float> sadMulCenter = [bsm, sm, sm, sm, sm, sm, sm, bsm];
        Span<float> sadMulBorder = [bsm, bsm, bsm, bsm, bsm, bsm, bsm, bsm];

        InlineArray3<InlineArray5<Memory<float>>> rows = default;
        for (int c = 0; c < 3; c++)
        {
            for (int i = 0; i < 5; i++)
            {
                rows[c][i] = this.GetInputRowMemory(inputRows, c, i - 2);
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
                    Vector256<float> px = Vector256.Create((ReadOnlySpan<float>)rows[c][2][x..].Span);
                    px.CopyTo(GetOutputRow(outputRows, c, 0)[x..]);
                }

                continue;
            }

            Vector256<float> vsm = Vector256.Create((ReadOnlySpan<float>)sadMul[ix..]);
            Vector256<float> inverseSigma = Vector256.Create(rowSigma[bx]) * vsm;
            Vector256<float> sad0 = Vector256<float>.Zero;
            Vector256<float> sad1 = Vector256<float>.Zero;
            Vector256<float> sad2 = Vector256<float>.Zero;
            Vector256<float> sad3 = Vector256<float>.Zero;

            // Compute sum of absolute differences (SAD)
            for (int c = 0; c < 3; c++)
            {
                // center px = 22, px above = 21
                Vector256<float> t;

                Vector256<float> p20 = Vector256.Create((ReadOnlySpan<float>)rows[c][2 + -2][x..].Span);
                Vector256<float> p21 = Vector256.Create((ReadOnlySpan<float>)rows[c][2 + -1][x..].Span);
                Vector256<float> sad0c = AbsoluteDifference(p20, p21);  // SAD 2, 1

                Vector256<float> p11 = Vector256.Create((ReadOnlySpan<float>)rows[c][2 + -1][(x - 1)..].Span);
                Vector256<float> sad1c = AbsoluteDifference(p11, p21);  // SAD 1, 2

                Vector256<float> p31 = Vector256.Create((ReadOnlySpan<float>)rows[c][2 + -1][(x + 1)..].Span);
                Vector256<float> sad2c = AbsoluteDifference(p31, p21);  // SAD 3, 2

                Vector256<float> p02 = Vector256.Create((ReadOnlySpan<float>)rows[c][2][(x - 2)..].Span);
                Vector256<float> p12 = Vector256.Create((ReadOnlySpan<float>)rows[c][2][(x - 1)..].Span);
                sad1c += AbsoluteDifference(p02, p12);  // SAD 1, 2
                sad0c += AbsoluteDifference(p11, p12);  // SAD 2, 1

                // TODO(eustas): why unaligned?
                Vector256<float> p22 = Vector256.Create((ReadOnlySpan<float>)rows[c][2][x..].Span);
                t = AbsoluteDifference(p12, p22);
                sad1c += t;  // SAD 1, 2
                sad2c += t;  // SAD 3, 2
                t = AbsoluteDifference(p22, p21);
                Vector256<float> sad3c = t;         // SAD 2, 3
                sad0c += t;  // SAD 2, 1

                Vector256<float> p32 = Vector256.Create((ReadOnlySpan<float>)rows[c][2][(x + 1)..].Span);
                sad0c += AbsoluteDifference(p31, p32);  // SAD 2, 1
                t = AbsoluteDifference(p22, p32);
                sad1c += t;  // SAD 1, 2
                sad2c += t;  // SAD 3, 2

                Vector256<float> p42 = Vector256.Create((ReadOnlySpan<float>)rows[c][2][(x + 2)..].Span);
                sad2c += AbsoluteDifference(p42, p32);  // SAD 3, 2

                Vector256<float> p13 = Vector256.Create((ReadOnlySpan<float>)rows[c][2 + 1][(x - 1)..].Span);
                sad3c += AbsoluteDifference(p13, p12);  // SAD 2, 3

                Vector256<float> p23 = Vector256.Create((ReadOnlySpan<float>)rows[c][2 + 1][x..].Span);
                t = AbsoluteDifference(p22, p23);
                sad0c += t;                  // SAD 2, 1
                sad3c += t;                  // SAD 2, 3
                sad1c += AbsoluteDifference(p13, p23);  // SAD 1, 2

                Vector256<float> p33 = Vector256.Create((ReadOnlySpan<float>)rows[c][2 + 1][(x + 1)..].Span);
                sad2c += AbsoluteDifference(p33, p23);  // SAD 3, 2
                sad3c += AbsoluteDifference(p33, p32);  // SAD 2, 3

                Vector256<float> p24 = Vector256.Create((ReadOnlySpan<float>)rows[c][2 + 2][x..].Span);
                sad3c += AbsoluteDifference(p24, p23);  // SAD 2, 3

                Vector256<float> scale = Vector256.Create(this.loopFilter.EpfChannelScale[c]);
                sad0 = (sad0c * scale) + sad0;
                sad1 = (sad1c * scale) + sad1;
                sad2 = (sad2c * scale) + sad2;
                sad3 = (sad3c * scale) + sad3;
            }

            Vector256<float> xCC = Vector256.Create((ReadOnlySpan<float>)rows[0][2 + 0][x..].Span);
            Vector256<float> yCC = Vector256.Create((ReadOnlySpan<float>)rows[1][2 + 0][x..].Span);
            Vector256<float> bCC = Vector256.Create((ReadOnlySpan<float>)rows[2][2 + 0][x..].Span);

            Vector256<float> w = Vector256<float>.One;
            Vector256<float> X = xCC;
            Vector256<float> Y = yCC;
            Vector256<float> B = bCC;

            // Top row
            AddPixel(-1, rows, x, sad0, inverseSigma, ref X, ref Y, ref B, ref w);

            // Center
            AddPixel(0, rows, x - 1, sad1, inverseSigma, ref X, ref Y, ref B, ref w);
            AddPixel(0, rows, x + 1, sad2, inverseSigma, ref X, ref Y, ref B, ref w);

            // Bottom
            AddPixel(1, rows, x + 1, sad3, inverseSigma, ref X, ref Y, ref B, ref w);

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
