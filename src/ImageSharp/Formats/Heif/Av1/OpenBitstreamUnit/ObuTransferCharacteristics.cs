// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Identifies the CICP transfer characteristics signaled by an AV1 sequence.
/// </summary>
internal enum ObuTransferCharacteristics
{
    /// <summary>
    /// ITU-R BT.709 transfer characteristics.
    /// </summary>
    Bt709 = 1,

    /// <summary>
    /// Unspecified transfer characteristics.
    /// </summary>
    Unspecified = 2,

    /// <summary>
    /// ITU-R BT.470 System M transfer characteristics.
    /// </summary>
    Bt470M = 4,

    /// <summary>
    /// ITU-R BT.470 System B and G transfer characteristics.
    /// </summary>
    Bt470BG = 5,

    /// <summary>
    /// ITU-R BT.601 transfer characteristics.
    /// </summary>
    Bt601 = 6,

    /// <summary>
    /// SMPTE 240M transfer characteristics.
    /// </summary>
    Smpte240 = 7,

    /// <summary>
    /// Linear light.
    /// </summary>
    Linear = 8,

    /// <summary>
    /// Logarithmic transfer with a 100:1 range.
    /// </summary>
    Log100 = 9,

    /// <summary>
    /// Logarithmic transfer with a 100 times square-root-of-ten to one range.
    /// </summary>
    Log100Sqrt10 = 10,

    /// <summary>
    /// IEC 61966-2-4 transfer characteristics.
    /// </summary>
    Iec61966 = 11,

    /// <summary>
    /// ITU-R BT.1361 transfer characteristics.
    /// </summary>
    Bt1361 = 12,

    /// <summary>
    /// IEC 61966-2-1 sRGB or sYCC transfer characteristics.
    /// </summary>
    Srgb = 13,

    /// <summary>
    /// ITU-R BT.2020 transfer characteristics for 10-bit systems.
    /// </summary>
    Bt202010Bit = 14,

    /// <summary>
    /// ITU-R BT.2020 transfer characteristics for 12-bit systems.
    /// </summary>
    Bt202012Bit = 15,

    /// <summary>
    /// SMPTE ST 2084 perceptual-quantizer transfer characteristics.
    /// </summary>
    Smpte2084 = 16,

    /// <summary>
    /// SMPTE ST 428 transfer characteristics.
    /// </summary>
    Smpte428 = 17,

    /// <summary>
    /// ITU-R BT.2100 hybrid-log-gamma transfer characteristics.
    /// </summary>
    Hlg = 18,
}
