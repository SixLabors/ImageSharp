// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantification;

internal class Av1DeQuantizationContext
{
    private readonly short[][] dcContent;
    private readonly short[][] acContent;

    public Av1DeQuantizationContext()
    {
        this.dcContent = new short[Av1Constants.MaxSegmentCount][];
        this.acContent = new short[Av1Constants.MaxSegmentCount][];
        for (int segmentId = 0; segmentId < Av1Constants.MaxSegmentCount; segmentId++)
        {
            this.dcContent[segmentId] = new short[Av1Constants.MaxPlanes];
            this.acContent[segmentId] = new short[Av1Constants.MaxPlanes];
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
