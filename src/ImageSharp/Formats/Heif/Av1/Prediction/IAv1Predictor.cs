// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Interface for predictor implementations.
/// </summary>
internal interface IAv1Predictor
{
    /// <summary>
    /// Predict using scalar logic within the 8-bit pipeline.
    /// </summary>
    /// <param name="destination">The destination to write to.</param>
    /// <param name="stride">The stride of the destination buffer.</param>
    /// <param name="above">Pointer to the first element of the block above.</param>
    /// <param name="left">Pointer to the first element of the block to the left.</param>
    public void PredictScalar(Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left);
}
