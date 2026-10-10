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
    /// The number of fractional bits used by AV1 motion-vector components.
    /// </summary>
    public const int SubpixelBits = 3;

    /// <summary>
    /// The number of motion-vector units in one full pixel.
    /// </summary>
    public const int SubpixelScale = 1 << SubpixelBits;

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
    /// <returns><see langword="true"/> when both components are equal, otherwise <see langword="false"/>.</returns>
    public static bool operator ==(Av1MotionVector left, Av1MotionVector right) => left.Equals(right);

    /// <summary>
    /// Determines whether two vectors have different components.
    /// </summary>
    /// <param name="left">The first vector.</param>
    /// <param name="right">The second vector.</param>
    /// <returns><see langword="true"/> when either component differs, otherwise <see langword="false"/>.</returns>
    public static bool operator !=(Av1MotionVector left, Av1MotionVector right) => !left.Equals(right);

    /// <summary>
    /// Computes the full-pixel displacement region that can produce distinct, padded block predictions.
    /// </summary>
    /// <param name="block">The block's luma rectangle in the coded frame.</param>
    /// <param name="frameSize">The frame dimensions rounded to mode-information units.</param>
    /// <param name="border">The allocated luma border on every side.</param>
    /// <returns>The displacement region with exclusive right and bottom edges.</returns>
    public static Rectangle GetFrameSearchBounds(Rectangle block, Size frameSize, int border)
    {
        // The bounds reserve eight samples for interpolation support.
        // They stop candidates beyond a replicated edge, where a farther move cannot change the prediction.
        // The bounds describe displacement from this block. Interior blocks can thus move across the frame and are not limited to the border width.
        int minimumColumn = Math.Max(-(block.X + border - 8), -(block.Right + 8));
        int minimumRow = Math.Max(-(block.Y + border - 8), -(block.Bottom + 8));
        int maximumColumn = Math.Min(frameSize.Width - block.Right + border - 8, frameSize.Width - block.X + 8);
        int maximumRow = Math.Min(frameSize.Height - block.Bottom + border - 8, frameSize.Height - block.Y + 8);
        return Rectangle.FromLTRB(minimumColumn, minimumRow, maximumColumn + 1, maximumRow + 1);
    }

    /// <summary>
    /// Restricts a search range so the block stays within eight samples of the visible frame, as a sharpness of three requires.
    /// </summary>
    /// <param name="bounds">The range to restrict, with exclusive right and bottom edges.</param>
    /// <param name="blockOrigin">The luma origin of the block, in samples.</param>
    /// <param name="blockSize">The luma size of the searched block.</param>
    /// <param name="visibleFrameSize">The visible frame size.</param>
    /// <param name="scale">One for full-pixel ranges, eight for eighth-sample ranges.</param>
    /// <returns>The restricted range.</returns>
    public static Rectangle ClampToSharpnessMargins(Rectangle bounds, Point blockOrigin, Size blockSize, Size visibleFrameSize, int scale)
    {
        // The range allows eight samples beyond the visible frame on each side, at full-pixel and at eighth-sample scale.
        // The clamp does not repair an empty range. An empty range cannot occur, because the reference vector stays within 16 samples of the block.
        // Also, the frame limits are at least the border less 12 samples beyond the margins.
        int topMargin = (blockOrigin.Y + 8) * scale;
        int leftMargin = (blockOrigin.X + 8) * scale;
        int bottomMargin = Math.Max((visibleFrameSize.Height - blockSize.Height - blockOrigin.Y + 8) * scale, -topMargin);
        int rightMargin = Math.Max((visibleFrameSize.Width - blockSize.Width - blockOrigin.X + 8) * scale, -leftMargin);
        int left = Math.Max(bounds.Left, -leftMargin);
        int top = Math.Max(bounds.Top, -topMargin);
        int right = Math.Min(bounds.Right - 1, rightMargin);
        int bottom = Math.Min(bounds.Bottom - 1, bottomMargin);
        return Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    /// <summary>
    /// Returns whether the vector, rounded to full samples, lies in the frame displacement region.
    /// The search skips a mode whose vector is outside the region. The margin clamp before the test never moves an outside vector into the region.
    /// </summary>
    /// <param name="frameBounds">The full-pixel region from <see cref="GetFrameSearchBounds"/>.</param>
    /// <returns><see langword="true"/> when the rounded vector is inside the region.</returns>
    public bool IsInFrameSearchBounds(Rectangle frameBounds)
    {
        int row = (this.Row + 3 + (this.Row >= 0 ? 1 : 0)) >> 3;
        int column = (this.Column + 3 + (this.Column >= 0 ? 1 : 0)) >> 3;
        return row >= frameBounds.Top && row < frameBounds.Bottom && column >= frameBounds.Left && column < frameBounds.Right;
    }

    /// <summary>
    /// Restricts a frame displacement region to representable full-pixel candidates around this reference.
    /// </summary>
    /// <param name="frameBounds">The full-pixel region from <see cref="GetFrameSearchBounds"/>.</param>
    /// <returns>The full-pixel search region with exclusive right and bottom edges.</returns>
    public Rectangle GetFullPixelSearchBounds(Rectangle frameBounds)
    {
        const int MaximumDisplacement = 1023;

        // Both endpoints must fit inside a 1023-pixel displacement from the fractional reference.
        // The lower endpoint rounds toward positive infinity and the upper endpoint toward negative infinity, also for negative references.
        // The reserved endpoints of the vector domain also stay out of the search.
        int minimumColumn = Math.Max(frameBounds.Left, Math.Max(((this.Column + 7) >> 3) - MaximumDisplacement, (LowerBound >> 3) + 1));
        int minimumRow = Math.Max(frameBounds.Top, Math.Max(((this.Row + 7) >> 3) - MaximumDisplacement, (LowerBound >> 3) + 1));
        int maximumColumn = Math.Min(frameBounds.Right - 1, Math.Min((this.Column >> 3) + MaximumDisplacement, (UpperBound >> 3) - 1));
        int maximumRow = Math.Min(frameBounds.Bottom - 1, Math.Min((this.Row >> 3) + MaximumDisplacement, (UpperBound >> 3) - 1));
        maximumColumn = Math.Max(minimumColumn, maximumColumn);
        maximumRow = Math.Max(minimumRow, maximumRow);
        return Rectangle.FromLTRB(minimumColumn, minimumRow, maximumColumn + 1, maximumRow + 1);
    }

    /// <summary>
    /// Restricts a frame displacement region to representable fractional candidates around this reference.
    /// </summary>
    /// <param name="frameBounds">The full-pixel region from <see cref="GetFrameSearchBounds"/>.</param>
    /// <returns>The eighth-sample search region with exclusive right and bottom edges.</returns>
    public Rectangle GetSubpixelSearchBounds(Rectangle frameBounds)
    {
        const int MaximumDisplacement = 1023 * SubpixelScale;

        // The refinement uses the original frame region, not the rounded full-pixel intersection.
        // The rounded intersection loses the fractional part between an integer endpoint and the reference-centered limit.
        int minimumColumn = Math.Max(frameBounds.Left * SubpixelScale, this.Column - MaximumDisplacement);
        int minimumRow = Math.Max(frameBounds.Top * SubpixelScale, this.Row - MaximumDisplacement);
        int maximumColumn = Math.Min((frameBounds.Right - 1) * SubpixelScale, this.Column + MaximumDisplacement);
        int maximumRow = Math.Min((frameBounds.Bottom - 1) * SubpixelScale, this.Row + MaximumDisplacement);
        maximumColumn = Math.Min(UpperBound - 1, Math.Max(minimumColumn, maximumColumn));
        maximumRow = Math.Min(UpperBound - 1, Math.Max(minimumRow, maximumRow));
        minimumColumn = Math.Max(LowerBound + 1, minimumColumn);
        minimumRow = Math.Max(LowerBound + 1, minimumRow);
        return Rectangle.FromLTRB(minimumColumn, minimumRow, maximumColumn + 1, maximumRow + 1);
    }

    /// <summary>
    /// Reduces this vector to the motion-vector precision selected by the current frame.
    /// </summary>
    /// <param name="allowHighPrecision">A value indicating whether one-eighth-sample precision can stay.</param>
    /// <param name="forceInteger">A value indicating whether both components round to integer-sample precision.</param>
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

        // Low precision removes the one-eighth-sample bit. An odd component moves one unit toward zero, so it always lands on the even value nearer zero.
        // The AV1 specification applies this rule to spatial and temporal candidate vectors.
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

        // Candidate derivation allows the full block size plus sixteen luma samples beyond each visible frame edge.
        // These limits apply only to the candidate list. The prediction applies tighter limits later, when it reads samples.
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

        // The reciprocal table holds 1 / denominator in Q14. Signed power-of-two rounding keeps negative components symmetric with positive ones.
        // AV1 excludes the two reserved endpoints from projected motion vectors.
        // The motion field keeps only source components up to 4095, so the full Q14 product fits in Int32.
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
    /// <returns><see langword="true"/> when both components are equal, otherwise <see langword="false"/>.</returns>
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

        // Exactly half an integer sample has magnitude four. AV1 keeps the truncated base for that case.
        // Thus positive and negative half ties move toward zero. Only larger remainders move to the adjacent sample.
        if (Math.Abs(remainder) > 4)
        {
            value += remainder > 0 ? 8 : -8;
        }

        return value;
    }
}
