// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;

/// <summary>
/// Reconstructs AV1 transform coefficients from quantized coefficient levels.
/// </summary>
internal class Av1InverseQuantizer
{
    /// <summary>
    /// The sequence-level color configuration that determines coefficient precision.
    /// </summary>
    private readonly ObuSequenceHeader sequenceHeader;

    /// <summary>
    /// The frame-level segmentation and quantization configuration.
    /// </summary>
    private readonly ObuFrameHeader frameHeader;

    /// <summary>
    /// The current per-segment, per-plane dequantization values, including any superblock delta-Q update.
    /// </summary>
    private Av1DeQuantizationContext deQuantsDeltaQ;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1InverseQuantizer"/> class.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header that supplies coded bit depth and color configuration.</param>
    /// <param name="frameHeader">The frame header that supplies segmentation and quantization parameters.</param>
    public Av1InverseQuantizer(ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        this.sequenceHeader = sequenceHeader;
        this.frameHeader = frameHeader;
        this.deQuantsDeltaQ = new(sequenceHeader, frameHeader);
    }

    /// <summary>
    /// Updates the active dequantization context for a superblock, applying its delta-Q value when signaled.
    /// </summary>
    /// <param name="deQuants">The frame dequantization context to update and retain.</param>
    /// <param name="superblockInfo">The superblock whose quantizer adjustment is applied.</param>
    public void UpdateDequant(Av1DeQuantizationContext deQuants, Av1SuperblockInfo superblockInfo)
    {
        Av1BitDepth bitDepth = this.sequenceHeader.ColorConfig.BitDepth;
        Guard.NotNull(deQuants, nameof(deQuants));
        this.deQuantsDeltaQ = deQuants;
        if (this.frameHeader.DeltaQParameters.IsPresent)
        {
            for (int i = 0; i < Av1Constants.MaxSegmentCount; i++)
            {
                int currentQIndex = Av1QuantizationLookup.GetQIndex(
                    this.frameHeader.SegmentationParameters,
                    i,
                    superblockInfo.SuperblockQuantizerIndex);

                for (Av1Plane plane = 0; (int)plane < Av1Constants.MaxPlanes; plane++)
                {
                    int dcDeltaQ = this.frameHeader.QuantizationParameters.DeltaQDc[(int)plane];
                    int acDeltaQ = this.frameHeader.QuantizationParameters.DeltaQAc[(int)plane];

                    this.deQuantsDeltaQ.SetDc(i, plane, Av1QuantizationLookup.GetDcQuant(currentQIndex, dcDeltaQ, bitDepth));
                    this.deQuantsDeltaQ.SetAc(i, plane, Av1QuantizationLookup.GetAcQuant(currentQIndex, acDeltaQ, bitDepth));
                }
            }
        }
    }

