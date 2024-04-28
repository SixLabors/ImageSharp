// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal enum ObuRestorationType : uint
{
    None = 0,
    Switchable = 1,
    Weiner = 2,
    SgrProj = 3,
}
