// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Describes the fractional dimensions and center offsets of a HEIF clean-aperture property.
/// </summary>
internal readonly struct HeifCleanAperture : IEquatable<HeifCleanAperture>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HeifCleanAperture"/> struct.
    /// </summary>
    /// <param name="widthNumerator">The clean-aperture width numerator.</param>
    /// <param name="widthDenominator">The clean-aperture width denominator.</param>
    /// <param name="heightNumerator">The clean-aperture height numerator.</param>
    /// <param name="heightDenominator">The clean-aperture height denominator.</param>
    /// <param name="horizontalOffsetNumerator">The horizontal center-offset numerator.</param>
    /// <param name="horizontalOffsetDenominator">The horizontal center-offset denominator.</param>
    /// <param name="verticalOffsetNumerator">The vertical center-offset numerator.</param>
    /// <param name="verticalOffsetDenominator">The vertical center-offset denominator.</param>
    public HeifCleanAperture(
        int widthNumerator,
        int widthDenominator,
        int heightNumerator,
        int heightDenominator,
        int horizontalOffsetNumerator,
        int horizontalOffsetDenominator,
        int verticalOffsetNumerator,
        int verticalOffsetDenominator)
    {
        this.WidthNumerator = widthNumerator;
        this.WidthDenominator = widthDenominator;
        this.HeightNumerator = heightNumerator;
        this.HeightDenominator = heightDenominator;
        this.HorizontalOffsetNumerator = horizontalOffsetNumerator;
        this.HorizontalOffsetDenominator = horizontalOffsetDenominator;
        this.VerticalOffsetNumerator = verticalOffsetNumerator;
        this.VerticalOffsetDenominator = verticalOffsetDenominator;
    }

    /// <summary>
    /// Gets the clean-aperture width numerator.
    /// </summary>
    public int WidthNumerator { get; }

    /// <summary>
    /// Gets the clean-aperture width denominator.
    /// </summary>
    public int WidthDenominator { get; }

    /// <summary>
    /// Gets the clean-aperture height numerator.
    /// </summary>
    public int HeightNumerator { get; }

    /// <summary>
    /// Gets the clean-aperture height denominator.
    /// </summary>
    public int HeightDenominator { get; }

    /// <summary>
    /// Gets the horizontal center-offset numerator.
    /// </summary>
    public int HorizontalOffsetNumerator { get; }

    /// <summary>
    /// Gets the horizontal center-offset denominator.
    /// </summary>
    public int HorizontalOffsetDenominator { get; }

    /// <summary>
    /// Gets the vertical center-offset numerator.
    /// </summary>
    public int VerticalOffsetNumerator { get; }

    /// <summary>
    /// Gets the vertical center-offset denominator.
    /// </summary>
    public int VerticalOffsetDenominator { get; }

    /// <summary>
    /// Converts the clean aperture to an integer crop rectangle within the coded image extent.
    /// </summary>
    /// <param name="imageExtent">The coded image dimensions.</param>
    /// <returns>The clean-aperture crop rectangle.</returns>
    /// <exception cref="InvalidImageContentException">
    /// The clean-aperture fractions do not describe an integer rectangle inside the coded image extent.
    /// </exception>
    public Rectangle ToRectangle(Size imageExtent)
    {
        if (this.WidthNumerator <= 0 || this.HeightNumerator <= 0 ||
            this.WidthDenominator <= 0 || this.HeightDenominator <= 0 ||
            this.HorizontalOffsetDenominator <= 0 || this.VerticalOffsetDenominator <= 0)
        {
            throw new InvalidImageContentException("The clean aperture dimensions and denominators must be positive.");
        }

        if ((this.WidthNumerator % this.WidthDenominator) != 0 ||
            (this.HeightNumerator % this.HeightDenominator) != 0)
        {
            throw new InvalidImageContentException("The clean aperture dimensions must resolve to integer pixels.");
        }

        int width = this.WidthNumerator / this.WidthDenominator;
        int height = this.HeightNumerator / this.HeightDenominator;

        // Express each top-left coordinate over twice the offset denominator. This is the exact
        // center-plus-offset-minus-half-size equation without floating-point rounding.
        long xNumerator = ((long)(imageExtent.Width - width) * this.HorizontalOffsetDenominator) +
            (2L * this.HorizontalOffsetNumerator);
        long xDenominator = 2L * this.HorizontalOffsetDenominator;
        long yNumerator = ((long)(imageExtent.Height - height) * this.VerticalOffsetDenominator) +
            (2L * this.VerticalOffsetNumerator);
        long yDenominator = 2L * this.VerticalOffsetDenominator;

        if ((xNumerator % xDenominator) != 0 || (yNumerator % yDenominator) != 0)
        {
            throw new InvalidImageContentException("The clean aperture offsets must resolve to integer pixels.");
        }

        long x = xNumerator / xDenominator;
        long y = yNumerator / yDenominator;
        if (x < 0 || y < 0 || x + width > imageExtent.Width || y + height > imageExtent.Height)
        {
            throw new InvalidImageContentException("The clean aperture lies outside the coded image extent.");
        }

        return new Rectangle((int)x, (int)y, width, height);
    }

    /// <inheritdoc/>
    public bool Equals(HeifCleanAperture other)
        => this.WidthNumerator == other.WidthNumerator &&
            this.WidthDenominator == other.WidthDenominator &&
            this.HeightNumerator == other.HeightNumerator &&
            this.HeightDenominator == other.HeightDenominator &&
            this.HorizontalOffsetNumerator == other.HorizontalOffsetNumerator &&
            this.HorizontalOffsetDenominator == other.HorizontalOffsetDenominator &&
            this.VerticalOffsetNumerator == other.VerticalOffsetNumerator &&
            this.VerticalOffsetDenominator == other.VerticalOffsetDenominator;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is HeifCleanAperture other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hashCode = default;
        hashCode.Add(this.WidthNumerator);
        hashCode.Add(this.WidthDenominator);
        hashCode.Add(this.HeightNumerator);
        hashCode.Add(this.HeightDenominator);
        hashCode.Add(this.HorizontalOffsetNumerator);
        hashCode.Add(this.HorizontalOffsetDenominator);
        hashCode.Add(this.VerticalOffsetNumerator);
        hashCode.Add(this.VerticalOffsetDenominator);
        return hashCode.ToHashCode();
    }
}
