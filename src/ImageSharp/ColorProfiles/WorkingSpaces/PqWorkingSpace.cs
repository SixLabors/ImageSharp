// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;

/// <summary>
/// Represents an RGB working space with the SMPTE ST 2084 perceptual-quantizer transfer curve.
/// </summary>
/// <remarks>
/// A linear component of one represents <see cref="PeakLuminance"/> cd/m².
/// The peak must be positive and finite, and 10000 divided by that peak must be representable as a finite float.
/// Negative components use the magnitude curve with their original sign. Components can exceed one within the PQ curve's mathematical domain.
/// Converted RGB is bounded to the normalized range [-65504,65504]. Alpha remains unchanged.
/// </remarks>
public sealed class PqWorkingSpace : RgbWorkingSpace
{
    private readonly PqCompanding.ExpandOperator expand;
    private readonly PqCompanding.CompressOperator compress;

    /// <summary>
    /// Initializes a new instance of the <see cref="PqWorkingSpace"/> class.
    /// </summary>
    /// <param name="peakLuminance">The luminance represented by linear one, in cd/m². It must permit finite PQ normalization.</param>
    /// <param name="referenceWhite">The reference white point.</param>
    /// <param name="chromaticityCoordinates">The chromaticity coordinates of the RGB primaries.</param>
    public PqWorkingSpace(float peakLuminance, CieXyz referenceWhite, RgbPrimariesChromaticityCoordinates chromaticityCoordinates)
        : base(referenceWhite, chromaticityCoordinates)
    {
        PqCompanding.ValidatePeakLuminance(peakLuminance);
        this.expand = new(PqCompanding.ReferenceLuminance / peakLuminance);
        this.compress = new(peakLuminance * (1F / PqCompanding.ReferenceLuminance));
        this.PeakLuminance = peakLuminance;
    }

    /// <summary>
    /// Gets the luminance represented by linear one, in cd/m².
    /// </summary>
    public float PeakLuminance { get; }

    /// <inheritdoc/>
    public override void Expand(Span<Vector4> vectors)
        => CompandingUtilities.Compand(vectors, this.expand, PixelColorRange.Extended);

    /// <inheritdoc/>
    public override void Expand(Span<Vector3> vectors)
        => CompandingUtilities.Compand(vectors, this.expand, PixelColorRange.Extended);

    /// <inheritdoc/>
    public override Vector4 Expand(Vector4 vector)
        => CompandingUtilities.Compand(vector, this.expand, PixelColorRange.Extended);

    /// <inheritdoc/>
    public override void Compress(Span<Vector4> vectors)
        => CompandingUtilities.Compand(vectors, this.compress, PixelColorRange.Extended);

    /// <inheritdoc/>
    public override void Compress(Span<Vector3> vectors)
        => CompandingUtilities.Compand(vectors, this.compress, PixelColorRange.Extended);

    /// <inheritdoc/>
    public override Vector4 Compress(Vector4 vector)
        => CompandingUtilities.Compand(vector, this.compress, PixelColorRange.Extended);

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is PqWorkingSpace other
        && this.PeakLuminance.Equals(other.PeakLuminance)
        && this.WhitePoint.Equals(other.WhitePoint)
        && this.ChromaticityCoordinates.Equals(other.ChromaticityCoordinates);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(
        typeof(PqWorkingSpace),
        this.WhitePoint,
        this.ChromaticityCoordinates,
        this.PeakLuminance);
}
