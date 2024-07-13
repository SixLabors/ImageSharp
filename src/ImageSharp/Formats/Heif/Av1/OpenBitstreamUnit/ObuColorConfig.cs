// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuColorConfig
{
    public bool IsColorDescriptionPresent { get; set; }

    /// <summary>
    /// Gets or sets the number of color channels in this image.
    /// </summary>
    public int PlaneCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the image has a single greyscale plane, will have
    /// <see cref="Av1Constants.MaxPlanes"/> color planes otherwise.
    /// </summary>
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
