// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Formats.WebP.Lossless
{
    internal class Vp8LRefsCursor
    {
        public Vp8LRefsCursor(Vp8LBackwardRefs refs)
        {
            //this.Refs = refs;
            //this.CurrentPos = 0;
        }

        //public PixOrCopy Refs { get; }

        public PixOrCopy CurrentPos { get; }
    }
}
