// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal class Av1BlockModeInfo
{
    public Av1BlockSize BlockSize { get; }

    public Av1PredictionMode PredictionMode { get; }

    public Av1PartitionType Partition { get; }

    public int SegmentId { get; }
}