    /// <summary>
    /// Converts scan-ordered quantized levels into clamped, raster-ordered transform coefficients.
    /// </summary>
    /// <param name="mode">The block mode information containing the active segment identifier.</param>
    /// <param name="level">The packed coefficient buffer: the first element is the coefficient count and the remaining elements are scan-ordered levels.</param>
    /// <param name="qCoefficients">The destination for raster-ordered dequantized coefficients.</param>
    /// <param name="transformType">The transform type that selects the coefficient scan and matrix class.</param>
    /// <param name="transformSize">The transform dimensions and scale.</param>
    /// <param name="plane">The color plane whose quantizer and matrix are used.</param>
    /// <returns>The number of coefficient levels consumed.</returns>
    /// <remarks>SVT-AV1: <c>svt_aom_inverse_quantize</c>.</remarks>
    public int InverseQuantize(Av1BlockModeInfo mode, Span<int> level, Span<int> qCoefficients, Av1TransformType transformType, Av1TransformSize transformSize, Av1Plane plane)
    {
        Guard.NotNull(this.deQuantsDeltaQ);
        Av1ScanOrder scanOrder = Av1ScanOrderConstants.GetScanOrder(transformSize, transformType);
        ReadOnlySpan<short> scanIndices = scanOrder.Scan;

        // AV1 bounds reconstructed coefficients to a signed range with seven headroom bits beyond pixel precision.
        int maxValue = (1 << (7 + this.sequenceHeader.ColorConfig.BitDepth.GetBitCount())) - 1;
        int minValue = -(1 << (7 + this.sequenceHeader.ColorConfig.BitDepth.GetBitCount()));
        bool usingQuantizationMatrix = this.frameHeader.QuantizationParameters.IsUsingQMatrix;
        bool lossless = this.frameHeader.LosslessArray[mode.SegmentId];
        short dequantDc = this.deQuantsDeltaQ.GetDc(mode.SegmentId, plane);
        short dequantAc = this.deQuantsDeltaQ.GetAc(mode.SegmentId, plane);

        // The final matrix level is flat. Lossless blocks and frames without matrices select it globally. AV1 also
        // requires identity and one-dimensional transform types, which occupy the enum range from Identity onward,
        // to bypass frequency weighting even when the frame signals quantization matrices.
        int qmLevel = lossless || !usingQuantizationMatrix
            ? Av1ScanOrderConstants.QuantizationMatrixLevelCount - 1
            : this.frameHeader.SegmentationParameters.QMLevel[(int)plane][mode.SegmentId];

        ReadOnlySpan<int> iqMatrix = transformType < Av1TransformType.Identity
            ? Av1InverseQuantizationLookup.GetQuantizationMatrix(qmLevel, plane, transformSize)
            : Av1InverseQuantizationLookup.GetQuantizationMatrix(Av1Constants.QuantificationMatrixLevelCount - 1, Av1Plane.Y, transformSize);

        int shift = transformSize.GetScale();

        // Entropy decoding stores the populated coefficient count in the leading slot and the levels after it.
        int coefficientCount = level[0];
        level = level[1..];
        int lev = level[0];
        int qCoefficient;
        if (lev != 0)
        {
            int pos = scanIndices[0];

            // Preserve the AV1 24-bit dequantization intermediate before removing transform-size scaling.
            qCoefficient = (int)(((long)Math.Abs(lev) * GetDeQuantizedValue(dequantDc, pos, iqMatrix)) & 0xffffff);
            qCoefficient >>= shift;

            if (lev < 0)
            {
                qCoefficient = -qCoefficient;
            }

            qCoefficients[0] = Av1Math.Clamp(qCoefficient, minValue, maxValue);
        }

        for (int i = 1; i < coefficientCount; i++)
        {
            lev = level[i];
            if (lev != 0)
            {
                int pos = scanIndices[i];

                // AC levels arrive in entropy scan order but the inverse transform consumes raster positions.
                qCoefficient = (int)(((long)Math.Abs(lev) * GetDeQuantizedValue(dequantAc, pos, iqMatrix)) & 0xffffff);
                qCoefficient >>= shift;

                if (lev < 0)
                {
                    qCoefficient = -qCoefficient;
                }

                qCoefficients[pos] = Av1Math.Clamp(qCoefficient, minValue, maxValue);
            }
        }

        return coefficientCount;
    }

    /// <summary>
    /// Applies an inverse quantization-matrix weight to a plane dequantization value.
    /// </summary>
    /// <param name="dequant">The unweighted DC or AC dequantization value.</param>
    /// <param name="coefficientIndex">The raster coefficient index into the inverse matrix.</param>
    /// <param name="iqMatrix">The inverse quantization matrix for the current level, plane, and transform size.</param>
    /// <returns>The matrix-weighted dequantization value.</returns>
    /// <remarks>SVT-AV1: <c>get_dqv</c>.</remarks>
    private static int GetDeQuantizedValue(short dequant, int coefficientIndex, ReadOnlySpan<int> iqMatrix)
    {
        // Matrix elements use fixed-point precision; adding half a unit produces nearest-integer rounding on shift.
        const int bias = 1 << (Av1Constants.QuantizationMatrixElementBitCount - 1);
        int deQuantifiedValue = dequant;

        deQuantifiedValue = ((iqMatrix[coefficientIndex] * deQuantifiedValue) + bias) >> Av1Constants.QuantizationMatrixElementBitCount;
        return deQuantifiedValue;
    }
}
