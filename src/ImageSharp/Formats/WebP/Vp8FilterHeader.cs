// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP
{
    internal class Vp8FilterHeader
    {
        private const int NumRefLfDeltas = 4;

        private const int NumModeLfDeltas = 4;

        public Vp8FilterHeader()
        {
            this.RefLfDelta = new int[NumRefLfDeltas];
            this.ModeLfDelta = new int[NumModeLfDeltas];
        }

        /// <summary>
        /// Gets or sets the loop filter.
        /// </summary>
        public LoopFilter LoopFilter { get; set; }

        // [0..63]
        public int Level { get; set; }

        // [0..7]
        public int Sharpness { get; set; }

        public bool UseLfDelta { get; set; }

        public int[] RefLfDelta { get; private set; }

        public int[] ModeLfDelta { get; private set; }
    }
}
