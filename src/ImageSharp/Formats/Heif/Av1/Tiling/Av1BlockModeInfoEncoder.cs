// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal partial class Av1TileWriter
{
    internal class Av1BlockModeInfoEncoder
    {
        public Av1BlockSize BlockSize { get; }

        public Av1PredictionMode PredictionMode { get; }

        public Av1PartitionType PartitionType { get; }

        public Av1PredictionMode UvPredictionMode { get; }

        public bool Skip { get; } = true;

        public bool SkipMode { get; } = true;

        public bool UseIntraBlockCopy { get; } = true;

        public int SegmentId { get; }

        public int TransformDepth { get; internal set; }
    }
}
