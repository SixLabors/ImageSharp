// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

internal static class JxlOpsinInverseParameters
{
    private const float M02 = 0.078f;
    private const float M00 = 0.30f;
    private const float M01 = 1.0f - M02 - M00;

    private const float M12 = 0.078f;
    private const float M10 = 0.23f;
    private const float M11 = 1.0f - M12 - M10;

    private const float M20 = 0.24342268924547819f;
    private const float M21 = 0.20476744424496821f;
    private const float M22 = 1.0f - M20 - M21;

    private static readonly float[][] Matrix =
    [
        [M00, M01, M02],
        [M10, M11, M12],
        [M20, M21, M22]
    ];

    public static JxlMatrix3x3F GetOpsinAbsorbanceInverseMatrix()
    {
        JxlMatrix3x3F matrix = new(Matrix);
        _ = JxlMatrix3x3F.Invert(ref matrix);
        return matrix;
    }
}
