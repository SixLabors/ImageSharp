// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Provides AV1 transform-class and transform-set lookups for compound transform types.
/// </summary>
internal static class Av1TransformTypeExtensions
{
    /// <summary>
    /// Maps each compound transform type to its entropy-context transform class.
    /// </summary>
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

    /// <summary>
    /// Indicates which compound transform types are enabled by each transform-set type.
    /// </summary>
    private static readonly bool[][] ExtendedTransformUsed = [
        [true, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false],
        [true, false, false, false, false, false, false, false, false, true, false, false, false, false, false, false],
        [true, true, true, true, false, false, false, false, false, true, false, false, false, false, false, false],
        [true, true, true, true, false, false, false, false, false, true, true, true, false, false, false, false],
        [true, true, true, true, true, true, true, true, true, true, true, true, false, false, false, false],
        [true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true],
    ];

    /// <summary>
    /// Gets the entropy-context class of a compound transform type.
    /// </summary>
    /// <param name="transformType">The compound transform type.</param>
    /// <returns>The two-dimensional, horizontal, or vertical transform class.</returns>
    public static Av1TransformClass ToClass(this Av1TransformType transformType) => Type2Class[(int)transformType];

    /// <summary>
    /// Determines whether a compound transform type belongs to an allowed transform set.
    /// </summary>
    /// <param name="transformType">The compound transform type.</param>
    /// <param name="setType">The allowed transform set.</param>
    /// <returns><see langword="true"/> when the transform is enabled by the set.</returns>
    public static bool IsExtendedSetUsed(this Av1TransformType transformType, Av1TransformSetType setType)
        => ExtendedTransformUsed[(int)setType][(int)transformType];
}
