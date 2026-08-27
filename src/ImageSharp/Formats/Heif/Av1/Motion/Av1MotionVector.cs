// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Represents an AV1 motion or displacement vector in one-eighth-sample units.
/// </summary>
internal readonly struct Av1MotionVector : IEquatable<Av1MotionVector>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Av1MotionVector"/> struct.
    /// </summary>
    /// <param name="row">The signed vertical displacement in one-eighth-sample units.</param>
    /// <param name="column">The signed horizontal displacement in one-eighth-sample units.</param>
    public Av1MotionVector(int row, int column)
    {
        this.Row = row;
        this.Column = column;
    }

    /// <summary>
    /// Gets the signed vertical displacement in one-eighth-sample units.
    /// </summary>
    public int Row { get; }

    /// <summary>
    /// Gets the signed horizontal displacement in one-eighth-sample units.
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// Gets a value indicating whether both displacement components are zero.
    /// </summary>
    public bool IsZero => this.Row == 0 && this.Column == 0;

    /// <summary>
    /// Adds a component delta to this vector.
    /// </summary>
    /// <param name="value">The reference vector.</param>
    /// <param name="delta">The decoded component delta.</param>
    /// <returns>The component-wise sum.</returns>
    public static Av1MotionVector operator +(Av1MotionVector value, Av1MotionVector delta)
        => new(value.Row + delta.Row, value.Column + delta.Column);

    /// <summary>
    /// Determines whether two vectors have equal components.
    /// </summary>
    /// <param name="left">The first vector.</param>
    /// <param name="right">The second vector.</param>
    /// <returns><see langword="true"/> when both components are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(Av1MotionVector left, Av1MotionVector right) => left.Equals(right);

    /// <summary>
    /// Determines whether two vectors have different components.
    /// </summary>
    /// <param name="left">The first vector.</param>
    /// <param name="right">The second vector.</param>
    /// <returns><see langword="true"/> when either component differs; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(Av1MotionVector left, Av1MotionVector right) => !left.Equals(right);

    /// <summary>
    /// Determines whether this vector has the same components as another vector.
    /// </summary>
    /// <param name="other">The vector to compare.</param>
    /// <returns><see langword="true"/> when both components are equal; otherwise, <see langword="false"/>.</returns>
    public bool Equals(Av1MotionVector other) => this.Row == other.Row && this.Column == other.Column;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Av1MotionVector other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(this.Row, this.Column);
}
