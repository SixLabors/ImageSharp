// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Identifies the CICP matrix coefficients used to derive luma and chroma components.
/// </summary>
internal enum ObuMatrixCoefficients
{
    /// <summary>
    /// The identity matrix used for GBR component ordering.
    /// </summary>
    Identity = 0,

    /// <summary>
    /// ITU-R BT.709 coefficients.
    /// </summary>
    Bt709 = 1,

    /// <summary>
    /// Unspecified coefficients.
    /// </summary>
    Unspecified = 2,

    /// <summary>
    /// United States FCC 73.628 coefficients.
    /// </summary>
    Fcc = 4,

    /// <summary>
    /// ITU-R BT.470 System B and G coefficients.
    /// </summary>
    Bt470BG = 5,

    /// <summary>
    /// ITU-R BT.601 coefficients.
    /// </summary>
    Bt601 = 6,

    /// <summary>
    /// SMPTE 240M coefficients.
    /// </summary>
    Smpte240 = 7,

    /// <summary>
    /// SMPTE YCgCo coefficients.
    /// </summary>
    SmpteYCgCo = 8,

    /// <summary>
    /// ITU-R BT.2020 non-constant-luminance coefficients.
    /// </summary>
    Bt2020NonConstantLuminance = 9,

    /// <summary>
    /// ITU-R BT.2020 constant-luminance coefficients.
    /// </summary>
    Bt2020ConstantLuminance = 10,

    /// <summary>
    /// SMPTE ST 2085 YDzDx coefficients.
    /// </summary>
    Smpte2085 = 11,

    /// <summary>
    /// Chromaticity-derived non-constant-luminance coefficients.
    /// </summary>
    ChromaticityDerivedNonConstantLuminance = 12,

    /// <summary>
    /// Chromaticity-derived constant-luminance coefficients.
    /// </summary>
    ChromaticityDerivedConstantLuminance = 13,

    /// <summary>
    /// ITU-R BT.2100 ICtCp coefficients.
    /// </summary>
    Bt2100ICtCp = 14,
}
