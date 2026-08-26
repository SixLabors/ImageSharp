// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;

/// <summary>
/// Stores the AV1 DC and AC dequantization values for every segment and color plane in a frame.
/// </summary>
internal class Av1DeQuantizationContext
{
    /// <summary>
    /// The DC dequantization values indexed by segment and then plane.
    /// </summary>
    private readonly short[][] dcContent;

    /// <summary>
    /// The AC dequantization values indexed by segment and then plane.
    /// </summary>
    private readonly short[][] acContent;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1DeQuantizationContext"/> class from the frame's base quantizer,
    /// segment adjustments, plane deltas, and coded bit depth.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header that supplies the coded bit depth.</param>
    /// <param name="frameHeader">The frame header that supplies segmentation and quantization parameters.</param>
    /// <remarks>SVT-AV1: <c>svt_aom_setup_segmentation_dequant</c>.</remarks>
    public Av1DeQuantizationContext(ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        Av1BitDepth bitDepth = sequenceHeader.ColorConfig.BitDepth;
        this.dcContent = new short[Av1Constants.MaxSegmentCount][];
        this.acContent = new short[Av1Constants.MaxSegmentCount][];
        for (int segmentId = 0; segmentId < Av1Constants.MaxSegmentCount; segmentId++)
        {
            this.dcContent[segmentId] = new short[Av1Constants.MaxPlanes];
            this.acContent[segmentId] = new short[Av1Constants.MaxPlanes];
            int qindex = Av1QuantizationLookup.GetQIndex(frameHeader.SegmentationParameters, segmentId, frameHeader.QuantizationParameters.BaseQIndex);

            for (int plane = 0; plane < Av1Constants.MaxPlanes; plane++)
            {
                int dc_delta_q = frameHeader.QuantizationParameters.DeltaQDc[plane];
                int ac_delta_q = frameHeader.QuantizationParameters.DeltaQAc[plane];

                this.dcContent[segmentId][plane] = Av1QuantizationLookup.GetDcQuant(qindex, dc_delta_q, bitDepth);
                this.acContent[segmentId][plane] = Av1QuantizationLookup.GetAcQuant(qindex, ac_delta_q, bitDepth);
            }
        }
    }

    /// <summary>
    /// Gets the DC dequantization value for a segment and color plane.
    /// </summary>
    /// <param name="segmentId">The zero-based AV1 segment identifier.</param>
    /// <param name="plane">The color plane.</param>
    /// <returns>The DC dequantization value.</returns>
    public short GetDc(int segmentId, Av1Plane plane)
        => this.dcContent[segmentId][(int)plane];

    /// <summary>
    /// Gets the AC dequantization value for a segment and color plane.
    /// </summary>
    /// <param name="segmentId">The zero-based AV1 segment identifier.</param>
    /// <param name="plane">The color plane.</param>
    /// <returns>The AC dequantization value.</returns>
    public short GetAc(int segmentId, Av1Plane plane)
        => this.acContent[segmentId][(int)plane];

    /// <summary>
    /// Sets the AC dequantization value for a segment and color plane.
    /// </summary>
    /// <param name="segmentId">The zero-based AV1 segment identifier.</param>
    /// <param name="plane">The color plane.</param>
    /// <param name="value">The AC dequantization value.</param>
    public void SetAc(int segmentId, Av1Plane plane, short value)
        => this.acContent[segmentId][(int)plane] = value;

    /// <summary>
    /// Sets the DC dequantization value for a segment and color plane.
    /// </summary>
    /// <param name="segmentId">The zero-based AV1 segment identifier.</param>
    /// <param name="plane">The color plane.</param>
    /// <param name="value">The DC dequantization value.</param>
    public void SetDc(int segmentId, Av1Plane plane, short value)
        => this.dcContent[segmentId][(int)plane] = value;
}
