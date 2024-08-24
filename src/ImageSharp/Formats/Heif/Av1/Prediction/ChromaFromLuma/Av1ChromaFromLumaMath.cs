// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;

internal static class Av1ChromaFromLumaMath
{
    private const int Signs = 3;
    private const int AlphabetSizeLog2 = 4;

    public const int SignZero = 0;
    public const int SignNegative = 1;
    public const int SignPositive = 2;

    public static int SignU(int jointSign) => ((jointSign + 1) * 11) >> 5;

    public static int SignV(int jointSign) => (jointSign + 1) - (Signs * SignU(jointSign));

    public static int IndexU(int index) => index >> AlphabetSizeLog2;

    public static int IndexV(int index) => index & (AlphabetSizeLog2 - 1);

    public static int ContextU(int jointSign) => jointSign + 1 - Signs;

    public static int ContextV(int jointSign) => (SignV(jointSign) * Signs) + SignU(jointSign) - Signs;
}
