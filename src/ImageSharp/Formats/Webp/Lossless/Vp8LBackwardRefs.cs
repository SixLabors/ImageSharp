// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless
{
    internal class Vp8LBackwardRefs
    {
        public Vp8LBackwardRefs(int pixels) => this.Refs = new List<PixOrCopy>(pixels);

        /// <summary>
        /// Gets or sets the common block-size.
        /// </summary>
        public int BlockSize { get; set; }

        /// <summary>
        /// Gets the backward references.
        /// </summary>
        public List<PixOrCopy> Refs { get; }

        public void Add(PixOrCopy pixOrCopy) => this.Refs.Add(pixOrCopy);
    }
}
