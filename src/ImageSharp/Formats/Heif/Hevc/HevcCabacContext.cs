// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Maintains the adaptive probability state for one HEVC context-coded binary syntax element.
/// </summary>
internal struct HevcCabacContext
{
    /// <summary>
    /// Maps each packed context state to the state that follows its most-probable symbol.
    /// </summary>
    private static readonly byte[] MostProbableStateTransitions =
    [
        2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17,
        18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33,
        34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49,
        50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65,
        66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81,
        82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 97,
        98, 99, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113,
        114, 115, 116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 124, 125, 126, 127
    ];

    /// <summary>
    /// Maps each packed context state to the state that follows its least-probable symbol.
    /// </summary>
    private static readonly byte[] LeastProbableStateTransitions =
    [
        1, 0, 0, 1, 2, 3, 4, 5, 4, 5, 8, 9, 8, 9, 10, 11,
        12, 13, 14, 15, 16, 17, 18, 19, 18, 19, 22, 23, 22, 23, 24, 25,
        26, 27, 26, 27, 30, 31, 30, 31, 32, 33, 32, 33, 36, 37, 36, 37,
        38, 39, 38, 39, 42, 43, 42, 43, 44, 45, 44, 45, 46, 47, 48, 49,
        48, 49, 50, 51, 52, 53, 52, 53, 54, 55, 54, 55, 56, 57, 58, 59,
        58, 59, 60, 61, 60, 61, 60, 61, 62, 63, 64, 65, 64, 65, 66, 67,
        66, 67, 66, 67, 68, 69, 68, 69, 70, 71, 70, 71, 70, 71, 72, 73,
        72, 73, 72, 73, 74, 75, 74, 75, 74, 75, 76, 77, 76, 77, 126, 127
    ];

    /// <summary>
    /// The packed probability-state index and most-probable-symbol value.
    /// </summary>
    private byte state;

    /// <summary>
    /// Initializes a new instance of the <see cref="HevcCabacContext"/> struct.
    /// </summary>
    /// <param name="quantizationParameter">The luma quantization parameter that selects the initial probability.</param>
    /// <param name="initializationValue">The syntax-element initialization value.</param>
    public HevcCabacContext(int quantizationParameter, byte initializationValue)
    {
        int clippedQuantizationParameter = Math.Clamp(quantizationParameter, 0, 51);
        int slope = ((initializationValue >> 4) * 5) - 45;
        int offset = ((initializationValue & 15) << 3) - 16;
        int initializationState = Math.Clamp(
            ((slope * clippedQuantizationParameter) >> 4) + offset,
            1,
            126);

        bool mostProbableSymbol = initializationState >= 64;
        this.state = (byte)(
            ((mostProbableSymbol ? initializationState - 64 : 63 - initializationState) << 1)
            + (mostProbableSymbol ? 1 : 0));
    }

    /// <summary>
    /// Gets the probability-state index used to select the least-probable-symbol range.
    /// </summary>
    public readonly int StateIndex => this.state >> 1;

    /// <summary>
    /// Gets a value indicating whether one is the current most-probable symbol.
    /// </summary>
    public readonly bool MostProbableSymbol => (this.state & 1) != 0;

    /// <summary>
    /// Advances the context after decoding its most-probable symbol.
    /// </summary>
    public void UpdateMostProbableSymbol() => this.state = MostProbableStateTransitions[this.state];

    /// <summary>
    /// Advances the context after decoding its least-probable symbol.
    /// </summary>
    public void UpdateLeastProbableSymbol() => this.state = LeastProbableStateTransitions[this.state];
}
