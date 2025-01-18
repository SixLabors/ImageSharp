// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Implementation of a specific forward 1-dimensional transform function.
/// </summary>
internal interface IAv1Transformer1d
{
    /// <summary>
    /// Execute the 1 dimensional transformation.
    /// </summary>
    /// <param name="input">Input pixels.</param>
    /// <param name="output">Output coefficients.</param>
    /// <param name="cosBit">The cosinus bit.</param>
    /// <param name="stageRange">Stage ranges.</param>
    void Transform(Span<int> input, Span<int> output, int cosBit, Span<byte> stageRange);
}
