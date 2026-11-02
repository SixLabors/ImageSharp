// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;

/// <summary>
/// Kind of tree to use.
/// </summary>
// TODO: this enum wasn't documented
// https://github.com/libjxl/libjxl/blob/main/lib/jxl/modular/options.h#L100-L111
internal enum JxlTreeKind : byte
{
    TrivialTreeNoPredictor,
    Learn,
    JpegTranscodeAcMeta,
    FalconAcMeta,
    AcMeta,
    WpFixedDc,
    GradientFixedDc
}
