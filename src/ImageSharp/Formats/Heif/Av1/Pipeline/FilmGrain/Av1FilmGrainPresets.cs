// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.FilmGrain;

/// <summary>
/// The sixteen film grain parameter sets that emulate different types of film grain. The parameters are valid for
/// any bit depth. Reference: film_grain_test_vectors.
/// </summary>
internal static class Av1FilmGrainPresets
{
    /// <summary>
    /// The number of presets.
    /// </summary>
    public const int Count = 16;

    /// <summary>
    /// Loads a preset into a film grain parameter set.
    /// </summary>
    /// <param name="preset">The preset, from 1 to 16.</param>
    /// <param name="parameters">The parameters that receive the preset.</param>
    /// <exception cref="ArgumentOutOfRangeException">The preset is outside the range 1 to 16.</exception>
    public static void Load(int preset, ObuFilmGrainParameters parameters)
    {
        switch (preset)
        {
            case 1:
                Set(
                    parameters,
                    true,
                    [16, 0, 25, 136, 33, 144, 41, 160, 48, 168, 56, 136, 67, 128, 82, 144, 97, 152, 113, 144, 128, 176, 143, 168, 158, 176, 178, 184],
                    14,
                    [16, 0, 20, 64, 28, 88, 60, 104, 90, 136, 105, 160, 134, 168, 168, 208],
                    8,
                    [16, 0, 28, 96, 56, 80, 66, 96, 80, 104, 108, 96, 122, 112, 137, 112, 169, 176],
                    9,
                    11,
                    2,
                    [0, 0, -58, 0, 0, 0, -76, 100, -43, 0, -51, 82],
                    [0, 0, -49, 0, 0, 0, -36, 22, -30, 0, -38, 7, 39],
                    [0, 0, -47, 0, 0, 0, -31, 31, -25, 0, -32, 13, -100],
                    8,
                    [247, 192, 18, 229, 192, 54],
                    false,
                    true,
                    false,
                    0,
                    45231);

                break;
            case 2:
                Set(
                    parameters,
                    true,
                    [0, 96, 255, 96],
                    2,
                    [0, 64, 255, 64],
                    2,
                    [0, 64, 255, 64],
                    2,
                    11,
                    3,
                    [4, 1, 3, 0, 1, -3, 8, -3, 7, -23, 1, -25, 0, -10, 6, -17, -4, 53, 36, 5, -5, -17, 8, 66],
                    [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 127],
                    [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 127],
                    7,
                    [128, 192, 256, 128, 192, 256],
                    true,
                    false,
                    false,
                    0,
                    45231);

                break;
            case 3:
                Set(
                    parameters,
                    true,
                    [0, 192, 255, 192],
                    2,
                    [0, 128, 255, 128],
                    2,
                    [0, 128, 255, 128],
                    2,
                    11,
                    3,
                    [4, 1, 3, 0, 1, -3, 8, -3, 7, -23, 1, -25, 0, -10, 6, -17, -4, 53, 36, 5, -5, -17, 8, 66],
                    [4, -7, 2, 4, 12, -12, 5, -8, 6, 8, -19, -16, 19, -10, -2, 17, -42, 58, -2, -13, 9, 14, -36, 67, 0],
                    [4, -7, 2, 4, 12, -12, 5, -8, 6, 8, -19, -16, 19, -10, -2, 17, -42, 58, -2, -13, 9, 14, -36, 67, 0],
                    7,
                    [128, 192, 256, 128, 192, 256],
                    true,
                    true,
                    false,
                    1,
                    45231);

                break;
            case 4:
                Set(
                    parameters,
                    true,
                    [16, 0, 24, 137, 53, 146, 63, 155, 78, 155, 107, 150, 122, 147, 136, 147, 166, 153],
                    9,
                    [16, 0, 20, 72, 27, 82, 33, 91, 69, 121, 95, 143, 108, 154, 134, 169, 147, 177],
                    9,
                    [16, 0, 24, 95, 54, 93, 65, 94, 79, 98, 109, 107, 124, 119, 139, 136, 169, 170],
                    9,
                    11,
                    3,
                    [7, -9, 2, 4, 7, -12, 7, -18, 18, -30, -27, -42, 13, -20, 7, -18, 6, 107, 55, -2, -4, -9, -22, 113],
                    [-3, -1, -4, 3, -6, -2, 3, 1, -4, -10, -10, -5, -5, -3, -1, -13, -28, -25, -31, -6, -4, 14, -64, 66, 0],
                    [0, 4, -3, 13, 0, 1, -3, 0, -3, -10, -68, -4, -2, -5, 2, -3, -20, 62, -31, 0, -4, -1, -8, -29, 0],
                    8,
                    [128, 192, 256, 128, 192, 256],
                    true,
                    false,
                    false,
                    0,
                    45231);

                break;
            case 5:
                Set(
                    parameters,
                    false,
                    [0, 64, 255, 64],
                    2,
                    [0, 96, 32, 90, 64, 83, 96, 76, 128, 68, 159, 59, 191, 48, 223, 34, 255, 0],
                    9,
                    [0, 0, 32, 34, 64, 48, 96, 59, 128, 68, 159, 76, 191, 83, 223, 90, 255, 96],
                    9,
                    11,
                    3,
                    [4, 1, 3, 0, 1, -3, 8, -3, 7, -23, 1, -25, 0, -10, 6, -17, -4, 53, 36, 5, -5, -17, 8, 66],
                    [-2, 2, -5, 7, -6, 4, -2, -1, 1, -2, 0, -2, 2, -3, -5, 13, -13, 6, -14, 8, -1, 18, -36, 58, 0],
                    [-2, -1, -3, 14, -4, -1, -3, 0, -1, 7, -31, 7, 2, 0, 1, 0, -7, 50, -8, -2, 2, 2, 2, -4, 0],
                    7,
                    [128, 192, 256, 128, 192, 256],
                    true,
                    true,
                    false,
                    0,
                    1063);

                break;
            case 6:
                Set(
                    parameters,
                    true,
                    [0, 96, 20, 92, 39, 88, 59, 84, 78, 80, 98, 75, 118, 70, 137, 65, 157, 60, 177, 53, 196, 46, 216, 38, 235, 27, 255, 0],
                    14,
                    [],
                    0,
                    [],
                    0,
                    11,
                    3,
                    [4, 1, 3, 0, 1, -3, 8, -3, 7, -23, 1, -25, 0, -10, 6, -17, -4, 53, 36, 5, -5, -17, 8, 66],
                    [],
                    [],
                    7,
                    [128, 192, 256, 128, 192, 256],
                    true,
                    true,
                    false,
                    0,
                    2754);

                break;
            case 7:
                Set(
                    parameters,
                    true,
                    [0, 0, 20, 27, 39, 38, 59, 46, 78, 53, 98, 60, 118, 65, 137, 70, 157, 75, 177, 80, 196, 84, 216, 88, 235, 92, 255, 96],
                    14,
                    [0, 0, 255, 0],
                    2,
                    [0, 0, 255, 0],
                    2,
                    11,
                    3,
                    [4, 1, 3, 0, 1, -3, 8, -3, 7, -23, 1, -25, 0, -10, 6, -17, -4, 53, 36, 5, -5, -17, 8, 66],
                    [],
                    [],
                    7,
                    [128, 192, 256, 128, 192, 256],
                    true,
                    true,
                    false,
                    0,
                    45231);

                break;
            case 8:
                Set(
                    parameters,
                    true,
                    [0, 96, 255, 96],
                    2,
                    [0, 62, 255, 62],
                    2,
                    [0, 62, 255, 62],
                    2,
                    11,
                    3,
                    [4, 1, 3, 0, 1, -3, 8, -3, 7, -23, 1, -25, 0, -10, 6, -17, -4, 53, 36, 5, -5, -17, 8, 66],
                    [0, -2, -2, 8, 5, -1, 1, -1, 5, 16, -33, -9, 6, -1, -3, 10, -47, 63, 0, -15, 3, 11, -42, 75, -69],
                    [1, -1, -1, 9, 5, 0, 1, -1, 5, 15, -32, -10, 8, -2, -4, 11, -46, 62, 1, -16, 3, 13, -43, 75, -55],
                    7,
                    [128, 192, 256, 128, 192, 256],
                    true,
                    false,
                    false,
                    0,
                    45231);

                break;
            case 9:
                Set(
                    parameters,
                    false,
                    [0, 48, 255, 48],
                    2,
                    [0, 32, 255, 32],
                    2,
                    [0, 32, 255, 32],
                    2,
                    10,
                    2,
                    [10, -30, -20, -39, 1, -24, 12, 103, 60, -9, -24, 113],
                    [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 127],
                    [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 127],
                    8,
                    [128, 192, 256, 128, 192, 256],
                    true,
                    false,
                    false,
                    0,
                    45231);

                break;
            case 10:
                Set(
                    parameters,
                    true,
                    [0, 48, 255, 48],
                    2,
                    [0, 32, 255, 32],
                    2,
                    [0, 32, 255, 32],
                    2,
                    10,
                    2,
                    [10, -30, -20, -39, 1, -24, 12, 103, 60, -9, -24, 113],
                    [-7, -6, -48, -22, 2, -3, -45, 73, -11, -26, -52, 76, 0],
                    [-7, -6, -48, -22, 2, -3, -45, 73, -11, -26, -52, 76, 0],
                    8,
                    [128, 192, 256, 128, 192, 256],
                    true,
                    false,
                    false,
                    0,
                    45231);

                break;
            case 11:
                Set(
                    parameters,
                    false,
                    [0, 32, 255, 32],
                    2,
                    [0, 48, 32, 45, 64, 42, 96, 38, 128, 34, 159, 29, 191, 24, 223, 17, 255, 0],
                    9,
                    [0, 0, 32, 17, 64, 24, 96, 29, 128, 34, 159, 38, 191, 42, 223, 45, 255, 48],
                    9,
                    10,
                    3,
                    [7, -9, 2, 4, 7, -12, 7, -18, 18, -30, -27, -42, 13, -20, 7, -18, 6, 107, 55, -2, -4, -9, -22, 113],
                    [-3, -1, -4, 3, -6, -2, 3, 1, -4, -10, -10, -5, -5, -3, -1, -13, -28, -25, -31, -6, -4, 14, -64, 66, 0],
                    [0, 4, -3, 13, 0, 1, -3, 0, -3, -10, -68, -4, -2, -5, 2, -3, -20, 62, -31, 0, -4, -1, -8, -29, 0],
                    8,
                    [128, 192, 256, 128, 192, 256],
                    true,
                    true,
                    false,
                    0,
                    1357);

                break;
            case 12:
                Set(
                    parameters,
                    true,
                    [16, 0, 24, 49, 39, 69, 46, 84, 53, 91, 63, 100, 78, 114, 92, 134, 164, 139],
                    9,
                    [16, 0, 20, 31, 26, 42, 33, 54, 40, 65, 47, 72, 56, 85, 84, 123, 152, 157],
                    9,
                    [16, 0, 25, 14, 39, 33, 47, 40, 54, 47, 64, 62, 79, 76, 94, 83, 167, 101],
                    9,
                    10,
                    2,
                    [0, 0, -58, 0, 0, 0, -76, 100, -43, 0, -51, 82],
                    [0, 0, -49, 0, 0, 0, -36, 22, -30, 0, -38, 7, 39],
                    [0, 0, -47, 0, 0, 0, -31, 31, -25, 0, -32, 13, -100],
                    8,
                    [128, 192, 256, 128, 192, 256],
                    false,
                    false,
                    false,
                    0,
                    45231);

                break;
            case 13:
                Set(
                    parameters,
                    true,
                    [0, 48, 20, 46, 39, 44, 59, 42, 78, 40, 98, 38, 118, 35, 137, 33, 157, 30, 177, 27, 196, 23, 216, 19, 235, 13, 255, 0],
                    14,
                    [],
                    0,
                    [],
                    0,
                    10,
                    2,
                    [10, -30, -20, -39, 1, -24, 12, 103, 60, -9, -24, 113],
                    [],
                    [],
                    8,
                    [128, 192, 256, 128, 192, 256],
                    true,
                    false,
                    false,
                    0,
                    45231);

                break;
            case 14:
                Set(
                    parameters,
                    true,
                    [0, 0, 20, 13, 39, 19, 59, 23, 78, 27, 98, 30, 118, 33, 137, 35, 157, 38, 177, 40, 196, 42, 216, 44, 235, 46, 255, 48],
                    14,
                    [],
                    0,
                    [],
                    0,
                    10,
                    2,
                    [10, -30, -20, -39, 1, -24, 12, 103, 60, -9, -24, 113],
                    [],
                    [],
                    8,
                    [128, 192, 256, 128, 192, 256],
                    true,
                    true,
                    false,
                    0,
                    45231);

                break;
            case 15:
                Set(
                    parameters,
                    true,
                    [0, 96],
                    1,
                    [],
                    0,
                    [],
                    0,
                    11,
                    2,
                    [5, -15, -10, -19, 0, -12, 6, 51, 30, -5, -12, 56],
                    [2, 2, -24, -5, 1, 1, -18, 37, -2, 0, -15, 39, -70],
                    [2, 3, -24, -5, -1, 0, -18, 38, -2, 0, -15, 39, -55],
                    7,
                    [128, 192, 256, 128, 192, 256],
                    true,
                    false,
                    true,
                    0,
                    45231);

                break;
            case 16:
                Set(
                    parameters,
                    true,
                    [16, 0, 58, 126, 87, 120, 97, 122, 112, 125, 126, 131, 141, 139, 199, 153],
                    8,
                    [16, 0, 59, 68, 66, 76, 73, 82, 79, 85, 86, 86, 151, 95, 192, 101],
                    8,
                    [16, 0, 59, 64, 89, 80, 99, 86, 114, 90, 129, 93, 144, 97, 203, 85],
                    8,
                    10,
                    3,
                    [4, 1, 3, 0, 1, -3, 8, -3, 7, -23, 1, -25, 0, -10, 6, -17, -4, 53, 36, 5, -5, -17, 8, 66],
                    [0, -2, -2, 8, 5, -1, 1, -1, 5, 16, -33, -9, 6, -1, -3, 10, -47, 63, 0, -15, 3, 11, -42, 75, -69],
                    [1, -1, -1, 9, 5, 0, 1, -1, 5, 15, -32, -10, 8, -2, -4, 11, -46, 62, 1, -16, 3, 13, -43, 75, -55],
                    7,
                    [128, 192, 256, 128, 192, 256],
                    true,
                    false,
                    false,
                    2,
                    45231);

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(preset), preset, "The film grain preset must be from 1 to 16.");
        }
    }

    /// <summary>
    /// Sets every field of a parameter set in the order of the preset table. Every preset applies grain, and the
    /// scaling points are value and scaling pairs.
    /// </summary>
    /// <param name="parameters">The parameters that receive the preset.</param>
    /// <param name="updateGrain">Whether an inter frame signals the parameters, rather than reusing a reference's.</param>
    /// <param name="lumaPoints">The luma scaling points as value and scaling pairs.</param>
    /// <param name="lumaPointCount">The number of luma points the preset uses, which may be fewer than listed.</param>
    /// <param name="blueDifferencePoints">The blue-difference scaling points as value and scaling pairs.</param>
    /// <param name="blueDifferencePointCount">The number of blue-difference points the preset uses.</param>
    /// <param name="redDifferencePoints">The red-difference scaling points as value and scaling pairs.</param>
    /// <param name="redDifferencePointCount">The number of red-difference points the preset uses.</param>
    /// <param name="scalingShift">The scaling shift, from 8 to 11.</param>
    /// <param name="autoRegressionLag">The autoregressive lag, from 0 to 3.</param>
    /// <param name="lumaCoefficients">The luma autoregressive coefficients; unlisted ones are zero.</param>
    /// <param name="blueDifferenceCoefficients">The blue-difference autoregressive coefficients.</param>
    /// <param name="redDifferenceCoefficients">The red-difference autoregressive coefficients.</param>
    /// <param name="coefficientShift">The autoregressive coefficient shift, from 6 to 9.</param>
    /// <param name="chromaMultipliers">
    /// The blue-difference multiplier, luma multiplier and offset, then the same three for red-difference.
    /// </param>
    /// <param name="overlap">Whether the grain blocks overlap.</param>
    /// <param name="clipToRestrictedRange">Whether the grained samples are clipped to the restricted range.</param>
    /// <param name="chromaScalingFromLuma">Whether the chroma scaling follows the luma scaling function.</param>
    /// <param name="grainScaleShift">The grain scale shift, from 0 to 3.</param>
    /// <param name="randomSeed">The first random seed of the sequence.</param>
    private static void Set(
        ObuFilmGrainParameters parameters,
        bool updateGrain,
        ReadOnlySpan<byte> lumaPoints,
        int lumaPointCount,
        ReadOnlySpan<byte> blueDifferencePoints,
        int blueDifferencePointCount,
        ReadOnlySpan<byte> redDifferencePoints,
        int redDifferencePointCount,
        int scalingShift,
        int autoRegressionLag,
        ReadOnlySpan<sbyte> lumaCoefficients,
        ReadOnlySpan<sbyte> blueDifferenceCoefficients,
        ReadOnlySpan<sbyte> redDifferenceCoefficients,
        int coefficientShift,
        ReadOnlySpan<short> chromaMultipliers,
        bool overlap,
        bool clipToRestrictedRange,
        bool chromaScalingFromLuma,
        int grainScaleShift,
        ushort randomSeed)
    {
        parameters.ApplyGrain = true;
        parameters.UpdateGrain = updateGrain;
        parameters.NumYPoints = (uint)lumaPointCount;
        SetPoints(lumaPoints, lumaPointCount, parameters.PointYValue, parameters.PointYScaling);
        parameters.NumCbPoints = (uint)blueDifferencePointCount;
        SetPoints(blueDifferencePoints, blueDifferencePointCount, parameters.PointCbValue, parameters.PointCbScaling);
        parameters.NumCrPoints = (uint)redDifferencePointCount;
        SetPoints(redDifferencePoints, redDifferencePointCount, parameters.PointCrValue, parameters.PointCrScaling);
        parameters.GrainScalingMinus8 = (uint)(scalingShift - 8);
        parameters.ArCoeffLag = (uint)autoRegressionLag;
        SetCoefficients(lumaCoefficients, parameters.ArCoeffsYPlus128);
        SetCoefficients(blueDifferenceCoefficients, parameters.ArCoeffsCbPlus128);
        SetCoefficients(redDifferenceCoefficients, parameters.ArCoeffsCrPlus128);
        parameters.ArCoeffShiftMinus6 = (uint)(coefficientShift - 6);
        parameters.CbMult = (uint)chromaMultipliers[0];
        parameters.CbLumaMult = (uint)chromaMultipliers[1];
        parameters.CbOffset = (uint)chromaMultipliers[2];
        parameters.CrMult = (uint)chromaMultipliers[3];
        parameters.CrLumaMult = (uint)chromaMultipliers[4];
        parameters.CrOffset = (uint)chromaMultipliers[5];
        parameters.OverlapFlag = overlap;
        parameters.ClipToRestrictedRange = clipToRestrictedRange;
        parameters.ChromaScalingFromLuma = chromaScalingFromLuma;
        parameters.GrainScaleShift = (uint)grainScaleShift;
        parameters.GrainSeed = randomSeed;
    }

    /// <summary>
    /// Copies the first scaling points of a preset, and clears the rest.
    /// </summary>
    /// <param name="points">The scaling points as value and scaling pairs.</param>
    /// <param name="count">The number of points to copy.</param>
    /// <param name="values">The point values that receive the points.</param>
    /// <param name="scalings">The point scalings that receive the points.</param>
    private static void SetPoints(ReadOnlySpan<byte> points, int count, Span<byte> values, Span<byte> scalings)
    {
        values.Clear();
        scalings.Clear();
        for (int i = 0; i < count; i++)
        {
            values[i] = points[2 * i];
            scalings[i] = points[(2 * i) + 1];
        }
    }

    /// <summary>
    /// Copies the autoregressive coefficients of a preset with their offset of 128. Unlisted coefficients are zero.
    /// </summary>
    /// <param name="coefficients">The signed coefficients of the preset.</param>
    /// <param name="plus128">The coefficients that receive the values, each plus 128.</param>
    private static void SetCoefficients(ReadOnlySpan<sbyte> coefficients, Span<byte> plus128)
    {
        plus128.Fill(128);
        for (int i = 0; i < coefficients.Length; i++)
        {
            plus128[i] = (byte)(coefficients[i] + 128);
        }
    }
}
