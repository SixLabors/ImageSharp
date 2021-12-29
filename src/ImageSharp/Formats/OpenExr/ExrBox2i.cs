// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.OpenExr
{
    internal struct ExrBox2i
    {
        public ExrBox2i(int xMin, int yMin, int xMax, int yMax)
        {
            this.XMin = xMin;
            this.YMin = yMin;
            this.XMax = xMax;
            this.YMax = yMax;
        }

        public int XMin { get; }

        public int YMin { get; }

        public int XMax { get; }

        public int YMax { get; }
    }
}
