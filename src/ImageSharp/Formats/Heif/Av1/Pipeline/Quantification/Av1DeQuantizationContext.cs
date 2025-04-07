// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantification;

internal class Av1DeQuantizationContext
{
    private readonly short[][] dcContent;
    private readonly short[][] acContent;

    /// <remarks>
    /// SVT: svt_aom_setup_segmentation_dequant
    /// </remarks>
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

    public short GetDc(int segmentId, Av1Plane plane)
        => this.dcContent[segmentId][(int)plane];

    public short GetAc(int segmentId, Av1Plane plane)
        => this.acContent[segmentId][(int)plane];

    public void SetAc(int segmentId, Av1Plane plane, short value)
        => this.dcContent[segmentId][(int)plane] = value;

    public void SetDc(int segmentId, Av1Plane plane, short value)
        => this.dcContent[segmentId][(int)plane] = value;
}
