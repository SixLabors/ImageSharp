// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuColorConfig
{
    public bool IsColorDescriptionPresent { get; set; }

    public int ChannelCount { get; set; }

    public bool IsMonochrome { get; set; }

    public ObuColorPrimaries ColorPrimaries { get; set; }

    public ObuTransferCharacteristics TransferCharacteristics { get; set; }

    public ObuMatrixCoefficients MatrixCoefficients { get; set; }

    public bool ColorRange { get; set; }

    public bool SubSamplingX { get; set; }

    public bool SubSamplingY { get; set; }

    public bool HasSeparateUvDelta { get; set; }

    public ObuChromoSamplePosition ChromaSamplePosition { get; set; }

    public int BitDepth { get; set; }

    public Av1ColorFormat GetColorFormat()
    {
        Av1ColorFormat format = Av1ColorFormat.Yuv400;
        if (this.SubSamplingX && this.SubSamplingY)
        {
            format = Av1ColorFormat.Yuv420;
        }
        else if (this.SubSamplingX & !this.SubSamplingY)
        {
            format = Av1ColorFormat.Yuv422;
        }
        else if (!this.SubSamplingX && !this.SubSamplingY)
        {
            format = Av1ColorFormat.Yuv444;
        }

        return format;
    }
}
