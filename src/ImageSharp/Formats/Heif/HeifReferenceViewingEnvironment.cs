// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Describes the display surround and periphery in which a HEIF image was mastered.
/// </summary>
public readonly struct HeifReferenceViewingEnvironment : IEquatable<HeifReferenceViewingEnvironment>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HeifReferenceViewingEnvironment"/> struct.
    /// </summary>
    /// <param name="surroundLuminance">The luminance of the area immediately surrounding the display.</param>
    /// <param name="surroundLight">The CIE 1931 chromaticity coordinates of the surround light.</param>
    /// <param name="peripheryLuminance">The luminance of the environment outside the display surround.</param>
    /// <param name="peripheryLight">The CIE 1931 chromaticity coordinates of the periphery light.</param>
    public HeifReferenceViewingEnvironment(
        double surroundLuminance,
        CieXyChromaticityCoordinates surroundLight,
        double peripheryLuminance,
        CieXyChromaticityCoordinates peripheryLight)
    {
        this.SurroundLuminance = surroundLuminance;
        this.SurroundLight = surroundLight;
        this.PeripheryLuminance = peripheryLuminance;
        this.PeripheryLight = peripheryLight;
    }

    /// <summary>
    /// Gets the luminance of the area immediately surrounding the display in candelas per square metre.
    /// </summary>
    public double SurroundLuminance { get; }

    /// <summary>
    /// Gets the CIE 1931 chromaticity coordinates of the surround light.
    /// </summary>
    public CieXyChromaticityCoordinates SurroundLight { get; }

    /// <summary>
    /// Gets the luminance of the environment outside the display surround in candelas per square metre.
    /// </summary>
    public double PeripheryLuminance { get; }

    /// <summary>
    /// Gets the CIE 1931 chromaticity coordinates of the periphery light.
    /// </summary>
    public CieXyChromaticityCoordinates PeripheryLight { get; }

    /// <summary>
    /// Compares two reference viewing environments for equality.
    /// </summary>
    /// <param name="left">The first reference viewing environment.</param>
    /// <param name="right">The second reference viewing environment.</param>
    /// <returns><see langword="true"/> when every surround and periphery value is equal.</returns>
    public static bool operator ==(HeifReferenceViewingEnvironment left, HeifReferenceViewingEnvironment right)
        => left.Equals(right);

    /// <summary>
    /// Compares two reference viewing environments for inequality.
    /// </summary>
    /// <param name="left">The first reference viewing environment.</param>
    /// <param name="right">The second reference viewing environment.</param>
    /// <returns><see langword="true"/> when any surround or periphery value differs.</returns>
    public static bool operator !=(HeifReferenceViewingEnvironment left, HeifReferenceViewingEnvironment right)
        => !left.Equals(right);

    /// <summary>
    /// Determines whether the specified object is a reference viewing environment with the same values.
    /// </summary>
    /// <param name="obj">The object to compare with this value.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> contains the same environment values.</returns>
    public override bool Equals(object? obj)
        => obj is HeifReferenceViewingEnvironment other && this.Equals(other);

    /// <summary>
    /// Determines whether the specified reference viewing environment has the same values as this value.
    /// </summary>
    /// <param name="other">The reference viewing environment to compare with this value.</param>
    /// <returns><see langword="true"/> when every surround and periphery value is equal.</returns>
    public bool Equals(HeifReferenceViewingEnvironment other)
        => this.SurroundLuminance.Equals(other.SurroundLuminance)
            && this.SurroundLight.Equals(other.SurroundLight)
            && this.PeripheryLuminance.Equals(other.PeripheryLuminance)
            && this.PeripheryLight.Equals(other.PeripheryLight);

    /// <summary>
    /// Returns a hash code for this reference viewing environment.
    /// </summary>
    /// <returns>A hash code derived from the surround and periphery values.</returns>
    public override int GetHashCode()
        => HashCode.Combine(
            this.SurroundLuminance,
            this.SurroundLight,
            this.PeripheryLuminance,
            this.PeripheryLight);
}
