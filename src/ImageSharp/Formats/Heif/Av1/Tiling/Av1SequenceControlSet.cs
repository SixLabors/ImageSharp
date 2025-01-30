// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal class Av1SequenceControlSet
{
    public required ObuSequenceHeader SequenceHeader { get; internal set; }

    public int MaxBlockCount { get; internal set; }
}
