// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.WebP
{
    internal class Vp8LDecoder
    {
        public Vp8LDecoder(int width, int height)
        {
            this.Width = width;
            this.Height = height;
            this.Metadata = new Vp8LMetadata();
        }

        public int Width { get; set; }

        public int Height { get; set; }

        public Vp8LMetadata Metadata { get; set; }

        public List<Vp8LTransform> Transforms { get; set; }
    }
}
