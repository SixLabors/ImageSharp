// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;

/// <summary>
/// Reconstructs AV1 transform coefficients from quantized coefficient levels.
/// </summary>
internal sealed class Av1InverseQuantizer
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
    private readonly Av1DeQuantizationContext deQuantsDeltaQ;

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
    /// <param name="superblockInfo">The superblock whose quantizer adjustment is applied.</param>
    public void UpdateDequant(Av1SuperblockInfo superblockInfo)
    {
        Av1BitDepth bitDepth = this.sequenceHeader.ColorConfig.BitDepth;
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
    /// Applies the active segment, plane, matrix, and transform scale to decoded coefficient magnitudes.
    /// </summary>
    public readonly ref struct TransformParameters
    {
        private readonly short dc;
        private readonly short ac;
        private readonly int minimum;
        private readonly int maximum;
        private readonly int shift;
        private readonly ReadOnlySpan<int> inverseMatrix;

        /// <summary>
        /// Initializes a new instance of the <see cref="TransformParameters"/> struct.
        /// </summary>
        /// <param name="quantizer">The active frame and superblock quantization values.</param>
        /// <param name="mode">The block mode selecting the segment.</param>
        /// <param name="transformType">The transform type selecting frequency weighting.</param>
        /// <param name="transformSize">The transform dimensions and coefficient scale.</param>
        /// <param name="plane">The color plane selecting DC, AC, and matrix values.</param>
        public TransformParameters(
            Av1InverseQuantizer quantizer,
            Av1BlockModeInfo mode,
            Av1TransformType transformType,
            Av1TransformSize transformSize,
            Av1Plane plane)
        {
            int bitCount = quantizer.sequenceHeader.ColorConfig.BitDepth.GetBitCount();
            this.minimum = -(1 << (7 + bitCount));
            this.maximum = (1 << (7 + bitCount)) - 1;
            this.dc = quantizer.deQuantsDeltaQ.GetDc(mode.SegmentId, plane);
            this.ac = quantizer.deQuantsDeltaQ.GetAc(mode.SegmentId, plane);
            this.shift = transformSize.GetScale();

            // Lossless segments and one-dimensional or identity transforms use the flat matrix. Matrix lookup
            // happens once per transform, before the entropy loop supplies its nonzero magnitudes and signs.
            int matrixLevel = quantizer.frameHeader.LosslessArray[mode.SegmentId] ||
                !quantizer.frameHeader.QuantizationParameters.IsUsingQMatrix ||
                transformType >= Av1TransformType.Identity
                ? Av1ScanOrderConstants.QuantizationMatrixLevelCount - 1
                : quantizer.frameHeader.SegmentationParameters.QMLevel[(int)plane][mode.SegmentId];

            this.inverseMatrix = Av1InverseQuantizationLookup.GetQuantizationMatrix(matrixLevel, plane, transformSize);
        }

        /// <summary>
        /// Dequantizes one coefficient magnitude and applies its sign and precision bounds.
        /// </summary>
        /// <param name="magnitude">The nonnegative coefficient magnitude masked to twenty bits.</param>
        /// <param name="coefficientIndex">The coefficient's raster position.</param>
        /// <param name="negative">Whether the decoded coefficient sign is negative.</param>
        /// <returns>The signed, scaled, and clipped transform coefficient.</returns>
        public int Dequantize(int magnitude, int coefficientIndex, bool negative)
        {
            int dequant = coefficientIndex == 0 ? this.dc : this.ac;

            // Matrix weights have five fractional bits. Round the weighted quantizer first, then retain the
            // normative 24-bit product before removing transform-size scaling. Sign and clipping follow the shift.
            const int bias = 1 << (Av1Constants.QuantizationMatrixElementBitCount - 1);
            dequant = ((this.inverseMatrix[coefficientIndex] * dequant) + bias) >> Av1Constants.QuantizationMatrixElementBitCount;
            int coefficient = (int)(((long)magnitude * dequant) & 0xffffff) >> this.shift;
            coefficient = negative ? -coefficient : coefficient;
            return Av1Math.Clamp(coefficient, this.minimum, this.maximum);
        }
    }
}
