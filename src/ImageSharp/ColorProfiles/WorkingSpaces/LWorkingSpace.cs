// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles.Companding;

namespace SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;

/// <summary>
/// L* working space.
/// </summary>
public sealed class LWorkingSpace : RgbWorkingSpace
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LWorkingSpace" /> class.
    /// </summary>
    /// <param name="referenceWhite">The reference white point.</param>
    /// <param name="chromaticityCoordinates">The chromaticity of the rgb primaries.</param>
    public LWorkingSpace(CieXyz referenceWhite, RgbPrimariesChromaticityCoordinates chromaticityCoordinates)
        : base(referenceWhite, chromaticityCoordinates)
    {
    }

    /// <inheritdoc/>
    public override void Compress(Span<Vector4> vectors) => LCompanding.Compress(vectors);

    /// <inheritdoc/>
    public override void Expand(Span<Vector4> vectors) => LCompanding.Expand(vectors);

    /// <inheritdoc/>
    public override Vector4 Compress(Vector4 vector) => LCompanding.Compress(vector);

    /// <inheritdoc/>
    public override Vector4 Expand(Vector4 vector) => LCompanding.Expand(vector);
}
