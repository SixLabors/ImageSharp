// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;

/// <summary>
/// Flags for context prediction.
/// </summary>
[Flags]
internal enum JxlPredictorMode : byte
{
    /// <summary>
    /// Should tree-based prediction be used?
    /// </summary>
    UseTree = 1,

    /// <summary>
    /// Should the weighted predictor be used?
    /// </summary>
    UseWeightedPrediction = 2,

    /// <summary>
    /// Should properties be computed? (When this bit is 0,
    /// the properties are not set and therefore have their
    /// default values)
    /// </summary>
    ForceComputeProperties = 4,

    /// <summary>
    /// Try all predictors?
    /// </summary>
    AllPredictions = 8,

    NoEdgeCases = 16
}
