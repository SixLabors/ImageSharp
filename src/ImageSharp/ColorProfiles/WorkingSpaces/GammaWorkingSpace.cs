// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles.Companding;

namespace SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;

/// <summary>
/// The gamma working space.
/// </summary>
public sealed class GammaWorkingSpace : RgbWorkingSpace
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GammaWorkingSpace" /> class.
    /// </summary>
    /// <param name="gamma">The gamma value.</param>
    /// <param name="referenceWhite">The reference white point.</param>
    /// <param name="chromaticityCoordinates">The chromaticity of the rgb primaries.</param>
    public GammaWorkingSpace(float gamma, CieXyz referenceWhite, RgbPrimariesChromaticityCoordinates chromaticityCoordinates)
        : base(referenceWhite, chromaticityCoordinates) => this.Gamma = gamma;

    /// <summary>
    /// Gets the gamma value.
    /// </summary>
    public float Gamma { get; }

    /// <inheritdoc/>
    public override void Compress(Span<Vector4> vectors) => GammaCompanding.Compress(vectors, this.Gamma);

    /// <inheritdoc/>
    public override void Expand(Span<Vector4> vectors) => GammaCompanding.Expand(vectors, this.Gamma);

    /// <inheritdoc/>
    public override Vector4 Compress(Vector4 vector) => GammaCompanding.Compress(vector, this.Gamma);

    /// <inheritdoc/>
    public override Vector4 Expand(Vector4 vector) => GammaCompanding.Expand(vector, this.Gamma);

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

        if (obj is GammaWorkingSpace other)
        {
            return this.Gamma.Equals(other.Gamma)
                && this.WhitePoint.Equals(other.WhitePoint)
                && this.ChromaticityCoordinates.Equals(other.ChromaticityCoordinates);
        }

        return false;
    }

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(
        this.WhitePoint,
        this.ChromaticityCoordinates,
        this.Gamma);
}
