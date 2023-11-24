// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Cicp;

/// <summary>
/// Represents a Cicp profile as per ITU-T H.273 / ISO/IEC 23091-2_2019 providing access to color space information
/// </summary>
public sealed class CicpProfile : IDeepCloneable<CicpProfile>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CicpProfile"/> class.
    /// </summary>
    public CicpProfile()
        : this(2, 2, 2, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CicpProfile"/> class.
    /// </summary>
    /// <param name="colorPrimaries">The color primaries as number according to ITU-T H.273 / ISO/IEC 23091-2_2019.</param>
    /// <param name="transferCharacteristics">The transfer characteristics as number according to ITU-T H.273 / ISO/IEC 23091-2_2019.</param>
    /// <param name="matrixCoefficients">The matrix coefficients as number according to ITU-T H.273 / ISO/IEC 23091-2_2019.</param>
    /// <param name="fullRange">The full range flag, or null if unknown.</param>
    public CicpProfile(byte colorPrimaries, byte transferCharacteristics, byte matrixCoefficients, bool? fullRange)
    {
        this.ColorPrimaries = Enum.IsDefined(typeof(CicpColorPrimaries), colorPrimaries) ? (CicpColorPrimaries)colorPrimaries : CicpColorPrimaries.Unspecified;
        this.TransferCharacteristics = Enum.IsDefined(typeof(CicpTransferCharacteristics), transferCharacteristics) ? (CicpTransferCharacteristics)transferCharacteristics : CicpTransferCharacteristics.Unspecified;
        this.MatrixCoefficients = Enum.IsDefined(typeof(CicpMatrixCoefficients), matrixCoefficients) ? (CicpMatrixCoefficients)matrixCoefficients : CicpMatrixCoefficients.Unspecified;
        this.FullRange = fullRange ?? (this.MatrixCoefficients == CicpMatrixCoefficients.Identity);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CicpProfile"/> class
    /// by making a copy from another CICP profile.
    /// </summary>
    /// <param name="other">The other CICP profile, where the clone should be made from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>>
    private CicpProfile(CicpProfile other)
    {
        Guard.NotNull(other, nameof(other));

        this.ColorPrimaries = other.ColorPrimaries;
        this.TransferCharacteristics = other.TransferCharacteristics;
        this.MatrixCoefficients = other.MatrixCoefficients;
        this.FullRange = other.FullRange;
    }

    /// <summary>
    /// Gets or sets the color primaries
    /// </summary>
    public CicpColorPrimaries ColorPrimaries { get; set; }

    /// <summary>
    /// Gets or sets the transfer characteristics
    /// </summary>
    public CicpTransferCharacteristics TransferCharacteristics { get; set; }

    /// <summary>
    /// Gets or sets the matrix coefficients
    /// </summary>
    public CicpMatrixCoefficients MatrixCoefficients { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the colors use the full numeric range
    /// </summary>
    public bool FullRange { get; set; }

    /// <inheritdoc/>
    public CicpProfile DeepClone() => new(this);
}
