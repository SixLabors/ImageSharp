// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;

/// <summary>
/// Defines the type of the predictor using neighboring
/// pixels, similar to intra prediction used in codecs like
/// VP8, AV1 and H.264.
/// </summary>
internal enum JxlPredictor : uint
{
    Zero = 0,
    Left = 1,
    Top = 2,
    Average0 = 3,
    Select = 4,
    Gradient = 5,
    Weighted = 6,
    TopRight = 7,
    TopLeft = 8,
    LeftLeft = 9,
    Average1 = 10,
    Average2 = 11,
    Average3 = 12,
    Average4 = 13,
    Best = 14,
    Variable = 15,

    /// <summary>
    /// Undefined predictor.
    /// </summary>
    Undefined = ~0u
}
