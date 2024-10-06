// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal static class Av1TransformTypeExtensions
{
    private static readonly Av1TransformClass[] Type2Class = [
        Av1TransformClass.Class2D, // DCT_DCT
        Av1TransformClass.Class2D, // ADST_DCT
        Av1TransformClass.Class2D, // DCT_ADST
        Av1TransformClass.Class2D, // ADST_ADST
        Av1TransformClass.Class2D, // FLIPADST_DCT
        Av1TransformClass.Class2D, // DCT_FLIPADST
        Av1TransformClass.Class2D, // FLIPADST_FLIPADST
        Av1TransformClass.Class2D, // ADST_FLIPADST
        Av1TransformClass.Class2D, // FLIPADST_ADST
        Av1TransformClass.Class2D, // IDTX
        Av1TransformClass.ClassVertical, // V_DCT
        Av1TransformClass.ClassHorizontal, // H_DCT
        Av1TransformClass.ClassVertical, // V_ADST
        Av1TransformClass.ClassHorizontal, // H_ADST
        Av1TransformClass.ClassVertical, // V_FLIPADST
        Av1TransformClass.ClassHorizontal, // H_FLIPADST
    ];

    private static readonly bool[][] ExtendedTransformUsed = [
        [true, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false],
        [true, false, false, false, false, false, false, false, false, true, false, false, false, false, false, false],
        [true, true, true, true, false, false, false, false, false, true, false, false, false, false, false, false],
        [true, true, true, true, false, false, false, false, false, true, true, true, false, false, false, false],
        [true, true, true, true, true, true, true, true, true, true, true, true, false, false, false, false],
        [true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true],
    ];

    public static Av1TransformClass ToClass(this Av1TransformType transformType) => Type2Class[(int)transformType];

    public static bool IsExtendedSetUsed(this Av1TransformType transformType, Av1TransformSetType setType)
        => ExtendedTransformUsed[(int)setType][(int)transformType];
}
