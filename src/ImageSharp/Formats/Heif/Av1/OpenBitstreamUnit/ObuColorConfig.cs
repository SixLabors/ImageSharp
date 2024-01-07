// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuColorConfig
{
    internal bool IsColorDescriptionPresent { get; set; }

    internal int ChannelCount { get; set; }

    internal bool Monochrome { get; set; }

    internal ObuColorPrimaries ColorPrimaries { get; set; }

    internal ObuTransferCharacteristics TransferCharacteristics { get; set; }

    internal ObuMatrixCoefficients MatrixCoefficients { get; set; }

    internal bool ColorRange { get; set; }

    internal bool SubSamplingX { get; set; }

    internal bool SubSamplingY { get; set; }

    internal bool HasSeparateUvDelta { get; set; }

    internal ObuChromoSamplePosition ChromaSamplePosition { get; set; }

    internal int BitDepth { get; set; }

    internal bool HasSeparateUvDeltaQ { get; set; }

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
