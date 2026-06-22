// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuDeltaParameters
{
    public bool IsPresent { get; internal set; }

    public int Resolution { get; internal set; }

    public bool IsMulti { get; internal set; }
}
