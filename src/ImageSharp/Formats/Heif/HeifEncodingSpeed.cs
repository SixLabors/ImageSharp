// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Specifies the balance between encoding speed and compression efficiency for AV1 images.
/// Higher levels prioritize encoding speed over compression efficiency.
/// </summary>
public enum HeifEncodingSpeed
{
    /// <summary>
    /// The slowest encoding level and the default setting.
    /// </summary>
    Level0 = 0,

    /// <summary>
    /// Encoding speed level 1.
    /// </summary>
    Level1 = 1,

    /// <summary>
    /// Encoding speed level 2.
    /// </summary>
    Level2 = 2,

    /// <summary>
    /// Encoding speed level 3.
    /// </summary>
    Level3 = 3,

    /// <summary>
    /// Encoding speed level 4.
    /// </summary>
    Level4 = 4,

    /// <summary>
    /// Encoding speed level 5.
    /// </summary>
    Level5 = 5,

    /// <summary>
    /// Encoding speed level 6.
    /// </summary>
    Level6 = 6,

    /// <summary>
    /// Encoding speed level 7.
    /// </summary>
    Level7 = 7,

    /// <summary>
    /// Encoding speed level 8.
    /// </summary>
    Level8 = 8,

    /// <summary>
    /// The fastest encoding level.
    /// </summary>
    Level9 = 9
}
