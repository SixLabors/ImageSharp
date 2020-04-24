// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP.Lossy
{
    internal class Vp8FilterHeader
    {
        private const int NumRefLfDeltas = 4;

        private const int NumModeLfDeltas = 4;

        private int filterLevel;

        private int sharpness;

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8FilterHeader"/> class.
        /// </summary>
        public Vp8FilterHeader()
        {
            this.RefLfDelta = new int[NumRefLfDeltas];
            this.ModeLfDelta = new int[NumModeLfDeltas];
        }

        /// <summary>
        /// Gets or sets the loop filter.
        /// </summary>
        public LoopFilter LoopFilter { get; set; }

        /// <summary>
        /// Gets or sets the filter level. Valid values are [0..63].
        /// </summary>
        public int FilterLevel
        {
            get => this.filterLevel;
            set
            {
                Guard.MustBeBetweenOrEqualTo(value, 0, 63, nameof(this.FilterLevel));
                this.filterLevel = value;
            }
        }

        /// <summary>
        /// Gets or sets the filter sharpness. Valid values are [0..7].
        /// </summary>
        public int Sharpness
        {
            get => this.sharpness;
            set
            {
                Guard.MustBeBetweenOrEqualTo(value, 0, 7, nameof(this.Sharpness));
                this.sharpness = value;
            }
        }

        public bool UseLfDelta { get; set; }

        public int[] RefLfDelta { get; }

        public int[] ModeLfDelta { get; }
    }
}
