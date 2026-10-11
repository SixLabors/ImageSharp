// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Modular;

internal enum JxlModularKind : byte
{
    GlobalData,
    VarDctDc,
    ModularDc,
    AcMetadata,
    QuantizerTable,
    ModularAc
}
