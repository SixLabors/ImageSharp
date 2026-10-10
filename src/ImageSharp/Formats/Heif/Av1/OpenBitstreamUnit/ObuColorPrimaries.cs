// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Identifies the CICP color-primary chromaticities signaled by an AV1 sequence.
/// </summary>
internal enum ObuColorPrimaries
{
    /// <summary>
    /// The reserved zero value.
    /// </summary>
    Reserved = 0,

    /// <summary>
    /// ITU-R BT.709 primaries.
    /// </summary>
    Bt709 = 1,

    /// <summary>
    /// Unspecified primaries.
    /// </summary>
    Unspecified = 2,

    /// <summary>
    /// ITU-R BT.470 System M primaries.
    /// </summary>
    Bt470M = 4,

    /// <summary>
    /// ITU-R BT.470 System B and G primaries.
    /// </summary>
    Bt470BG = 5,

    /// <summary>
    /// ITU-R BT.601 primaries.
    /// </summary>
    Bt601 = 6,

    /// <summary>
    /// SMPTE 240M primaries.
    /// </summary>
    Smpte240 = 7,

    /// <summary>
    /// Generic film primaries.
    /// </summary>
    GenericFilm = 8,

    /// <summary>
    /// ITU-R BT.2020 and BT.2100 primaries.
    /// </summary>
    Bt2020 = 9,

    /// <summary>
    /// SMPTE ST 428 CIE XYZ primaries.
    /// </summary>
    Xyz = 10,

    /// <summary>
    /// SMPTE RP 431-2 primaries.
    /// </summary>
    Smpte431 = 11,

    /// <summary>
    /// SMPTE EG 432-1 primaries.
    /// </summary>
    Smpte432 = 12,

    /// <summary>
    /// EBU Tech. 3213-E primaries.
    /// </summary>
    Ebu3213 = 22,
}
