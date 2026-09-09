// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Describes the optional color-primary and luminance limits of the image content represented by a HEIF image.
/// </summary>
public readonly struct HeifContentColorVolume : IEquatable<HeifContentColorVolume>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HeifContentColorVolume"/> struct.
    /// </summary>
    /// <param name="primaries">
    /// The CIE 1931 chromaticity coordinates of the content color primaries, or <see langword="null"/> when they
    /// are not specified.
    /// </param>
    /// <param name="minimumLuminance">
    /// The normalized minimum content luminance, or <see langword="null"/> when it is not specified.
    /// </param>
    /// <param name="maximumLuminance">
    /// The normalized maximum content luminance, or <see langword="null"/> when it is not specified.
    /// </param>
    /// <param name="averageLuminance">
    /// The normalized average content luminance, or <see langword="null"/> when it is not specified.
    /// </param>
    public HeifContentColorVolume(
        RgbPrimariesChromaticityCoordinates? primaries,
        double? minimumLuminance,
        double? maximumLuminance,
        double? averageLuminance)
    {
        this.Primaries = primaries;
        this.MinimumLuminance = minimumLuminance;
        this.MaximumLuminance = maximumLuminance;
        this.AverageLuminance = averageLuminance;
    }

    /// <summary>
    /// Gets the CIE 1931 chromaticity coordinates of the content color primaries, or <see langword="null"/> when
    /// they are not specified.
    /// </summary>
    public RgbPrimariesChromaticityCoordinates? Primaries { get; }

    /// <summary>
    /// Gets the normalized minimum content luminance, or <see langword="null"/> when it is not specified.
    /// </summary>
    /// <remarks>
    /// The value is interpreted according to the transfer characteristics signaled for the image and does not
    /// necessarily represent luminance in candelas per square metre.
    /// </remarks>
    public double? MinimumLuminance { get; }

    /// <summary>
    /// Gets the normalized maximum content luminance, or <see langword="null"/> when it is not specified.
    /// </summary>
    /// <remarks>
    /// The value is interpreted according to the transfer characteristics signaled for the image and does not
    /// necessarily represent luminance in candelas per square metre.
    /// </remarks>
    public double? MaximumLuminance { get; }

    /// <summary>
    /// Gets the normalized average content luminance, or <see langword="null"/> when it is not specified.
    /// </summary>
    /// <remarks>
    /// The value is interpreted according to the transfer characteristics signaled for the image and does not
    /// necessarily represent luminance in candelas per square metre.
    /// </remarks>
    public double? AverageLuminance { get; }

    /// <summary>
    /// Compares two content color volumes for equality.
    /// </summary>
    /// <param name="left">The first content color volume.</param>
    /// <param name="right">The second content color volume.</param>
    /// <returns><see langword="true"/> when every specified color-volume value is equal.</returns>
    public static bool operator ==(HeifContentColorVolume left, HeifContentColorVolume right)
        => left.Equals(right);

    /// <summary>
    /// Compares two content color volumes for inequality.
    /// </summary>
    /// <param name="left">The first content color volume.</param>
    /// <param name="right">The second content color volume.</param>
    /// <returns><see langword="true"/> when any specified color-volume value differs.</returns>
    public static bool operator !=(HeifContentColorVolume left, HeifContentColorVolume right)
        => !left.Equals(right);

    /// <summary>
    /// Determines whether the specified object is a content color volume with the same values.
    /// </summary>
    /// <param name="obj">The object to compare with this value.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> contains the same color-volume values.</returns>
    public override bool Equals(object? obj)
        => obj is HeifContentColorVolume other && this.Equals(other);

    /// <summary>
    /// Determines whether the specified content color volume has the same values as this value.
    /// </summary>
    /// <param name="other">The content color volume to compare with this value.</param>
    /// <returns><see langword="true"/> when every specified color-volume value is equal.</returns>
    public bool Equals(HeifContentColorVolume other)
        => this.Primaries.Equals(other.Primaries)
            && this.MinimumLuminance.Equals(other.MinimumLuminance)
            && this.MaximumLuminance.Equals(other.MaximumLuminance)
            && this.AverageLuminance.Equals(other.AverageLuminance);

    /// <summary>
    /// Returns a hash code for this content color volume.
    /// </summary>
    /// <returns>A hash code derived from the specified color-volume values.</returns>
    public override int GetHashCode()
        => HashCode.Combine(this.Primaries, this.MinimumLuminance, this.MaximumLuminance, this.AverageLuminance);
}
