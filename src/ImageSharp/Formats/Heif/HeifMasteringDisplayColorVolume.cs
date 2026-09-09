// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Describes the color volume of the display used to master a HEIF image.
/// </summary>
public readonly struct HeifMasteringDisplayColorVolume : IEquatable<HeifMasteringDisplayColorVolume>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HeifMasteringDisplayColorVolume"/> struct.
    /// </summary>
    /// <param name="primaries">The CIE 1931 chromaticity coordinates of the mastering display primaries.</param>
    /// <param name="whitePoint">The CIE 1931 chromaticity coordinates of the mastering display white point.</param>
    /// <param name="maximumLuminance">The nominal maximum mastering-display luminance in candelas per square metre.</param>
    /// <param name="minimumLuminance">The nominal minimum mastering-display luminance in candelas per square metre.</param>
    public HeifMasteringDisplayColorVolume(
        RgbPrimariesChromaticityCoordinates primaries,
        CieXyChromaticityCoordinates whitePoint,
        double maximumLuminance,
        double minimumLuminance)
    {
        this.Primaries = primaries;
        this.WhitePoint = whitePoint;
        this.MaximumLuminance = maximumLuminance;
        this.MinimumLuminance = minimumLuminance;
    }

    /// <summary>
    /// Gets the CIE 1931 chromaticity coordinates of the mastering display primaries.
    /// </summary>
    public RgbPrimariesChromaticityCoordinates Primaries { get; }

    /// <summary>
    /// Gets the CIE 1931 chromaticity coordinates of the mastering display white point.
    /// </summary>
    public CieXyChromaticityCoordinates WhitePoint { get; }

    /// <summary>
    /// Gets the nominal maximum mastering-display luminance in candelas per square metre.
    /// </summary>
    public double MaximumLuminance { get; }

    /// <summary>
    /// Gets the nominal minimum mastering-display luminance in candelas per square metre.
    /// </summary>
    public double MinimumLuminance { get; }

    /// <summary>
    /// Compares two mastering-display color volumes for equality.
    /// </summary>
    /// <param name="left">The first mastering-display color volume.</param>
    /// <param name="right">The second mastering-display color volume.</param>
    /// <returns><see langword="true"/> when every color-volume value is equal.</returns>
    public static bool operator ==(HeifMasteringDisplayColorVolume left, HeifMasteringDisplayColorVolume right)
        => left.Equals(right);

    /// <summary>
    /// Compares two mastering-display color volumes for inequality.
    /// </summary>
    /// <param name="left">The first mastering-display color volume.</param>
    /// <param name="right">The second mastering-display color volume.</param>
    /// <returns><see langword="true"/> when any color-volume value differs.</returns>
    public static bool operator !=(HeifMasteringDisplayColorVolume left, HeifMasteringDisplayColorVolume right)
        => !left.Equals(right);

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is HeifMasteringDisplayColorVolume other && this.Equals(other);

    /// <inheritdoc/>
    public bool Equals(HeifMasteringDisplayColorVolume other)
        => this.Primaries.Equals(other.Primaries)
            && this.WhitePoint.Equals(other.WhitePoint)
            && this.MaximumLuminance.Equals(other.MaximumLuminance)
            && this.MinimumLuminance.Equals(other.MinimumLuminance);

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(this.Primaries, this.WhitePoint, this.MaximumLuminance, this.MinimumLuminance);
}
