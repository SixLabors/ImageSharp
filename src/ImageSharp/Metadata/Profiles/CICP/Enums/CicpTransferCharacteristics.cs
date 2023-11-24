// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Cicp;

#pragma warning disable CA1707 // Underscores in enum values

/// <summary>
/// Transfer characteristics according to ITU-T H.273 / ISO/IEC 23091-2_2019 subclause 8.2
/// /// </summary>
public enum CicpTransferCharacteristics : byte
{
    /// <summary>
    /// Rec. ITU-R BT.709-6
    /// (functionally the same as the values 6, 14 and 15)
    /// </summary>
    ItuRBt709_6 = 1,

    /// <summary>
    /// Image characteristics are unknown or are determined by the application.
    /// </summary>
    Unspecified = 2,

    /// <summary>
    /// Assumed display gamma 2.2
    /// Rec. ITU-R BT.1700-0 625 PAL and 625 SECAM
    /// </summary>
    Gamma2_2 = 4,

    /// <summary>
    /// Assumed display gamma 2.8
    /// Rec. ITU-R BT.470-6 System B, G (historical)
    /// </summary>
    Gamma2_8 = 5,

    /// <summary>
    /// Rec. ITU-R BT.601-7 525 or 625
    /// Rec. ITU-R BT.1700-0 NTSC
    /// SMPTE ST 170 (2004)
    /// (functionally the same as the values 1, 14 and 15)
    /// </summary>
    ItuRBt601_7 = 6,

    /// <summary>
    /// SMPTE ST 240 (1999)
    /// </summary>
    SmpteSt240 = 7,

    /// <summary>
    /// Linear transfer characteristics
    /// </summary>
    Linear = 8,

    /// <summary>
    /// Logarithmic transfer characteristic (100:1 range)
    /// </summary>
    Log100 = 9,

    /// <summary>
    /// Logarithmic transfer characteristic (100 * Sqrt( 10 ) : 1 range)
    /// </summary>
    Log100Sqrt = 10,

    /// <summary>
    /// IEC 61966-2-4
    /// </summary>
    Iec61966_2_4 = 11,

    /// <summary>
    /// Rec. ITU-R BT.1361-0 extended colour gamut system (historical)
    /// </summary>
    ItuRBt1361_0 = 12,

    /// <summary>
    /// IEC 61966-2-1 sRGB or sYCC / Display P3
    /// </summary>
    Iec61966_2_1 = 13,

    /// <summary>
    /// Rec. ITU-R BT.2020-2 (10-bit system)
    /// (functionally the same as the values 1, 6 and 15)
    /// </summary>
    ItuRBt2020_2_10bit = 14,

    /// <summary>
    /// Rec. ITU-R BT.2020-2 (12-bit system)
    /// (functionally the same as the values 1, 6 and 14)
    /// /// </summary>
    ItuRBt2020_2_12bit = 15,

    /// <summary>
    /// SMPTE ST 2084 (2014) for 10-, 12-, 14- and 16-bit systems
    /// Rec. ITU-R BT.2100-2 perceptual quantization (PQ) system
    /// </summary>
    SmpteSt2084 = 16,

    /// <summary>
    /// SMPTE ST 428-1 (2019)
    /// </summary>
    SmpteSt428_1 = 17,

    /// <summary>
    /// ARIB STD-B67 (2015)
    /// Rec. ITU-R BT.2100-2 hybrid log-gamma (HLG) system
    /// </summary>
    AribStdB67 = 18,
}

#pragma warning restore CA1707 // Underscores in enum members
