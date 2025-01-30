// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

// Inter modes are not defined here, as they do not apply to pictures.
internal enum Av1PredictionMode
{
    DC,
    Vertical,
    Horizontal,
    Directional45Degrees,
    Directional135Degrees,
    Directional113Degrees,
    Directional157Degrees,
    Directional203Degrees,
    Directional67Degrees,
    Smooth,
    SmoothVertical,
    SmoothHorizontal,
    Paeth,
    UvChromaFromLuma,
    IntraModeStart = DC,
    IntraModeEnd = Paeth + 1,
    IntraModes = Paeth,
    UvIntraModes = UvChromaFromLuma + 1,
    IntraInvalid = 25,
}
