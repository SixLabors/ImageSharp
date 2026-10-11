// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;

/// <summary>
/// MA tree lookup result
/// </summary>
internal record struct JxlMaTreeLookupResult(uint Context, JxlPredictor Predictor, int Offset, int Multiplier);
