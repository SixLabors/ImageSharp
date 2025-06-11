// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.ColorProfiles.Conversion.Icc;

internal partial class LutABCalculator
{
    private enum CalculationType
    {
        AtoB = 1 << 3,
        BtoA = 1 << 4,

        SingleCurve = 1,
        CurveMatrix = 2,
        CurveClut = 3,
        Full = 4,
    }
}
