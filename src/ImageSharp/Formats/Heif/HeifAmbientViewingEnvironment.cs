// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Describes the nominal ambient environment intended for viewing a HEIF image.
/// </summary>
public readonly struct HeifAmbientViewingEnvironment : IEquatable<HeifAmbientViewingEnvironment>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HeifAmbientViewingEnvironment"/> struct.
    /// </summary>
    /// <param name="illuminance">The environmental illuminance in lux.</param>
    /// <param name="ambientLight">
    /// The CIE 1931 chromaticity coordinates of the ambient light in the nominal viewing environment.
    /// </param>
    public HeifAmbientViewingEnvironment(double illuminance, CieXyChromaticityCoordinates ambientLight)
    {
        this.Illuminance = illuminance;
        this.AmbientLight = ambientLight;
    }

    /// <summary>
    /// Gets the environmental illuminance in lux.
    /// </summary>
    public double Illuminance { get; }

    /// <summary>
    /// Gets the CIE 1931 chromaticity coordinates of the ambient light in the nominal viewing environment.
    /// </summary>
    public CieXyChromaticityCoordinates AmbientLight { get; }

    /// <summary>
    /// Compares two ambient viewing environments for equality.
    /// </summary>
    /// <param name="left">The first ambient viewing environment.</param>
    /// <param name="right">The second ambient viewing environment.</param>
    /// <returns><see langword="true"/> when the illuminance and ambient-light coordinates are equal.</returns>
    public static bool operator ==(HeifAmbientViewingEnvironment left, HeifAmbientViewingEnvironment right)
        => left.Equals(right);

    /// <summary>
    /// Compares two ambient viewing environments for inequality.
    /// </summary>
    /// <param name="left">The first ambient viewing environment.</param>
    /// <param name="right">The second ambient viewing environment.</param>
    /// <returns><see langword="true"/> when the illuminance or ambient-light coordinates differ.</returns>
    public static bool operator !=(HeifAmbientViewingEnvironment left, HeifAmbientViewingEnvironment right)
        => !left.Equals(right);

    /// <summary>
    /// Determines whether the specified object is an ambient viewing environment with the same values.
    /// </summary>
    /// <param name="obj">The object to compare with this value.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> contains the same environment values.</returns>
    public override bool Equals(object? obj)
        => obj is HeifAmbientViewingEnvironment other && this.Equals(other);

    /// <summary>
    /// Determines whether the specified ambient viewing environment has the same values as this value.
    /// </summary>
    /// <param name="other">The ambient viewing environment to compare with this value.</param>
    /// <returns><see langword="true"/> when the illuminance and ambient-light coordinates are equal.</returns>
    public bool Equals(HeifAmbientViewingEnvironment other)
        => this.Illuminance.Equals(other.Illuminance) && this.AmbientLight.Equals(other.AmbientLight);

    /// <summary>
    /// Returns a hash code for this ambient viewing environment.
    /// </summary>
    /// <returns>A hash code derived from the illuminance and ambient-light coordinates.</returns>
    public override int GetHashCode() => HashCode.Combine(this.Illuminance, this.AmbientLight);
}
