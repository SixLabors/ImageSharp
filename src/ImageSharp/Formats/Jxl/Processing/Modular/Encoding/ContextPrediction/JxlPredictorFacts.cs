// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;

/// <summary>
/// Tools and constants for predictor types.
/// </summary>
internal static class JxlPredictorFacts
{
    /// <summary>
    /// Number of modular predictors.
    /// </summary>
    public const int ModularPredictors = (int)JxlPredictor.Average4 + 1;

    /// <summary>
    /// Nearest power of 2 of modular predictors.
    /// </summary>
    public const int ModularPredictorsAlignment = 16;

    /// <summary>
    /// Number of modular encoder predictors.
    /// </summary>
    public const int ModularEncoderPredictors = (int)JxlPredictor.Variable + 1;

    /// <summary>
    /// Number of static properties.
    /// </summary>
    public const int StaticProperties = 2;
}
