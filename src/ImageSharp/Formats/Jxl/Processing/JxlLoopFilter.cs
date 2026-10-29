// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

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
    /// <see cref="InverseSigmaNum"/> / 3
    /// </summary>
    public const float MinimumSigma = -3.90524291751269967465540850526868f;

    /// <summary>
    /// Gets the number of EPF (Edge-preserving filter) sharp entries.
    /// </summary>
    public const int EpfSharpEntries = 8;

    /// <summary>
    /// Default Gaborish weights. Constant evaluation preserves the native defaults without doing double arithmetic while parsing.
    /// </summary>
    private const float DefaultGaborishWeight1 = (float)(1.1 * 0.104699568F);
    private const float DefaultGaborishWeight2 = (float)(1.1 * 0.055680538F);

    private bool allDefault;
    private bool useGaborishConvolution;
    private bool gaborishCustom;
    private float gaborishXWeight1;
    private float gaborishXWeight2;
    private float gaborishYWeight1;
    private float gaborishYWeight2;
    private float gaborishBWeight1;
    private float gaborishBWeight2;
    private int epfIterations;
    private bool epfSharpCustom;
    private bool epfCustomWeights;
    private float epfPass1ZeroFlush;
    private float epfPass2ZeroFlush;
    private bool epfCustomSigma;
    private float epfQuantMultiplier;
    private float epfPass0SigmaScale;
    private float epfPass2SigmaScale;
    private float epfBorderSadMul;
    private float epfSigmaForModular;
    private ulong extensions;

    /// <summary>
    /// Initializes the loop-filter fields to the bitstream defaults.
    /// </summary>
    public JxlLoopFilter() => JxlBundle.Init(this);

    /// <summary>
    /// Gets or sets whether the owning frame uses Modular encoding. This controls conditional EPF fields and is not serialized here.
    /// </summary>
    public bool IsModular { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether gaborish
    /// convolution is preferred.
    /// </summary>
    public bool UseGaborishConvolution
    {
        get => this.useGaborishConvolution;
        set => this.useGaborishConvolution = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether custom
    /// gaborish weights are used.
    /// </summary>
    public bool GaborishCustom
    {
        get => this.gaborishCustom;
        set => this.gaborishCustom = value;
    }

    /// <summary>
    /// Gets or sets the first custom X gaborish weight.
    /// </summary>
    public float GaborishXWeight1
    {
        get => this.gaborishXWeight1;
        set => this.gaborishXWeight1 = value;
    }

    /// <summary>
    /// Gets or sets the second custom X gaborish weight.
    /// </summary>
    public float GaborishXWeight2
    {
        get => this.gaborishXWeight2;
        set => this.gaborishXWeight2 = value;
    }

    /// <summary>
    /// Gets or sets the first custom Y gaborish weight.
    /// </summary>
    public float GaborishYWeight1
    {
        get => this.gaborishYWeight1;
        set => this.gaborishYWeight1 = value;
    }

    /// <summary>
    /// Gets or sets the second custom Y gaborish weight.
    /// </summary>
    public float GaborishYWeight2
    {
        get => this.gaborishYWeight2;
        set => this.gaborishYWeight2 = value;
    }

    /// <summary>
    /// Gets or sets the first custom B gaborish weight.
    /// </summary>
    public float GaborishBWeight1
    {
        get => this.gaborishBWeight1;
        set => this.gaborishBWeight1 = value;
    }

    /// <summary>
    /// Gets or sets the second custom B gaborish weight.
    /// </summary>
    public float GaborishBWeight2
    {
        get => this.gaborishBWeight2;
        set => this.gaborishBWeight2 = value;
    }

    /// <summary>
    /// Gets or sets the number of EPF (Edge-preserving filter) steps.
    /// 0 means EPF is disabled, 1 applies only the first stage,
    /// 2 applies both stages and 3 applies the first stage twice
    /// and the second stage once.
    /// </summary>
    public int EpfIterations
    {
        get => this.epfIterations;
        set => this.epfIterations = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether custom EPF sharpness
    /// is used.
    /// </summary>
    public bool EpfSharpCustom
    {
        get => this.epfSharpCustom;
        set => this.epfSharpCustom = value;
    }

    /// <summary>
    /// Gets or sets a value with 8 elements representing EPF sharpness lookup tables (LUTs).
    /// </summary>
    public float[] EpfSharpLookup { get; set; } = new float[8];

    /// <summary>
    /// Gets or sets a value indicating whether custom EPF weights are used.
    /// </summary>
    public bool EpfCustomWeights
    {
        get => this.epfCustomWeights;
        set => this.epfCustomWeights = value;
    }

    /// <summary>
    /// Gets or sets the relative weight of each channel.
    /// </summary>
    public float[] EpfChannelScale { get; set; } = new float[3];

    /// <summary>
    /// Gets or sets the value that represents the minimum weight for first pass.
    /// </summary>
    public float EpfPass1ZeroFlush
    {
        get => this.epfPass1ZeroFlush;
        set => this.epfPass1ZeroFlush = value;
    }

    /// <summary>
    /// Gets or sets the value that represents the minimum weight for second pass.
    /// </summary>
    public float EpfPass2ZeroFlush
    {
        get => this.epfPass2ZeroFlush;
        set => this.epfPass2ZeroFlush = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether custom sigma parameters are used for EPF.
    /// </summary>
    public bool EpfCustomSigma
    {
        get => this.epfCustomSigma;
        set => this.epfCustomSigma = value;
    }

    /// <summary>
    /// Gets or sets the quant multiplier.
    /// </summary>
    public float EpfQuantMultiplier
    {
        get => this.epfQuantMultiplier;
        set => this.epfQuantMultiplier = value;
    }

    /// <summary>
    /// Gets or sets the multiplier for sigma in pass 0.
    /// </summary>
    public float EpfPass0SigmaScale
    {
        get => this.epfPass0SigmaScale;
        set => this.epfPass0SigmaScale = value;
    }

    /// <summary>
    /// Gets or sets the multiplier for sigma in pass 2.
    /// </summary>
    public float EpfPass2SigmaScale
    {
        get => this.epfPass2SigmaScale;
        set => this.epfPass2SigmaScale = value;
    }

    /// <summary>
    /// Gets or sets the inverse multiplier for sigma on borders.
    /// </summary>
    public float EpfBorderSadMul
    {
        get => this.epfBorderSadMul;
        set => this.epfBorderSadMul = value;
    }

    /// <summary>
    /// Gets or sets the EPF sigma for modular.
    /// </summary>
    // NOTE: This value is not documented by libjxl.
    public float EpfSigmaForModular
    {
        get => this.epfSigmaForModular;
        set => this.epfSigmaForModular = value;
    }

    /// <summary>
    /// Gets or sets the number of extensions.
    /// </summary>
    public ulong Extensions
    {
        get => this.extensions;
        set => this.extensions = value;
    }

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

    public bool ComputeSigma(Rectangle blockRect, JxlPassesDecoderContext state)
    {
        if (this.EpfIterations <= 0)
        {
            return false;
        }

        JxlAcStrategyImage acStrategy = state.Shared.AcStrategy;
        float quantScale = state.Shared.Quantizer.Scale;

        int sigmaStride = state.Sigma!.PixelsPerRow;
        int sharpnessStride = state.Shared.EpfSharpness.PixelsPerRow;

        for (int by = 0; by < blockRect.Height; by++)
        {
            Span<float> sigmaRow = state.Sigma.GetRow(by);
            Span<byte> sharpnessRow = state.Shared.EpfSharpness.GetRow(by);
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

                if (by + blockRect.Y + acs.CoveredBlocksX == state.Shared.FrameDimensions.YSizeBlocks)
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

    /// <inheritdoc />
    public bool Visit(JxlVisitor visitor)
    {
        if (visitor.AllDefault(this, ref this.allDefault))
        {
            visitor.SetDefault(this);
            return true;
        }

        if (!visitor.Boolean(true, ref this.useGaborishConvolution))
        {
            return false;
        }

        // Each Gaborish kernel has a fixed center weight of one and four copies of each surrounding weight.
        // Reject a near-zero total before the renderer normalizes the kernel by that total.
        if (visitor.Conditional(this.useGaborishConvolution))
        {
            if (!visitor.Boolean(false, ref this.gaborishCustom))
            {
                return false;
            }

            if (visitor.Conditional(this.gaborishCustom))
            {
                if (!visitor.F16(DefaultGaborishWeight1, ref this.gaborishXWeight1) || !visitor.F16(DefaultGaborishWeight2, ref this.gaborishXWeight2))
                {
                    return false;
                }

                if (MathF.Abs(1F + ((this.gaborishXWeight1 + this.gaborishXWeight2) * 4F)) < 1E-8F)
                {
                    return false;
                }

                if (!visitor.F16(DefaultGaborishWeight1, ref this.gaborishYWeight1) || !visitor.F16(DefaultGaborishWeight2, ref this.gaborishYWeight2))
                {
                    return false;
                }

                if (MathF.Abs(1F + ((this.gaborishYWeight1 + this.gaborishYWeight2) * 4F)) < 1E-8F)
                {
                    return false;
                }

                if (!visitor.F16(DefaultGaborishWeight1, ref this.gaborishBWeight1) || !visitor.F16(DefaultGaborishWeight2, ref this.gaborishBWeight2))
                {
                    return false;
                }

                if (MathF.Abs(1F + ((this.gaborishBWeight1 + this.gaborishBWeight2) * 4F)) < 1E-8F)
                {
                    return false;
                }
            }
        }

        if (!visitor.Bits(2, 2, ref Unsafe.As<int, uint>(ref this.epfIterations)))
        {
            return false;
        }

        if (visitor.Conditional(this.epfIterations > 0))
        {
            // VarDCT derives sigma from quantization and the sharpness table. Modular supplies sigma directly below.
            if (visitor.Conditional(!this.IsModular))
            {
                if (!visitor.Boolean(false, ref this.epfSharpCustom))
                {
                    return false;
                }

                if (visitor.Conditional(this.epfSharpCustom))
                {
                    for (int i = 0; i < EpfSharpEntries; i++)
                    {
                        if (!visitor.F16((float)i / (EpfSharpEntries - 1), ref this.EpfSharpLookup[i]))
                        {
                            return false;
                        }
                    }
                }
            }

            if (!visitor.Boolean(false, ref this.epfCustomWeights))
            {
                return false;
            }

            if (visitor.Conditional(this.epfCustomWeights))
            {
                if (!visitor.F16(40F, ref this.EpfChannelScale[0])
                    || !visitor.F16(5F, ref this.EpfChannelScale[1])
                    || !visitor.F16(3.5F, ref this.EpfChannelScale[2])
                    || !visitor.F16(0.45F, ref this.epfPass1ZeroFlush)
                    || !visitor.F16(0.6F, ref this.epfPass2ZeroFlush))
                {
                    return false;
                }
            }

            if (!visitor.Boolean(false, ref this.epfCustomSigma))
            {
                return false;
            }

            if (visitor.Conditional(this.epfCustomSigma))
            {
                if (visitor.Conditional(!this.IsModular) && !visitor.F16(0.46F, ref this.epfQuantMultiplier))
                {
                    return false;
                }

                if (!visitor.F16(0.9F, ref this.epfPass0SigmaScale)
                    || !visitor.F16(6.5F, ref this.epfPass2SigmaScale)
                    || !visitor.F16(0.6666666666666666F, ref this.epfBorderSadMul))
                {
                    return false;
                }
            }

            if (visitor.Conditional(this.IsModular))
            {
                if (!visitor.F16(1F, ref this.epfSigmaForModular))
                {
                    return false;
                }

                // The Modular sigma is used as a divisor. Match the native minimum rather than accepting a zero or negative divisor.
                if (this.epfSigmaForModular < 1E-8F)
                {
                    return false;
                }
            }
        }

        if (!visitor.BeginExtensions(ref this.extensions))
        {
            return false;
        }

        return visitor.EndExtensions();
    }
}
