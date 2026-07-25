// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuHeader
{
    public int Size { get; set; }

    public ObuType Type { get; set; }

    public bool HasSize { get; set; }

    public bool HasExtension { get; set; }

    public int TemporalId { get; set; }

    public int SpatialId { get; set; }

    public int PayloadSize { get; set; }
}
