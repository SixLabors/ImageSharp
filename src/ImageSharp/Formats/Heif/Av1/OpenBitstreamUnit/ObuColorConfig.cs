// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Contains the color configuration signaled by an AV1 sequence header.
/// </summary>
internal class ObuColorConfig
{
    /// <summary>
    /// Stores whether the sequence uses a single monochrome plane.
    /// </summary>
    private bool isMonochrome;

    /// <summary>
    /// Gets or sets a value indicating whether color-description syntax is present.
    /// </summary>
    public bool IsColorDescriptionPresent { get; set; }

    /// <summary>
    /// Gets the number of color channels in this image. Can have the value 1 or 3.
    /// </summary>
    public int PlaneCount { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether the image has a single greyscale plane, will have
    /// <see cref="Av1Constants.MaxPlanes"/> color planes otherwise.
    /// </summary>
    public bool IsMonochrome
    {
        get => this.isMonochrome;
        set
        {
            // Plane count is derived from the monochrome flag throughout the decoder, so update
            // both values atomically rather than allowing the two pieces of state to diverge.
            this.PlaneCount = value ? 1 : Av1Constants.MaxPlanes;
            this.isMonochrome = value;
        }
    }

    /// <summary>
    /// Gets or sets the color-primary chromaticities.
    /// </summary>
    public ObuColorPrimaries ColorPrimaries { get; set; }

    /// <summary>
    /// Gets or sets the transfer characteristics.
    /// </summary>
    public ObuTransferCharacteristics TransferCharacteristics { get; set; }

    /// <summary>
    /// Gets or sets the matrix coefficients used to derive luma and chroma components.
    /// </summary>
    public ObuMatrixCoefficients MatrixCoefficients { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether samples use the full numeric range.
    /// </summary>
    public bool ColorRange { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether chroma is subsampled horizontally.
    /// </summary>
    public bool SubSamplingX { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether chroma is subsampled vertically.
    /// </summary>
    public bool SubSamplingY { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the U and V planes use separate quantizer deltas.
    /// </summary>
    public bool HasSeparateUvDelta { get; set; }

    /// <summary>
    /// Gets or sets the chroma sample position for vertically subsampled images.
    /// </summary>
    public ObuChromoSamplePosition ChromaSamplePosition { get; set; }

    /// <summary>
    /// Gets or sets the encoded sample bit depth.
    /// </summary>
    public Av1BitDepth BitDepth { get; set; }

    /// <summary>
    /// Gets the color format represented by the monochrome and chroma-subsampling flags.
    /// </summary>
    /// <returns>The corresponding AV1 color format.</returns>
    public Av1ColorFormat GetColorFormat()
    {
        if (this.IsMonochrome)
        {
            // AV1 sets both subsampling flags for monochrome sequences even though no chroma planes exist. The
            // mono_chrome syntax therefore owns the plane layout and must take precedence over those derived flags.
            return Av1ColorFormat.Yuv400;
        }

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
