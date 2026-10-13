// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Describes the nominal diffuse-white luminance of a HEIF image.
/// </summary>
public readonly struct HeifNominalDiffuseWhite : IEquatable<HeifNominalDiffuseWhite>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HeifNominalDiffuseWhite"/> struct.
    /// </summary>
    /// <param name="luminance">
    /// The nominal diffuse-white luminance in candelas per square metre, or <see langword="null"/> to use the
    /// standard default.
    /// </param>
    public HeifNominalDiffuseWhite(double? luminance) => this.Luminance = luminance;

    /// <summary>
    /// Gets the nominal diffuse-white luminance in candelas per square metre, or <see langword="null"/> when the
    /// image requests the standard default.
    /// </summary>
    public double? Luminance { get; }

    /// <summary>
    /// Compares two nominal diffuse-white descriptions for equality.
    /// </summary>
    /// <param name="left">The first nominal diffuse-white description.</param>
    /// <param name="right">The second nominal diffuse-white description.</param>
    /// <returns><see langword="true"/> when both descriptions specify the same luminance behavior.</returns>
    public static bool operator ==(HeifNominalDiffuseWhite left, HeifNominalDiffuseWhite right)
        => left.Equals(right);

    /// <summary>
    /// Compares two nominal diffuse-white descriptions for inequality.
    /// </summary>
    /// <param name="left">The first nominal diffuse-white description.</param>
    /// <param name="right">The second nominal diffuse-white description.</param>
    /// <returns><see langword="true"/> when the descriptions specify different luminance behavior.</returns>
    public static bool operator !=(HeifNominalDiffuseWhite left, HeifNominalDiffuseWhite right)
        => !left.Equals(right);

    /// <summary>
    /// Determines whether the specified object is a nominal diffuse-white description with the same value.
    /// </summary>
    /// <param name="obj">The object to compare with this value.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> specifies the same luminance behavior.</returns>
    public override bool Equals(object? obj)
        => obj is HeifNominalDiffuseWhite other && this.Equals(other);

    /// <summary>
    /// Determines whether the specified nominal diffuse-white description has the same value as this value.
    /// </summary>
    /// <param name="other">The nominal diffuse-white description to compare with this value.</param>
    /// <returns><see langword="true"/> when both descriptions specify the same luminance behavior.</returns>
    public bool Equals(HeifNominalDiffuseWhite other) => this.Luminance.Equals(other.Luminance);

    /// <summary>
    /// Returns a hash code for this nominal diffuse-white description.
    /// </summary>
    /// <returns>A hash code derived from the luminance behavior.</returns>
    public override int GetHashCode() => this.Luminance.GetHashCode();
}
