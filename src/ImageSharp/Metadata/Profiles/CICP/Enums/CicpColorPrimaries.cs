// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.CICP;

#pragma warning disable CA1707 // Underscores in enum members

/// <summary>
/// Color primaries according to ITU-T H.273 / ISO/IEC 23091-2_2019 subclause 8.1
/// </summary>
public enum CicpColorPrimaries : byte
{
    /// <summary>
    /// Rec. ITU-R BT.709-6
    /// IEC 61966-2-1 sRGB or sYCC
    /// IEC 61966-2-4
    /// SMPTE RP 177 (1993) Annex B
    /// </summary>
    ItuRBt709_6 = 1,

    /// <summary>
    /// Image characteristics are unknown or are determined by the application.
    /// </summary>
    Unspecified = 2,

    /// <summary>
    /// Rec. ITU-R BT.470-6 System M (historical)
    /// </summary>
    ItuRBt470_6M = 4,

    /// <summary>
    /// Rec. ITU-R BT.601-7 625
    /// Rec. ITU-R BT.1700-0 625 PAL and 625 SECAM
    /// </summary>
    ItuRBt601_7_625 = 5,

    /// <summary>
    /// Rec. ITU-R BT.601-7 525
    /// Rec. ITU-R BT.1700-0 NTSC
    /// SMPTE ST 170 (2004)
    /// (functionally the same as the value 7)
    /// </summary>
    ItuRBt601_7_525 = 6,

    /// <summary>
    /// SMPTE ST 240 (1999)
    /// (functionally the same as the value 6)
    /// </summary>
    SmpteSt240 = 7,

    /// <summary>
    /// Generic film (colour filters using Illuminant C)
    /// </summary>
    GenericFilm = 8,

    /// <summary>
    /// Rec. ITU-R BT.2020-2
    /// Rec. ITU-R BT.2100-2
    /// </summary>
    ItuRBt2020_2 = 9,

    /// <summary>
    /// SMPTE ST 428-1 (2019)
    /// (CIE 1931 XYZ as in ISO 11664-1)
    /// </summary>
    SmpteSt428_1 = 10,

    /// <summary>
    /// SMPTE RP 431-2 (2011)
    /// DCI P3
    /// </summary>
    SmpteRp431_2 = 11,

    /// <summary>
    /// SMPTE ST 432-1 (2010)
    /// P3 D65 / Display P3
    /// </summary>
    SmpteEg432_1 = 12,

    /// <summary>
    /// EBU Tech.3213-E
    /// </summary>
    EbuTech3213E = 22,
}

#pragma warning restore CA1707 // Underscores in enum members
