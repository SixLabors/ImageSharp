// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Fields;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// JPEG XL loop filter.
/// </summary>
internal sealed class JxlLoopFilter : IJxlFields
{
    private const int SigmaBorder = 1;
    private const int SigmaPadding = 2;

    /// <summary>
    /// 4 * (sqrt(0.5)-1), so that Weight(sigma) = 0.5
    /// </summary>
    private const float InverseSigmaNum = -1.1715728752538099024f;

    /// <summary>
    /// kInvSigmaNum / 0.3
    /// </summary>
    private const float MinSigma = -3.90524291751269967465540850526868f;

    /// <summary>
    /// Gets the number of EPF (Edge-preserving filter) sharp entries.
    /// </summary>
    public const int EpfSharpEntries = 8;

    /// <summary>
    /// Gets or sets a value indicating whether gaborish
    /// convolution is preferred.
    /// </summary>
    public bool UseGaborishConvolution { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether custom
    /// gaborish weights are used.
    /// </summary>
    public bool GaborishCustom { get; set; }

    /// <summary>
    /// Gets or sets the first custom X gaborish weight.
    /// </summary>
    public float GaborishXWeight1 { get; set; }

    /// <summary>
    /// Gets or sets the second custom X gaborish weight.
    /// </summary>
    public float GaborishXWeight2 { get; set; }

    /// <summary>
    /// Gets or sets the first custom Y gaborish weight.
    /// </summary>
    public float GaborishYWeight1 { get; set; }

    /// <summary>
    /// Gets or sets the second custom Y gaborish weight.
    /// </summary>
    public float GaborishYWeight2 { get; set; }

    /// <summary>
    /// Gets or sets the first custom B gaborish weight.
    /// </summary>
    public float GaborishBWeight1 { get; set; }

    /// <summary>
    /// Gets or sets the second custom B gaborish weight.
    /// </summary>
    public float GaborishBWeight2 { get; set; }

    /// <summary>
    /// Gets or sets the number of EPF (Edge-preserving filter) steps.
    /// 0 means EPF is disabled, 1 applies only the first stage,
    /// 2 applies both stages and 3 applies the first stage twice
    /// and the second stage once.
    /// </summary>
    public int EpfIterations { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether custom EPF sharpness
    /// is used.
    /// </summary>
    public bool EpfSharpCustom { get; set; }

    /// <summary>
    /// Gets or sets a value with 8 elements representing EPF sharpness lookup tables (LUTs).
    /// </summary>
    public float[] EpfSharpLookup { get; set; } = new float[8];

    /// <summary>
    /// Gets or sets a value indicating whether custom EPF weights are used.
    /// </summary>
    public bool EpfCustomWeights { get; set; }

    /// <summary>
    /// Gets or sets the relative weight of each channel.
    /// </summary>
    public float[] EpfChannelScale { get; set; } = new float[3];

    /// <summary>
    /// Gets or sets the value that represents the minimum weight for first pass.
    /// </summary>
    public float EpfPass1ZeroFlush { get; set; }

    /// <summary>
    /// Gets or sets the value that represents the minimum weight for second pass.
    /// </summary>
    public float EpfPass2ZeroFlush { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether custom sigma parameters are used for EPF.
    /// </summary>
    public bool EpfCustomSigma { get; set; }

    /// <summary>
    /// Gets or sets the quant multiplier.
    /// </summary>
    public float EpfQuantMultiplier { get; set; }

    /// <summary>
    /// Gets or sets the multiplier for sigma in pass 0.
    /// </summary>
    public float EpfPass0SigmaScale { get; set; }

    /// <summary>
    /// Gets or sets the multiplier for sigma in pass 2.
    /// </summary>
    public float EpfPass2SigmaScale { get; set; }

    /// <summary>
    /// Gets or sets the inverse multiplier for sigma on borders.
    /// </summary>
    public float EpfBorderSadMul { get; set; }

    /// <summary>
    /// Gets or sets the EPF sigma for modular.
    /// </summary>
    // NOTE: This value is not documented by libjxl.
    public float EpfSigmaForModular { get; set; }

    /// <summary>
    /// Gets or sets the number of extensions.
    /// </summary>
    public long Extensions { get; set; }

    /// <summary>
    /// Mirror n floats starting at *span and store them before span.
    /// </summary>
    private static void LeftMirror(Span<float> span, int n)
    {
        ref float p = ref MemoryMarshal.GetReference(span);
        for (int i = 0; i < n; i++)
        {
            Unsafe.Add(ref p, -1 - i) = span[i];
        }
    }

    /// <summary>
    /// Mirror n floats starting at *(span - n) and store them at *span.
    /// </summary>
    private static void RightMirror(Span<float> span, int n)
    {
        ref float p = ref MemoryMarshal.GetReference(span);
        for (int i = 0; i < n; i++)
        {
            span[i] = Unsafe.Add(ref p, -1 - i);
        }
    }

    public bool ComputeSigma(Rectangle blockRect, JxlPassesDecoderState state)
    {
        if (this.EpfIterations <= 0)
        {
            return false;
        }

        JxlAcStrategyImage acStrategy = state.Shared.AcStrategy;
        float quantScale = state.Shared.Quantizer.Scale;

        int sigmaStride = state.Sigma.PixelsPerRow;
        int sharpnessStride = state.Shared.EpfSharpness.PixelsPerRow;

        for (int by = 0; by < blockRect.Height; by++)
        {
            Span<float> sigmaRow = state.Sigma.GetRowSpan(by);
            Span<byte> sharpnessRow = state.Shared.EpfSharpness.GetRowSpan(by);
            JxlAcStrategyRow acsRow = acStrategy.GetRow(in blockRect, by);
            Span<int> rowQuant = state.Shared.RawQuantField.GetRow(by);

            for (int bx = 0; bx < blockRect.Width; bx++)
            {
                JxlAcStrategy acs = acsRow[bx];
                int llfX = acs.CoveredBlocksX;

                if (!acs.IsFirstBlock)
                {
                    continue;
                }

                float sigmaQuant = this.EpfQuantMultiplier / (quantScale * rowQuant[bx] * InverseSigmaNum);

                for (int iy = 0; iy < acs.CoveredBlocksY; iy++)
                {
                    for (int ix = 0; ix < acs.CoveredBlocksY; ix++)
                    {
                        float sigma = sigmaQuant * this.EpfSharpLookup[sharpnessRow[bx + ix + iy + sharpnessStride]];
                        sigma = MathF.Min(-1e-4f, sigma);
                        sigmaRow[bx + ix + SigmaPadding + ((iy + SigmaPadding) * sigmaStride)] = 1.0f / sigma;
                    }
                }

                if (bx + blockRect.X == 0)
                {
                    for (int iy = 0; iy < acs.CoveredBlocksY; iy++)
                    {
                        LeftMirror(sigmaRow[(SigmaPadding + ((iy + SigmaPadding) * sigmaStride))..], SigmaBorder);
                    }
                }

                if (bx + blockRect.X + llfX == state.Shared.FrameDimensions.XSizeBlocks)
                {
                    for (int iy = 0; iy < acs.CoveredBlocksY; iy++)
                    {
                        RightMirror(sigmaRow[(SigmaPadding + bx + llfX + ((iy + SigmaPadding) * sigmaStride))..], SigmaBorder);
                    }
                }

                int offsetBefore = bx + blockRect.X == 0 ? 1 : bx + SigmaPadding;
                int offsetAfter = bx + blockRect.X + llfX == state.Shared.FrameDimensions.XSizeBlocks
                    ? SigmaPadding + llfX + bx + SigmaBorder
                    : SigmaPadding + llfX + bx;

                int num = offsetAfter - offsetBefore;

                if (by + blockRect.Y == 0)
                {
                    for (int iy = 0; iy < SigmaBorder; iy++)
                    {
                        sigmaRow.Slice(offsetBefore + ((SigmaPadding - 1 - iy) * sigmaStride), num)
                                .CopyTo(sigmaRow[(offsetBefore + ((SigmaPadding + iy) * sigmaStride))..]);
                    }
                }

                if (by + blockRect.Y + acs.CoveredBlocksX == state.Shared.FrameDimensions.YSizeBloks)
                {
                    for (int iy = 0; iy < SigmaBorder; iy++)
                    {
                        sigmaRow[(offsetBefore + (sigmaStride * (acs.CoveredBlocksX + SigmaPadding + iy)))..]
                                .CopyTo(sigmaRow[(offsetBefore + (sigmaStride * (acs.CoveredBlocksY + SigmaPadding - 1 - iy)))..]);
                    }
                }
            }
        }

        return true;
    }

    public bool Visit(JxlVisitor visitor) => throw new NotImplementedException();
}
