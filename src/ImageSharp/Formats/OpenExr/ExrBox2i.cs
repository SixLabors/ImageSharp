// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.OpenExr
{
    internal struct ExrBox2i
    {
        public ExrBox2i(int xMin, int yMin, int xMax, int yMax)
        {
            this.xMin = xMin;
            this.yMin = yMin;
            this.xMax = xMax;
            this.yMax = yMax;
        }

        public int xMin { get; }

        public int yMin { get; }

        public int xMax { get; }

        public int yMax { get; }
    }
}
