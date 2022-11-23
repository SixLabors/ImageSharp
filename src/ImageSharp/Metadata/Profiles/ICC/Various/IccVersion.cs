// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <summary>
/// Represents the ICC profile version number.
/// </summary>
public readonly struct IccVersion : IEquatable<IccVersion>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IccVersion"/> struct.
    /// </summary>
    /// <param name="major">The major version number.</param>
    /// <param name="minor">The minor version number.</param>
    /// <param name="patch">The patch version number.</param>
    public IccVersion(int major, int minor, int patch)
    {
        this.Major = major;
        this.Minor = minor;
        this.Patch = patch;
    }

    /// <summary>
    /// Gets the major version number.
    /// </summary>
    public int Major { get; }

    /// <summary>
    /// Gets the minor version number.
    /// </summary>
    public int Minor { get; }

    /// <summary>
    /// Gets the patch number.
    /// </summary>
    public int Patch { get; }

    /// <summary>
    /// Returns a value indicating whether the two values are equal.
    /// </summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> if the two value are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(IccVersion left, IccVersion right)
        => left.Equals(right);

    /// <summary>
    /// Returns a value indicating whether the two values are not equal.
    /// </summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> if the two value are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(IccVersion left, IccVersion right)
        => !(left == right);

    /// <inheritdoc/>
    public override bool Equals(object obj)
        => obj is IccVersion iccVersion && this.Equals(iccVersion);

    /// <inheritdoc/>
    public bool Equals(IccVersion other) =>
        this.Major == other.Major &&
        this.Minor == other.Minor &&
        this.Patch == other.Patch;

    /// <inheritdoc/>
    public override string ToString()
        => string.Join(".", this.Major, this.Minor, this.Patch);

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(this.Major, this.Minor, this.Patch);
}
