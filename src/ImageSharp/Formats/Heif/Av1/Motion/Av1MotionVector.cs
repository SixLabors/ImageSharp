// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Represents an AV1 motion or displacement vector in one-eighth-sample units.
/// </summary>
internal readonly struct Av1MotionVector : IEquatable<Av1MotionVector>
{
    /// <summary>
    /// The greatest absolute temporal distance used by AV1 motion-vector projection.
    /// </summary>
    public const int MaximumTemporalDistance = 31;

    /// <summary>
    /// The reserved lower endpoint of the signed AV1 motion-vector domain.
    /// </summary>
    private const int LowerBound = -16384;

    /// <summary>
    /// The exclusive upper endpoint of the signed AV1 motion-vector domain.
    /// </summary>
    private const int UpperBound = 16384;

    /// <summary>
    /// The additional sixteen-sample border admitted while deriving spatial reference candidates, in one-eighth-sample units.
    /// </summary>
    private const int ReferenceBorder = 16 << 3;

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
    /// Gets a value indicating whether both components lie strictly between the two reserved AV1 endpoints.
    /// </summary>
    public bool IsValid =>
        this.Row > LowerBound &&
        this.Row < UpperBound &&
        this.Column > LowerBound &&
        this.Column < UpperBound;

    /// <summary>
    /// Gets the reciprocal table used by AV1 temporal projection in fourteen-bit fixed-point precision.
    /// </summary>
    private static ReadOnlySpan<int> ProjectionDivisors =>
        [0, 16384, 8192, 5461, 4096, 3276, 2730, 2340, 2048, 1820, 1638, 1489, 1365, 1260, 1170, 1092,
         1024, 963, 910, 862, 819, 780, 744, 712, 682, 655, 630, 606, 585, 564, 546, 528];

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
    /// Reduces this vector to the motion-vector precision selected by the current frame.
    /// </summary>
    /// <param name="allowHighPrecision">
    /// A value indicating whether one-eighth-sample precision may be retained.
    /// </param>
    /// <param name="forceInteger">
    /// A value indicating whether both components must be rounded to integer-sample precision.
    /// </param>
    /// <returns>The precision-reduced vector.</returns>
    public Av1MotionVector LowerPrecision(bool allowHighPrecision, bool forceInteger)
    {
        if (forceInteger)
        {
            return new(RoundToIntegerPrecision(this.Row), RoundToIntegerPrecision(this.Column));
        }

        if (allowHighPrecision)
        {
            return this;
        }

        // Low precision removes the one-eighth-sample bit. Odd components move toward zero rather than rounding to
        // the nearest even value, which is the normative lower_mv_precision behavior used by spatial and temporal MVs.
        int row = (this.Row & 1) != 0 ? this.Row + (this.Row > 0 ? -1 : 1) : this.Row;
        int column = (this.Column & 1) != 0 ? this.Column + (this.Column > 0 ? -1 : 1) : this.Column;
        return new(row, column);
    }

    /// <summary>
    /// Clamps this vector to the spatial reference-candidate limits for a coding block.
    /// </summary>
    /// <param name="blockWidth">The coding-block width in luma samples.</param>
    /// <param name="blockHeight">The coding-block height in luma samples.</param>
    /// <param name="blockToLeftEdge">The signed distance to the left frame edge in one-eighth-sample units.</param>
    /// <param name="blockToRightEdge">The signed distance to the right frame edge in one-eighth-sample units.</param>
    /// <param name="blockToTopEdge">The signed distance to the top frame edge in one-eighth-sample units.</param>
    /// <param name="blockToBottomEdge">The signed distance to the bottom frame edge in one-eighth-sample units.</param>
    /// <returns>The vector clamped to the permitted spatial reference-candidate range.</returns>
    public Av1MotionVector ClampReference(
        int blockWidth,
        int blockHeight,
        int blockToLeftEdge,
        int blockToRightEdge,
        int blockToTopEdge,
        int blockToBottomEdge)
    {
        int blockWidthSubpixel = blockWidth << 3;
        int blockHeightSubpixel = blockHeight << 3;

        // Candidate derivation permits the complete block extent plus sixteen further luma samples beyond each
        // visible frame edge. These are stack limits, not the tighter UMV limits applied later while sampling pixels.
        int minimumColumn = blockToLeftEdge - blockWidthSubpixel - ReferenceBorder;
        int maximumColumn = blockToRightEdge + blockWidthSubpixel + ReferenceBorder;
        int minimumRow = blockToTopEdge - blockHeightSubpixel - ReferenceBorder;
        int maximumRow = blockToBottomEdge + blockHeightSubpixel + ReferenceBorder;
        return new(
            Av1Math.Clip3(minimumRow, maximumRow, this.Row),
            Av1Math.Clip3(minimumColumn, maximumColumn, this.Column));
    }

    /// <summary>
    /// Projects this vector across a ratio of temporal frame distances.
    /// </summary>
    /// <param name="numerator">The signed source-to-target frame distance.</param>
    /// <param name="denominator">The positive source-to-reference frame distance.</param>
    /// <returns>The projected vector clamped inside the AV1 motion-vector domain.</returns>
    public Av1MotionVector ProjectTemporal(int numerator, int denominator)
    {
        denominator = Math.Min(denominator, MaximumTemporalDistance);
        numerator = Av1Math.Clip3(-MaximumTemporalDistance, MaximumTemporalDistance, numerator);

        // The reciprocal table represents 1 / denominator in Q14. Signed power-of-two rounding preserves symmetry
        // for negative components, and AV1 excludes the two reserved endpoints from projected motion vectors.
        // Motion-field retention limits each source component to 4095, keeping the complete Q14 product inside Int32.
        int row = Av1Math.RoundPowerOf2Signed(this.Row * numerator * ProjectionDivisors[denominator], 14);
        int column = Av1Math.RoundPowerOf2Signed(this.Column * numerator * ProjectionDivisors[denominator], 14);
        row = Av1Math.Clip3(LowerBound + 1, UpperBound - 1, row);
        column = Av1Math.Clip3(LowerBound + 1, UpperBound - 1, column);
        return new(row, column);
    }

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

    /// <summary>
    /// Rounds one component to the nearest integer-sample displacement.
    /// </summary>
    /// <param name="value">The component in one-eighth-sample units.</param>
    /// <returns>The integer-precision component in one-eighth-sample units.</returns>
    private static int RoundToIntegerPrecision(int value)
    {
        int remainder = value % 8;
        value -= remainder;

        // Exactly half an integer sample has magnitude four. AV1 leaves that truncated base unchanged, so both
        // positive and negative half ties move toward zero; only larger remainders advance to the adjacent sample.
        if (Math.Abs(remainder) > 4)
        {
            value += remainder > 0 ? 8 : -8;
        }

        return value;
    }
}
