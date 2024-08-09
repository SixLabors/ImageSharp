// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuQuantizationParameters
{
    public int BaseQIndex { get; set; }

    public int[] QIndex { get; set; } = new int[Av1Constants.MaxSegmentCount];

    public bool IsUsingQMatrix { get; internal set; }

    public int[] DeltaQDc { get; internal set; } = new int[3];

    public int[] DeltaQAc { get; internal set; } = new int[3];

    public int[] QMatrix { get; internal set; } = new int[3];

    public bool HasSeparateUvDelta { get; internal set; }
}
