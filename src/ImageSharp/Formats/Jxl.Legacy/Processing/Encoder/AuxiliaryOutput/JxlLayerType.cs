// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AuxiliaryOutput;

internal enum JxlLayerType : byte
{
    Header = 0,
    Toc,
    Dictionary,
    Splines,
    Noise,
    Quant,
    ModularTree,
    ModularGlobal,
    Dc,
    ModularDcGroup,
    ControlFields,
    Order,
    Ac,
    AcTokens,
    ModularAcGroup,
}
