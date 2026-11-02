// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;

/// <summary>
/// The result of context prediction.
/// </summary>
/// <param name="Context">Context used in MA lookup.</param>
/// <param name="Guess">Predicted coefficient.</param>
/// <param name="Predictor">Kind of predictor mode used.</param>
/// <param name="Multiplier">Multiplier used in MA lookup.</param>
internal record struct JxlPredictionResult(int Context, int Guess, JxlPredictor Predictor, int Multiplier);
