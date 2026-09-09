// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;

/// <summary>
/// Provides AV1 chroma-from-luma sign, magnitude-index, and entropy-context mappings.
/// </summary>
internal static class Av1ChromaFromLumaMath
{
    /// <summary>
    /// The number of alpha sign states: zero, negative, and positive.
    /// </summary>
    private const int Signs = 3;

    /// <summary>
    /// The number of bits occupied by each plane's packed alpha-magnitude index.
    /// </summary>
    private const int AlphabetSizeLog2 = 4;

    /// <summary>
    /// The number of nonzero alpha magnitudes represented by each plane's alphabet.
    /// </summary>
    public const int AlphaMagnitudeCount = 1 << AlphabetSizeLog2;

    /// <summary>
    /// The number of signed alpha candidates including zero.
    /// </summary>
    public const int AlphaCandidateCount = (AlphaMagnitudeCount * 2) + 1;

    /// <summary>
    /// The candidate index representing a zero alpha.
    /// </summary>
    public const int AlphaZeroIndex = AlphaMagnitudeCount;

    /// <summary>
    /// The alpha sign value representing a zero multiplier.
    /// </summary>
    public const int SignZero = 0;

    /// <summary>
    /// The alpha sign value representing a negative multiplier.
    /// </summary>
    public const int SignNegative = 1;

    /// <summary>
    /// The alpha sign value representing a positive multiplier.
    /// </summary>
    public const int SignPositive = 2;

    /// <summary>
    /// Extracts the U-plane sign from a joint chroma sign symbol.
    /// </summary>
    /// <param name="jointSign">The coded joint U/V sign symbol.</param>
    /// <returns>The U-plane sign state.</returns>
    public static int SignU(int jointSign) => ((jointSign + 1) * 11) >> 5;

    /// <summary>
    /// Extracts the V-plane sign from a joint chroma sign symbol.
    /// </summary>
    /// <param name="jointSign">The coded joint U/V sign symbol.</param>
    /// <returns>The V-plane sign state.</returns>
    public static int SignV(int jointSign) => (jointSign + 1) - (Signs * SignU(jointSign));

    /// <summary>
    /// Extracts the U-plane alpha-magnitude index from the high four bits of the packed index.
    /// </summary>
    /// <param name="index">The packed U/V alpha-magnitude index.</param>
    /// <returns>The U-plane magnitude index.</returns>
    public static int IndexU(int index) => index >> AlphabetSizeLog2;

    /// <summary>
    /// Extracts the V-plane alpha-magnitude index from the low four bits of the packed index.
    /// </summary>
    /// <param name="index">The packed U/V alpha-magnitude index.</param>
    /// <returns>The V-plane magnitude index.</returns>
    public static int IndexV(int index) => index & ((1 << AlphabetSizeLog2) - 1);

    /// <summary>
    /// Maps a joint sign symbol to the entropy context used for the U-plane alpha magnitude.
    /// </summary>
    /// <param name="jointSign">The coded joint U/V sign symbol.</param>
    /// <returns>The U-plane alpha entropy context.</returns>
    public static int ContextU(int jointSign) => jointSign + 1 - Signs;

    /// <summary>
    /// Maps a joint sign symbol to the symmetric entropy context used for the V-plane alpha magnitude.
    /// </summary>
    /// <param name="jointSign">The coded joint U/V sign symbol.</param>
    /// <returns>The V-plane alpha entropy context.</returns>
    public static int ContextV(int jointSign) => (SignV(jointSign) * Signs) + SignU(jointSign) - Signs;

    /// <summary>
    /// Converts a signed-candidate index to its alpha value in Q3 units.
    /// </summary>
    /// <param name="candidateIndex">The candidate index in negative-to-positive order.</param>
    /// <returns>The signed Q3 alpha value.</returns>
    public static int CandidateIndexToAlpha(int candidateIndex) => candidateIndex - AlphaZeroIndex;

    /// <summary>
    /// Converts a signed Q3 alpha value to its coded sign state.
    /// </summary>
    /// <param name="alphaQ3">The signed alpha value.</param>
    /// <returns>The zero, negative, or positive sign state.</returns>
    public static int AlphaToSign(int alphaQ3)
        => alphaQ3 == 0 ? SignZero : alphaQ3 < 0 ? SignNegative : SignPositive;

    /// <summary>
    /// Converts a nonzero signed Q3 alpha value to its coded magnitude index.
    /// </summary>
    /// <param name="alphaQ3">The signed alpha value.</param>
    /// <returns>The zero-based magnitude index, or zero for a zero alpha.</returns>
    public static int AlphaToMagnitudeIndex(int alphaQ3) => alphaQ3 == 0 ? 0 : Math.Abs(alphaQ3) - 1;

    /// <summary>
    /// Combines the U and V sign states into the coded joint symbol.
    /// </summary>
    /// <param name="signU">The U-plane sign state.</param>
    /// <param name="signV">The V-plane sign state.</param>
    /// <returns>The joint sign symbol.</returns>
    public static int JointSign(int signU, int signV) => (signU * Signs) + signV - 1;

    /// <summary>
    /// Packs the U and V alpha-magnitude indices into the coded byte.
    /// </summary>
    /// <param name="indexU">The U-plane magnitude index.</param>
    /// <param name="indexV">The V-plane magnitude index.</param>
    /// <returns>The packed magnitude indices.</returns>
    public static int PackIndices(int indexU, int indexV) => (indexU << AlphabetSizeLog2) + indexV;
}
