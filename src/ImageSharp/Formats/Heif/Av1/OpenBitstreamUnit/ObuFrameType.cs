// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal enum ObuFrameType
{
    KeyFrame = 0,
    InterFrame = 1,
    IntraOnlyFrame = 2,
    SwitchFrame = 3,
}
