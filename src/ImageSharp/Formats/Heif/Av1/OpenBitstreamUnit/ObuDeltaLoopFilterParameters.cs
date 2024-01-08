// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuDeltaLoopFilterParameters
{
    public bool IsPresent { get; internal set; }

    public int Resolution { get; internal set; }

    public bool Multi { get; internal set; }
}
