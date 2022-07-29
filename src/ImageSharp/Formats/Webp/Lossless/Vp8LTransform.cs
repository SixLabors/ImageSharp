// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Diagnostics;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless
{
    /// <summary>
    /// Data associated with a VP8L transformation to reduce the entropy.
    /// </summary>
    [DebuggerDisplay("Transformtype: {" + nameof(TransformType) + "}")]
    internal class Vp8LTransform
    {
        public Vp8LTransform(Vp8LTransformType transformType, int xSize, int ySize)
        {
            this.TransformType = transformType;
            this.XSize = xSize;
            this.YSize = ySize;
        }

        /// <summary>
        /// Gets the transform type.
        /// </summary>
        public Vp8LTransformType TransformType { get; }

        /// <summary>
        /// Gets or sets the subsampling bits defining the transform window.
        /// </summary>
        public int Bits { get; set; }

        /// <summary>
        /// Gets or sets the transform window X index.
        /// </summary>
        public int XSize { get; set; }

        /// <summary>
        /// Gets the transform window Y index.
        /// </summary>
        public int YSize { get; }

        /// <summary>
        /// Gets or sets the transform data.
        /// </summary>
        public IMemoryOwner<uint> Data { get; set; }
    }
}
