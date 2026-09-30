// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;

/// <summary>
/// Information about a multiplier for Modular.
/// </summary>
/// <param name="Range">
/// A static property range, with each item containing channel and group ID.
/// </param>
/// <param name="Multiplier">
/// The multiplier.
/// </param>
internal record struct JxlModularMultiplierInfo(
    InlineArray2<InlineArray2<uint>> Range,
    uint Multiplier);
