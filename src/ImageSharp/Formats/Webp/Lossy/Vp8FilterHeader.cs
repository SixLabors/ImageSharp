// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
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

        /// <summary>
        /// Gets or sets a value indicating whether the filtering type is: 0=complex, 1=simple.
        /// </summary>
        public bool Simple { get; set; }

        /// <summary>
        /// Gets or sets delta filter level for i4x4 relative to i16x16.
        /// </summary>
        public int I4x4LfDelta { get; set; }

        public bool UseLfDelta { get; set; }

        public int[] RefLfDelta { get; }

        public int[] ModeLfDelta { get; }
    }
}
