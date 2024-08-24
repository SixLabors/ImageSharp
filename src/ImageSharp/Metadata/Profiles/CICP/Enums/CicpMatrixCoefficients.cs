// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Cicp;

#pragma warning disable CA1707 // Underscores in enum members

/// <summary>
/// Matrix coefficients according to ITU-T H.273 / ISO/IEC 23091-2_2019 subclause 8.3
/// </summary>
public enum CicpMatrixCoefficients : byte
{
    /// <summary>
    /// The identity matrix.
    /// IEC 61966-2-1 sRGB
    /// SMPTE ST 428-1 (2019)
    /// </summary>
    Identity = 0,

    /// <summary>
    /// Rec. ITU-R BT.709-6
    /// IEC 61966-2-4 xvYCC709
    /// SMPTE RP 177 (1993) Annex B
    /// </summary>
    ItuRBt709_6 = 1,

    /// <summary>
    /// Image characteristics are unknown or are determined by the application.
    /// </summary>
    Unspecified = 2,

    /// <summary>
    /// FCC Title 47 Code of Federal Regulations 73.682 (a) (20)
    /// </summary>
    Fcc47 = 4,

    /// <summary>
    /// Rec. ITU-R BT.601-7 625
    /// Rec. ITU-R BT.1700-0 625 PAL and 625 SECAM
    /// IEC 61966-2-1 sYCC
    /// IEC 61966-2-4 xvYCC601
    /// (functionally the same as the value 6)
    /// </summary>
    ItuRBt601_7_625 = 5,

    /// <summary>
    /// Rec. ITU-R BT.601-7 525
    /// Rec. ITU-R BT.1700-0 NTSC
    /// SMPTE ST 170 (2004)
    /// (functionally the same as the value 5)
    /// </summary>
    ItuRBt601_7_525 = 6,

    /// <summary>
    /// SMPTE ST 240 (1999)
    /// </summary>
    SmpteSt240 = 7,

    /// <summary>
    /// YCgCo
    /// </summary>
    YCgCo = 8,

    /// <summary>
    /// Rec. ITU-R BT.2020-2 (non-constant luminance)
    /// Rec. ITU-R BT.2100-2 Y′CbCr
    /// </summary>
    ItuRBt2020_2_Ncl = 9,

    /// <summary>
    /// Rec. ITU-R BT.2020-2 (constant luminance)
    /// </summary>
    ItuRBt2020_2_Cl = 10,

    /// <summary>
    /// SMPTE ST 2085 (2015)
    /// </summary>
    SmpteSt2085 = 11,

    /// <summary>
    /// Chromaticity-derived non-constant luminance system
    /// </summary>
    ChromaDerivedNcl = 12,

    /// <summary>
    /// Chromaticity-derived constant luminance system
    /// </summary>
    ChromaDerivedCl = 13,

    /// <summary>
    /// Rec. ITU-R BT.2100-2 ICtCp
    /// </summary>
    ICtCp = 14,
}

#pragma warning restore CA1707 // Underscores in enum members
