// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Splines;

internal enum JxlSplineEntropyContext : byte
{
    QuantizationAdjustment,
    StartingPosition,
    NumSplines,
    NumControlPoints,
    ControlPoints,
    Dct,
    NumSplineContexts
}
