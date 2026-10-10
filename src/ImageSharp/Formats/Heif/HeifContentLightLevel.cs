// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Describes the maximum content and picture-average light levels of a HEIF image.
/// </summary>
public readonly struct HeifContentLightLevel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HeifContentLightLevel"/> struct.
    /// </summary>
    /// <param name="maximumContentLightLevel">
    /// The maximum light level of any individual sample, in candelas per square metre, or zero when unspecified.
    /// </param>
    /// <param name="maximumPictureAverageLightLevel">
    /// The maximum average light level of any picture, in candelas per square metre, or zero when unspecified.
    /// </param>
    public HeifContentLightLevel(ushort maximumContentLightLevel, ushort maximumPictureAverageLightLevel)
    {
        this.MaximumContentLightLevel = maximumContentLightLevel;
        this.MaximumPictureAverageLightLevel = maximumPictureAverageLightLevel;
    }

    /// <summary>
    /// Gets the maximum light level of any individual sample, in candelas per square metre, or zero when unspecified.
    /// </summary>
    public ushort MaximumContentLightLevel { get; }

    /// <summary>
    /// Gets the maximum average light level of any picture, in candelas per square metre, or zero when unspecified.
    /// </summary>
    public ushort MaximumPictureAverageLightLevel { get; }
}
