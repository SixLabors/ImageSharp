// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.WebP.Lossless
{
    internal class Vp8LBackwardRefs
    {
        public Vp8LBackwardRefs()
        {
            this.Refs = new List<PixOrCopy>();
        }

        /// <summary>
        /// Common block-size.
        /// </summary>
        public int BlockSize { get; set; }

        public List<PixOrCopy> Refs { get; }

        public void Add(PixOrCopy pixOrCopy)
        {
            this.Refs.Add(pixOrCopy);
        }
    }
}
