// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
{
    internal class Vp8EncSegmentHeader
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8EncSegmentHeader"/> class.
        /// </summary>
        /// <param name="numSegments">Number of segments.</param>
        public Vp8EncSegmentHeader(int numSegments)
        {
            this.NumSegments = numSegments;
            this.UpdateMap = this.NumSegments > 1;
            this.Size = 0;
        }

        /// <summary>
        /// Gets the actual number of segments. 1 segment only = unused.
        /// </summary>
        public int NumSegments { get; }

        /// <summary>
        /// Gets or sets a value indicating whether to update the segment map or not. Must be false if there's only 1 segment.
        /// </summary>
        public bool UpdateMap { get; set; }

        /// <summary>
        /// Gets or sets the bit-cost for transmitting the segment map.
        /// </summary>
        public int Size { get; set; }
    }
}
