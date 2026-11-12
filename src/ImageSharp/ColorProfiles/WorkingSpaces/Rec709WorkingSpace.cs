// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;

/// <summary>
/// Rec. 709 (ITU-R Recommendation BT.709) working space.
/// </summary>
public sealed class Rec709WorkingSpace : RgbWorkingSpace
{
    // The coefficients do not depend on the RGB primaries or white point. Prepare the two wrapped
    // operations once; the existing companding traversal retains alpha and handles every SIMD width.
    private static readonly CompandingUtilities.SymmetricOperator<Rec709Companding.ExpandOperator> SymmetricExpand = new(
        new Rec709Companding.ExpandOperator(Rec709Companding.NonlinearScale, Rec709Companding.NonlinearOffset, Rec709Companding.NonlinearThreshold));

    private static readonly CompandingUtilities.SymmetricOperator<Rec709Companding.CompressOperator> SymmetricCompress = new(
        new Rec709Companding.CompressOperator(Rec709Companding.NonlinearScale, Rec709Companding.NonlinearOffset, Rec709Companding.LinearThreshold));

    /// <summary>
    /// Initializes a new instance of the <see cref="Rec709WorkingSpace" /> class.
    /// </summary>
    /// <param name="referenceWhite">The reference white point.</param>
    /// <param name="chromaticityCoordinates">The chromaticity of the rgb primaries.</param>
    public Rec709WorkingSpace(CieXyz referenceWhite, RgbPrimariesChromaticityCoordinates chromaticityCoordinates)
        : this(referenceWhite, chromaticityCoordinates, false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Rec709WorkingSpace"/> class with the selected negative-component conversion.
    /// </summary>
    /// <param name="referenceWhite">The reference white point.</param>
    /// <param name="chromaticityCoordinates">The RGB primary chromaticities.</param>
    /// <param name="useSymmetricCurves">Whether negative components use the positive curve with their original sign.</param>
    public Rec709WorkingSpace(CieXyz referenceWhite, RgbPrimariesChromaticityCoordinates chromaticityCoordinates, bool useSymmetricCurves)
        : base(referenceWhite, chromaticityCoordinates)
    {
        this.UseSymmetricCurves = useSymmetricCurves;
    }

    /// <summary>
    /// Gets a value indicating whether negative components use the positive curve with their original sign.
    /// </summary>
    public bool UseSymmetricCurves { get; }

    /// <inheritdoc/>
    public override void Compress(Span<Vector4> vectors)
    {
        if (this.UseSymmetricCurves)
        {
            CompandingUtilities.Compand(vectors, SymmetricCompress, PixelColorRange.Extended);
        }
        else
        {
            Rec709Companding.Compress(vectors);
        }
    }

    /// <inheritdoc/>
    public override void Expand(Span<Vector4> vectors)
    {
        if (this.UseSymmetricCurves)
        {
            CompandingUtilities.Compand(vectors, SymmetricExpand, PixelColorRange.Extended);
        }
        else
        {
            Rec709Companding.Expand(vectors);
        }
    }

    /// <inheritdoc/>
    public override Vector4 Compress(Vector4 vector)
        => this.UseSymmetricCurves
            ? CompandingUtilities.Compand(vector, SymmetricCompress, PixelColorRange.Extended)
            : Rec709Companding.Compress(vector);

    /// <inheritdoc/>
    public override Vector4 Expand(Vector4 vector)
        => this.UseSymmetricCurves
            ? CompandingUtilities.Compand(vector, SymmetricExpand, PixelColorRange.Extended)
            : Rec709Companding.Expand(vector);

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is Rec709WorkingSpace other
        && this.UseSymmetricCurves == other.UseSymmetricCurves
        && this.WhitePoint.Equals(other.WhitePoint)
        && this.ChromaticityCoordinates.Equals(other.ChromaticityCoordinates);

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(typeof(Rec709WorkingSpace), this.WhitePoint, this.ChromaticityCoordinates, this.UseSymmetricCurves);
}
