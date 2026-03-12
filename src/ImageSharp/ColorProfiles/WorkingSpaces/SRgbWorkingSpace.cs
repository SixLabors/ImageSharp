// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles.Companding;

namespace SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;

/// <summary>
/// The sRgb working space.
/// </summary>
public sealed class SRgbWorkingSpace : RgbWorkingSpace
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SRgbWorkingSpace" /> class.
    /// </summary>
    /// <param name="referenceWhite">The reference white point.</param>
    /// <param name="chromaticityCoordinates">The chromaticity of the rgb primaries.</param>
    public SRgbWorkingSpace(CieXyz referenceWhite, RgbPrimariesChromaticityCoordinates chromaticityCoordinates)
        : base(referenceWhite, chromaticityCoordinates)
    {
    }

    /// <inheritdoc/>
    public override void Compress(Span<Vector4> vectors) => SRgbCompanding.Compress(vectors);

    /// <inheritdoc/>
    public override void Expand(Span<Vector4> vectors) => SRgbCompanding.Expand(vectors);

    /// <inheritdoc/>
    public override Vector4 Compress(Vector4 vector) => SRgbCompanding.Compress(vector);

    /// <inheritdoc/>
    public override Vector4 Expand(Vector4 vector) => SRgbCompanding.Expand(vector);
}
