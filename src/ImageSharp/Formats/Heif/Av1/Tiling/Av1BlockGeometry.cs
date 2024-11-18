// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal partial class Av1TileWriter
{
    internal class Av1BlockGeometry
    {
        public Av1BlockSize BlockSize { get; internal set; }

        public Point Origin { get; internal set; }

        public bool HasUv { get; internal set; }

        public int BlockWidth { get; internal set; }

        public int BlockHeight { get; internal set; }

        public required Av1TransformSize[] TransformSize { get; internal set; }
    }
}
