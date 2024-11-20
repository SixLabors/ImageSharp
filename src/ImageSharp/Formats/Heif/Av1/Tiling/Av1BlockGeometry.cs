// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal partial class Av1TileWriter
{
    internal class Av1BlockGeometry
    {
        public Av1BlockGeometry()
        {
            this.TransformOrigin = new Point[Av1Constants.MaxVarTransform + 1][];
            for (int i = 0; i < this.TransformOrigin.Length; i++)
            {
                this.TransformOrigin[i] = new Point[Av1Constants.MaxTransformBlockCount];
            }
        }

        public Av1BlockSize BlockSize { get; internal set; }

        public Av1BlockSize BlockSizeUv { get; internal set; }

        /// <summary>
        /// Gets or sets the Origin point from lop left of the superblock.
        /// </summary>
        public Point Origin { get; internal set; }

        public bool HasUv { get; internal set; }

        /// <summary>
        /// Gets or sets the blocks width.
        /// </summary>
        public int BlockWidth { get; internal set; }

        /// <summary>
        /// Gets or sets the blocks height.
        /// </summary>
        public int BlockHeight { get; internal set; }

        public int[] TransformBlockCount { get; } = new int[Av1Constants.MaxVarTransform + 1];

        public Av1TransformSize[] TransformSize { get; } = new Av1TransformSize[Av1Constants.MaxVarTransform + 1];

        public Av1TransformSize[] TransformSizeUv { get; } = new Av1TransformSize[Av1Constants.MaxVarTransform + 1];

        public Point[][] TransformOrigin { get; private set; }

        /// <summary>
        /// Gets or sets the block index in md scan.
        /// </summary>
        public int BlockIndex { get; set; }
    }
}
