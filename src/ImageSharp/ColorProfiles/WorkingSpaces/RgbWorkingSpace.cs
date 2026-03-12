// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;

/// <summary>
/// Base class for all implementations of <see cref="RgbWorkingSpace"/>.
/// </summary>
public abstract class RgbWorkingSpace
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RgbWorkingSpace"/> class.
    /// </summary>
    /// <param name="referenceWhite">The reference white point.</param>
    /// <param name="chromaticityCoordinates">The chromaticity of the rgb primaries.</param>
    protected RgbWorkingSpace(CieXyz referenceWhite, RgbPrimariesChromaticityCoordinates chromaticityCoordinates)
    {
        this.WhitePoint = referenceWhite;
        this.ChromaticityCoordinates = chromaticityCoordinates;
    }

    /// <summary>
    /// Gets the reference white point
    /// </summary>
    public CieXyz WhitePoint { get; }

    /// <summary>
    /// Gets the chromaticity of the rgb primaries.
    /// </summary>
    public RgbPrimariesChromaticityCoordinates ChromaticityCoordinates { get; }

    /// <summary>
    /// Compresses the linear vectors to their nonlinear equivalents with respect to the energy.
    /// </summary>
    /// <param name="vectors">The span of vectors.</param>
    public abstract void Compress(Span<Vector4> vectors);

    /// <summary>
    /// Expands the nonlinear vectors to their linear equivalents with respect to the energy.
    /// </summary>
    /// <param name="vectors">The span of vectors.</param>
    public abstract void Expand(Span<Vector4> vectors);

    /// <summary>
    /// Compresses the linear vector to its nonlinear equivalent with respect to the energy.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    public abstract Vector4 Compress(Vector4 vector);

    /// <summary>
    /// Compresses the linear vector to its nonlinear equivalent with respect to the energy.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    public abstract Vector4 Expand(Vector4 vector);

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj is RgbWorkingSpace other)
        {
            return this.WhitePoint.Equals(other.WhitePoint)
                && this.ChromaticityCoordinates.Equals(other.ChromaticityCoordinates);
        }

        return false;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(this.WhitePoint, this.ChromaticityCoordinates);
}
