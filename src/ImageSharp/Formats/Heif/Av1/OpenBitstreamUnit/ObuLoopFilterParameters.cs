// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuLoopFilterParameters
{
    public int[] FilterLevel { get; internal set; } = new int[2];
    public int FilterLevelU { get; internal set; }
    public int FilterLevelV { get; internal set; }
    public int SharpnessLevel { get; internal set; }
    public bool ReferenceDeltaModeEnabled { get; internal set; }
}
