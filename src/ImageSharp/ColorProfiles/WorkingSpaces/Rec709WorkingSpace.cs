// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles.Companding;

namespace SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;

/// <summary>
/// Rec. 709 (ITU-R Recommendation BT.709) working space.
/// </summary>
public sealed class Rec709WorkingSpace : RgbWorkingSpace
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Rec709WorkingSpace" /> class.
    /// </summary>
    /// <param name="referenceWhite">The reference white point.</param>
    /// <param name="chromaticityCoordinates">The chromaticity of the rgb primaries.</param>
    public Rec709WorkingSpace(CieXyz referenceWhite, RgbPrimariesChromaticityCoordinates chromaticityCoordinates)
        : base(referenceWhite, chromaticityCoordinates)
    {
    }

    /// <inheritdoc/>
    public override void Compress(Span<Vector4> vectors) => Rec709Companding.Compress(vectors);

    /// <inheritdoc/>
    public override void Expand(Span<Vector4> vectors) => Rec709Companding.Expand(vectors);

    /// <inheritdoc/>
    public override Vector4 Compress(Vector4 vector) => Rec709Companding.Compress(vector);

    /// <inheritdoc/>
    public override Vector4 Expand(Vector4 vector) => Rec709Companding.Expand(vector);
}
