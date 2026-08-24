// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Defines scalar reconstruction of an 8-bit AV1 intra-prediction block from its neighboring samples.
/// </summary>
internal interface IAv1Predictor
{
    /// <summary>
    /// Writes the predicted block using scalar 8-bit arithmetic.
    /// </summary>
    /// <param name="destination">The destination block, starting at its top-left sample.</param>
    /// <param name="stride">The distance, in samples, between destination rows.</param>
    /// <param name="above">The neighboring samples immediately above the block.</param>
    /// <param name="left">The neighboring samples immediately left of the block.</param>
    public void PredictScalar(Span<byte> destination, nuint stride, Span<byte> above, Span<byte> left);
}
