// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding;

internal enum JxlMaTreeContext : byte
{
    SplitValue,

    Property,

    Predictor,

    Offset,

    MultiplierLog,

    MultiplierBits
}
