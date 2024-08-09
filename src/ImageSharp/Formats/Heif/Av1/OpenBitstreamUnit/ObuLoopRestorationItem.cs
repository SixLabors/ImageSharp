// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuLoopRestorationItem
{
    internal int Size { get; set; }

    internal ObuRestorationType Type { get; set; } = ObuRestorationType.None;
}
